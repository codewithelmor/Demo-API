namespace DemoApi.Bff.Session;

internal abstract record RefreshOutcome
{
    private RefreshOutcome()
    {
    }

    /// <summary>The session holds a usable access token (refreshed now or still fresh).</summary>
    public sealed record Fresh(SessionRecord Session) : RefreshOutcome;

    /// <summary>The session no longer exists or was ended (refresh rejected, logout won, expired).</summary>
    public sealed record SessionGone : RefreshOutcome;

    public sealed record Throttled(int RetryAfterSeconds) : RefreshOutcome;

    /// <summary>The API or the session store could not be reached. The session is kept.</summary>
    public sealed record Unavailable : RefreshOutcome;
}

/// <summary>
/// Single-flight token refresh (FR-F003, G4). One refresh runs per session; every other caller for that session,
/// from any tab, awaits the same task. The API treats a second use of a refresh token as reuse and revokes the
/// whole family, so there is no retry and the call is never cancelled by a browser abort.
/// </summary>
internal sealed class RefreshCoordinator(
    SessionStore store,
    ApiAuthClient api,
    RefreshRateLimiter limiter,
    TimeProvider time,
    SecurityLog events,
    ILogger<RefreshCoordinator> log)
{
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, Lazy<Task<RefreshOutcome>>> inflight = new(StringComparer.Ordinal);

    /// <param name="sessionId">The session.</param>
    /// <param name="rejectedAccessToken">The token the caller used. With <paramref name="force"/> a refresh is only
    /// needed while the session still holds this token (the API rejected it).</param>
    /// <param name="force">True after an API 401; false means "refresh when 60 s or less are left".</param>
    public async Task<RefreshOutcome> EnsureFreshAsync(string sessionId, string? rejectedAccessToken, bool force, string clientIp, CancellationToken ct, bool spendBudget = true)
    {
        RefreshOutcome outcome = await JoinAsync(sessionId, rejectedAccessToken, force, clientIp, spendBudget, ct);

        // A forced call can join a flight that decided nothing was needed. Run once more in that case.
        if (force
            && outcome is RefreshOutcome.Fresh fresh
            && string.Equals(fresh.Session.AccessToken, rejectedAccessToken, StringComparison.Ordinal))
        {
            outcome = await JoinAsync(sessionId, rejectedAccessToken, force, clientIp, spendBudget, ct);
        }

        return outcome;
    }

    private async Task<RefreshOutcome> JoinAsync(string sessionId, string? rejected, bool force, string clientIp, bool spendBudget, CancellationToken ct)
    {
        Lazy<Task<RefreshOutcome>>? candidate = null;
        candidate = new Lazy<Task<RefreshOutcome>>(() => RunFlightAsync(sessionId, rejected, force, clientIp, spendBudget, candidate!));
        Lazy<Task<RefreshOutcome>> flight = inflight.GetOrAdd(sessionId, candidate);

        // Only this caller's wait is cancellable. The refresh itself keeps running.
        return await flight.Value.WaitAsync(ct);
    }

    // The flight removes its own entry when it ends, whoever is still waiting (compare-and-remove, never a newer entry).
    private async Task<RefreshOutcome> RunFlightAsync(string sessionId, string? rejected, bool force, string clientIp, bool spendBudget, Lazy<Task<RefreshOutcome>> self)
    {
        try
        {
            return await RefreshAsync(sessionId, rejected, force, clientIp, spendBudget);
        }
        finally
        {
            _ = inflight.TryRemove(new KeyValuePair<string, Lazy<Task<RefreshOutcome>>>(sessionId, self));
        }
    }

    private Task<bool> RevokeUnsavedAsync(TokenSet tokens) => api.LogoutAsync(tokens.AccessToken, tokens.RefreshToken);

    private async Task<RefreshOutcome> RefreshAsync(string sessionId, string? rejected, bool force, string clientIp, bool spendBudget)
    {
        try
        {
            // 1. Re-read: another request or tab may have refreshed already.
            SessionRecord? session = await store.GetAsync(sessionId, CancellationToken.None);
            if (session is null)
            {
                events.RefreshOutcome(SecurityReasons.SessionGone, sessionId, clientIp);
                return new RefreshOutcome.SessionGone();
            }

            DateTimeOffset now = time.GetUtcNow();
            bool needed = force
                ? string.Equals(session.AccessToken, rejected, StringComparison.Ordinal)
                : session.AccessTokenExpiresAt - now <= Margin;
            if (!needed)
            {
                return new RefreshOutcome.Fresh(session);
            }

            if (session.RefreshTokenExpiresAt <= now)
            {
                await store.DeleteAsync(sessionId);
                events.SessionEnded(SecurityReasons.RefreshTokenExpired, sessionId, clientIp);
                return new RefreshOutcome.SessionGone();
            }

            if (spendBudget && !limiter.TryAcquire(clientIp, out int retryAfter))
            {
                events.RateLimited("refresh", clientIp);
                events.RefreshOutcome(SecurityReasons.BudgetExhausted, sessionId, clientIp);
                return new RefreshOutcome.Throttled(retryAfter);
            }

            // 2. EP-05 with no caller token: a browser abort must not leave the API rotated and the BFF on the old token.
            ApiCall<TokenSet> call = await api.RefreshAsync(session.RefreshToken, CancellationToken.None);
            switch (call.Outcome)
            {
                case ApiOutcome.Rejected:
                    // 401 invalid-refresh-token (or any 4xx): the session cannot continue.
                    await store.DeleteAsync(sessionId);
                    events.SessionEnded(SecurityReasons.RefreshRejected, sessionId, clientIp);
                    events.RefreshOutcome(SecurityReasons.Rejected, sessionId, clientIp);
                    return new RefreshOutcome.SessionGone();
                case ApiOutcome.Throttled:
                    events.RefreshOutcome(SecurityReasons.ApiThrottled, sessionId, clientIp);
                    return new RefreshOutcome.Throttled(call.RetryAfterSeconds);
                case ApiOutcome.Unavailable:
                    events.RefreshOutcome(SecurityReasons.ApiUnavailable, sessionId, clientIp);
                    return new RefreshOutcome.Unavailable();
            }

            TokenSet tokens = call.Value!;

            // 4. Re-read EP-08 with the new token; keep the old summary if it fails.
            ApiCall<UserSummary> me = await api.GetCurrentUserAsync(tokens.AccessToken, CancellationToken.None);
            UserSummary user = me.IsOk ? me.Value! : session.User;

            SessionRecord updated = session with
            {
                AccessToken = tokens.AccessToken,
                AccessTokenExpiresAt = tokens.AccessTokenExpiresAt,
                RefreshToken = tokens.RefreshToken,
                RefreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
                User = user,
                Version = session.Version + 1,
            };

            // 3. Conditional save before the waiters are released. Logout and session end always win (NFR-F31).
            SessionRecord? saved;
            try
            {
                saved = await store.TryReplaceAsync(sessionId, session.Version, updated, CancellationToken.None);
            }
            catch (SessionStoreUnavailableException)
            {
                await RevokeUnsavedAsync(tokens);
                throw;
            }

            if (saved is null)
            {
                // The session ended while EP-05 ran. The new pair is held by nobody, so revoke it at the API.
                await RevokeUnsavedAsync(tokens);
                events.RefreshOutcome(SecurityReasons.LostSave, sessionId, clientIp);
                return new RefreshOutcome.SessionGone();
            }

            events.RefreshOutcome(SecurityReasons.Rotated, sessionId, clientIp);
            return new RefreshOutcome.Fresh(saved);
        }
        catch (SessionStoreUnavailableException)
        {
            log.LogWarning("Token refresh failed because the session store is not available");
            events.RefreshOutcome(SecurityReasons.StoreUnavailable, sessionId, clientIp);
            return new RefreshOutcome.Unavailable();
        }
    }
}