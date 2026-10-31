namespace DemoApi.Bff.Configuration;

/// <summary>Settings from the <c>Bff</c> section (ADR-0015, NFR-F29).</summary>
public sealed class BffOptions
{
    public const string SectionName = "Bff";

    /// <summary>Base address of the Orders API, for example <c>http://api:8080</c>.</summary>
    public Uri ApiBaseUrl { get; set; } = new("http://api:8080");

    /// <summary>Exact origins allowed to make state-changing requests (CSRF allow-list).</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Shared limit for sign-in, register, confirm, resend, forgot, reset, change password and erase.</summary>
    public int AuthStrictPermitLimit { get; set; } = 10;

    /// <summary>Limit for calls to the API refresh endpoint, per client IP.</summary>
    public int RefreshPermitLimit { get; set; } = 30;

    /// <summary>Limit for unauthenticated calls to /bff and /api, per client IP.</summary>
    public int AnonymousPermitLimit { get; set; } = 60;

    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>Limit for GET /bff/health/ready per IP (it calls the API and Valkey, so it is not free).</summary>
    public int ReadyPermitLimit { get; set; } = 120;

    /// <summary>How long a readiness result is reused, in seconds.</summary>
    public int ReadyCacheSeconds { get; set; } = 8;

    /// <summary>Security events logged per client and event ID per minute. The rest are counted and summarized (F-2).</summary>
    public int SecurityEventsPerMinute { get; set; } = 10;

    public int AdminIdleMinutes { get; set; } = 30;

    public int AdminAbsoluteHours { get; set; } = 12;
}

/// <summary>Session store settings. The connection string is required outside Development (fail closed).</summary>
public sealed class ValkeyOptions
{
    public const string SectionName = "Valkey";

    public string? ConnectionString { get; set; }

    /// <summary>
    /// Allows process-memory sessions when no connection string is set. Not set means: allowed in Development only.
    /// Compose runs the BFF as Production, so it must set this explicitly (or the connection string).
    /// </summary>
    public bool? AllowInMemory { get; set; }
}

/// <summary>Data Protection key ring location (a file volume, ADR-0015).</summary>
public sealed class KeyStoreOptions
{
    public const string SectionName = "DataProtection";

    public string? KeysPath { get; set; }
}