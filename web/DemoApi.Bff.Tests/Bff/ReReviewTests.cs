using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using DemoApi.Bff.Security;
using DemoApi.Bff.Session;
using DemoApi.Bff.Tests.Support;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry.Trace;

using StackExchange.Redis;

namespace DemoApi.Bff.Tests.Bff;

/// <summary>Fixes from the delta re-review (F-1, F-2, F-3, F-4, I-1).</summary>
public sealed class StoreErrorTextTests : BffTestBase
{
    [Fact]
    public async Task A_cache_error_that_names_the_session_key_never_reaches_a_log_exception()
    {
        SwitchableCache cache = new();
        Factory.Customize = services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddSingleton<IDistributedCache>(cache);
        };
        BffClient client = await SignInAsync();
        string id = client.Cookie!;
        cache.Down = true;

        using HttpResponseMessage user = await client.GetAsync("/bff/user");
        using HttpResponseMessage proxied = await client.GetAsync("/api/v1/products");
        using HttpResponseMessage login = await client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        user.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        proxied.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        login.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        Factory.Logs.Events.ShouldNotBeEmpty();
        foreach (CapturedLog entry in Factory.Logs.Events)
        {
            (entry.Message + "\n" + entry.ExceptionText).ShouldNotContain(id);
            (entry.Message + "\n" + entry.ExceptionText).ShouldNotContain("bff:session:");
            (entry.Message + "\n" + entry.ExceptionText).ShouldNotContain("leaked-detail");
        }

        string text = await TextOf(user) + await TextOf(proxied) + await TextOf(login);
        text.ShouldNotContain(id);
    }

    [Fact]
    public void Redis_connections_never_put_command_or_key_details_into_exceptions()
    {
        ConfigurationOptions options = BffHost.SafeRedisOptions("valkey.internal:6379,ssl=true");

        options.IncludeDetailInExceptions.ShouldBeFalse();
        options.IncludePerformanceCountersInExceptions.ShouldBeFalse();
    }

    private sealed class SwitchableCache : IDistributedCache
    {
        private readonly MemoryDistributedCache inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public bool Down { get; set; }

        private void Check(string key)
        {
            if (Down)
            {
                throw new InvalidOperationException("leaked-detail GET " + key, new InvalidOperationException("inner " + key));
            }
        }

        public byte[]? Get(string key)
        {
            Check(key);
            return inner.Get(key);
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Check(key);
            return inner.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Check(key);
            inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Check(key);
            return inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => Check(key);

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            Check(key);
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            Check(key);
            inner.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Check(key);
            return inner.RemoveAsync(key, token);
        }
    }
}

public sealed class LogFloodTests : BffTestBase
{
    private SecurityLog Security => Factory.Services.GetRequiredService<SecurityLog>();

    private CapturedLog[] Events(int id) => [.. Factory.Logs.Events.Where(e => e.Id.Id == id)];

    [Fact]
    public async Task A_thousand_probes_from_one_client_log_a_few_lines_and_one_summary()
    {
        for (int i = 0; i < 1000; i++)
        {
            using HttpResponseMessage response = await Client.GetAsync("/bff/x" + i);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        Events(SecurityLog.RouteProbeId).Length.ShouldBe(10);
        Events(SecurityLog.EventsSuppressedId).ShouldBeEmpty(); // the window is still open
        Factory.Time.Advance(TimeSpan.FromMinutes(2)); // the sweep timer closes it
        CapturedLog summary = Events(SecurityLog.EventsSuppressedId).Single();
        summary.Values["SuppressedEventId"].ShouldBe("5006");
        summary.Values["Count"].ShouldBe("990");
        summary.Values["Client"].ShouldBe("unknown");
    }

    [Fact]
    public async Task Random_cookies_log_a_few_session_ends_not_one_per_request()
    {
        for (int i = 0; i < 300; i++)
        {
            BffClient client = Factory.NewClient();
            client.Cookie = SessionIds.New();
            using HttpResponseMessage response = await client.GetAsync("/bff/user");
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
        }

        Events(SecurityLog.SessionEndedId).Length.ShouldBe(10);
        Events(SecurityLog.RateLimitedId).Length.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task A_flood_does_not_hide_events_of_another_kind_or_another_client()
    {
        for (int i = 0; i < 100; i++)
        {
            (await Client.GetAsync("/bff/x" + i)).Dispose();
        }

        using HttpResponseMessage csrf = await Client.SendAsync(HttpMethod.Post, "/bff/logout", new { }, csrf: false);
        Security.SignInFailed("invalid-credentials", "198.51.100.5");
        for (int i = 0; i < 100; i++)
        {
            Security.RouteProbe(404, "bff", "GET", "198.51.100.5");
        }

        Events(SecurityLog.CsrfRejectedId).Length.ShouldBe(1);
        Events(SecurityLog.SignInFailedId).Single().Values["Client"].ShouldBe("198.51.100.0/24");
        Events(SecurityLog.RouteProbeId).Count(e => e.Values["Client"] == "198.51.100.0/24").ShouldBe(10);
        Events(SecurityLog.RouteProbeId).Count(e => e.Values["Client"] == "unknown").ShouldBe(10);
    }

    [Fact]
    public async Task Every_event_is_counted_in_a_metric_with_fixed_tags()
    {
        SecurityLog security = Security; // starts the host first, so the instrument exists when the listener starts
        List<(long Value, Dictionary<string, object?> Tags)> measurements = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == SecurityLog.MeterName && ReferenceEquals(instrument.Meter.Scope, security))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            lock (measurements)
            {
                measurements.Add((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
            }
        });
        listener.Start();

        for (int i = 0; i < 50; i++)
        {
            (await Client.GetAsync("/bff/y" + i)).Dispose();
        }

        (long Value, Dictionary<string, object?> Tags)[] snapshot;
        lock (measurements)
        {
            snapshot = [.. measurements];
        }

        snapshot.Sum(m => m.Value).ShouldBe(50);
        snapshot.Where(m => (string?)m.Tags["outcome"] == "logged").Sum(m => m.Value).ShouldBe(10);
        snapshot.Where(m => (string?)m.Tags["outcome"] == "suppressed").Sum(m => m.Value).ShouldBe(40);
        foreach ((long _, Dictionary<string, object?> tags) in snapshot)
        {
            tags.Keys.Order().ShouldBe(["event", "outcome"]);
            tags["event"].ShouldBe("RouteProbe");
        }
    }
}

public sealed class TelemetryAllowListTests : BffTestBase
{
    private static readonly string[] Attacker = ["evil-host-9431.example", "random-path-8821", "evil-agent-5512", "QUERY-SECRET-3340"];

    private static readonly string[] AllowedSpanTags =
    [
        "http.request.method", "http.response.status_code", "url.path", "url.scheme", "http.route", "error.type",
        "network.protocol.version", "server.port", "peer.service", "otel.status_code", "otel.scope.name", "otel.scope.version",
    ];

    private async Task SendHostileRequestsAsync()
    {
        BffClient client = await SignInAsync();
        foreach (string path in new[] { "/api/v1/products", "/bff/user", "/random-path-8821", "/bff/random-path-8821", "/api/random-path-8821" })
        {
            using HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, path + "?q=QUERY-SECRET-3340", configure: r =>
            {
                r.Headers.Host = "evil-host-9431.example";
                r.Headers.TryAddWithoutValidation("User-Agent", "evil-agent-5512");
            });
        }
    }

    [Fact]
    public async Task Server_spans_keep_only_allow_listed_tags_and_never_the_host_or_user_agent()
    {
        await SendHostileRequestsAsync();

        CapturedSpan[] servers = [.. Factory.Spans.Spans.Where(s => s.Kind == ActivityKind.Server)];
        servers.ShouldNotBeEmpty();
        foreach (CapturedSpan span in servers)
        {
            span.Tags.Keys.ShouldBeSubsetOf(AllowedSpanTags);
            string all = span.Name + string.Join('|', span.Tags.Select(t => t.Key + "=" + t.Value));
            foreach (string bad in new[] { "evil-host-9431.example", "evil-agent-5512", "QUERY-SECRET-3340" })
            {
                all.ShouldNotContain(bad);
            }
        }
    }

    [Fact]
    public async Task A_failing_request_leaves_no_status_text_and_no_span_events()
    {
        Api.ThrowOnLogin = true;

        using HttpResponseMessage response = await Client.LoginAsync(FakeApi.CustomerEmail, FakeApi.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        CapturedSpan[] spans = [.. Factory.Spans.Spans];
        spans.ShouldNotBeEmpty();
        spans.ShouldAllBe(s => s.EventCount == 0);
        spans.ShouldAllBe(s => string.IsNullOrEmpty(s.StatusDescription));
        spans.SelectMany(s => s.Tags.Values).ShouldNotContain(v => v != null && v.Contains("Exception", StringComparison.Ordinal) && v.Length > 40);
    }

    [Fact]
    public void The_processor_drops_everything_not_on_the_allow_list_and_cleans_status_and_urls()
    {
        using ActivitySource source = new("allowlist-test");
        using ActivityListener listener = new()
        {
            ShouldListenTo = s => s.Name == "allowlist-test",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity server = source.StartActivity("server", ActivityKind.Server)!;
        foreach ((string key, string value) in new[]
        {
            ("user_agent.original", "evil-agent"), ("http.user_agent", "evil-agent"), ("net.peer.ip", "203.0.113.9"), ("http.client_ip", "203.0.113.9"),
            ("client.address", "203.0.113.9"), ("server.address", "evil-host"), ("http.request.method_original", "BREW"), ("url.query", "token=abc"),
            ("exception.message", "boom abc"), ("otel.status_description", "token abc"), ("http.request.header.cookie", "abc"),
            ("url.path", "/bff/user"), ("http.route", "/bff/user"), ("http.request.method", "GET"), ("http.response.status_code", "200"),
        })
        {
            _ = server.SetTag(key, value);
        }

        _ = server.SetStatus(ActivityStatusCode.Error, "exception text with token abc");
        new RedactingProcessor().OnEnd(server);

        server.TagObjects.Select(t => t.Key).Order().ShouldBe(["http.request.method", "http.response.status_code", "http.route", "url.path"]);
        server.Status.ShouldBe(ActivityStatusCode.Error);
        server.StatusDescription.ShouldBeNull();

        using Activity client = source.StartActivity("client", ActivityKind.Client)!;
        _ = client.SetTag("server.address", "api.internal");
        _ = client.SetTag("url.full", "https://user:pw@api.internal/api/v1/products?token=abc#frag");
        _ = client.SetTag("user_agent.original", "evil-agent");
        new RedactingProcessor().OnEnd(client);

        client.GetTagItem("server.address").ShouldBe("api.internal");
        client.GetTagItem("url.full").ShouldBe("https://api.internal/api/v1/products");
        client.GetTagItem("user_agent.original").ShouldBeNull();
    }

    [Fact]
    public async Task Metric_tags_are_a_fixed_set_whatever_host_path_or_method_the_client_sends()
    {
        List<(string Meter, string Instrument, Dictionary<string, string?> Tags)> seen = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
            where T : struct
        {
            Dictionary<string, string?> copy = tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString());
            lock (seen)
            {
                seen.Add((instrument.Meter.Name, instrument.Name, copy));
            }
        }

        listener.SetMeasurementEventCallback<double>(Record);
        listener.SetMeasurementEventCallback<long>(Record);
        listener.SetMeasurementEventCallback<int>(Record);
        listener.Start();

        await SendHostileRequestsAsync();
        using HttpResponseMessage odd = await Client.SendAsync(new HttpMethod("BREWCOFFEE"), "/bff/user", csrf: false);

        (string Meter, string Instrument, Dictionary<string, string?> Tags)[] snapshot;
        lock (seen)
        {
            snapshot = [.. seen];
        }

        snapshot.ShouldNotBeEmpty();
        string[] allowedKeys =
        [
            "http.request.method", "http.response.status_code", "http.route", "url.scheme", "network.protocol.version", "error.type",
            "aspnetcore.request.is_unhandled", "aspnetcore.routing.match_status", "aspnetcore.rate_limiting.policy", "aspnetcore.rate_limiting.result",
            "aspnetcore.diagnostics.exception.result", "aspnetcore.diagnostics.handler.type", "exception.type",
            "aspnetcore.routing.is_fallback", "aspnetcore.rate_limiting.reason", "network.transport", "server.address", "server.port", "tls.protocol.name",
            "aspnetcore.kestrel.connection.status", "aspnetcore.http.connections.transport", "aspnetcore.http.connections.method",
        ];
        foreach ((string meter, string instrument, Dictionary<string, string?> tags) in snapshot)
        {
            tags.Keys.ShouldBeSubsetOf(allowedKeys, meter + "/" + instrument);
            foreach (string bad in Attacker.Concat(["BREWCOFFEE"]))
            {
                tags.Values.ShouldNotContain(v => v != null && v.Contains(bad, StringComparison.Ordinal), meter + "/" + instrument + " leaked " + bad);
            }
        }
    }
}

public sealed class ExporterOrderTests : BffTestBase
{
    [Fact]
    public async Task The_otlp_exporter_receives_only_redacted_spans()
    {
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using HttpListener collector = new();
        collector.Prefixes.Add($"http://127.0.0.1:{port}/");
        collector.Start();
        List<byte[]> bodies = [];
        Task pump = Task.Run(async () =>
        {
            while (collector.IsListening)
            {
                try
                {
                    HttpListenerContext context = await collector.GetContextAsync();
                    using MemoryStream copy = new();
                    await context.Request.InputStream.CopyToAsync(copy);
                    lock (bodies)
                    {
                        bodies.Add(copy.ToArray());
                    }

                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }
            }
        });
        Factory.Settings["OTEL_EXPORTER_OTLP_ENDPOINT"] = $"http://127.0.0.1:{port}";
        Factory.Settings["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";
        BffClient client = await SignInAsync();

        (await client.SendAsync(HttpMethod.Get, "/bff/user?token=EXPORT-SECRET-7781", configure: r => r.Headers.TryAddWithoutValidation("User-Agent", "export-agent-4410"))).Dispose();
        Factory.Services.GetRequiredService<TracerProvider>().ForceFlush(10_000).ShouldBeTrue();
        for (int i = 0; i < 300 && bodies.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        byte[][] received;
        lock (bodies)
        {
            received = [.. bodies];
        }

        received.ShouldNotBeEmpty();
        string text = string.Join('\n', received.Select(b => Encoding.Latin1.GetString(b)));
        text.ShouldContain("/bff/user");
        text.ShouldContain("demoapi-bff");
        text.ShouldNotContain("EXPORT-SECRET-7781");
        text.ShouldNotContain("export-agent-4410");
        text.ShouldNotContain("token=");

        collector.Stop();
        await pump;
    }
}

public sealed class QuietCategoryTests : BffTestBase
{
    [Fact]
    public async Task Request_urls_stay_out_of_logs_even_when_configuration_asks_for_trace()
    {
        Factory.Settings["Logging:LogLevel:Default"] = "Trace";
        Factory.Settings["Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Trace";
        Factory.Settings["Logging:LogLevel:Yarp"] = "Trace";
        Factory.Settings["Logging:LogLevel:System.Net.Http.HttpClient"] = "Trace";
        Factory.Settings["Logging:Console:LogLevel:Default"] = "Trace";
        BffClient client = await SignInAsync();

        (await client.GetAsync("/api/v1/products?token=QUERYSECRET")).Dispose();
        (await client.SendAsync(HttpMethod.Post, "/api/v1/auth/reset-password?token=QUERYSECRET", new { })).Dispose();

        Factory.Logs.Events.Count.ShouldBeGreaterThan(5); // Trace is really on for the rest
        foreach (CapturedLog entry in Factory.Logs.Events)
        {
            (entry.Message + string.Join(' ', entry.Values.Values) + entry.ExceptionText).ShouldNotContain("QUERYSECRET");
            if (entry.Category.StartsWith("Microsoft.AspNetCore.Hosting.Diagnostics", StringComparison.Ordinal)
                || entry.Category.StartsWith("Yarp", StringComparison.Ordinal)
                || entry.Category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal))
            {
                ((int)entry.Level).ShouldBeGreaterThanOrEqualTo((int)LogLevel.Warning, entry.Category);
            }
        }
    }
}
