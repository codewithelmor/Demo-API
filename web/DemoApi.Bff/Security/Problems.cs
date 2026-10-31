namespace DemoApi.Bff.Security;

/// <summary>
/// The only place that writes error bodies. Text is fixed per slug, so nothing from the API, an exception or the
/// request can leak into a response (NFR-F39). Types are relative: <c>/problems/{slug}</c>, as in the API.
/// </summary>
internal static class Problems
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Dictionary<string, (string Title, string Detail)> Texts = new(StringComparer.Ordinal)
    {
        ["validation-failed"] = ("One or more validation errors occurred.", "The request is not valid."),
        ["invalid-credentials"] = ("Invalid credentials.", "The email or password is incorrect."),
        ["email-not-confirmed"] = ("Email not confirmed.", "Confirm your email address before signing in."),
        ["not-signed-in"] = ("Not signed in.", "Sign in to continue."),
        ["session-ended"] = ("Session ended.", "Your session ended. Sign in again."),
        ["forbidden"] = ("Forbidden.", "You do not have access to this."),
        ["csrf-check-failed"] = ("Request check failed.", "The request did not pass the cross-site request check."),
        ["not-found"] = ("Not found.", "The resource was not found."),
        ["method-not-allowed"] = ("Method not allowed.", "The method is not allowed for this resource."),
        ["payload-too-large"] = ("Payload too large.", "The request body is too large."),
        ["unsupported-media-type"] = ("Unsupported media type.", "The request body must be JSON."),
        ["rate-limited"] = ("Too many requests.", "Too many requests. Try again later."),
        ["upstream-unavailable"] = ("Service unavailable.", "The service is not available right now."),
        ["internal-error"] = ("Internal server error.", "Something went wrong."),
    };

    public static string SlugFor(int status) => status switch
    {
        400 => "validation-failed",
        401 => "not-signed-in",
        403 => "forbidden",
        404 => "not-found",
        405 => "method-not-allowed",
        413 => "payload-too-large",
        415 => "unsupported-media-type",
        429 => "rate-limited",
        503 => "upstream-unavailable",
        _ => status >= 500 ? "internal-error" : "forbidden",
    };

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string slug,
        IReadOnlyDictionary<string, string[]>? errors = null,
        int? retryAfterSeconds = null)
    {
        HttpResponse response = context.Response;
        if (response.HasStarted)
        {
            return Task.CompletedTask;
        }

        // Allow-list misses and wrong methods are probes. Only BFF-made 404/405 reach here, never an API answer.
        if (status is StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed && RequestPaths.IsApiOrBff(context.Request.Path))
        {
            context.SecurityEvents().RouteProbe(
                status,
                RequestPaths.IsBff(context.Request.Path) ? SecurityReasons.BffRoute : SecurityReasons.ApiRoute,
                context.Request.Method,
                ClientIp.Key(context));
        }

        (string title, string detail) = Texts[slug];
        response.StatusCode = status;
        response.Headers.ContentLength = null;
        if (retryAfterSeconds is { } retry)
        {
            response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
        }

        ProblemBody body = new(
            $"/problems/{slug}",
            title,
            status,
            detail,
            context.Request.Path.Value,
            Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            errors);
        return response.WriteAsJsonAsync(body, Json, "application/problem+json", context.RequestAborted);
    }

    public static Task WriteAsync(HttpContext context, int status) => WriteAsync(context, status, SlugFor(status));

    private sealed record ProblemBody(
        string Type,
        string Title,
        int Status,
        string Detail,
        string? Instance,
        string TraceId,
        IReadOnlyDictionary<string, string[]>? Errors);
}

/// <summary>An <see cref="IResult"/> that writes a BFF problem.</summary>
internal sealed class ProblemResult(
    int status,
    string slug,
    IReadOnlyDictionary<string, string[]>? errors = null,
    int? retryAfterSeconds = null) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext) => Problems.WriteAsync(httpContext, status, slug, errors, retryAfterSeconds);
}