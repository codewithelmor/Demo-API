namespace DemoApi.Bff.Security;

/// <summary>
/// OpenTelemetry for the BFF (NFR-F15, F19, F39). Traces and metrics from ASP.NET Core and HttpClient. The OTLP exporter
/// is added only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set; the service name comes from <c>OTEL_SERVICE_NAME</c>
/// (default <c>demoapi-bff</c>). No header is captured, and URLs are recorded without the query string.
/// </summary>
internal static class BffTelemetry
{
    public const string DefaultServiceName = "demoapi-bff";

    public static IServiceCollection AddBffTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        string serviceName = configuration["OTEL_SERVICE_NAME"] is { Length: > 0 } configured ? configured : DefaultServiceName;
        bool export = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        // A trace starts in the BFF (ADR-0015). The hosting layer must not adopt a traceparent sent by the browser.
        // Two layers adopt it: ASP.NET hosting (DistributedContextPropagator) and the OpenTelemetry instrumentation (text-map propagator).
        services.AddSingleton<DistributedContextPropagator>(new RootingPropagator(DistributedContextPropagator.Current));
        Sdk.SetDefaultTextMapPropagator(new RootingTextMapPropagator());

        OpenTelemetryBuilder otel = services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService(serviceName));
        _ = otel.WithTracing(tracing =>
        {
            _ = tracing
                .AddAspNetCoreInstrumentation(static options =>
                {
                    options.RecordException = false;
                    options.Filter = static context => !context.Request.Path.StartsWithSegments("/bff/health", StringComparison.Ordinal);
                })
                .AddHttpClientInstrumentation(static options => options.RecordException = false)
                .AddProcessor(new RedactingProcessor()); // before any exporter, so exporters only see redacted spans
            if (export)
            {
                _ = tracing.AddOtlpExporter();
            }
        });
        _ = otel.WithMetrics(metrics =>
        {
            _ = metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddMeter(SecurityLog.MeterName);
            if (export)
            {
                _ = metrics.AddOtlpExporter();
            }
        });
        return services;
    }
}

/// <summary>
/// Ignores an incoming <c>traceparent</c> and <c>tracestate</c>, so every request starts a new root trace in the BFF.
/// Outgoing calls still carry the BFF trace (inject is the default behaviour). A page script cannot choose or pin a
/// trace ID, and a stolen or guessed one cannot be used to join another trace.
/// </summary>
internal sealed class RootingPropagator(DistributedContextPropagator inner) : DistributedContextPropagator
{
    public override IReadOnlyCollection<string> Fields => inner.Fields;

    public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter) =>
        inner.Inject(activity, carrier, setter);

    public override void ExtractTraceIdAndState(object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
    {
        traceId = null;
        traceState = null;
    }

    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(object? carrier, PropagatorGetterCallback? getter) => null;
}

/// <summary>The OpenTelemetry side of <see cref="RootingPropagator"/>: never extracts, injects as usual (W3C).</summary>
internal sealed class RootingTextMapPropagator : TextMapPropagator
{
    private readonly TextMapPropagator inner = new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]);

    public override ISet<string>? Fields => inner.Fields;

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter) => context;

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter) =>
        inner.Inject(context, carrier, setter);
}

/// <summary>
/// Allow-list redaction for every span, run before any exporter (F-3). Only tags that are fixed by the framework or the
/// BFF configuration survive; everything else is removed, including the query string, header tags, user agent, client
/// and peer addresses, exception and status text. A new instrumentation tag is therefore dropped until it is reviewed
/// and added here. Client spans (calls to the API) also keep the configured API host and the URL without query and
/// user info. Server spans never keep <c>server.address</c>: it comes from the Host header.
/// </summary>
/// <remarks>
/// Activity events cannot be removed after the fact. None are produced: exception recording is off for both
/// instrumentations and the BFF adds none (a test checks both).
/// </remarks>
internal sealed class RedactingProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "http.request.method",
        "http.response.status_code",
        "url.path",
        "url.scheme",
        "http.route",
        "error.type",
        "network.protocol.version",
        "server.port",
        "peer.service",
        "otel.status_code",
        "otel.scope.name",
        "otel.scope.version",

        // Old (pre-stable) names for the same facts.
        "http.method",
        "http.status_code",
        "http.scheme",
        "http.flavor",
        "net.host.port",
        "net.peer.port",
    };

    private static readonly HashSet<string> ClientOnly = new(StringComparer.Ordinal)
    {
        "server.address",
        "url.full",
        "http.url",
        "net.peer.name",
    };

    public override void OnEnd(Activity data)
    {
        bool client = data.Kind == ActivityKind.Client;
        List<(string Key, object? Value)>? changes = null;
        foreach (KeyValuePair<string, object?> tag in data.TagObjects)
        {
            if (Allowed.Contains(tag.Key))
            {
                continue;
            }

            if (client && ClientOnly.Contains(tag.Key))
            {
                string? cleaned = tag.Key is "url.full" or "http.url" ? StripQueryAndUserInfo(tag.Value as string) : tag.Value as string;
                if (!string.Equals(cleaned, tag.Value as string, StringComparison.Ordinal))
                {
                    (changes ??= []).Add((tag.Key, cleaned));
                }

                continue;
            }

            (changes ??= []).Add((tag.Key, null));
        }

        if (changes is not null)
        {
            foreach ((string key, object? value) in changes)
            {
                _ = data.SetTag(key, value);
            }
        }

        // Status text can carry exception messages; the code is enough.
        if (!string.IsNullOrEmpty(data.StatusDescription))
        {
            _ = data.SetStatus(data.Status);
        }
    }

    private static string? StripQueryAndUserInfo(string? url)
    {
        if (url is null)
        {
            return null;
        }

        int cut = url.IndexOfAny(['?', '#']);
        string withoutQuery = cut >= 0 ? url[..cut] : url;
        int scheme = withoutQuery.IndexOf("://", StringComparison.Ordinal);
        int at = withoutQuery.IndexOf('@', scheme < 0 ? 0 : scheme + 3);
        int slash = withoutQuery.IndexOf('/', scheme < 0 ? 0 : scheme + 3);
        return scheme >= 0 && at > scheme && (slash < 0 || at < slash) ? withoutQuery[..(scheme + 3)] + withoutQuery[(at + 1)..] : withoutQuery;
    }
}
