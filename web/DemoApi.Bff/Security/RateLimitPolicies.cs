namespace DemoApi.Bff.Security;

internal static class RateLimitPolicies
{
    /// <summary>One shared bucket per IP for sign-in, register, confirm, resend, forgot, reset, change password, erase.</summary>
    public const string AuthStrict = "auth-strict";

    /// <summary>Readiness calls the API and Valkey, so it is limited per IP (generously).</summary>
    public const string HealthReady = "health-ready";

    public static IServiceCollection AddBffRateLimiting(this IServiceCollection services)
    {
        _ = services.AddRateLimiter(static options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static async (context, ct) =>
            {
                int retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan after)
                    ? Math.Max(1, (int)Math.Ceiling(after.TotalSeconds))
                    : 60;
                string policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "default";
                context.HttpContext.SecurityEvents().RateLimited(policy, ClientIp.Key(context.HttpContext));
                await Problems.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, "rate-limited", null, retry);
            };
        });

        _ = services.AddOptions<RateLimiterOptions>().Configure<IOptions<BffOptions>>(static (limiter, bff) =>
        {
            BffOptions settings = bff.Value;
            limiter.AddPolicy(AuthStrict, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp.Key(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.AuthStrictPermitLimit,
                    Window = TimeSpan.FromSeconds(settings.RateLimitWindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
            limiter.AddPolicy(HealthReady, context => RateLimitPartition.GetFixedWindowLimiter(
                "ready:" + ClientIp.Key(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.ReadyPermitLimit,
                    Window = TimeSpan.FromSeconds(settings.RateLimitWindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
        });
        return services;
    }
}