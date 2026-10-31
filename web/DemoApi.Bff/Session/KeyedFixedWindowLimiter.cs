namespace DemoApi.Bff.Session;

/// <summary>A fixed-window limiter keyed by client IP, used where the built-in middleware does not fit.</summary>
internal abstract class KeyedFixedWindowLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter;
    private readonly int windowSeconds;

    protected KeyedFixedWindowLimiter(int permitLimit, int windowSeconds)
    {
        this.windowSeconds = windowSeconds;
        limiter = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }

    public bool TryAcquire(string key, out int retryAfterSeconds)
    {
        using RateLimitLease lease = limiter.AttemptAcquire(key);
        if (lease.IsAcquired)
        {
            retryAfterSeconds = 0;
            return true;
        }

        retryAfterSeconds = lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retry)
            ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds))
            : windowSeconds;
        return false;
    }

    public void Dispose() => limiter.Dispose();
}

/// <summary>30 per 60 s per IP, counted on each EP-05 call the coordinator makes (FR-F008).</summary>
internal sealed class RefreshRateLimiter(IOptions<BffOptions> options)
    : KeyedFixedWindowLimiter(options.Value.RefreshPermitLimit, options.Value.RateLimitWindowSeconds);

/// <summary>60 per 60 s per IP for unauthenticated /bff and /api calls (NFR-F37).</summary>
internal sealed class AnonymousRateLimiter(IOptions<BffOptions> options)
    : KeyedFixedWindowLimiter(options.Value.AnonymousPermitLimit, options.Value.RateLimitWindowSeconds);