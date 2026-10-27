namespace DemoApi.IntegrationTests.Security;

/// <summary>
/// The permission matrix (docs/product/02-requirements/permission-matrix.md) as data: every S1-S4 endpoint is
/// called as Anonymous, Customer and Admin. Route IDs are random, bodies are "{}": an allowed caller gets past
/// authorization (400/404/2xx), a denied caller gets 401/403 ProblemDetails. Never quarantine these tests.
/// </summary>
[Security("ST-01")]
public sealed class AuthorizationMatrixTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    public enum Access
    {
        Allow,
        Unauthorized,
        Forbidden,
    }

    private const Access A = Access.Allow;
    private const Access U = Access.Unauthorized;
    private const Access F = Access.Forbidden;

    private static readonly (string Ep, string Method, string Route, Access Anonymous, Access Customer, Access Admin)[] Matrix =
    [
        ("EP-01", "POST", "/api/v1/auth/register", A, A, A),
        ("EP-02", "POST", "/api/v1/auth/confirm-email", A, A, A),
        ("EP-03", "POST", "/api/v1/auth/resend-confirmation", A, A, A),
        ("EP-04", "POST", "/api/v1/auth/login", A, A, A),
        ("EP-05", "POST", "/api/v1/auth/refresh", A, A, A),
        ("EP-06", "POST", "/api/v1/auth/logout", U, A, A),
        ("EP-07", "POST", "/api/v1/auth/logout-all", U, A, A),
        ("EP-08", "GET", "/api/v1/auth/me", U, A, A),
        ("EP-09", "POST", "/api/v1/auth/change-password", U, A, A),
        ("EP-10", "POST", "/api/v1/auth/forgot-password", A, A, A),
        ("EP-11", "POST", "/api/v1/auth/reset-password", A, A, A),
        ("EP-12", "GET", "/api/v1/admin/users", U, F, A),
        ("EP-13", "GET", "/api/v1/admin/users/{id}", U, F, A),
        ("EP-14", "POST", "/api/v1/admin/users/{id}/lock", U, F, A),
        ("EP-15", "POST", "/api/v1/admin/users/{id}/unlock", U, F, A),
        ("EP-16", "PUT", "/api/v1/admin/users/{id}/roles/Admin", U, F, A),
        ("EP-17", "DELETE", "/api/v1/admin/users/{id}/roles/Admin", U, F, A),
        ("EP-18", "GET", "/api/v1/admin/roles", U, F, A),
        ("EP-19", "POST", "/api/v1/admin/users", U, F, A),
        ("EP-22", "GET", "/api/v1/products", U, A, A),
        ("EP-23", "GET", "/api/v1/products/{id}", U, A, A),
        ("EP-24", "POST", "/api/v1/products", U, F, A),
        ("EP-25", "PUT", "/api/v1/products/{id}", U, F, A),
        ("EP-26", "DELETE", "/api/v1/products/{id}", U, F, A),
        ("EP-27", "POST", "/api/v1/products/{id}/restore", U, F, A),
        ("EP-29", "POST", "/api/v1/customers", U, F, A),
        ("EP-30", "GET", "/api/v1/customers", U, F, A),
        ("EP-31", "GET", "/api/v1/customers/{id}", U, F, A),
        ("EP-32", "PUT", "/api/v1/customers/{id}", U, F, A),
        ("EP-33", "DELETE", "/api/v1/customers/{id}", U, F, A),
        ("EP-35", "GET", "/api/v1/customers/me", U, A, F),
        ("EP-36", "PUT", "/api/v1/customers/me", U, A, F),
        ("EP-37", "POST", "/api/v1/customers/me/erase", U, A, F),
        ("EP-38", "POST", "/api/v1/orders", U, F, A),
        ("EP-39", "GET", "/api/v1/orders", U, F, A),
        ("EP-40", "GET", "/api/v1/orders/{id}", U, F, A),
        ("EP-41", "PUT", "/api/v1/orders/{id}/items", U, F, A),
        ("EP-42", "POST", "/api/v1/orders/{id}/status", U, F, A),
        ("EP-43", "POST", "/api/v1/orders/{id}/cancel", U, F, A),
        ("EP-45", "POST", "/api/v1/customers/me/orders", U, A, F),
        ("EP-46", "GET", "/api/v1/customers/me/orders", U, A, F),
        ("EP-47", "GET", "/api/v1/customers/me/orders/{id}", U, A, F),
        ("EP-48", "POST", "/api/v1/customers/me/orders/{id}/cancel", U, A, F),
        ("EP-50", "GET", "/api/v1/reports/order-summaries", U, F, A),
        ("EP-51", "GET", "/api/v1/customers/{id}/order-history", U, F, A),
        ("EP-52", "GET", "/api/v1/reports/sales", U, F, A),
        ("EP-53", "GET", "/api/v1/reports/low-stock", U, F, A),
        ("EP-54", "GET", "/api/v1/reports/top-products", U, F, A),
        ("EP-55", "GET", "/api/v1/audit-logs", U, F, A),
        ("EP-56", "GET", "/health/live", A, A, A),
        ("EP-57", "GET", "/health/ready", A, A, A),
    ];

    public static TheoryData<string, string, string, string> Cases()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var row in Matrix)
        {
            data.Add(row.Ep, row.Method, row.Route, $"{row.Anonymous},{row.Customer},{row.Admin}");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Endpoint_enforces_the_permission_matrix(string ep, string method, string route, string expected)
    {
        var access = expected.Split(',').Select(Enum.Parse<Access>).ToArray();
        var customer = await CustomerAsync();
        var admin = await AdminAsync();

        await AssertAsync(ep, "Anonymous", Anonymous(), method, route, access[0]);
        await AssertAsync(ep, "Customer", customer.Client, method, route, access[1]);
        await AssertAsync(ep, "Admin", admin.Client, method, route, access[2]);
    }

    [Fact]
    public void Matrix_lists_every_registered_endpoint()
    {
        var registered = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => $"{e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Single()} /{e.RoutePattern.RawText!.TrimStart('/')}")
            .Where(r => r.Contains("/api/v1/", StringComparison.Ordinal) || r.Contains("/health/", StringComparison.Ordinal))
            .Select(Normalize)
            .Order()
            .ToArray();
        var listed = Matrix.Select(r => Normalize($"{r.Method} {r.Route}")).Order().ToArray();

        registered.ShouldBe(listed, "a new endpoint needs a row in the permission matrix and here");
    }

    [Fact]
    [Security("ST-04")]
    public async Task Expired_wrong_issuer_and_unsigned_tokens_return_401()
    {
        var customer = await CustomerAsync();
        var parts = customer.Tokens.AccessToken.Split('.');
        var unsigned = Base64Url("{\"alg\":\"none\",\"typ\":\"at+jwt\"}") + "." + parts[1] + ".";
        var tampered = parts[0] + "." + Base64Url(Encoding.UTF8.GetString(FromBase64Url(parts[1])).Replace("\"Customer\"", "\"Admin\"", StringComparison.Ordinal)) + "." + parts[2];

        foreach (var token in new[] { unsigned, tampered, customer.Tokens.AccessToken[..^4] + "AAAA", "Bearer" })
        {
            await (await Factory.CreateApiClient(token).GetAsync("/api/v1/auth/me", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + tampered);
        await (await Anonymous().SendAsync(request, Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Security("ST-04")]
    public async Task Refresh_token_cannot_be_used_as_an_access_token()
    {
        var customer = await CustomerAsync();

        await (await Factory.CreateApiClient(customer.Tokens.RefreshToken).GetAsync("/api/v1/auth/me", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    private async Task AssertAsync(string ep, string who, HttpClient client, string method, string route, Access access)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal));
        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, Ct);
        var because = $"{ep} {method} {route} as {who}";
        switch (access)
        {
            case Access.Unauthorized:
                await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
                response.Headers.WwwAuthenticate.ShouldNotBeEmpty(because);
                break;
            case Access.Forbidden:
                await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
                break;
            default:
                var status = (int)response.StatusCode;
                status.ShouldNotBe(401, because + ": " + await response.Content.ReadAsStringAsync(Ct));
                status.ShouldNotBe(403, because + ": " + await response.Content.ReadAsStringAsync(Ct));
                status.ShouldBeLessThan(500, because + ": " + await response.Content.ReadAsStringAsync(Ct));
                break;
        }
    }

    private static string Normalize(string route) =>
        System.Text.RegularExpressions.Regex.Replace(route, @"\{[^}]+\}|(?<=/roles/)Admin$", "{}").Replace("/api/v1/", "/", StringComparison.Ordinal);

    private static string Base64Url(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }
}