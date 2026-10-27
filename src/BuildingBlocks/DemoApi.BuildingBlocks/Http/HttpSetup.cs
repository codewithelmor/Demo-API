namespace DemoApi.BuildingBlocks.Http;

/// <summary>Security headers and a default <c>Cache-Control: no-store</c> on every response (NFR-06).</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (!context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            headers.Remove("Server");
            if (string.IsNullOrEmpty(headers.CacheControl) || context.Response.StatusCode >= 400)
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });
        return next(context);
    }
}

public sealed class CorsOptions
{
    public string[] AllowedOrigins { get; set; } = [];
}

public static class HealthTags
{
    public const string Ready = "ready";
}

/// <summary><c>SELECT 1</c> with a 500 ms budget (NFR-14). Unhealthy = PostgreSQL down.</summary>
public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource, PostgresCircuitBreaker breaker) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (breaker.IsOpen)
        {
            return HealthCheckResult.Unhealthy("circuit open");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cts.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or OperationCanceledException or TimeoutException or System.Net.Sockets.SocketException)
        {
            return HealthCheckResult.Unhealthy("postgres unavailable");
        }
    }
}

/// <summary><c>PING</c> with a 500 ms budget. Valkey down = Degraded (G3).</summary>
public sealed class ValkeyHealthCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var factory = services.GetService<Func<Task<IConnectionMultiplexer>>>();
        if (factory is null)
        {
            return HealthCheckResult.Degraded("valkey not configured");
        }

        try
        {
            var mux = await factory().WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
            await mux.GetDatabase().PingAsync().WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or OperationCanceledException)
        {
            return HealthCheckResult.Degraded("valkey unavailable");
        }
    }
}

public static class HttpSetup
{
    public static IServiceCollection AddAppHttp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CorsOptions>(configuration.GetSection("Cors"));
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (origins.Length > 0)
            {
                p.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST", "PUT", "DELETE").WithExposedHeaders("ETag", "Location", "Retry-After");
            }
        }));
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o =>
        {
            o.Limits.MaxRequestBodySize = 1024 * 1024;
            o.AddServerHeader = false;
        });
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: [HealthTags.Ready])
            .AddCheck<ValkeyHealthCheck>("valkey", tags: [HealthTags.Ready]);
        return services;
    }
}