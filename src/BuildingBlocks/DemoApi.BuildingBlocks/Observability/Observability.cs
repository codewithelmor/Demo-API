namespace DemoApi.BuildingBlocks.Observability;

/// <summary>App metrics (NFR-12, ADR-0013).</summary>
public static class AppMetrics
{
    public const string MeterName = "DemoApi";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("demoapi.cache.hits");
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("demoapi.cache.misses");
    public static readonly Counter<long> CacheErrors = Meter.CreateCounter<long>("demoapi.cache.errors");
    public static readonly Counter<long> RateLimited = Meter.CreateCounter<long>("demoapi.ratelimit.rejected");
    public static readonly Counter<long> RefreshReuse = Meter.CreateCounter<long>("demoapi.auth.refresh_reuse");
    public static readonly Counter<long> LoginFailures = Meter.CreateCounter<long>("demoapi.auth.login_failures");
}

public static class OpenTelemetrySetup
{
    public static IServiceCollection AddAppObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var otlp = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var builder = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("demoapi"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation()
                    .AddSource("Npgsql");
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    t.AddOtlpExporter();
                }
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(AppMetrics.MeterName, "Npgsql");
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    m.AddOtlpExporter();
                }
            });
        _ = builder;
        return services;
    }
}