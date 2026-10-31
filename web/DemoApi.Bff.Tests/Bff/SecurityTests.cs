using System.Net;
using System.Text.RegularExpressions;

using DemoApi.Bff.Tests.Support;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace DemoApi.Bff.Tests.Bff;

public sealed class SecurityTests : BffTestBase
{
    [Fact]
    public async Task No_token_appears_in_any_response_or_log()
    {
        BffClient client = await SignInAsync();
        List<string> seen = [];
        seen.Add(client.LastSetCookie!);

        foreach ((HttpMethod method, string url) in new[]
        {
            (HttpMethod.Get, "/bff/user"),
            (HttpMethod.Get, "/api/v1/products"),
            (HttpMethod.Get, "/api/v1/auth/me"),
            (HttpMethod.Get, "/"),
            (HttpMethod.Get, "/nope/missing.js"),
        })
        {
            using HttpResponseMessage r = await client.SendAsync(method, url);
            seen.Add(await TextOf(r));
        }

        using HttpResponseMessage bad = await Client.LoginAsync("nobody@example.com", FakeApi.Password);
        seen.Add(await TextOf(bad));
        using HttpResponseMessage logout = await client.SendAsync(HttpMethod.Post, "/bff/logout");
        seen.Add(await TextOf(logout));
        seen.AddRange(Factory.Logs.Entries.Select(e => e.Message));

        Api.IssuedTokens.ShouldNotBeEmpty();
        string everything = string.Join('\n', seen);
        everything.ShouldNotContain("accessToken", Case.Insensitive);
        everything.ShouldNotContain("refreshToken", Case.Insensitive);
        everything.ShouldNotContain(FakeApi.Password);
        foreach (string token in Api.IssuedTokens)
        {
            everything.ShouldNotContain(token);
        }
    }

    [Theory]
    [InlineData("nocsrf")]
    [InlineData("noorigin")]
    [InlineData("wrongorigin")]
    [InlineData("nullorigin")]
    [InlineData("otherport")]
    [InlineData("crosssite")]
    public async Task Unsafe_requests_without_csrf_proof_get_403_before_forwarding(string variant)
    {
        BffClient client = await SignInAsync();
        int before = Api.Requests.Count;

        foreach (string url in new[] { "/bff/login", "/bff/logout", "/api/v1/orders" })
        {
            using HttpResponseMessage response = variant switch
            {
                "nocsrf" => await client.SendAsync(HttpMethod.Post, url, new { }, csrf: false),
                "noorigin" => await client.SendAsync(HttpMethod.Post, url, new { }, origin: null),
                "wrongorigin" => await client.SendAsync(HttpMethod.Post, url, new { }, origin: "https://evil.example"),
                "nullorigin" => await client.SendAsync(HttpMethod.Post, url, new { }, origin: "null"),
                "otherport" => await client.SendAsync(HttpMethod.Post, url, new { }, origin: "https://localhost:5173"),
                _ => await client.SendAsync(HttpMethod.Post, url, new { }, configure: r => r.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site")),
            };

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await JsonOf(response)).GetProperty("type").GetString().ShouldBe("/problems/csrf-check-failed");
        }

        Api.Requests.Count.ShouldBe(before);
        using HttpResponseMessage still = await client.GetAsync("/bff/user");
        still.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_on_logout_is_405_and_there_is_no_cors()
    {
        using HttpResponseMessage get = await Client.GetAsync("/bff/logout");
        get.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);

        using HttpResponseMessage preflight = await Client.SendAsync(
            HttpMethod.Options, "/api/v1/products", origin: "https://evil.example",
            configure: r => r.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET"));
        preflight.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("null")]
    [InlineData("http://localhost:8081/")]
    [InlineData("http://localhost:8081/path")]
    public void Startup_fails_for_invalid_allowed_origins(string origin)
    {
        using BffFactory factory = new();
        factory.Settings["Bff:AllowedOrigins:0"] = origin;

        Should.Throw<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public async Task Every_response_carries_the_full_security_header_set()
    {
        BffClient client = await SignInAsync();
        foreach (string url in new[] { "/", "/products/abc", "/bff/user", "/api/v1/products", "/api/v1/nope", "/api/v1/auth/login" })
        {
            using HttpResponseMessage r = await client.GetAsync(url);
            string csp = r.Headers.GetValues("Content-Security-Policy").Single();
            csp.ShouldContain("default-src 'self'");
            csp.ShouldContain("frame-ancestors 'none'");
            csp.ShouldNotContain("unsafe-inline");
            r.Headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY");
            r.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
            r.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
            r.Headers.Contains("Permissions-Policy").ShouldBeTrue();
            r.Headers.GetValues("Cross-Origin-Opener-Policy").Single().ShouldBe("same-origin");
            r.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
            r.Headers.Contains("Server").ShouldBeFalse();
        }
    }

    [Fact]
    public async Task A_session_blob_copied_to_another_key_is_rejected()
    {
        BffClient alice = await SignInAsync();
        BffClient bob = await SignInAsync(FakeApi.AdminEmail);
        IDistributedCache cache = Factory.Services.GetRequiredService<IDistributedCache>();
        byte[] aliceBlob = (await cache.GetAsync("bff:session:" + alice.Cookie))!;
        await cache.SetAsync("bff:session:" + bob.Cookie, aliceBlob);

        using HttpResponseMessage response = await bob.GetAsync("/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await cache.GetAsync("bff:session:" + bob.Cookie)).ShouldBeNull();
    }

    [Fact]
    public async Task Admin_session_ends_after_idle_timeout_and_reports_idle_seconds()
    {
        BffClient admin = await SignInAsync(FakeApi.AdminEmail);
        using HttpResponseMessage first = await admin.GetAsync("/bff/user");
        (await JsonOf(first)).GetProperty("idleSecondsLeft").GetInt32().ShouldBe(1800);

        Factory.Time.Advance(TimeSpan.FromMinutes(10));
        using HttpResponseMessage later = await admin.GetAsync("/bff/user");
        (await JsonOf(later)).GetProperty("idleSecondsLeft").GetInt32().ShouldBe(1200);

        Factory.Time.Advance(TimeSpan.FromMinutes(21));
        using HttpResponseMessage ended = await admin.GetAsync("/bff/user");
        ended.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonOf(ended)).GetProperty("type").GetString().ShouldBe("/problems/session-ended");
        Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1);
    }

    [Fact]
    public async Task Spa_falls_back_to_index_for_app_paths_only()
    {
        using HttpResponseMessage page = await Client.GetAsync("/products/42");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).ShouldContain("<title>spa</title>");
        page.Headers.CacheControl!.NoStore.ShouldBeTrue();

        using HttpResponseMessage asset = await Client.GetAsync("/assets/missing.js");
        asset.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Exceptions_become_generic_problems()
    {
        Factory.Api.LogoutFails = false;
        BffClient client = await SignInAsync();
        // A malformed JSON body is a client error, never a 500 with details.
        using HttpResponseMessage response = await Client.SendAsync(HttpMethod.Post, "/bff/login", null, configure: r =>
        {
            r.Content = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json");
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Regex.IsMatch(await response.Content.ReadAsStringAsync(), "Exception|StackTrace").ShouldBeFalse();
        client.Cookie.ShouldNotBeNull();
    }
}