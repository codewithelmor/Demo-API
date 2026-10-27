namespace DemoApi.Api.Features.Health;

/// <summary>Own small status body, not ProblemDetails (D10). <see cref="Checks"/> is written in Development only.</summary>
public sealed record HealthResponse(string Status)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<HealthCheckEntry>? Checks { get; init; }
}

public sealed record HealthCheckEntry(string Name, string Status, long DurationMs);

// EP-56
public sealed class LivenessEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("/health/live");
        RoutePrefixOverride(string.Empty);
        AllowAnonymous(); // probes run without a token; an invalid bearer token is ignored, never 401
        Options(x => x.RateLimit(RateLimitPolicies.None));
        Summary(s =>
        {
            s.Summary = "Liveness probe";
            s.Description = "Anonymous so orchestrator probes work without a token. Checks the process only (no dependencies); always 200 { status: Healthy }. Not rate limited.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(new HealthResponse(nameof(HealthStatus.Healthy)), ct);
}

// EP-57
public sealed class ReadinessEndpoint(HealthCheckService health, IHostEnvironment environment, IMemoryCache memory) : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("/health/ready");
        RoutePrefixOverride(string.Empty);
        AllowAnonymous(); // probes run without a token; an invalid bearer token is ignored, never 401
        Options(x => x.RateLimit(RateLimitPolicies.None));
        Summary(s =>
        {
            s.Summary = "Readiness probe";
            s.Description = "Anonymous so orchestrator probes work without a token. Checks PostgreSQL and Valkey (500 ms each): 200 Healthy, 200 Degraded (Valkey down), 503 Unhealthy (PostgreSQL down). Development adds per-check details. Not rate limited.";
            s.Responses[503] = "PostgreSQL is unavailable.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Anonymous and unlimited, so the dependency checks are shared for 2 s instead of running on every call (T-33).
        var report = await memory.GetOrCreateAsync("health:ready", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2);
            return await health.CheckHealthAsync(c => c.Tags.Contains(HealthTags.Ready), CancellationToken.None);
        }) ?? await health.CheckHealthAsync(c => c.Tags.Contains(HealthTags.Ready), ct);
        var body = new HealthResponse(report.Status.ToString())
        {
            Checks = environment.IsDevelopment()
                ? report.Entries.Select(e => new HealthCheckEntry(e.Key, e.Value.Status.ToString(), (long)e.Value.Duration.TotalMilliseconds)).ToList()
                : null,
        };
        var status = report.Status == HealthStatus.Unhealthy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;
        await Send.ResponseAsync(body, status, ct);
    }
}