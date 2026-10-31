using System.Net;

using DemoApi.Bff.Tests.Support;

namespace DemoApi.Bff.Tests.Bff;

public sealed class RefreshAndLogoutTests : BffTestBase
{
    [Fact]
    public async Task Twenty_parallel_requests_cause_exactly_one_refresh()
    {
        BffClient client = await SignInAsync();
        string loginBearer = Api.Requests.Last(r => r.Path == "/api/v1/auth/me").Headers["Authorization"];
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.RefreshGate = gate.Task;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5)); // 30 s left

        Task<HttpResponseMessage>[] calls = [.. Enumerable.Range(0, 20).Select(_ => client.GetAsync("/api/v1/products"))];
        for (int i = 0; i < 500 && Api.RefreshCalls == 0; i++)
        {
            await Task.Delay(10);
        }

        await Task.Delay(150);
        Api.RefreshCalls.ShouldBe(1);
        gate.SetResult();
        HttpResponseMessage[] responses = await Task.WhenAll(calls);

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        Api.RefreshCalls.ShouldBe(1);
        Api.ReuseDetected.ShouldBe(0);
        string[] bearers = [.. Api.Requests.Where(r => r.Path == "/api/v1/products").Select(r => r.Headers["Authorization"])];
        bearers.Length.ShouldBe(20);
        bearers.Distinct().Count().ShouldBe(1);
        bearers[0].ShouldNotBe(loginBearer);
    }

    [Fact]
    public async Task Api_401_triggers_one_refresh_and_one_replay()
    {
        BffClient client = await SignInAsync();
        Api.InvalidateAccessTokens();

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/api/v1/orders", new { a = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Api.RefreshCalls.ShouldBe(1);
        Api.Requests.Where(r => r.Path == "/api/v1/orders").ToArray().Length.ShouldBe(2);
        Api.Requests.Last().Body.ShouldContain("\"a\":1"); // the replay carries the buffered body
    }

    [Fact]
    public async Task Rejected_refresh_ends_the_session_with_session_ended()
    {
        BffClient client = await SignInAsync();
        string old = client.Cookie!;
        Api.RefreshRejects = true;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        using HttpResponseMessage response = await client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonOf(response)).GetProperty("type").GetString().ShouldBe("/problems/session-ended");
        response.Headers.GetValues("Set-Cookie").Single().ShouldContain("1970");
        BffClient replay = Factory.NewClient();
        replay.Cookie = old;
        using HttpResponseMessage after = await replay.GetAsync("/bff/user");
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Api.Count("GET", "/api/v1/products").ShouldBe(0);
    }

    [Fact]
    public async Task Logout_clears_session_and_cookie_and_revokes_the_refresh_token()
    {
        BffClient client = await SignInAsync();
        string old = client.Cookie!;

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Single().ShouldContain("1970");
        response.Headers.GetValues("Clear-Site-Data").Single().ShouldBe("\"cache\", \"storage\"");
        Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1);
        BffClient replay = Factory.NewClient();
        replay.Cookie = old;
        using HttpResponseMessage after = await replay.GetAsync("/bff/user");
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_clears_even_when_the_api_fails()
    {
        BffClient client = await SignInAsync();
        string old = client.Cookie!;
        Api.LogoutFails = true;

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Single().ShouldContain("1970");
        BffClient replay = Factory.NewClient();
        replay.Cookie = old;
        using HttpResponseMessage after = await replay.GetAsync("/bff/user");
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_without_a_session_still_returns_204_and_expires_the_cookie()
    {
        using HttpResponseMessage response = await Client.SendAsync(HttpMethod.Post, "/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Single().ShouldContain("1970");
    }

    [Fact]
    public async Task Logout_after_the_access_token_expired_still_revokes_at_the_api()
    {
        BffClient client = await SignInAsync();
        Factory.Time.Advance(TimeSpan.FromMinutes(20));

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Api.RefreshCalls.ShouldBe(1);
        Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1);
    }
}