namespace DemoApi.Bff.Upstream;

internal enum ApiOutcome
{
    Ok,
    Rejected,
    Throttled,
    Unavailable,
}

/// <summary>
/// What the BFF keeps from an API answer. Never the raw body: only a validated problem slug, sanitized field errors
/// and the Retry-After seconds (NFR-F39).
/// </summary>
internal sealed record ApiCall<T>(
    ApiOutcome Outcome,
    int Status,
    T? Value,
    string? Slug,
    IReadOnlyDictionary<string, string[]>? Errors,
    int RetryAfterSeconds)
{
    public bool IsOk => Outcome == ApiOutcome.Ok;

    public static ApiCall<T> Ok(T value) => new(ApiOutcome.Ok, 200, value, null, null, 0);

    public static ApiCall<T> Unavailable(int status = 0) => new(ApiOutcome.Unavailable, status, default, null, null, 0);
}

internal sealed record TokenSet(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)
{
    public override string ToString() => "TokenSet { redacted }";
}

/// <summary>
/// Calls the API auth endpoints (EP-04, 05, 06, 07, 08) and the readiness probe. Deliberately has no retry and no
/// resilience handler: a retried EP-05 counts as token reuse and revokes the whole family (ADR-0015).
/// </summary>
internal sealed partial class ApiAuthClient(IHttpClientFactory factory, ILogger<ApiAuthClient> log)
{
    public const string ClientName = "bff-api";

    private const int MaxFieldErrors = 20;
    private const int MaxMessagesPerField = 5;
    private const int MaxTextLength = 200;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public Task<ApiCall<TokenSet>> LoginAsync(string email, string password, CancellationToken ct) =>
        SendAsync("login", HttpMethod.Post, "/api/v1/auth/login", null, new { email, password }, ParseTokensAsync, ct);

    // The refresh call must finish even when the browser goes away (callers pass CancellationToken.None).
    public Task<ApiCall<TokenSet>> RefreshAsync(string refreshToken, CancellationToken ct) =>
        SendAsync("refresh", HttpMethod.Post, "/api/v1/auth/refresh", null, new { refreshToken }, ParseTokensAsync, ct);

    public Task<ApiCall<UserSummary>> GetCurrentUserAsync(string accessToken, CancellationToken ct) =>
        SendAsync("me", HttpMethod.Get, "/api/v1/auth/me", accessToken, null, ParseUserAsync, ct);

    public async Task<bool> LogoutAsync(string accessToken, string refreshToken)
    {
        ApiCall<bool> result = await SendAsync("logout", HttpMethod.Post, "/api/v1/auth/logout", accessToken, new { refreshToken }, AcceptAsync, CancellationToken.None);
        return result.IsOk;
    }

    public async Task<bool> LogoutAllAsync(string accessToken)
    {
        ApiCall<bool> result = await SendAsync("logout-all", HttpMethod.Post, "/api/v1/auth/logout-all", accessToken, null, AcceptAsync, CancellationToken.None);
        return result.IsOk;
    }

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        ApiCall<bool> result = await SendAsync("ready", HttpMethod.Get, "/health/ready", null, null, AcceptAsync, ct);
        return result.IsOk;
    }

    private static Task<TokenSet?> ParseTokensAsync(HttpResponseMessage response, CancellationToken ct) =>
        ParseAsync<TokenWire, TokenSet>(
            response,
            static wire => string.IsNullOrWhiteSpace(wire.AccessToken) || string.IsNullOrWhiteSpace(wire.RefreshToken)
                ? null
                : new TokenSet(wire.AccessToken, wire.AccessTokenExpiresAt, wire.RefreshToken, wire.RefreshTokenExpiresAt),
            ct);

    private static Task<UserSummary?> ParseUserAsync(HttpResponseMessage response, CancellationToken ct) =>
        ParseAsync<UserWire, UserSummary>(
            response,
            static wire => string.IsNullOrWhiteSpace(wire.Email)
                ? null
                : new UserSummary(wire.UserId, wire.Email, wire.EmailConfirmed, wire.Roles ?? [], wire.CustomerId),
            ct);

    private static Task<bool> AcceptAsync(HttpResponseMessage response, CancellationToken ct) => Task.FromResult(true);

    private static async Task<TOut?> ParseAsync<TWire, TOut>(HttpResponseMessage response, Func<TWire, TOut?> map, CancellationToken ct)
        where TOut : class
    {
        TWire? wire = await response.Content.ReadFromJsonAsync<TWire>(Web, ct);
        return wire is null ? null : map(wire);
    }

    private async Task<ApiCall<T>> SendAsync<T>(
        string operation,
        HttpMethod method,
        string path,
        string? accessToken,
        object? body,
        Func<HttpResponseMessage, CancellationToken, Task<T?>> parse,
        CancellationToken ct)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Web);
        }

        try
        {
            HttpClient http = factory.CreateClient(ClientName);
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (response.IsSuccessStatusCode)
            {
                T? value = await parse(response, ct);
                return value is null ? ApiCall<T>.Unavailable((int)response.StatusCode) : ApiCall<T>.Ok(value);
            }

            return await FailureAsync<T>(operation, response, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or InvalidOperationException
            || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Network error, timeout, oversized or malformed answer. The message can hold the URL, never a token.
            log.LogWarning("API call {Operation} failed: {ExceptionType}", operation, ex.GetType().Name);
            return ApiCall<T>.Unavailable();
        }
    }

    private async Task<ApiCall<T>> FailureAsync<T>(string operation, HttpResponseMessage response, CancellationToken ct)
    {
        int status = (int)response.StatusCode;
        if (status >= 500)
        {
            log.LogWarning("API call {Operation} returned {Status}", operation, status);
            return ApiCall<T>.Unavailable(status);
        }

        if (status == StatusCodes.Status429TooManyRequests)
        {
            return new ApiCall<T>(ApiOutcome.Throttled, status, default, "rate-limited", null, RetryAfterSeconds(response));
        }

        if (status != StatusCodes.Status400BadRequest && status != StatusCodes.Status401Unauthorized && status != StatusCodes.Status403Forbidden)
        {
            // Unmapped 4xx: operation and status only, never the body, headers or tokens.
            log.LogWarning("API call {Operation} returned {Status}", operation, status);
        }

        (string? slug, IReadOnlyDictionary<string, string[]>? errors) = await ReadProblemAsync(response, ct);
        return new ApiCall<T>(ApiOutcome.Rejected, status, default, slug, errors, 0);
    }

    private static int RetryAfterSeconds(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? header = response.Headers.RetryAfter;
        double seconds = header?.Delta?.TotalSeconds ?? 60;
        return (int)Math.Clamp(Math.Ceiling(seconds), 1, 3600);
    }

    /// <summary>Takes only a well-formed problem slug and trimmed field errors out of an API error body.</summary>
    private static async Task<(string? Slug, IReadOnlyDictionary<string, string[]>? Errors)> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct),
                new JsonDocumentOptions { MaxDepth = 8 },
                ct);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            string? slug = null;
            if (root.TryGetProperty("type", out JsonElement type)
                && type.ValueKind == JsonValueKind.String
                && ProblemType().Match(type.GetString() ?? string.Empty) is { Success: true } match)
            {
                slug = match.Groups[1].Value;
            }

            Dictionary<string, string[]>? errors = null;
            if (root.TryGetProperty("errors", out JsonElement errorsElement) && errorsElement.ValueKind == JsonValueKind.Object)
            {
                errors = [];
                foreach (JsonProperty field in errorsElement.EnumerateObject())
                {
                    if (errors.Count >= MaxFieldErrors)
                    {
                        break;
                    }

                    if (field.Value.ValueKind != JsonValueKind.Array || field.Name.Length > 64)
                    {
                        continue;
                    }

                    string[] messages = [.. field.Value.EnumerateArray()
                        .Where(static m => m.ValueKind == JsonValueKind.String)
                        .Select(static m => m.GetString() ?? string.Empty)
                        .Select(static m => m.Length > MaxTextLength ? m[..MaxTextLength] : m)
                        .Take(MaxMessagesPerField)];
                    if (messages.Length > 0)
                    {
                        errors[field.Name] = messages;
                    }
                }
            }

            return (slug, errors is { Count: > 0 } ? errors : null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or HttpRequestException)
        {
            return (null, null);
        }
    }

    [GeneratedRegex("^/problems/([a-z0-9][a-z0-9-]{0,63})$", RegexOptions.CultureInvariant)]
    private static partial Regex ProblemType();

    private sealed record TokenWire(string? AccessToken, DateTimeOffset AccessTokenExpiresAt, string? RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    private sealed record UserWire(Guid UserId, string? Email, bool EmailConfirmed, string[]? Roles, Guid? CustomerId);
}