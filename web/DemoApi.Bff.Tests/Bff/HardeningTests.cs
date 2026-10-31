using System.Net;

using DemoApi.Bff.Session;
using DemoApi.Bff.Tests.Support;
using DemoApi.Bff.Upstream;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DemoApi.Bff.Tests.Bff;

/// <summary>Fixes from the security review: L-1, L-2, I-1 (refresh, sign-out), I-8 (limits and size).</summary>
public sealed class RefreshHardeningTests : BffTestBase
{
    private async Task WaitForRefreshCallAsync()
    {
        for (int i = 0; i < 500 && Api.RefreshCalls == 0; i++)
        {
            await Task.Delay(10);
        }

        Api.RefreshCalls.ShouldBe(1);
    }

    [Fact]
    public async Task A_flight_abandoned_by_its_only_waiter_does_not_leave_a_stale_outcome()
    {
        BffClient client = await SignInAsync();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.RefreshGate = gate.Task;
        Api.RefreshFails = true;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        using CancellationTokenSource abort = new();
        Task<HttpResponseMessage> call = client.SendAsync(HttpMethod.Get, "/api/v1/products", cancellationToken: abort.Token);
        await WaitForRefreshCallAsync();
        await abort.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(call);

        gate.SetResult(); // the refresh finishes (503) with nobody waiting
        await Task.Delay(200);
        Api.RefreshFails = false;

        using HttpResponseMessage next = await client.GetAsync("/api/v1/products");

        next.StatusCode.ShouldBe(HttpStatusCode.OK); // a new flight, not the stale 503
        Api.RefreshCalls.ShouldBe(2);
    }

    [Fact]
    public async Task A_browser_abort_during_refresh_keeps_the_session_consistent()
    {
        BffClient client = await SignInAsync();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.RefreshGate = gate.Task;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        using CancellationTokenSource abort = new();
        Task<HttpResponseMessage> call = client.SendAsync(HttpMethod.Get, "/api/v1/products", cancellationToken: abort.Token);
        await WaitForRefreshCallAsync();
        await abort.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(call);
        gate.SetResult();
        await Task.Delay(200);

        using HttpResponseMessage next = await client.GetAsync("/api/v1/products");

        next.StatusCode.ShouldBe(HttpStatusCode.OK);
        Api.RefreshCalls.ShouldBe(1); // the saved pair is used; no second EP-05, so no reuse
        Api.ReuseDetected.ShouldBe(0);
    }

    [Fact]
    public async Task A_refresh_that_loses_to_a_session_end_revokes_the_new_pair()
    {
        BffClient client = await SignInAsync();
        string loginBearer = Api.Requests.Last(r => r.Path == "/api/v1/auth/me").Headers["Authorization"];
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.RefreshGate = gate.Task;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        Task<HttpResponseMessage> call = client.GetAsync("/api/v1/products");
        await WaitForRefreshCallAsync();
        await Factory.Services.GetRequiredService<SessionStore>().DeleteAsync(client.Cookie!); // session ends while EP-05 runs
        gate.SetResult();
        using HttpResponseMessage response = await call;

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonOf(response)).GetProperty("type").GetString().ShouldBe("/problems/session-ended");
        Api.Count("GET", "/api/v1/products").ShouldBe(0);
        RecordedRequest revoke = Api.Requests.Single(r => r.Path == "/api/v1/auth/logout");
        revoke.Headers["Authorization"].ShouldNotBe(loginBearer); // the NEW pair was revoked
        Api.RefreshCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Logout_during_a_refresh_leaves_the_session_deleted_and_revokes_the_new_pair()
    {
        BffClient client = await SignInAsync();
        string old = client.Cookie!;
        string loginBearer = Api.Requests.Last(r => r.Path == "/api/v1/auth/me").Headers["Authorization"];
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.RefreshGate = gate.Task;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        Task<HttpResponseMessage> call = client.GetAsync("/api/v1/products");
        await WaitForRefreshCallAsync();
        Task<HttpResponseMessage> logout = client.SendAsync(HttpMethod.Post, "/bff/logout");
        await Task.Delay(100);
        gate.SetResult();
        using HttpResponseMessage logoutResponse = await logout;
        using HttpResponseMessage callResponse = await call;

        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        callResponse.Dispose();
        Api.RefreshCalls.ShouldBe(1);
        Api.Requests.Single(r => r.Path == "/api/v1/auth/logout").Headers["Authorization"].ShouldNotBe(loginBearer);
        BffClient replay = Factory.NewClient();
        replay.Cookie = old;
        using HttpResponseMessage after = await replay.GetAsync("/bff/user");
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_still_tries_to_revoke_when_the_refresh_cannot_run()
    {
        BffClient client = await SignInAsync();
        Factory.Time.Advance(TimeSpan.FromMinutes(20));
        Api.RefreshFails = true;

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Single().ShouldContain("1970");
        Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1);
    }

    [Fact]
    public async Task Logout_does_not_spend_the_refresh_budget()
    {
        Factory.Settings["Bff:RefreshPermitLimit"] = "1";
        BffClient client = await SignInAsync();
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));
        using HttpResponseMessage spend = await client.GetAsync("/api/v1/products"); // uses the only permit
        spend.StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Time.Advance(TimeSpan.FromMinutes(20));

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Api.RefreshCalls.ShouldBe(2);
        Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_budget_is_enforced_for_requests_with_429()
    {
        Factory.Settings["Bff:RefreshPermitLimit"] = "1";
        BffClient client = await SignInAsync();
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));
        using HttpResponseMessage first = await client.GetAsync("/api/v1/products");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        using HttpResponseMessage second = await client.GetAsync("/api/v1/products");

        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.Contains("Retry-After").ShouldBeTrue();
        Api.RefreshCalls.ShouldBe(1);
    }

    [Fact]
    public async Task A_login_whose_session_cannot_be_stored_revokes_the_new_tokens()
    {
        Factory.Customize = services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddSingleton<IDistributedCache, FailingWritesCache>();
        };

        using HttpResponseMessage response = await Client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1);
    }

    [Fact]
    public async Task A_refresh_save_does_not_rewind_recorded_activity()
    {
        SessionStore store = Factory.Services.GetRequiredService<SessionStore>();
        DateTimeOffset now = Factory.Time.GetUtcNow();
        TokenSet tokens = new("a", now.AddMinutes(15), "r", now.AddDays(7));
        UserSummary user = new(Guid.NewGuid(), "x@example.com", true, ["Customer"], null);
        SessionRecord created = await store.CreateAsync(tokens, user, now, CancellationToken.None);
        await store.TouchAsync(created.SessionId, now.AddMinutes(5), CancellationToken.None);

        SessionRecord? saved = await store.TryReplaceAsync(created.SessionId, created.Version, created with { Version = 2, AccessToken = "b" }, CancellationToken.None);

        saved.ShouldNotBeNull();
        saved.AccessToken.ShouldBe("b");
        saved.LastActivityAt.ShouldBe(now.AddMinutes(5));
        (await store.TryReplaceAsync(created.SessionId, 1, saved with { Version = 3 }, CancellationToken.None)).ShouldBeNull(); // stale version
    }

    private sealed class FailingWritesCache : IDistributedCache
    {
        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("down");

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("down");

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }
}

public sealed class DevelopmentErrorTests : BffTestBase
{
    private static readonly string[] Forbidden = ["boom-secret-detail", "Exception", "StackTrace", "   at ", "<html", "Developer"];

    [Fact]
    public async Task Development_forced_500_is_a_generic_problem_without_a_developer_page()
    {
        using BffFactory dev = new() { Environment = "Development" };
        dev.Api.ThrowOnLogin = true;
        BffClient client = dev.NewClient();

        using HttpResponseMessage response = await client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        await AssertGenericAsync(response);
    }

    [Fact]
    public async Task Development_failing_exception_handler_still_gives_a_generic_problem()
    {
        using BffFactory dev = new()
        {
            Environment = "Development",
            Customize = services =>
            {
                services.RemoveAll<IExceptionHandler>();
                services.AddSingleton<IExceptionHandler, ThrowingHandler>();
            },
        };
        dev.Api.ThrowOnLogin = true;
        BffClient client = dev.NewClient();

        using HttpResponseMessage response = await client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        await AssertGenericAsync(response);
    }

    [Fact]
    public async Task Production_failing_exception_handler_still_gives_a_generic_problem()
    {
        Factory.Customize = services =>
        {
            services.RemoveAll<IExceptionHandler>();
            services.AddSingleton<IExceptionHandler, ThrowingHandler>();
        };
        Api.ThrowOnLogin = true;

        using HttpResponseMessage response = await Client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        await AssertGenericAsync(response);
    }

    private static async Task AssertGenericAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        string text = response.ToString() + await response.Content.ReadAsStringAsync();
        foreach (string word in Forbidden)
        {
            text.ShouldNotContain(word);
        }

        response.Headers.Contains("Content-Security-Policy").ShouldBeTrue();
    }

    private sealed class ThrowingHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("handler-failed-secret");
    }
}

public sealed class StartupValidationTests
{
    [Theory]
    [InlineData("Production", null, null, true)]
    [InlineData("Production", null, "true", false)]
    [InlineData("Production", null, "false", true)]
    [InlineData("Development", null, null, false)]
    [InlineData("Development", null, "false", true)]
    [InlineData("Production", "localhost:6379", null, false)]
    public void Session_store_needs_valkey_or_an_explicit_in_memory_flag(string environment, string? connection, string? allowInMemory, bool fails)
    {
        using BffFactory factory = new() { Environment = environment };
        factory.Settings["Valkey:ConnectionString"] = connection;
        factory.Settings["Valkey:AllowInMemory"] = allowInMemory;

        if (fails)
        {
            Should.Throw<Exception>(() => factory.CreateClient());
        }
        else
        {
            Should.NotThrow(() => factory.CreateClient());
        }
    }

    [Fact]
    public void Production_requires_an_existing_writable_keys_folder()
    {
        using BffFactory unset = new();
        unset.Settings["DataProtection:KeysPath"] = null;
        Should.Throw<Exception>(() => unset.CreateClient());

        using BffFactory missing = new();
        missing.Settings["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "bff-missing-" + Guid.NewGuid().ToString("N"));
        Should.Throw<Exception>(() => missing.CreateClient());

        using BffFactory good = new();
        Should.NotThrow(() => good.CreateClient());
    }

    [Fact]
    public void Development_may_run_without_a_keys_folder()
    {
        using BffFactory dev = new() { Environment = "Development" };
        dev.Settings["DataProtection:KeysPath"] = null;

        Should.NotThrow(() => dev.CreateClient());
    }
}

public sealed class LimitsAndHealthTests : BffTestBase
{
    [Fact]
    public async Task Ready_result_is_reused_for_a_few_seconds_and_live_does_no_work()
    {
        for (int i = 0; i < 3; i++)
        {
            using HttpResponseMessage ready = await Client.GetAsync("/bff/health/ready");
            ready.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await JsonOf(ready)).GetProperty("status").GetString().ShouldBe("Healthy");
        }

        Api.Count("GET", "/health/ready").ShouldBe(1);
        Factory.Time.Advance(TimeSpan.FromSeconds(9));
        using HttpResponseMessage again = await Client.GetAsync("/bff/health/ready");
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        Api.Count("GET", "/health/ready").ShouldBe(2);

        for (int i = 0; i < 150; i++)
        {
            using HttpResponseMessage live = await Client.GetAsync("/bff/health/live");
            live.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        Api.Count("GET", "/health/ready").ShouldBe(2);
    }

    [Fact]
    public async Task Ready_has_its_own_limit()
    {
        Factory.Settings["Bff:ReadyPermitLimit"] = "2";

        using HttpResponseMessage one = await Client.GetAsync("/bff/health/ready");
        using HttpResponseMessage two = await Client.GetAsync("/bff/health/ready");
        using HttpResponseMessage three = await Client.GetAsync("/bff/health/ready");

        one.StatusCode.ShouldBe(HttpStatusCode.OK);
        two.StatusCode.ShouldBe(HttpStatusCode.OK);
        three.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        three.Headers.Contains("Retry-After").ShouldBeTrue();
        using HttpResponseMessage live = await Client.GetAsync("/bff/health/live");
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sixty_first_anonymous_call_in_a_minute_is_429()
    {
        for (int i = 0; i < 60; i++)
        {
            using HttpResponseMessage r = await Client.GetAsync("/bff/user");
            r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using HttpResponseMessage limited = await Client.GetAsync("/bff/user");

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").ShouldBeTrue();

        BffClient signedIn = Factory.NewClient();
        signedIn.Cookie = null;
        using HttpResponseMessage spoofed = await signedIn.SendAsync(HttpMethod.Get, "/bff/user",
            configure: r => r.Headers.TryAddWithoutValidation("X-Forwarded-For", "9.9.9.9"));
        spoofed.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests); // same bucket: the header is not read
    }

    [Fact]
    public async Task Api_429_is_passed_through_and_never_triggers_a_refresh()
    {
        BffClient client = await SignInAsync();
        Api.ThrottleProxied = true;

        using HttpResponseMessage response = await client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("Retry-After").Single().ShouldBe("7");
        Api.RefreshCalls.ShouldBe(0);
        using HttpResponseMessage user = await client.GetAsync("/bff/user");
        user.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Body_over_one_megabyte_is_413_and_never_reaches_the_api()
    {
        BffClient client = await SignInAsync();
        int before = Api.Requests.Count;

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/api/v1/orders", new { big = new string('x', 1_100_000) });

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await JsonOf(response)).GetProperty("type").GetString().ShouldBe("/problems/payload-too-large");
        Api.Requests.Count.ShouldBe(before);
    }
}