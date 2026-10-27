namespace DemoApi.BuildingBlocks.RateLimiting;

public static class RateLimitPolicies
{
    public const string AuthStrict = "auth-strict";
    public const string Refresh = "refresh";
    public const string General = "general";
    public const string None = "none";
}

/// <summary>Endpoint metadata that picks the rate-limit policy; endpoints without it use <c>general</c>.</summary>
public sealed record RateLimitPolicyMetadata(string Policy);

public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public int AnonymousPermitLimit { get; set; }

    public int WindowSeconds { get; set; } = 60;
}

public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    public RateLimitWindow AuthStrict { get; set; } = new() { PermitLimit = 10 };

    public RateLimitWindow Refresh { get; set; } = new() { PermitLimit = 30 };

    public RateLimitWindow General { get; set; } = new() { PermitLimit = 300, AnonymousPermitLimit = 60 };

    public int ReportsConcurrency { get; set; } = 4;
}

public static class RateLimitingSetup
{
    public static TBuilder RateLimit<TBuilder>(this TBuilder builder, string policy)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RateLimitPolicyMetadata(policy));

    /// <summary>
    /// ADR-0010: fixed windows per policy (partitioned by user or IP) chained with a concurrency limit for
    /// reports. One global limiter so the policy comes from endpoint metadata.
    /// </summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(); // registers the middleware services; the options below set the limits
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.Section);
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingOptions>>((o, limits) =>
        {
            var cfg = limits.Value;
            o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                    ctx.Request.Path.StartsWithSegments("/api/v1/reports", StringComparison.OrdinalIgnoreCase)
                        ? RateLimitPartition.GetConcurrencyLimiter("reports", _ => new ConcurrencyLimiterOptions { PermitLimit = cfg.ReportsConcurrency, QueueLimit = 0 })
                        : RateLimitPartition.GetNoLimiter("none")),
                PartitionedRateLimiter.Create<HttpContext, string>(ctx => Partition(ctx, cfg)));
            o.OnRejected = OnRejectedAsync;
        });
        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext ctx, RateLimitingOptions cfg)
    {
        var endpoint = ctx.GetEndpoint();
        if (endpoint is null)
        {
            return RateLimitPartition.GetNoLimiter("no-endpoint");
        }

        var policy = endpoint.Metadata.GetMetadata<RateLimitPolicyMetadata>()?.Policy ?? RateLimitPolicies.General;
        var ip = ClientIp(ctx);
        return policy switch
        {
            RateLimitPolicies.None => RateLimitPartition.GetNoLimiter("none"),
            RateLimitPolicies.AuthStrict => Fixed($"auth-strict:ip:{ip}", cfg.AuthStrict.PermitLimit, cfg.AuthStrict.WindowSeconds),
            RateLimitPolicies.Refresh => Fixed($"refresh:ip:{ip}", cfg.Refresh.PermitLimit, cfg.Refresh.WindowSeconds),
            _ => ctx.User.FindFirstValue(AppClaims.Subject) is { Length: > 0 } sub
                ? Fixed($"general:user:{sub}", cfg.General.PermitLimit, cfg.General.WindowSeconds)
                : Fixed($"general:anon:{ip}", cfg.General.AnonymousPermitLimit, cfg.General.WindowSeconds),
        };
    }

    private static RateLimitPartition<string> Fixed(string key, int permits, int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    /// <summary>Remote address only (no X-Forwarded-For); IPv6 keyed by its /64 prefix.</summary>
    private static string ClientIp(HttpContext ctx)
    {
        var address = ctx.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes) + "/64";
        }

        return address.ToString();
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken ct)
    {
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : 1;
        context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, seconds).ToString(CultureInfo.InvariantCulture);
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("DemoApi.RateLimiting")
            .LogWarning("Rate limit exceeded on {Path}", context.HttpContext.Request.Path.Value);
        Observability.AppMetrics.RateLimited.Add(1);
        await Problems.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, "rate-limited", "Too many requests", "Rate limit exceeded. Try again later.");
    }
}