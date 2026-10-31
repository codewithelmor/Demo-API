using System.Net;

using DemoApi.Bff.Tests.Support;

namespace DemoApi.Bff.Tests.Bff;

public sealed class LoginTests : BffTestBase
{
    [Fact]
    public async Task Login_happy_path_returns_summary_and_sets_host_cookie()
    {
        using HttpResponseMessage response = await Client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        System.Text.Json.JsonElement body = await JsonOf(response);
        body.GetProperty("email").GetString().ShouldBe(FakeApi.CustomerEmail);
        body.GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
        body.GetProperty("roles")[0].GetString().ShouldBe("Customer");
        body.GetProperty("customerId").GetString().ShouldNotBeNull();

        string cookie = Client.LastSetCookie!.ToLowerInvariant();
        cookie.ShouldStartWith("__host-demoapi=");
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("path=/");
        cookie.ShouldNotContain("domain=");
        Client.Cookie!.Length.ShouldBeGreaterThanOrEqualTo(43);

        Api.Count("POST", "/api/v1/auth/login").ShouldBe(1);
        Api.Count("GET", "/api/v1/auth/me").ShouldBe(1);

        using HttpResponseMessage user = await Client.GetAsync("/bff/user");
        user.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonOf(user)).GetProperty("email").GetString().ShouldBe(FakeApi.CustomerEmail);
    }

    [Fact]
    public async Task Login_with_bad_credentials_gives_own_problem_without_api_body_or_cookie()
    {
        using HttpResponseMessage response = await Client.LoginAsync("nobody@example.com", FakeApi.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        System.Text.Json.JsonElement body = await JsonOf(response);
        body.GetProperty("type").GetString().ShouldBe("/problems/invalid-credentials");
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        (await TextOf(response)).ShouldNotContain(FakeApi.Canary);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Login_passes_unconfirmed_throttled_and_validation_as_own_problems()
    {
        using HttpResponseMessage unconfirmed = await Client.LoginAsync("unconfirmed@example.com", FakeApi.Password);
        unconfirmed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await JsonOf(unconfirmed)).GetProperty("type").GetString().ShouldBe("/problems/email-not-confirmed");

        using HttpResponseMessage throttled = await Client.LoginAsync("throttled@example.com", FakeApi.Password);
        throttled.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        throttled.Headers.GetValues("Retry-After").Single().ShouldBe("42");

        using HttpResponseMessage shape = await Client.LoginAsync("badshape@example.com", FakeApi.Password);
        shape.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        System.Text.Json.JsonElement body = await JsonOf(shape);
        body.GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe("Email is not valid.");

        foreach (HttpResponseMessage r in new[] { unconfirmed, throttled, shape })
        {
            (await TextOf(r)).ShouldNotContain(FakeApi.Canary);
        }
    }

    [Fact]
    public async Task Login_with_missing_password_is_400_without_calling_the_api()
    {
        using HttpResponseMessage response = await Client.SendAsync(HttpMethod.Post, "/bff/login", new { email = "a@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Eleventh_login_in_a_minute_is_429_with_retry_after_and_forwarded_for_is_ignored()
    {
        for (int i = 0; i < 10; i++)
        {
            string spoofed = $"10.0.0.{i + 1}";
            using HttpResponseMessage ok = await Client.SendAsync(
                HttpMethod.Post, "/bff/login", new { email = "nobody@example.com", password = "x" },
                configure: r => r.Headers.TryAddWithoutValidation("X-Forwarded-For", spoofed));
            ok.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using HttpResponseMessage limited = await Client.LoginAsync("nobody@example.com", "x");
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").ShouldBeTrue();
        (await JsonOf(limited)).GetProperty("type").GetString().ShouldBe("/problems/rate-limited");
    }

    [Fact]
    public async Task Planted_cookie_is_not_adopted_and_a_second_login_ends_the_first_session()
    {
        string planted = new('A', 43);
        Client.Cookie = planted;
        using HttpResponseMessage first = await Client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        Client.Cookie.ShouldNotBe(planted);

        string firstSession = Client.Cookie!;
        using HttpResponseMessage second = await Client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        Client.Cookie.ShouldNotBe(firstSession);
        Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1);

        BffClient old = Factory.NewClient();
        old.Cookie = firstSession;
        using HttpResponseMessage stale = await old.GetAsync("/bff/user");
        stale.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonOf(stale)).GetProperty("type").GetString().ShouldBe("/problems/session-ended");
    }

    [Fact]
    public async Task Unmapped_4xx_from_the_api_is_logged_with_operation_and_status_only()
    {
        using HttpResponseMessage response = await Client.LoginAsync("teapot@example.com", FakeApi.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (string Level, string Message)[] hits = [.. Factory.Logs.Entries
            .Where(e => e.Message.Contains("returned 418", StringComparison.Ordinal))
            .Select(e => (e.Level.ToString(), e.Message))];
        hits.Length.ShouldBe(1);
        hits[0].Level.ShouldBe("Warning");
        hits[0].Message.ShouldContain("login");
        hits[0].Message.ShouldNotContain(FakeApi.Canary);
        hits[0].Message.ShouldNotContain(FakeApi.Password);
    }
}