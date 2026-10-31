using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DemoApi.Bff.Tests.Support;

public sealed record RecordedRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, string Body);

/// <summary>
/// In-process stand-in for the Orders API. It issues tokens, expires them with the fake clock, treats a second use
/// of a refresh token as reuse, and answers every other allow-listed route with 200 when the bearer token is valid.
/// Error bodies carry a canary so tests can prove the BFF never passes a raw API body on.
/// </summary>
public sealed class FakeApi(TimeProvider time) : HttpMessageHandler
{
    public const string Canary = "LEAK-CANARY-7731";
    // Generated per run so no password literal lives in the test code.
    public static readonly string Password = "pw-" + Guid.NewGuid().ToString("N");
    public const string CustomerEmail = "user@example.com";
    public const string AdminEmail = "admin@example.com";

    private readonly ConcurrentQueue<RecordedRequest> requests = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> accessTokens = new();
    private readonly ConcurrentDictionary<string, string> refreshTokens = new(); // token -> email
    private readonly ConcurrentBag<string> issued = [];
    private int counter;

    public IReadOnlyCollection<RecordedRequest> Requests => requests;

    public IReadOnlyCollection<string> IssuedTokens => issued;

    public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public int RefreshCalls => Count("POST", "/api/v1/auth/refresh");

    public int ReuseDetected { get; private set; }

    /// <summary>Awaited inside the refresh endpoint, so a test can hold a refresh open.</summary>
    public Task? RefreshGate { get; set; }

    public bool LogoutFails { get; set; }

    public bool RefreshRejects { get; set; }

    /// <summary>Refresh answers 503 (API trouble) after the gate.</summary>
    public bool RefreshFails { get; set; }

    /// <summary>Login throws a non-HTTP exception, to force an unhandled error in the BFF.</summary>
    public bool ThrowOnLogin { get; set; }

    /// <summary>Authenticated routes answer 429 with Retry-After 7, before any token check (as the real API does).</summary>
    public bool ThrottleProxied { get; set; }

    /// <summary>When set, proxied (authorized) calls answer this status with a small problem body.</summary>
    public HttpStatusCode? ProxiedStatus { get; set; }

    public int Count(string method, string path) =>
        requests.Count(r => r.Method == method && r.Path == path);

    public void InvalidateAccessTokens() => accessTokens.Clear();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Dictionary<string, string> headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        if (request.Content is not null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> h in request.Content.Headers)
            {
                headers[h.Key] = string.Join(",", h.Value);
            }
        }

        string path = request.RequestUri!.AbsolutePath;
        requests.Enqueue(new RecordedRequest(request.Method.Method, path, headers, body));

        HttpResponseMessage response = await RouteAsync(request, path, body);
        response.RequestMessage = request;
        return response;
    }

    private async Task<HttpResponseMessage> RouteAsync(HttpRequestMessage request, string path, string body)
    {
        string method = request.Method.Method;
        switch ((method, path))
        {
            case ("GET", "/health/ready"):
                return Json(HttpStatusCode.OK, new { status = "Healthy" });
            case ("POST", "/api/v1/auth/login"):
                return Login(body);
            case ("POST", "/api/v1/auth/refresh"):
                return await RefreshAsync(body);
        }

        if (path.StartsWith("/api/v1/auth/", StringComparison.Ordinal)
            && path is "/api/v1/auth/register" or "/api/v1/auth/confirm-email" or "/api/v1/auth/resend-confirmation"
                or "/api/v1/auth/forgot-password" or "/api/v1/auth/reset-password")
        {
            return Json(HttpStatusCode.Accepted, new { message = "ok" });
        }

        if (ProxiedStatus is { } forced && path.StartsWith("/api/v1/products", StringComparison.Ordinal))
        {
            return Problem(forced, "not-found");
        }

        if (ThrottleProxied)
        {
            HttpResponseMessage limited = Problem(HttpStatusCode.TooManyRequests, "rate-limited");
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return limited;
        }

        string? token = request.Headers.Authorization is { Scheme: "Bearer" } auth ? auth.Parameter : null;
        if (token is null || !accessTokens.TryGetValue(token, out DateTimeOffset expires) || expires <= time.GetUtcNow())
        {
            return Problem(HttpStatusCode.Unauthorized, "unauthorized");
        }

        switch ((method, path))
        {
            case ("GET", "/api/v1/auth/me"):
                bool admin = EmailFor(token) == AdminEmail;
                return Json(HttpStatusCode.OK, new
                {
                    userId = Guid.NewGuid(),
                    email = EmailFor(token),
                    emailConfirmed = true,
                    roles = new[] { admin ? "Admin" : "Customer" },
                    customerId = admin ? (Guid?)null : Guid.Parse("11111111-1111-1111-1111-111111111111"),
                });
            case ("POST", "/api/v1/auth/logout"):
                if (LogoutFails)
                {
                    return Problem(HttpStatusCode.InternalServerError, "internal-error");
                }

                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    _ = refreshTokens.TryRemove(doc.RootElement.GetProperty("refreshToken").GetString()!, out _);
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("POST", "/api/v1/auth/change-password"):
                return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        HttpResponseMessage ok = Json(HttpStatusCode.OK, new { path, ok = true });
        _ = ok.Headers.TryAddWithoutValidation("Set-Cookie", "api-cookie=1; Path=/");
        _ = ok.Headers.TryAddWithoutValidation("Server", "FakeApi");
        _ = ok.Headers.TryAddWithoutValidation("ETag", "\"42\"");
        _ = ok.Headers.TryAddWithoutValidation("Cache-Control", "private, no-cache");
        return ok;
    }

    private HttpResponseMessage Login(string body)
    {
        if (ThrowOnLogin)
        {
            throw new ArgumentException("boom-secret-detail");
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        string email = doc.RootElement.GetProperty("email").GetString() ?? string.Empty;
        string password = doc.RootElement.GetProperty("password").GetString() ?? string.Empty;

        if (email == "unconfirmed@example.com")
        {
            return Problem(HttpStatusCode.Forbidden, "email-not-confirmed");
        }

        if (email == "throttled@example.com")
        {
            HttpResponseMessage limited = Problem(HttpStatusCode.TooManyRequests, "rate-limited");
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return limited;
        }

        if (email == "teapot@example.com")
        {
            return Problem((HttpStatusCode)418, "teapot");
        }

        if (email == "badshape@example.com")
        {
            return Json(HttpStatusCode.BadRequest, new
            {
                type = "/problems/validation-failed",
                errors = new Dictionary<string, string[]> { ["email"] = ["Email is not valid."] },
                internalSecret = Canary,
            }, "application/problem+json");
        }

        if ((email != CustomerEmail && email != AdminEmail) || password != Password)
        {
            return Problem(HttpStatusCode.Unauthorized, "invalid-credentials");
        }

        return Tokens(email);
    }

    private async Task<HttpResponseMessage> RefreshAsync(string body)
    {
        if (RefreshGate is not null)
        {
            await RefreshGate;
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        string token = doc.RootElement.GetProperty("refreshToken").GetString() ?? string.Empty;
        if (RefreshFails)
        {
            return Problem(HttpStatusCode.ServiceUnavailable, "service-unavailable");
        }

        if (RefreshRejects || !refreshTokens.TryRemove(token, out string? email))
        {
            ReuseDetected++;
            return Problem(HttpStatusCode.Unauthorized, "invalid-refresh-token");
        }

        return Tokens(email);
    }

    private HttpResponseMessage Tokens(string email)
    {
        int n = Interlocked.Increment(ref counter);
        string access = $"at-{n}-{Guid.NewGuid():N}";
        string refresh = $"rt-{n}-{Guid.NewGuid():N}";
        DateTimeOffset now = time.GetUtcNow();
        accessTokens[access] = now + AccessLifetime;
        refreshTokens[refresh] = email;
        accessTokenOwners[access] = email;
        issued.Add(access);
        issued.Add(refresh);
        return Json(HttpStatusCode.OK, new
        {
            tokenType = "Bearer",
            accessToken = access,
            accessTokenExpiresAt = now + AccessLifetime,
            refreshToken = refresh,
            refreshTokenExpiresAt = now.AddDays(7),
        });
    }

    private readonly ConcurrentDictionary<string, string> accessTokenOwners = new();

    private string EmailFor(string accessToken) => accessTokenOwners.TryGetValue(accessToken, out string? e) ? e : CustomerEmail;

    private static HttpResponseMessage Problem(HttpStatusCode status, string slug) =>
        Json(status, new { type = $"/problems/{slug}", title = Canary, status = (int)status, detail = Canary, internalSecret = Canary }, "application/problem+json");

    private static HttpResponseMessage Json(HttpStatusCode status, object value, string contentType = "application/json") =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, JsonSerializerOptions.Web), Encoding.UTF8, contentType),
        };
}

public sealed class FakeUpstreamHandlerFactory(FakeApi api) : DemoApi.Bff.Upstream.IUpstreamHandlerFactory
{
    public HttpMessageHandler Create() => api;
}