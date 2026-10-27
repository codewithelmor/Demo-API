namespace DemoApi.BuildingBlocks.Caching;

/// <summary>HybridCache wrapper (ADR-0009). Never throws for cache problems; the factory result is returned.</summary>
public sealed class AppHybridCache(HybridCache cache, ILogger<AppHybridCache> logger) : IAppCache
{
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan l2Ttl, TimeSpan l1Ttl, CancellationToken ct)
    {
        var hit = true;
        var options = new HybridCacheEntryOptions { Expiration = l2Ttl, LocalCacheExpiration = l1Ttl };
        var value = await cache.GetOrCreateAsync(
            key,
            factory,
            async (f, token) =>
            {
                hit = false;
                return await f(token);
            },
            options,
            cancellationToken: ct);
        (hit ? AppMetrics.CacheHits : AppMetrics.CacheMisses).Add(1);
        return value;
    }

    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync(keys, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache remove failed");
        }
    }

    public Task<string> GetListGenerationAsync(string generationKey, CancellationToken ct) =>
        GetOrCreateAsync(generationKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), TimeSpan.FromDays(1), TimeSpan.FromSeconds(5), ct);

    public async Task BumpListGenerationAsync(string generationKey, CancellationToken ct)
    {
        try
        {
            await cache.SetAsync(
                generationKey,
                Guid.NewGuid().ToString("N"),
                new HybridCacheEntryOptions { Expiration = TimeSpan.FromDays(1), LocalCacheExpiration = TimeSpan.FromSeconds(5) },
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache generation bump failed");
        }
    }
}

/// <summary>
/// L2 decorator (ADR-0011): 250 ms total budget incl. up to 3 fast retries, a circuit breaker, and every
/// failure treated as a miss. Valkey being down never causes a 5xx.
/// </summary>
public sealed class ResilientDistributedCache : IDistributedCache
{
    private readonly IDistributedCache _inner;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger _logger;

    public ResilientDistributedCache(IDistributedCache inner, ILogger<ResilientDistributedCache> logger)
    {
        _inner = inner;
        _logger = logger;
        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromMilliseconds(250))
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(e => e is not OperationCanceledException || e is TimeoutRejectedException),
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(10),
                ShouldHandle = new PredicateBuilder().Handle<RedisConnectionException>().Handle<RedisTimeoutException>(),
            })
            .Build();
    }

    // IDistributedCache requires the sync members; HybridCache only calls the async ones, so these exist for the interface.
    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        await RunAsync(async ct => await _inner.GetAsync(key, ct), null, token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options).GetAwaiter().GetResult();

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        RunAsync(async ct => { await _inner.SetAsync(key, value, options, ct); return true; }, false, token);

    public void Refresh(string key) => RefreshAsync(key).GetAwaiter().GetResult();

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        RunAsync(async ct => { await _inner.RefreshAsync(key, ct); return true; }, false, token);

    public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        RunAsync(async ct => { await _inner.RemoveAsync(key, ct); return true; }, false, token);

    private async Task<T> RunAsync<T>(Func<CancellationToken, ValueTask<T>> work, T fallback, CancellationToken token)
    {
        try
        {
            return await _pipeline.ExecuteAsync(work, token);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            AppMetrics.CacheErrors.Add(1);
            _logger.LogWarning("Valkey cache call failed ({Error}); treated as a miss", ex.GetType().Name);
            return fallback;
        }
    }
}

public static class CachingSetup
{
    public static IServiceCollection AddAppCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var valkey = configuration.GetConnectionString("Valkey");
        if (!string.IsNullOrWhiteSpace(valkey))
        {
            var options = ConfigurationOptions.Parse(valkey);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000;
            options.SyncTimeout = 250;
            options.AsyncTimeout = 250;
            var multiplexer = new Lazy<Task<IConnectionMultiplexer>>(async () => await ConnectionMultiplexer.ConnectAsync(options));
            services.AddSingleton<Func<Task<IConnectionMultiplexer>>>(() => multiplexer.Value);
            services.AddSingleton(sp => new RedisCache(Microsoft.Extensions.Options.Options.Create(new RedisCacheOptions
            {
                ConnectionMultiplexerFactory = () => multiplexer.Value,
                InstanceName = string.Empty,
            })));
            services.AddSingleton<IDistributedCache>(sp => new ResilientDistributedCache(
                sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<ILogger<ResilientDistributedCache>>()));
        }

        services.AddHybridCache(o =>
        {
            o.MaximumPayloadBytes = 1024 * 1024;
            o.DefaultEntryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(2), LocalCacheExpiration = TimeSpan.FromSeconds(30) };
        });
        services.AddSingleton<IAppCache, AppHybridCache>();
        return services;
    }
}