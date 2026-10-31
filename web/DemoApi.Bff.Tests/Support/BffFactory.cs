using System.Diagnostics;
using System.Net.Http.Json;

using DemoApi.Bff.Upstream;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace DemoApi.Bff.Tests.Support;

/// <summary>One captured log call: level, event, formatted text and the structured values.</summary>
public sealed record CapturedLog(LogLevel Level, EventId Id, string Category, string Message, IReadOnlyDictionary<string, string?> Values, string? ExceptionText = null);

/// <summary>One finished span, copied when it ended (after the BFF redaction processor ran).</summary>
public sealed record CapturedSpan(string Name, ActivityKind Kind, string TraceId, string ParentSpanId, IReadOnlyDictionary<string, string?> Tags, int EventCount = 0, string? StatusDescription = null);

public sealed class CapturingSpanProcessor : BaseProcessor<Activity>
{
    public System.Collections.Concurrent.ConcurrentQueue<CapturedSpan> Spans { get; } = new();

    public override void OnEnd(Activity data) =>
        Spans.Enqueue(new CapturedSpan(
            data.DisplayName,
            data.Kind,
            data.TraceId.ToString(),
            data.ParentSpanId.ToString(),
            data.TagObjects.ToDictionary(t => t.Key, t => t.Value?.ToString(), StringComparer.Ordinal),
            data.Events.Count(),
            data.StatusDescription));
}

/// <summary>Captures log messages so tests can assert on what the BFF logs.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

    public System.Collections.Concurrent.ConcurrentQueue<CapturedLog> Events { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            owner.Entries.Enqueue((logLevel, message));
            Dictionary<string, string?> values = [];
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (KeyValuePair<string, object?> pair in pairs)
                {
                    values[pair.Key] = pair.Value?.ToString();
                }
            }

            owner.Events.Enqueue(new CapturedLog(logLevel, eventId, category, message, values, exception?.ToString()));
        }
    }
}

/// <summary>The BFF in process with a fake API, an in-memory session cache and a fake clock. Valkey is not needed.</summary>
public sealed class BffFactory : WebApplicationFactory<Program>
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "bff-tests-" + Guid.NewGuid().ToString("N"));

    public BffFactory()
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));
        Api = new FakeApi(Time);
        _ = Directory.CreateDirectory(WebRoot);
        File.WriteAllText(Path.Combine(WebRoot, "index.html"), "<!doctype html><meta name=\"csp-nonce\" content=\"__CSP_NONCE__\" /><title>spa</title>");
    }

    public FakeTimeProvider Time { get; }

    public FakeApi Api { get; }

    public CapturingLoggerProvider Logs { get; } = new();

    public CapturingSpanProcessor Spans { get; } = new();

    public string WebRoot => Path.Combine(root, "wwwroot");

    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Bff:ApiBaseUrl"] = "http://api.test",
        ["Bff:AllowedOrigins:0"] = "https://localhost",
        ["Valkey:ConnectionString"] = "localhost:6379",
    };

    public string Environment { get; set; } = "Production";

    public string KeysPath => Path.Combine(root, "keys");

    /// <summary>Extra service changes applied after the defaults (for example a failing exception handler).</summary>
    public Action<IServiceCollection>? Customize { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _ = builder.UseEnvironment(Environment);
        _ = builder.UseWebRoot(WebRoot);
        _ = Directory.CreateDirectory(KeysPath);
        Settings.TryAdd("DataProtection:KeysPath", KeysPath);
        foreach ((string key, string? value) in Settings)
        {
            _ = builder.UseSetting(key, value);
        }

        _ = builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        _ = builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<IUpstreamHandlerFactory>();
            services.AddSingleton<IUpstreamHandlerFactory>(new FakeUpstreamHandlerFactory(Api));
            services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddProcessor(Spans)); // after the BFF redaction processor
            Customize?.Invoke(services);
        });
    }

    public BffClient NewClient()
    {
        HttpClient http = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        return new BffClient(http);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(root))
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // Temp files only.
            }
        }
    }
}

public sealed class BffClient(HttpClient http)
{
    public const string Origin = "https://localhost";

    public string? Cookie { get; set; }

    public string? LastSetCookie { get; private set; }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? json = null,
        bool csrf = true,
        string? origin = Origin,
        bool withCookie = true,
        Action<HttpRequestMessage>? configure = null,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(method, url);
        if (json is not null)
        {
            request.Content = JsonContent.Create(json);
        }

        if (csrf)
        {
            request.Headers.Add("X-CSRF", "1");
        }

        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        if (withCookie && Cookie is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", "__Host-demoapi=" + Cookie);
        }

        configure?.Invoke(request);
        HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
        CaptureCookie(response);
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url);

    public Task<HttpResponseMessage> LoginAsync(string email, string secret) =>
        SendAsync(HttpMethod.Post, "/bff/login", new { email, password = secret });

    private void CaptureCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
        {
            return;
        }

        foreach (string value in values.Where(v => v.StartsWith("__Host-demoapi=", StringComparison.Ordinal)))
        {
            LastSetCookie = value;
            string token = value["__Host-demoapi=".Length..].Split(';')[0];
            Cookie = token.Length == 0 ? null : token;
        }
    }
}