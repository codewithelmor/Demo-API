namespace DemoApi.Bff.Auth;

/// <summary>The user summary sent to the SPA. No tokens, ever (NFR-F09).</summary>
internal sealed record UserView(string Email, bool EmailConfirmed, IReadOnlyList<string> Roles, Guid? CustomerId, int? IdleSecondsLeft);

internal sealed record LoginBody(string? Email, string? Password);

/// <summary>/bff/login, /bff/user, /bff/logout, /bff/logout-all (ADR-0015).</summary>
internal static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffAuth(this IEndpointRouteBuilder app)
    {
        _ = app.MapPost("/bff/login", LoginAsync).RequireRateLimiting(RateLimitPolicies.AuthStrict);
        _ = app.MapGet("/bff/user", GetUserAsync);
        _ = app.MapPost("/bff/logout", (HttpContext c, SessionService s) => EndAsync(c, s, false));
        _ = app.MapPost("/bff/logout-all", (HttpContext c, SessionService s) => EndAsync(c, s, true));
        return app;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        ApiAuthClient api,
        SessionService sessions,
        SessionStore store,
        SecurityLog events,
        TimeProvider time)
    {
        string ip = ClientIp.Key(context);
        if (!context.Request.HasJsonContentType())
        {
            events.SignInFailed(SecurityReasons.UnsupportedMediaType, ip);
            return new ProblemResult(StatusCodes.Status415UnsupportedMediaType, "unsupported-media-type");
        }

        LoginBody? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<LoginBody>(context.RequestAborted);
        }
        catch (JsonException)
        {
            body = null;
        }

        Dictionary<string, string[]> errors = [];
        if (string.IsNullOrWhiteSpace(body?.Email) || body.Email.Length > 256)
        {
            errors["email"] = ["Enter your email address."];
        }

        if (string.IsNullOrEmpty(body?.Password) || body.Password.Length > 1024)
        {
            errors["password"] = ["Enter your password."];
        }

        if (errors.Count > 0)
        {
            events.SignInFailed(SecurityReasons.ValidationFailed, ip);
            return new ProblemResult(StatusCodes.Status400BadRequest, "validation-failed", errors);
        }

        ApiCall<TokenSet> login = await api.LoginAsync(body!.Email!, body.Password!, CancellationToken.None); // never abandon a login the API may have completed
        if (!login.IsOk)
        {
            events.SignInFailed(FailureReason(login), ip);
            return LoginFailure(login);
        }

        TokenSet tokens = login.Value!;

        // Credentials are good: now end any session this cookie pointed at (fixation, NFR-F30).
        string? oldId = SessionCookie.Read(context.Request);
        if (SessionIds.IsValid(oldId))
        {
            await sessions.SignOutAsync(oldId!, ip, SecurityReasons.Replaced);
        }

        ApiCall<UserSummary> me = await api.GetCurrentUserAsync(tokens.AccessToken, CancellationToken.None);
        if (!me.IsOk || tokens.RefreshTokenExpiresAt <= time.GetUtcNow())
        {
            _ = await api.LogoutAsync(tokens.AccessToken, tokens.RefreshToken);
            events.SignInFailed(SecurityReasons.UpstreamUnavailable, ip);
            return new ProblemResult(StatusCodes.Status503ServiceUnavailable, "upstream-unavailable");
        }

        // A new ID on every sign-in; a cookie value planted by someone else is never adopted.
        SessionRecord session;
        try
        {
            session = await store.CreateAsync(tokens, me.Value!, time.GetUtcNow(), CancellationToken.None);
        }
        catch (SessionStoreUnavailableException)
        {
            _ = await api.LogoutAsync(tokens.AccessToken, tokens.RefreshToken);
            events.SignInFailed(SecurityReasons.StoreUnavailable, ip);
            throw;
        }

        SessionCookie.Write(context.Response, session.SessionId, session.RefreshTokenExpiresAt);
        events.SignInSucceeded(session.SessionId, ip);
        return Results.Ok(ToView(session, null));
    }

    // The reason code for the security log: a fixed value, never text from the API.
    private static string FailureReason(ApiCall<TokenSet> call) => call.Outcome switch
    {
        ApiOutcome.Throttled => SecurityReasons.Throttled,
        ApiOutcome.Rejected when call.Status == StatusCodes.Status400BadRequest => SecurityReasons.ValidationFailed,
        ApiOutcome.Rejected when call.Status == StatusCodes.Status401Unauthorized => SecurityReasons.InvalidCredentials,
        ApiOutcome.Rejected when call.Status == StatusCodes.Status403Forbidden && call.Slug == "email-not-confirmed" => SecurityReasons.EmailNotConfirmed,
        ApiOutcome.Rejected when call.Status == StatusCodes.Status403Forbidden => SecurityReasons.Forbidden,
        _ => SecurityReasons.UpstreamUnavailable,
    };

    // Builds our own problem from a few validated facts. The API body is never passed on (NFR-F39).
    private static ProblemResult LoginFailure(ApiCall<TokenSet> call) => call.Outcome switch
    {
        ApiOutcome.Throttled => new ProblemResult(StatusCodes.Status429TooManyRequests, "rate-limited", null, call.RetryAfterSeconds),
        ApiOutcome.Rejected when call.Status == StatusCodes.Status400BadRequest =>
            new ProblemResult(StatusCodes.Status400BadRequest, "validation-failed", call.Errors),
        ApiOutcome.Rejected when call.Status == StatusCodes.Status401Unauthorized =>
            new ProblemResult(StatusCodes.Status401Unauthorized, "invalid-credentials"),
        ApiOutcome.Rejected when call.Status == StatusCodes.Status403Forbidden && call.Slug == "email-not-confirmed" =>
            new ProblemResult(StatusCodes.Status403Forbidden, "email-not-confirmed"),
        ApiOutcome.Rejected when call.Status == StatusCodes.Status403Forbidden =>
            new ProblemResult(StatusCodes.Status403Forbidden, "forbidden"),
        _ => new ProblemResult(StatusCodes.Status503ServiceUnavailable, "upstream-unavailable"),
    };

    private static async Task<IResult> GetUserAsync(HttpContext context, SessionService sessions)
    {
        SessionLookup lookup = await sessions.ResolveAsync(context);
        switch (lookup.State)
        {
            case SessionState.Active:
                return Results.Ok(ToView(lookup.Record!, sessions.IdleSecondsLeft(lookup.Record!)));
            case SessionState.Ended:
                SessionCookie.Delete(context.Response);
                return new ProblemResult(StatusCodes.Status401Unauthorized, "session-ended");
            default:
                return new ProblemResult(StatusCodes.Status401Unauthorized, "not-signed-in");
        }
    }

    // Always clears the session and the cookie, even when the API or the store fails (FR-F004).
    private static async Task<IResult> EndAsync(HttpContext context, SessionService sessions, bool everywhere)
    {
        string? id = SessionCookie.Read(context.Request);
        if (SessionIds.IsValid(id))
        {
            await sessions.SignOutAsync(id!, ClientIp.Key(context), everywhere ? SecurityReasons.LogoutAll : SecurityReasons.Logout);
        }

        SessionCookie.Delete(context.Response);
        context.Response.Headers["Clear-Site-Data"] = "\"cache\", \"storage\"";
        return Results.NoContent();
    }

    private static UserView ToView(SessionRecord session, int? idleSecondsLeft) =>
        new(session.User.Email, session.User.EmailConfirmed, session.User.Roles, session.User.CustomerId, idleSecondsLeft);
}