namespace DemoApi.Bff.Security;

/// <summary>
/// Outermost BFF middleware. It makes sure every request has a W3C activity (so traceparent can be forwarded and
/// traceId is stable) and is the last safety net: nothing may escape to the developer exception page that the host
/// adds in Development (NFR-F39, T-30). An exception that gets here, even one thrown by the exception handler
/// itself, becomes a generic 500 problem.
/// </summary>
internal sealed class TraceMiddleware(RequestDelegate next, ILogger<TraceMiddleware> log)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (Activity.Current is not null)
        {
            await RunAsync(context);
            return;
        }

        using Activity activity = new("DemoApi.Bff.Request");
        _ = activity.Start();
        await RunAsync(context);
    }

    private async Task RunAsync(HttpContext context)
    {
#pragma warning disable CA1031 // Last safety net: any escaped exception must become a generic 500, never a developer page.
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Unhandled exception reached the outermost middleware");
            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }

            await Problems.WriteAsync(context, StatusCodes.Status500InternalServerError, "internal-error");
        }
#pragma warning restore CA1031
    }
}

/// <summary>Full header set on every response, including errors, 404, 429 and static files (NFR-F13, NFR-F36).</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string Csp = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    private const string PermissionsPolicy = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    public Task InvokeAsync(HttpContext context)
    {
        bool noStore = RequestPaths.IsApiOrBff(context.Request.Path);
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary h = context.Response.Headers;
            // Only the index page (served by SpaIndex) gets a style nonce; every other response keeps style-src 'self'.
            h.ContentSecurityPolicy = context.Items.TryGetValue(SpaIndex.NonceItemKey, out object? nonce) && nonce is string value
                ? Csp.Replace("style-src 'self'", $"style-src 'self' 'nonce-{value}'", StringComparison.Ordinal)
                : Csp;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = PermissionsPolicy;
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            _ = h.Remove("Server");
            if (noStore)
            {
                h.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });
        return next(context);
    }
}

/// <summary>
/// Rejects odd paths under /api and /bff before routing (NFR-F32): encoded or raw slashes and dots, backslashes,
/// double or trailing slashes, non-ASCII, wrong-case prefix. Under /bff only the exact known routes exist; a known
/// path with the wrong method is 405.
/// </summary>
internal sealed class PathGuardMiddleware(RequestDelegate next)
{
    private static readonly Dictionary<string, string> BffRoutes = new(StringComparer.Ordinal)
    {
        ["/bff/login"] = "POST",
        ["/bff/user"] = "GET",
        ["/bff/logout"] = "POST",
        ["/bff/logout-all"] = "POST",
        ["/bff/health/live"] = "GET",
        ["/bff/health/ready"] = "GET",
    };

    private static readonly string[] EncodedDanger = ["%2f", "%5c", "%2e", "%00", "%25"];

    public Task InvokeAsync(HttpContext context)
    {
        PathString pathString = context.Request.Path;
        if (!RequestPaths.IsApiOrBff(pathString))
        {
            return next(context);
        }

        string path = pathString.Value ?? "/";
        if (!IsCanonical(context, path))
        {
            return Problems.WriteAsync(context, StatusCodes.Status404NotFound);
        }

        if (RequestPaths.IsBff(pathString))
        {
            if (!BffRoutes.TryGetValue(path, out string? method))
            {
                return Problems.WriteAsync(context, StatusCodes.Status404NotFound);
            }

            string actual = HttpMethods.IsHead(context.Request.Method) ? "GET" : context.Request.Method;
            if (!string.Equals(actual, method, StringComparison.Ordinal))
            {
                context.Response.Headers.Allow = method;
                return Problems.WriteAsync(context, StatusCodes.Status405MethodNotAllowed);
            }
        }

        return next(context);
    }

    private static bool IsCanonical(HttpContext context, string path)
    {
        if (!(path.StartsWith("/api/", StringComparison.Ordinal) || path.StartsWith("/bff/", StringComparison.Ordinal)))
        {
            return false;
        }

        if (path.EndsWith('/') || path.Contains("//", StringComparison.Ordinal) || path.Contains("/./", StringComparison.Ordinal)
            || path.Contains("/../", StringComparison.Ordinal) || path.EndsWith("/.", StringComparison.Ordinal) || path.EndsWith("/..", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Any(static c => c is < '!' or > '~' or '\\' or '%'))
        {
            return false;
        }

        string raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? string.Empty;
        int query = raw.IndexOf('?', StringComparison.Ordinal);
        string rawPath = query >= 0 ? raw[..query] : raw;
        return !EncodedDanger.Any(d => rawPath.Contains(d, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// CSRF (NFR-F11, NFR-F29): every non-safe method on /bff and /api needs <c>X-CSRF: 1</c> and an Origin from the
/// allow-list; Sec-Fetch-Site, when sent, must be same-origin. There is no CORS policy, so cross-site preflights fail.
/// </summary>
internal sealed class CsrfMiddleware(RequestDelegate next, IOptions<BffOptions> options)
{
    private readonly HashSet<string> origins = new(options.Value.AllowedOrigins, StringComparer.OrdinalIgnoreCase);

    public Task InvokeAsync(HttpContext context)
    {
        HttpRequest request = context.Request;
        if (RequestPaths.IsApiOrBff(request.Path) && !RequestPaths.IsSafeMethod(request.Method) && Check(request) is { } reason)
        {
            context.SecurityEvents().CsrfRejected(reason, ClientIp.Key(context));
            return Problems.WriteAsync(context, StatusCodes.Status403Forbidden, "csrf-check-failed");
        }

        return next(context);
    }

    // Null when the request passes; otherwise the reason code for the security log.
    private string? Check(HttpRequest request)
    {
        if (request.Headers["X-CSRF"].ToString() != "1")
        {
            return SecurityReasons.MissingCsrfHeader;
        }

        Microsoft.Extensions.Primitives.StringValues origin = request.Headers.Origin;
        if (origin.Count != 1 || origin[0] is not { Length: > 0 } value || !origins.Contains(value))
        {
            return SecurityReasons.BadOrigin;
        }

        Microsoft.Extensions.Primitives.StringValues site = request.Headers["Sec-Fetch-Site"];
        return site.Count == 0 || (site.Count == 1 && site[0] == "same-origin") ? null : SecurityReasons.BadFetchSite;
    }
}

/// <summary>Anonymous limit of 60/min per IP for /bff and /api (NFR-F37). "Anonymous" means no valid session.</summary>
internal sealed class AnonymousRateLimitMiddleware(RequestDelegate next, SessionService sessions, AnonymousRateLimiter limiter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        PathString path = context.Request.Path;
        if (!RequestPaths.IsApiOrBff(path) || RequestPaths.IsHealth(path))
        {
            await next(context);
            return;
        }

        bool signedIn = false;
        try
        {
            signedIn = (await sessions.ResolveAsync(context)).State == SessionState.Active;
        }
        catch (SessionStoreUnavailableException)
        {
            // Counted as anonymous; the endpoint decides what to do about the missing store.
        }

        if (!signedIn && !limiter.TryAcquire(ClientIp.Key(context), out int retryAfter))
        {
            context.SecurityEvents().RateLimited("anonymous", ClientIp.Key(context));
            await Problems.WriteAsync(context, StatusCodes.Status429TooManyRequests, "rate-limited", null, retryAfter);
            return;
        }

        await next(context);
    }
}

/// <summary>Maps exceptions to generic problems. Logs once, never the request or any token.</summary>
internal sealed class BffExceptionHandler(ILogger<BffExceptionHandler> log) : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                return true;
            case SessionStoreUnavailableException:
                log.LogWarning("The session store is not available");
                await Problems.WriteAsync(httpContext, StatusCodes.Status503ServiceUnavailable, "upstream-unavailable");
                return true;
            case BadHttpRequestException bad:
                await Problems.WriteAsync(httpContext, bad.StatusCode);
                return true;
            default:
                log.LogError(exception, "Unhandled exception");
                await Problems.WriteAsync(httpContext, StatusCodes.Status500InternalServerError, "internal-error");
                return true;
        }
    }
}