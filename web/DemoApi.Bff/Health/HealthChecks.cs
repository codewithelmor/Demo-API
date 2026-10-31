namespace DemoApi.Bff.Health;

/// <summary>
/// Readiness = the API answers and the session store answers. The unauthenticated endpoint would otherwise call both
/// on every request, so the result is reused for a few seconds (L-4). One probe at a time.
/// </summary>
internal sealed class ReadinessCheck(ApiAuthClient api, IDistributedCache cache, TimeProvider time, IOptions<BffOptions> options) : IHealthCheck, IDisposable
{
    public void Dispose() => gate.Dispose();

    private readonly SemaphoreSlim gate = new(1, 1);
    private HealthCheckResult cached;
    private DateTimeOffset cachedUntil = DateTimeOffset.MinValue;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (time.GetUtcNow() < cachedUntil)
        {
            return cached;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (time.GetUtcNow() < cachedUntil)
            {
                return cached;
            }

            bool apiReady = await api.IsReadyAsync(cancellationToken);
            bool storeReady = await StoreReadyAsync(cancellationToken);
            cached = apiReady && storeReady ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("not ready");
            cachedUntil = time.GetUtcNow().AddSeconds(options.Value.ReadyCacheSeconds);
            return cached;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    private async Task<bool> StoreReadyAsync(CancellationToken ct)
    {
#pragma warning disable CA1031 // A health check reports any failure as not ready.
        try
        {
            _ = await cache.GetAsync("bff:health", ct);
            return true;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return false;
        }
#pragma warning restore CA1031
    }
}

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapBffHealth(this IEndpointRouteBuilder app)
    {
        // No details in the body; the check logs nothing sensitive.
        static Task Write(HttpContext context, HealthReport report) =>
            context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });

        // Liveness does no work, so it stays free. Readiness has its own generous limit.
        _ = app.MapHealthChecks("/bff/health/live", new HealthCheckOptions { Predicate = static _ => false, ResponseWriter = Write })
            .DisableRateLimiting();
        _ = app.MapHealthChecks("/bff/health/ready", new HealthCheckOptions { Predicate = static r => r.Tags.Contains("ready"), ResponseWriter = Write })
            .RequireRateLimiting(RateLimitPolicies.HealthReady);
        return app;
    }
}