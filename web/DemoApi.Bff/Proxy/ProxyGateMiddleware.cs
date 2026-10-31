namespace DemoApi.Bff.Proxy;

/// <summary>
/// First step of the YARP pipeline. Runs after routing, CSRF and rate limiting. It checks literal case, finds the
/// session, refreshes the token when 60 s or less are left, buffers the body for one replay, and turns pipeline
/// errors into BFF problems. Tokens are added later by the request transform.
/// </summary>
internal sealed class ProxyGateMiddleware(
    RequestDelegate next,
    SessionService sessions,
    RefreshCoordinator refresh,
    ILogger<ProxyGateMiddleware> log)
{
    private const int MaxBodyBytes = 1_048_576;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint endpoint || !LiteralSegmentsMatch(endpoint, context.Request.Path))
        {
            await Problems.WriteAsync(context, StatusCodes.Status404NotFound);
            return;
        }

        IReadOnlyDictionary<string, string>? metadata = context.GetRouteModel().Config.Metadata;
        bool anonymous = metadata is not null && metadata.ContainsKey(AllowList.AnonymousMetadata);

        ProxyCall? call = null;
        if (!anonymous)
        {
            SessionLookup lookup = await sessions.ResolveAsync(context);
            if (lookup.State != SessionState.Active)
            {
                await RejectAsync(context, lookup.State);
                return;
            }

            string ip = ClientIp.Key(context);
            RefreshOutcome outcome = await refresh.EnsureFreshAsync(lookup.Record!.SessionId, lookup.Record.AccessToken, false, ip, context.RequestAborted);
            if (outcome is not RefreshOutcome.Fresh fresh)
            {
                await WriteOutcomeAsync(context, outcome);
                return;
            }

            await sessions.TouchAsync(fresh.Session, context.RequestAborted);

            byte[]? body = await BufferBodyAsync(context);
            if (body is null && HasBody(context.Request) && context.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                return;
            }

            call = new ProxyCall
            {
                SessionId = fresh.Session.SessionId,
                AccessToken = fresh.Session.AccessToken,
                ClientIp = ip,
                RefreshTokenExpiresAt = fresh.Session.RefreshTokenExpiresAt,
                Body = body,
            };
            context.Items[ProxyCall.ItemsKey] = call;

            // Sliding cookie: expiry follows the refresh token, updated after each refresh (also one done by a replay).
            DateTimeOffset initialExpiry = lookup.Record.RefreshTokenExpiresAt;
            ProxyCall tracked = call;
            context.Response.OnStarting(() =>
            {
                if (!tracked.Ended && tracked.RefreshTokenExpiresAt != initialExpiry)
                {
                    SessionCookie.Write(context.Response, tracked.SessionId, tracked.RefreshTokenExpiresAt);
                }

                return Task.CompletedTask;
            });
        }

        await next(context);

        IForwarderErrorFeature? error = context.Features.Get<IForwarderErrorFeature>();
        if (error is not null && !context.Response.HasStarted)
        {
            await WriteForwardErrorAsync(context, error, call);
        }
    }

    private async Task WriteForwardErrorAsync(HttpContext context, IForwarderErrorFeature error, ProxyCall? call)
    {
        if (error.Exception is ProxyAuthException auth)
        {
            if (call is not null)
            {
                call.Ended = auth.Outcome is RefreshOutcome.SessionGone;
            }

            await WriteOutcomeAsync(context, auth.Outcome);
            return;
        }

        // Network error, timeout, bad gateway: never show details (NFR-F39).
        log.LogWarning("Proxy call failed with {ForwarderError}", error.Error);
        await Problems.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "upstream-unavailable");
    }

    private static async Task WriteOutcomeAsync(HttpContext context, RefreshOutcome outcome)
    {
        switch (outcome)
        {
            case RefreshOutcome.Throttled throttled:
                await Problems.WriteAsync(context, StatusCodes.Status429TooManyRequests, "rate-limited", null, throttled.RetryAfterSeconds);
                break;
            case RefreshOutcome.Unavailable:
                await Problems.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "upstream-unavailable");
                break;
            default:
                await RejectAsync(context, SessionState.Ended);
                break;
        }
    }

    private static async Task RejectAsync(HttpContext context, SessionState state)
    {
        if (state == SessionState.Ended)
        {
            SessionCookie.Delete(context.Response);
            await Problems.WriteAsync(context, StatusCodes.Status401Unauthorized, "session-ended");
            return;
        }

        await Problems.WriteAsync(context, StatusCodes.Status401Unauthorized, "not-signed-in");
    }

    private static bool HasBody(HttpRequest request) =>
        request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding");

    /// <summary>Reads the body once (at most 1 MB) and puts a replayable stream back for YARP.</summary>
    private static async Task<byte[]?> BufferBodyAsync(HttpContext context)
    {
        HttpRequest request = context.Request;
        if (!HasBody(request))
        {
            return null;
        }

        if (request.ContentLength > MaxBodyBytes)
        {
            await Problems.WriteAsync(context, StatusCodes.Status413PayloadTooLarge);
            return null;
        }

        using MemoryStream buffer = new();
        byte[] chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                await Problems.WriteAsync(context, StatusCodes.Status413PayloadTooLarge);
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        byte[] bytes = buffer.ToArray();
        request.Body = new MemoryStream(bytes, writable: false);
        return bytes;
    }

    /// <summary>Route templates match case-insensitively; the allow-list must not. Literal segments are compared exactly.</summary>
    private static bool LiteralSegmentsMatch(RouteEndpoint endpoint, PathString path)
    {
        string[] actual = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<RoutePatternPathSegment> expected = endpoint.RoutePattern.PathSegments;
        if (actual.Length != expected.Count)
        {
            return false;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i].Parts is [RoutePatternLiteralPart literal]
                && !string.Equals(literal.Content, actual[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}