namespace DemoApi.Bff.Session;

internal enum SessionState
{
    /// <summary>No session cookie.</summary>
    NoCookie,

    /// <summary>A cookie was sent but the session is gone, expired, tampered or timed out.</summary>
    Ended,

    Active,
}

internal readonly record struct SessionLookup(SessionState State, SessionRecord? Record);

/// <summary>Finds the session for a request and applies the session lifetime rules.</summary>
internal sealed class SessionService(
    SessionStore store,
    RefreshCoordinator refresh,
    ApiAuthClient api,
    TimeProvider time,
    IOptions<BffOptions> options,
    SecurityLog events,
    ILogger<SessionService> log)
{
    private const string ItemKey = "bff.session";

    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    /// <summary>Looks the session up once per request. Does not count as user activity.</summary>
    public async Task<SessionLookup> ResolveAsync(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out object? cached) && cached is SessionLookup known)
        {
            return known;
        }

        string? id = SessionCookie.Read(context.Request);
        SessionLookup lookup = id is null
            ? new SessionLookup(SessionState.NoCookie, null)
            : await ResolveCoreAsync(id, ClientIp.Key(context), context.RequestAborted);
        context.Items[ItemKey] = lookup;
        return lookup;
    }

    /// <summary>Counts a user request as activity, at most one write per minute. Health and /bff/user must not call it.</summary>
    public async Task TouchAsync(SessionRecord session, CancellationToken ct)
    {
        DateTimeOffset now = time.GetUtcNow();
        if (now - session.LastActivityAt >= TouchInterval)
        {
            await store.TouchAsync(session.SessionId, now, ct);
        }
    }

    /// <summary>Seconds until the Admin idle timeout, or null for Customers (UX-D4).</summary>
    public int? IdleSecondsLeft(SessionRecord session)
    {
        if (!session.User.IsAdmin)
        {
            return null;
        }

        TimeSpan left = TimeSpan.FromMinutes(options.Value.AdminIdleMinutes) - (time.GetUtcNow() - session.LastActivityAt);
        return (int)Math.Max(0, Math.Floor(left.TotalSeconds));
    }

    /// <summary>
    /// Ends a session on purpose: the session entry is deleted first (so a running refresh cannot write it back),
    /// then the API token is revoked. Never throws; a sign-out always completes locally.
    /// </summary>
    public async Task SignOutAsync(string sessionId, string clientIp, string reason)
    {
        bool everywhere = reason == SecurityReasons.LogoutAll;
        SessionRecord? latest = null;
        try
        {
            // The access token may have expired since the last call; EP-06/EP-07 need a valid one. A sign-out must not
            // spend the refresh budget, so it is not counted against the limit.
            RefreshOutcome outcome = await refresh.EnsureFreshAsync(sessionId, null, false, clientIp, CancellationToken.None, spendBudget: false);
            latest = (outcome as RefreshOutcome.Fresh)?.Session;

            // Refresh not possible (API down): still try to revoke with what the session holds.
            latest ??= await store.GetAsync(sessionId, CancellationToken.None);
        }
        catch (SessionStoreUnavailableException)
        {
            log.LogWarning("Sign-out could not read the session store");
        }

        await DeleteQuietlyAsync(sessionId);
        events.SessionEnded(reason, sessionId, clientIp);
        if (latest is not null)
        {
            _ = everywhere
                ? await api.LogoutAllAsync(latest.AccessToken)
                : await api.LogoutAsync(latest.AccessToken, latest.RefreshToken);
        }
    }

    public async Task DeleteQuietlyAsync(string sessionId)
    {
        try
        {
            await store.DeleteAsync(sessionId);
        }
        catch (SessionStoreUnavailableException)
        {
            log.LogWarning("A session could not be deleted because the session store is not available");
        }
    }

    private async Task<SessionLookup> ResolveCoreAsync(string id, string clientIp, CancellationToken ct)
    {
        (SessionRecord? session, bool tampered) = await store.ReadAsync(id, ct);
        if (session is null)
        {
            // Tampered or copied blobs are named; expired, unknown and planted IDs look the same from here.
            events.SessionEnded(tampered ? SecurityReasons.Tampered : SecurityReasons.UnknownOrPlanted, id, clientIp);
            return new SessionLookup(SessionState.Ended, null);
        }

        DateTimeOffset now = time.GetUtcNow();
        if (session.RefreshTokenExpiresAt <= now)
        {
            await store.DeleteAsync(id);
            events.SessionEnded(SecurityReasons.RefreshTokenExpired, id, clientIp);
            return new SessionLookup(SessionState.Ended, null);
        }

        // Admin sessions also end after 30 minutes idle and 12 hours in total (FE-D4, S-D10).
        bool idle = session.User.IsAdmin && now - session.LastActivityAt > TimeSpan.FromMinutes(options.Value.AdminIdleMinutes);
        bool absolute = session.User.IsAdmin && now - session.CreatedAt > TimeSpan.FromHours(options.Value.AdminAbsoluteHours);
        if (idle || absolute)
        {
            await SignOutAsync(id, clientIp, idle ? SecurityReasons.Idle : SecurityReasons.Absolute);
            return new SessionLookup(SessionState.Ended, null);
        }

        return new SessionLookup(SessionState.Active, session);
    }
}