using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

using DemoApi.Bff.Security;
using DemoApi.Bff.Session;
using DemoApi.Bff.Tests.Support;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DemoApi.Bff.Tests.Bff;

/// <summary>Security events (L-5, T-55, NFR-F15, F19, F39): fixed event IDs, reason codes, hashed session, masked client.</summary>
public sealed class SecurityEventTests : BffTestBase
{
    private SecurityLog Security => Factory.Services.GetRequiredService<SecurityLog>();

    private CapturedLog[] Events(int id) => [.. Factory.Logs.Events.Where(e => e.Id.Id == id)];

    private CapturedLog Single(int id, string reason) =>
        Events(id).Single(e => e.Values.GetValueOrDefault("Reason") == reason || e.Values.GetValueOrDefault("Outcome") == reason || e.Values.GetValueOrDefault("Policy") == reason);

    [Fact]
    public async Task Sign_in_success_logs_event_5001_with_a_hashed_session_and_a_masked_client()
    {
        BffClient client = await SignInAsync();

        CapturedLog entry = Events(SecurityLog.SignInSucceededId).Single();

        entry.Id.Name.ShouldBe("SignInSucceeded");
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Values["Session"].ShouldBe(Security.HashSession(client.Cookie));
        entry.Values["Session"]!.Length.ShouldBe(16);
        entry.Values["Client"].ShouldBe("unknown"); // the in-process test server has no remote address
        entry.Message.ShouldNotContain(client.Cookie!);
    }

    [Fact]
    public async Task Failed_sign_in_logs_event_5002_with_a_reason_and_never_the_email()
    {
        const string email = "leaky.person@example.com";

        using HttpResponseMessage response = await Client.LoginAsync(email, FakeApi.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        CapturedLog entry = Events(SecurityLog.SignInFailedId).Single();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Values["Reason"].ShouldBe("invalid-credentials");
        AllLogText().ShouldNotContain(email);
        AllLogText().ShouldNotContain("leaky.person");
        AllLogText().ShouldNotContain(FakeApi.Password);
    }

    [Fact]
    public async Task Sign_in_with_a_bad_body_logs_a_validation_reason()
    {
        using HttpResponseMessage response = await Client.SendAsync(HttpMethod.Post, "/bff/login", new { email = "", password = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Events(SecurityLog.SignInFailedId).Single().Values["Reason"].ShouldBe("validation-failed");
    }

    [Theory]
    [InlineData("nocsrf", "missing-csrf-header")]
    [InlineData("wrongorigin", "bad-origin")]
    [InlineData("noorigin", "bad-origin")]
    [InlineData("crosssite", "bad-sec-fetch-site")]
    public async Task Csrf_rejections_log_event_5003_with_the_reason(string variant, string reason)
    {
        using HttpResponseMessage response = variant switch
        {
            "nocsrf" => await Client.SendAsync(HttpMethod.Post, "/bff/logout", new { }, csrf: false),
            "wrongorigin" => await Client.SendAsync(HttpMethod.Post, "/bff/logout", new { }, origin: "https://evil.example"),
            "noorigin" => await Client.SendAsync(HttpMethod.Post, "/bff/logout", new { }, origin: null),
            _ => await Client.SendAsync(HttpMethod.Post, "/bff/logout", new { }, configure: r => r.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site")),
        };

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        CapturedLog entry = Events(SecurityLog.CsrfRejectedId).Single();
        entry.Values["Reason"].ShouldBe(reason);
        entry.Message.ShouldNotContain("evil.example"); // the rejected Origin is not logged
    }

    [Fact]
    public async Task Logout_and_logout_all_log_event_5004_with_the_session_hash()
    {
        BffClient one = await SignInAsync();
        string oneId = one.Cookie!;
        BffClient two = await SignInAsync();
        string twoId = two.Cookie!;

        (await one.SendAsync(HttpMethod.Post, "/bff/logout")).Dispose();
        (await two.SendAsync(HttpMethod.Post, "/bff/logout-all")).Dispose();

        Events(SecurityLog.SessionEndedId).Select(e => (e.Values["Reason"], e.Values["Session"])).ShouldBe(
        [
            ("logout", Security.HashSession(oneId)),
            ("logout-all", Security.HashSession(twoId)),
        ]);
    }

    [Fact]
    public async Task A_new_sign_in_over_an_old_cookie_logs_replaced_on_login()
    {
        BffClient client = await SignInAsync();
        string old = client.Cookie!;

        using HttpResponseMessage again = await client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        Events(SecurityLog.SessionEndedId).Single().Values["Session"].ShouldBe(Security.HashSession(old));
        Events(SecurityLog.SessionEndedId).Single().Values["Reason"].ShouldBe("replaced-on-login");
    }

    [Fact]
    public async Task Admin_idle_timeout_logs_the_session_end()
    {
        BffClient admin = await SignInAsync(FakeApi.AdminEmail);
        string id = admin.Cookie!;
        Factory.Time.Advance(TimeSpan.FromMinutes(31));

        using HttpResponseMessage response = await admin.GetAsync("/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        CapturedLog entry = Single(SecurityLog.SessionEndedId, "idle-timeout");
        entry.Values["Session"].ShouldBe(Security.HashSession(id));
    }

    [Fact]
    public async Task Admin_absolute_timeout_logs_the_session_end()
    {
        BffClient admin = await SignInAsync(FakeApi.AdminEmail);
        string id = admin.Cookie!;
        for (int i = 0; i < 30; i++)
        {
            Factory.Time.Advance(TimeSpan.FromMinutes(25)); // active, so never idle
            using HttpResponseMessage ok = await admin.GetAsync("/api/v1/products");
            if (ok.StatusCode != HttpStatusCode.OK)
            {
                break;
            }
        }

        Single(SecurityLog.SessionEndedId, "absolute-timeout").Values["Session"].ShouldBe(Security.HashSession(id));
    }

    [Fact]
    public async Task A_planted_cookie_logs_unknown_or_planted_and_is_never_adopted()
    {
        string planted = SessionIds.New();
        BffClient client = Factory.NewClient();
        client.Cookie = planted;

        using HttpResponseMessage response = await client.GetAsync("/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Single(SecurityLog.SessionEndedId, "unknown-or-planted-cookie").Values["Session"].ShouldBe(Security.HashSession(planted));
        AllLogText().ShouldNotContain(planted);
    }

    [Fact]
    public async Task A_tampered_blob_logs_tampered_blob_and_the_session_is_removed()
    {
        BffClient client = await SignInAsync();
        string id = client.Cookie!;
        IDistributedCache cache = Factory.Services.GetRequiredService<IDistributedCache>();
        await cache.SetAsync("bff:session:" + client.Cookie, [1, 2, 3, 4]);

        using HttpResponseMessage response = await client.GetAsync("/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Single(SecurityLog.SessionEndedId, "tampered-blob").Values["Session"].ShouldBe(Security.HashSession(id));
        (await cache.GetAsync("bff:session:" + client.Cookie)).ShouldBeNull();
    }

    [Fact]
    public async Task Rotated_data_protection_keys_give_a_clean_401_and_a_tampered_event()
    {
        MemoryDistributedCache shared = new(Options.Create(new MemoryDistributedCacheOptions()));
        Factory.Customize = services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddSingleton<IDistributedCache>(shared);
        };
        BffClient client = await SignInAsync();

        using BffFactory rotated = new()
        {
            Customize = services =>
            {
                services.RemoveAll<IDistributedCache>();
                services.AddSingleton<IDistributedCache>(shared);
            },
        }; // same session store, brand-new key ring
        BffClient after = rotated.NewClient();
        after.Cookie = client.Cookie;

        using HttpResponseMessage response = await after.GetAsync("/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonOf(response)).GetProperty("type").GetString().ShouldBe("/problems/session-ended");
        string text = await TextOf(response);
        text.ShouldNotContain("Cryptographic", Case.Insensitive);
        text.ShouldNotContain("   at ");
        rotated.Logs.Events.Single(e => e.Id.Id == SecurityLog.SessionEndedId).Values["Reason"].ShouldBe("tampered-blob");
        rotated.Logs.Entries.ShouldNotContain(e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task A_rejected_refresh_logs_the_end_and_the_outcome()
    {
        BffClient client = await SignInAsync();
        string id = client.Cookie!;
        Api.RefreshRejects = true;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        using HttpResponseMessage response = await client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Single(SecurityLog.SessionEndedId, "refresh-rejected").Values["Session"].ShouldBe(Security.HashSession(id));
        Single(SecurityLog.RefreshOutcomeId, "rejected").Values["Session"].ShouldBe(Security.HashSession(id));
    }

    [Fact]
    public async Task A_successful_refresh_logs_rotated()
    {
        BffClient client = await SignInAsync();
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        using HttpResponseMessage response = await client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Single(SecurityLog.RefreshOutcomeId, "rotated").Values["Session"].ShouldBe(Security.HashSession(client.Cookie));
    }

    [Fact]
    public async Task A_refresh_that_loses_to_a_session_end_logs_lost_save()
    {
        BffClient client = await SignInAsync();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.RefreshGate = gate.Task;
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));

        Task<HttpResponseMessage> call = client.GetAsync("/api/v1/products");
        for (int i = 0; i < 500 && Api.RefreshCalls == 0; i++)
        {
            await Task.Delay(10);
        }

        await Factory.Services.GetRequiredService<SessionStore>().DeleteAsync(client.Cookie!);
        gate.SetResult();
        (await call).Dispose();

        Single(SecurityLog.RefreshOutcomeId, "lost-save").ShouldNotBeNull();
    }

    [Fact]
    public async Task The_thirty_first_refresh_in_a_minute_is_429_and_logged()
    {
        Factory.Settings["Bff:AuthStrictPermitLimit"] = "100";
        Factory.Settings["Bff:AnonymousPermitLimit"] = "100";
        List<BffClient> clients = [];
        for (int i = 0; i < 31; i++)
        {
            clients.Add(await SignInAsync());
        }

        Factory.Time.Advance(TimeSpan.FromMinutes(14.5)); // all 31 tokens are due at the same moment, from one IP
        List<HttpStatusCode> codes = [];
        foreach (BffClient client in clients)
        {
            using HttpResponseMessage response = await client.GetAsync("/api/v1/products");
            codes.Add(response.StatusCode);
        }

        codes.Count(c => c == HttpStatusCode.OK).ShouldBe(30);
        codes[^1].ShouldBe(HttpStatusCode.TooManyRequests);
        Api.RefreshCalls.ShouldBe(30);
        Events(SecurityLog.RateLimitedId).Single().Values["Policy"].ShouldBe("refresh");
        Single(SecurityLog.RefreshOutcomeId, "budget-exhausted").ShouldNotBeNull();
    }

    [Fact]
    public async Task Rate_limits_log_event_5005_with_the_policy_name()
    {
        Factory.Settings["Bff:AuthStrictPermitLimit"] = "1";
        Factory.Settings["Bff:ReadyPermitLimit"] = "1";
        Factory.Settings["Bff:AnonymousPermitLimit"] = "2";

        (await Client.LoginAsync("a@example.com", FakeApi.Password)).Dispose();
        using HttpResponseMessage strict = await Client.LoginAsync("a@example.com", FakeApi.Password);
        (await Client.GetAsync("/bff/health/ready")).Dispose();
        using HttpResponseMessage ready = await Client.GetAsync("/bff/health/ready");
        using HttpResponseMessage anonymous = await Client.GetAsync("/bff/user");

        strict.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        ready.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        Events(SecurityLog.RateLimitedId).Select(e => e.Values["Policy"]).ShouldBe(["auth-strict", "health-ready", "anonymous"]);
    }

    [Fact]
    public async Task Allow_list_misses_and_wrong_methods_log_event_5006_without_the_path()
    {
        BffClient client = await SignInAsync();

        (await client.GetAsync("/bff/secret-probe-path")).Dispose();
        (await client.SendAsync(HttpMethod.Post, "/bff/user", new { })).Dispose();
        (await client.GetAsync("/api/v1/not-on-the-list?token=PROBEQUERY")).Dispose();
        (await client.GetAsync("/api/V1/products")).Dispose(); // wrong case: literal segments are exact

        CapturedLog[] probes = Events(SecurityLog.RouteProbeId);
        probes.Select(p => (p.Values["Status"], p.Values["Area"])).ShouldBe(
        [
            ("404", "bff"),
            ("405", "bff"),
            ("404", "api"),
            ("404", "api"),
        ]);
        probes[1].Values["Method"].ShouldBe("POST");
        AllLogText().ShouldNotContain("secret-probe-path");
        AllLogText().ShouldNotContain("PROBEQUERY");
        AllLogText().ShouldNotContain("not-on-the-list");
    }

    [Fact]
    public async Task A_legitimate_api_404_is_not_a_probe()
    {
        BffClient client = await SignInAsync();
        Api.ProxiedStatus = HttpStatusCode.NotFound;

        using HttpResponseMessage response = await client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Events(SecurityLog.RouteProbeId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Password_change_or_erase_that_ends_the_session_logs_api_revoked()
    {
        BffClient client = await SignInAsync();
        string id = client.Cookie!;

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, "/api/v1/auth/change-password", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Single(SecurityLog.SessionEndedId, "api-revoked").Values["Session"].ShouldBe(Security.HashSession(id));
    }

    [Fact]
    public async Task Event_ids_are_fixed_and_unique()
    {
        int[] ids =
        [
            SecurityLog.SignInSucceededId, SecurityLog.SignInFailedId, SecurityLog.CsrfRejectedId, SecurityLog.SessionEndedId,
            SecurityLog.RateLimitedId, SecurityLog.RouteProbeId, SecurityLog.RefreshOutcomeId,
        ];

        ids.ShouldBe([5001, 5002, 5003, 5004, 5005, 5006, 5007]);
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("203.0.113.77", "203.0.113.0/24")]
    [InlineData("::ffff:203.0.113.77", "203.0.113.0/24")]
    [InlineData("2001:db8:aaaa:bbbb:cccc:dddd:eeee:ffff", "2001:db8:aaaa::/48")]
    [InlineData("2001:db8:aaaa:bbbb::", "2001:db8:aaaa::/48")]
    [InlineData("unknown", "unknown")]
    [InlineData(null, "unknown")]
    public void Client_addresses_are_masked(string? address, string expected) =>
        SecurityLog.MaskIp(address).ShouldBe(expected);

    [Fact]
    public void Session_hash_is_short_stable_per_process_and_not_the_id()
    {
        string id = SessionIds.New();

        string hash = Security.HashSession(id);

        hash.ShouldMatch("^[0-9a-f]{16}$");
        Security.HashSession(id).ShouldBe(hash);
        Security.HashSession(SessionIds.New()).ShouldNotBe(hash);
        hash.ShouldNotContain(id[..6]);
        Security.HashSession(null).ShouldBe("none");
    }

    private string AllLogText() => string.Join('\n', Factory.Logs.Events.Select(e => e.Message + " " + string.Join(' ', e.Values.Select(v => v.Key + "=" + v.Value))));
}

/// <summary>Nothing secret reaches a log line or a span tag, whatever the request did (NFR-F19, F39).</summary>
public sealed class TelemetryHygieneTests : BffTestBase
{
    private const string QuerySecret = "QUERYSECRET-7f3a";

    [Fact]
    public async Task No_secret_appears_in_any_log_value_or_span_tag()
    {
        const string email = "hygiene.user@example.com";
        BffClient client = await SignInAsync();
        string sessionId = client.Cookie!;
        using HttpResponseMessage page = await client.GetAsync("/");
        string nonce = Regex.Match(string.Join(' ', page.Headers.GetValues("Content-Security-Policy")), "'nonce-([^']+)'").Groups[1].Value;
        nonce.ShouldNotBeEmpty();

        (await Client.LoginAsync(email, "wrong-" + FakeApi.Password)).Dispose();
        (await client.GetAsync($"/api/v1/products?search={QuerySecret}&email={email}")).Dispose();
        (await client.GetAsync($"/bff/user?token={QuerySecret}")).Dispose();
        (await client.SendAsync(HttpMethod.Post, $"/api/v1/auth/confirm-email?token={QuerySecret}&userId=1", new { token = QuerySecret })).Dispose();
        (await client.SendAsync(HttpMethod.Post, $"/api/v1/auth/reset-password?token={QuerySecret}", new { token = QuerySecret, newPassword = "NewSecret-123!" })).Dispose();
        (await client.GetAsync($"/not-found-page?x={QuerySecret}")).Dispose();
        Factory.Time.Advance(TimeSpan.FromMinutes(14.5));
        (await client.GetAsync("/api/v1/products")).Dispose(); // a refresh
        (await client.SendAsync(HttpMethod.Post, "/bff/logout")).Dispose();

        string[] secrets = [QuerySecret, email, FakeApi.Password, "NewSecret-123!", sessionId, nonce, .. Api.IssuedTokens];
        string logs = string.Join('\n', Factory.Logs.Events.Select(e => e.Message + "\n" + string.Join('\n', e.Values.Select(v => v.Key + "=" + v.Value))));
        string spans = string.Join('\n', Factory.Spans.Spans.Select(s => s.Name + "\n" + string.Join('\n', s.Tags.Select(t => t.Key + "=" + t.Value))));
        logs.ShouldNotBeEmpty();
        spans.ShouldNotBeEmpty();
        foreach (string secret in secrets)
        {
            logs.ShouldNotContain(secret);
            spans.ShouldNotContain(secret);
        }

        logs.ShouldNotContain("eyJ"); // no JWT, whatever its content
        spans.ShouldNotContain("eyJ");
    }

    [Fact]
    public async Task Server_spans_keep_the_path_and_drop_the_query_string()
    {
        BffClient client = await SignInAsync();

        (await client.GetAsync($"/api/v1/products?search={QuerySecret}")).Dispose();
        (await client.GetAsync($"/bff/user?token={QuerySecret}")).Dispose();
        (await client.SendAsync(HttpMethod.Post, $"/api/v1/auth/reset-password?token={QuerySecret}", new { })).Dispose();

        CapturedSpan[] servers = [.. Factory.Spans.Spans.Where(s => s.Kind == ActivityKind.Server)];
        servers.Select(s => s.Tags.GetValueOrDefault("url.path")).ShouldContain("/api/v1/products");
        servers.Select(s => s.Tags.GetValueOrDefault("url.path")).ShouldContain("/bff/user");
        servers.Select(s => s.Tags.GetValueOrDefault("url.path")).ShouldContain("/api/v1/auth/reset-password");
        foreach (CapturedSpan span in servers)
        {
            span.Tags.Keys.ShouldNotContain("url.query");
            span.Tags.Keys.ShouldNotContain(k => k.StartsWith("http.request.header.", StringComparison.Ordinal));
            span.Tags.Values.ShouldNotContain(v => v != null && v.Contains('?', StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_redaction_processor_removes_queries_headers_and_full_addresses()
    {
        using ActivitySource source = new("hygiene-test");
        using ActivityListener listener = new()
        {
            ShouldListenTo = s => s.Name == "hygiene-test",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using Activity activity = source.StartActivity("client", ActivityKind.Client)!;
        _ = activity.SetTag("url.full", $"https://api.test/api/v1/products?token={QuerySecret}");
        _ = activity.SetTag("url.query", "token=" + QuerySecret);
        _ = activity.SetTag("http.target", "/x?token=" + QuerySecret);
        _ = activity.SetTag("http.request.header.authorization", "Bearer abc");
        _ = activity.SetTag("http.response.header.set-cookie", "a=b");
        _ = activity.SetTag("client.address", "203.0.113.77");
        _ = activity.SetTag("url.path", "/api/v1/products");

        new RedactingProcessor().OnEnd(activity);

        activity.GetTagItem("url.full").ShouldBe("https://api.test/api/v1/products");
        activity.GetTagItem("http.target").ShouldBeNull();
        activity.GetTagItem("url.query").ShouldBeNull();
        activity.GetTagItem("http.request.header.authorization").ShouldBeNull();
        activity.GetTagItem("http.response.header.set-cookie").ShouldBeNull();
        activity.GetTagItem("client.address").ShouldBeNull(); // not on the allow-list
        activity.GetTagItem("url.path").ShouldBe("/api/v1/products");
    }

    [Fact]
    public async Task A_browser_traceparent_does_not_start_or_join_the_bff_trace()
    {
        BffClient client = await SignInAsync();
        const string browserTrace = "11111111111111111111111111111111";

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, "/api/v1/products",
            configure: r => r.Headers.TryAddWithoutValidation("traceparent", $"00-{browserTrace}-2222222222222222-01"));
        using HttpResponseMessage missing = await client.SendAsync(HttpMethod.Get, "/bff/nope",
            configure: r => r.Headers.TryAddWithoutValidation("traceparent", $"00-{browserTrace}-2222222222222222-01"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CapturedSpan span = Factory.Spans.Spans.Last(s => s.Kind == ActivityKind.Server && s.Tags.GetValueOrDefault("url.path") == "/api/v1/products");
        span.TraceId.ShouldNotBe(browserTrace);
        span.ParentSpanId.ShouldBe("0000000000000000");
        Api.Requests.Last().Headers["traceparent"].ShouldNotContain(browserTrace); // the API trace is the BFF trace
        (await JsonOf(missing)).GetProperty("traceId").GetString().ShouldNotBe(browserTrace);
    }

    [Fact]
    public void The_service_name_defaults_to_demoapi_bff()
    {
        ServiceName().ShouldBe("demoapi-bff");
    }

    [Fact]
    public void The_service_name_follows_OTEL_SERVICE_NAME()
    {
        Factory.Settings["OTEL_SERVICE_NAME"] = "demoapi-bff-test";

        ServiceName().ShouldBe("demoapi-bff-test");
    }

    [Fact]
    public async Task An_unreachable_otlp_endpoint_does_not_break_requests()
    {
        Factory.Settings["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:1";

        using HttpResponseMessage response = await Client.GetAsync("/bff/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private string ServiceName()
    {
        _ = Factory.CreateClient(); // starts the host
        Resource resource = Factory.Services.GetRequiredService<TracerProvider>().GetResource();
        return resource.Attributes.Single(a => a.Key == "service.name").Value.ToString()!;
    }
}

/// <summary>I-8: the session store down is a clean 503 and fails closed (FT-06).</summary>
public sealed class StoreDownTests : BffTestBase
{
    [Fact]
    public async Task Valkey_down_gives_503_without_internals_for_user_proxy_and_login()
    {
        BffClient client = await SignInAsync();
        using BffFactory down = new()
        {
            Customize = services =>
            {
                services.RemoveAll<IDistributedCache>();
                services.AddSingleton<IDistributedCache, DownCache>();
            },
        };
        BffClient broken = down.NewClient();
        broken.Cookie = client.Cookie;

        using HttpResponseMessage user = await broken.GetAsync("/bff/user");
        using HttpResponseMessage proxied = await broken.GetAsync("/api/v1/products");
        using HttpResponseMessage login = await broken.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        foreach (HttpResponseMessage response in new[] { user, proxied, login })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await JsonOf(response)).GetProperty("type").GetString().ShouldBe("/problems/upstream-unavailable");
            string text = await TextOf(response);
            text.ShouldNotContain("valkey-down-secret", Case.Insensitive);
            text.ShouldNotContain("   at ");
        }

        login.Headers.Contains("Set-Cookie").ShouldBeFalse();
        down.Logs.Entries.Select(e => e.Message).ShouldNotContain(m => m.Contains("valkey-down-secret", StringComparison.OrdinalIgnoreCase));
        down.Api.Count("POST", "/api/v1/auth/logout").ShouldBe(1); // the login's new tokens were revoked
    }

    private sealed class DownCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("valkey-down-secret");

        public byte[]? Get(string key) => throw Down();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();

        public void Refresh(string key) => throw Down();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();

        public void Remove(string key) => throw Down();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
    }
}