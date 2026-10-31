using System.Net;
using System.Text.RegularExpressions;

using DemoApi.Bff.Tests.Support;

namespace DemoApi.Bff.Tests.Bff;

public sealed class ProxyTests : BffTestBase
{
    [Fact]
    public async Task Proxy_strips_browser_headers_adds_bearer_and_cleans_the_response()
    {
        BffClient client = await SignInAsync();

        using HttpResponseMessage response = await client.SendAsync(
            HttpMethod.Post, "/api/v1/orders?x=1", new { a = 1 },
            configure: r =>
            {
                r.Headers.TryAddWithoutValidation("Authorization", "Bearer evil");
                r.Headers.TryAddWithoutValidation("Forwarded", "for=6.6.6.6");
                r.Headers.TryAddWithoutValidation("X-Forwarded-For", "6.6.6.6");
                r.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example");
                r.Headers.TryAddWithoutValidation("X-HTTP-Method-Override", "DELETE");
                r.Headers.TryAddWithoutValidation("X-Original-URL", "/admin");
                r.Headers.TryAddWithoutValidation("X-Real-IP", "6.6.6.6");
                r.Headers.TryAddWithoutValidation("True-Client-IP", "6.6.6.6");
                r.Headers.TryAddWithoutValidation("Via", "1.1 evil");
                r.Headers.TryAddWithoutValidation("Referer", "https://localhost/secret?token=abc");
                r.Headers.TryAddWithoutValidation("X-Custom", "nope");
                r.Headers.TryAddWithoutValidation("Accept", "application/json");
                r.Headers.TryAddWithoutValidation("If-Match", "\"7\"");
                r.Headers.TryAddWithoutValidation("User-Agent", "bff-test-agent");
                r.Headers.TryAddWithoutValidation("traceparent", "00-11111111111111111111111111111111-2222222222222222-01");
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        RecordedRequest seen = Api.Requests.Last();
        seen.Path.ShouldBe("/api/v1/orders");
        seen.Body.ShouldContain("\"a\":1");
        string bearer = seen.Headers["Authorization"];
        bearer.ShouldStartWith("Bearer at-");
        Api.IssuedTokens.ShouldContain(bearer["Bearer ".Length..]);
        foreach (string dropped in new[] { "Cookie", "Forwarded", "X-Forwarded-For", "X-Forwarded-Host", "X-HTTP-Method-Override", "X-Original-URL", "X-CSRF", "Origin", "X-Real-IP", "True-Client-IP", "Via", "Referer", "X-Custom" })
        {
            seen.Headers.ContainsKey(dropped).ShouldBeFalse(dropped);
        }

        // The allow-list still lets the needed headers through.
        seen.Headers["If-Match"].ShouldBe("\"7\"");
        seen.Headers["User-Agent"].ShouldContain("bff-test-agent");
        seen.Headers["Accept"].ShouldContain("application/json");
        seen.Headers["Content-Type"].ShouldContain("application/json");

        Regex.IsMatch(seen.Headers["traceparent"], "^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$").ShouldBeTrue();
        seen.Headers["traceparent"].ShouldNotContain("2222222222222222"); // a new span, not the browser one

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        response.Headers.Contains("Server").ShouldBeFalse();
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        response.Headers.ETag!.Tag.ShouldBe("\"42\"");
    }

    [Fact]
    public async Task Proxy_needs_a_session()
    {
        using HttpResponseMessage none = await Client.GetAsync("/api/v1/products");
        none.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonOf(none)).GetProperty("type").GetString().ShouldBe("/problems/not-signed-in");

        Client.Cookie = new string('B', 43);
        using HttpResponseMessage unknown = await Client.GetAsync("/api/v1/products");
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonOf(unknown)).GetProperty("type").GetString().ShouldBe("/problems/session-ended");
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Anonymous_auth_routes_are_forwarded_without_a_token()
    {
        using HttpResponseMessage response = await Client.SendAsync(HttpMethod.Post, "/api/v1/auth/register", new { email = "a@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Api.Requests.Single().Headers.ContainsKey("Authorization").ShouldBeFalse();
    }

    [Fact]
    public async Task Allowed_routes_work_and_typed_constraints_apply()
    {
        BffClient client = await SignInAsync();
        using HttpResponseMessage list = await client.GetAsync("/api/v1/products");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage item = await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");
        item.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage role = await client.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{Guid.NewGuid()}/roles/Admin", new { });
        role.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/refresh")]
    [InlineData("POST", "/api/v1/auth/logout")]
    [InlineData("POST", "/api/v1/auth/logout-all")]
    [InlineData("DELETE", "/api/v1/products")]
    [InlineData("GET", "/api/v1/products/not-a-guid")]
    [InlineData("GET", "/api/v1/Products")]
    [InlineData("GET", "/api/v1/products/")]
    [InlineData("GET", "/api/v1//products")]
    [InlineData("GET", "/api/v1/products%2Fabc")]
    [InlineData("GET", "/api/v1/products%5Cabc")]
    [InlineData("GET", "/api/v1/%2e%2e/auth/login")]
    [InlineData("GET", "/api/v1/products/../auth/refresh")]
    [InlineData("GET", "/API/v1/products")]
    [InlineData("GET", "/api/unknown")]
    [InlineData("GET", "/api")]
    [InlineData("GET", "/bff/unknown")]
    [InlineData("GET", "/BFF/user")]
    [InlineData("GET", "/scalar")]
    [InlineData("GET", "/scalar/v1")]
    [InlineData("GET", "/openapi/v1.json")]
    [InlineData("GET", "/health/ready")]
    [InlineData("GET", "/health/live")]
    public async Task Everything_outside_the_allow_list_is_404_and_never_reaches_the_api(string method, string path)
    {
        BffClient client = await SignInAsync();
        int before = Api.Requests.Count;

        using HttpResponseMessage response = await client.SendAsync(new HttpMethod(method), path, method == "GET" ? null : new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await JsonOf(response)).GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        Api.Requests.Count.ShouldBe(before);
    }

    [Fact]
    public async Task Stretch_endpoints_are_not_listed()
    {
        BffClient client = await SignInAsync();
        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, $"/api/v1/admin/users/{Guid.NewGuid()}/revoke-sessions", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Change_password_success_ends_the_session()
    {
        BffClient client = await SignInAsync();
        string old = client.Cookie!;

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/api/v1/auth/change-password", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        client.Cookie.ShouldBeNull();
        BffClient replay = Factory.NewClient();
        replay.Cookie = old;
        using HttpResponseMessage after = await replay.GetAsync("/bff/user");
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}