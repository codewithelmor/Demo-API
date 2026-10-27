namespace DemoApi.BuildingBlocks.Errors;

/// <summary>RFC 9457 ProblemDetails with a stable <c>/problems/{slug}</c> type and a traceId (FR-124).</summary>
public static class Problems
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string title,
        string? detail,
        IReadOnlyDictionary<string, string[]>? errors = null,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = errors is null
            ? new ProblemDetails()
            : new HttpValidationProblemDetails(errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal));
        problem.Type = $"/problems/{code}";
        problem.Title = title;
        problem.Status = status;
        problem.Detail = detail;
        problem.Instance = context.Request.Path.Value;
        problem.Extensions["traceId"] = TraceId(context);
        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return problem;
    }

    public static string TraceId(HttpContext context) => Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public static IResult ToProblem(this Error error, HttpContext context)
    {
        var status = error.Type switch
        {
            ErrorType.Validation or ErrorType.InvalidToken => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            ErrorType.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            _ => StatusCodes.Status500InternalServerError,
        };
        var problem = Create(context, status, error.Code, error.Title, error.Detail, error.ValidationErrors, error.Extensions);
        return TypedResults.Json(problem, (System.Text.Json.JsonSerializerOptions?)null, ContentType, status);
    }

    public static async Task WriteAsync(HttpContext context, int status, string code, string title, string? detail)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(Create(context, status, code, title, detail), (System.Text.Json.JsonSerializerOptions?)null, ContentType);
    }

    /// <summary>Body for status codes produced without one (401, 403, 404, 405, 413, 415).</summary>
    public static Task WriteStatusCodePageAsync(StatusCodeContext context)
    {
        var http = context.HttpContext;
        var (code, title) = http.Response.StatusCode switch
        {
            400 => ("validation-failed", "Bad request"),
            401 => ("unauthorized", "Unauthorized"),
            403 => ("forbidden", "Forbidden"),
            404 => ("not-found", "Not found"),
            405 => ("method-not-allowed", "Method not allowed"),
            413 => ("payload-too-large", "Payload too large"),
            415 => ("unsupported-media-type", "Unsupported media type"),
            429 => ("rate-limited", "Too many requests"),
            503 => ("service-unavailable", "Service unavailable"),
            _ => ("error", "Error"),
        };
        return WriteAsync(http, http.Response.StatusCode, code, title, null);
    }
}

/// <summary>Unhandled exceptions: 503 for database outages, 500 otherwise; no exception text outside Development.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException bad)
        {
            var code = bad.StatusCode switch
            {
                StatusCodes.Status413PayloadTooLarge => "payload-too-large",
                StatusCodes.Status415UnsupportedMediaType => "unsupported-media-type",
                _ => "validation-failed",
            };
            await Problems.WriteAsync(httpContext, bad.StatusCode, code, "Bad request", null);
            return true;
        }

        if (IsDatabaseUnavailable(exception))
        {
            logger.LogError(exception, "Database unavailable");
            httpContext.Response.Headers.RetryAfter = "5";
            await Problems.WriteAsync(httpContext, StatusCodes.Status503ServiceUnavailable, "service-unavailable", "Service unavailable", "The service is temporarily unavailable. Try again later.");
            return true;
        }

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        logger.LogError(exception, "Unhandled exception");
        var detail = env.IsDevelopment() ? exception.Message : "An unexpected error occurred.";
        await Problems.WriteAsync(httpContext, StatusCodes.Status500InternalServerError, "internal-error", "Internal server error", detail);
        return true;
    }

    private static bool IsDatabaseUnavailable(Exception ex) => ex switch
    {
        DatabaseUnavailableException => true,
        RetryLimitExceededException => true,
        NpgsqlException { IsTransient: true } => true,
        InvalidOperationException { InnerException: { } inner } => IsDatabaseUnavailable(inner),
        _ => false,
    };
}