namespace DemoApi.Bff.Security;

/// <summary>Fixed reason codes for security events. Only these strings are ever logged as a reason (T-55, NFR-F15).</summary>
internal static class SecurityReasons
{
    // Sign-in failures.
    public const string InvalidCredentials = "invalid-credentials";
    public const string EmailNotConfirmed = "email-not-confirmed";
    public const string Forbidden = "forbidden";
    public const string ValidationFailed = "validation-failed";
    public const string UnsupportedMediaType = "unsupported-media-type";
    public const string Throttled = "throttled";
    public const string UpstreamUnavailable = "upstream-unavailable";
    public const string StoreUnavailable = "session-store-unavailable";

    // CSRF rejections.
    public const string MissingCsrfHeader = "missing-csrf-header";
    public const string BadOrigin = "bad-origin";
    public const string BadFetchSite = "bad-sec-fetch-site";

    // Session ends.
    public const string Logout = "logout";
    public const string LogoutAll = "logout-all";
    public const string Replaced = "replaced-on-login";
    public const string Idle = "idle-timeout";
    public const string Absolute = "absolute-timeout";
    public const string RefreshTokenExpired = "refresh-token-expired";
    public const string RefreshRejected = "refresh-rejected";
    public const string Tampered = "tampered-blob";
    public const string UnknownOrPlanted = "unknown-or-planted-cookie";
    public const string ApiRevoked = "api-revoked";

    // Refresh outcomes.
    public const string Rotated = "rotated";
    public const string Rejected = "rejected";
    public const string BudgetExhausted = "budget-exhausted";
    public const string ApiThrottled = "api-throttled";
    public const string ApiUnavailable = "api-unavailable";
    public const string SessionGone = "session-gone";
    public const string LostSave = "lost-save";

    // Probes.
    public const string BffRoute = "bff";
    public const string ApiRoute = "api";
}

/// <summary>
/// Security-event log (T-55, NFR-F15, F19, F39). Each event has a fixed ID and carries only reason codes, a hashed
/// session ID and a masked client address. Never passed here: email, password, tokens, cookie values, nonces, bodies,
/// query strings, paths or ProblemDetails text.
/// </summary>
/// <remarks>
/// The session hash is HMAC-SHA256 under a random per-process key, cut to 8 bytes (16 hex characters). It lets an
/// operator follow one session across events without the log ever holding a usable session ID. It does not match
/// across a restart; that is deliberate, so a leaked log cannot be linked to older logs.
/// </remarks>
internal sealed partial class SecurityLog : IDisposable
{
    public const int SignInSucceededId = 5001;
    public const int SignInFailedId = 5002;
    public const int CsrfRejectedId = 5003;
    public const int SessionEndedId = 5004;
    public const int RateLimitedId = 5005;
    public const int RouteProbeId = 5006;
    public const int RefreshOutcomeId = 5007;
    public const int EventsSuppressedId = 5008;

    /// <summary>The meter for security events. Tags are the fixed event name and logged/suppressed, nothing else.</summary>
    public const string MeterName = "DemoApi.Bff.Security";

    private const int MaxTrackedKeys = 10_000;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly byte[] hashKey = RandomNumberGenerator.GetBytes(32);
    private readonly ILogger<SecurityLog> log;
    private readonly TimeProvider time;
    private readonly int perWindow;
    private readonly Lock gate = new();
    private readonly Dictionary<(int Id, string Client), Burst> bursts = [];
    private readonly ITimer sweeper;
    private readonly Meter meter;
    private readonly Counter<long> eventCounter;

    public SecurityLog(ILogger<SecurityLog> log, TimeProvider time, IOptions<BffOptions> options)
    {
        this.log = log;
        this.time = time;
        perWindow = Math.Max(1, options.Value.SecurityEventsPerMinute);
        sweeper = time.CreateTimer(_ => Sweep(), null, Window, Window);
        meter = new Meter(MeterName, null, null, this); // the scope lets a test tell instances apart
        eventCounter = meter.CreateCounter<long>("bff.security.events", description: "Security events by name and whether they were logged");
    }

    // Every event goes through Admit: at most SecurityEventsPerMinute lines per client and event ID per minute, then one
    // EventsSuppressed summary with the count. Everything is counted in the metric (F-2).
    public void SignInSucceeded(string sessionId, string clientIp)
    {
        if (Admit(SignInSucceededId, "SignInSucceeded", clientIp))
        {
            SignInSucceededCore(HashSession(sessionId), MaskIp(clientIp));
        }
    }

    public void SignInFailed(string reason, string clientIp)
    {
        if (Admit(SignInFailedId, "SignInFailed", clientIp))
        {
            SignInFailedCore(reason, MaskIp(clientIp));
        }
    }

    public void CsrfRejected(string reason, string clientIp)
    {
        if (Admit(CsrfRejectedId, "CsrfRejected", clientIp))
        {
            CsrfRejectedCore(reason, MaskIp(clientIp));
        }
    }

    public void SessionEnded(string reason, string? sessionId, string? clientIp)
    {
        if (Admit(SessionEndedId, "SessionEnded", clientIp))
        {
            SessionEndedCore(reason, HashSession(sessionId), MaskIp(clientIp));
        }
    }

    public void RateLimited(string policy, string clientIp)
    {
        if (Admit(RateLimitedId, "RateLimited", clientIp))
        {
            RateLimitedCore(policy, MaskIp(clientIp));
        }
    }

    /// <summary>A 404 or 405 produced by the BFF under /api or /bff. The path is not logged: it is attacker-chosen text.</summary>
    public void RouteProbe(int status, string area, string method, string clientIp)
    {
        if (Admit(RouteProbeId, "RouteProbe", clientIp))
        {
            RouteProbeCore(status, area, KnownMethod(method), MaskIp(clientIp));
        }
    }

    public void RefreshOutcome(string outcome, string sessionId, string? clientIp)
    {
        if (Admit(RefreshOutcomeId, "RefreshOutcome", clientIp))
        {
            RefreshOutcomeCore(outcome, HashSession(sessionId), MaskIp(clientIp));
        }
    }

    public void Dispose()
    {
        sweeper.Dispose();
        meter.Dispose();
    }

    private bool Admit(int id, string name, string? clientIp)
    {
        DateTimeOffset now = time.GetUtcNow();
        (int Id, string Client) key = (id, clientIp ?? "unknown");
        List<(int Id, string Client, int Count)>? summaries = null;
        bool allowed;
        lock (gate)
        {
            if (bursts.TryGetValue(key, out Burst? burst) && now - burst.Start >= Window)
            {
                if (burst.Suppressed > 0)
                {
                    (summaries ??= []).Add((key.Id, key.Client, burst.Suppressed));
                }

                _ = bursts.Remove(key);
                burst = null;
            }

            if (burst is null && bursts.Count >= MaxTrackedKeys)
            {
                CollectExpired(now, ref summaries);
            }

            if (burst is null && bursts.Count >= MaxTrackedKeys)
            {
                allowed = false; // table full of live windows: count only
            }
            else
            {
                if (burst is null)
                {
                    burst = new Burst(now);
                    bursts[key] = burst;
                }

                allowed = burst.Logged < perWindow;
                if (allowed)
                {
                    burst.Logged++;
                }
                else
                {
                    burst.Suppressed++;
                }
            }
        }

        eventCounter.Add(1, new KeyValuePair<string, object?>("event", name), new KeyValuePair<string, object?>("outcome", allowed ? "logged" : "suppressed"));
        Emit(summaries);
        return allowed;
    }

    // Timer: closes windows that ended with suppressed events, so the summary appears even when traffic stops.
    private void Sweep()
    {
        List<(int Id, string Client, int Count)>? summaries = null;
        lock (gate)
        {
            CollectExpired(time.GetUtcNow(), ref summaries);
        }

        Emit(summaries);
    }

    private void CollectExpired(DateTimeOffset now, ref List<(int Id, string Client, int Count)>? summaries)
    {
        foreach (KeyValuePair<(int Id, string Client), Burst> entry in bursts.Where(b => now - b.Value.Start >= Window).ToList())
        {
            if (entry.Value.Suppressed > 0)
            {
                (summaries ??= []).Add((entry.Key.Id, entry.Key.Client, entry.Value.Suppressed));
            }

            _ = bursts.Remove(entry.Key);
        }
    }

    private void Emit(List<(int Id, string Client, int Count)>? summaries)
    {
        if (summaries is null)
        {
            return;
        }

        foreach ((int id, string client, int count) in summaries)
        {
            EventsSuppressedCore(id, count, MaskIp(client));
        }
    }

    private sealed class Burst(DateTimeOffset start)
    {
        public DateTimeOffset Start { get; } = start;

        public int Logged { get; set; }

        public int Suppressed { get; set; }
    }

    /// <summary>Hashes a session ID for logs. Null (no session) is "none".</summary>
    public string HashSession(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return "none";
        }

        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        _ = HMACSHA256.HashData(hashKey, Encoding.UTF8.GetBytes(sessionId), mac);
        return Convert.ToHexStringLower(mac[..8]);
    }

    /// <summary>IPv4 as a /24 (a.b.c.0/24), IPv6 as a /48. Anything else is "unknown".</summary>
    public static string MaskIp(string? address)
    {
        if (!IPAddress.TryParse(address, out IPAddress? ip))
        {
            return "unknown";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        Span<byte> bytes = stackalloc byte[16];
        _ = ip.TryWriteBytes(bytes, out int written);
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            bytes[3] = 0;
            return new IPAddress(bytes[..4]).ToString() + "/24";
        }

        bytes[6..written].Clear();
        return new IPAddress(bytes[..written]).ToString() + "/48";
    }

    private static string KnownMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method)
            || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsPatch(method)
            ? method.ToUpperInvariant()
            : "other";

    [LoggerMessage(EventId = EventsSuppressedId, EventName = "EventsSuppressed", Level = LogLevel.Warning,
        Message = "Security events suppressed to limit log volume. Event {SuppressedEventId}, count {Count}, client {Client}")]
    private partial void EventsSuppressedCore(int suppressedEventId, int count, string client);

    [LoggerMessage(EventId = SignInSucceededId, EventName = "SignInSucceeded", Level = LogLevel.Information,
        Message = "Sign-in succeeded. Session {Session}, client {Client}")]
    private partial void SignInSucceededCore(string session, string client);

    [LoggerMessage(EventId = SignInFailedId, EventName = "SignInFailed", Level = LogLevel.Warning,
        Message = "Sign-in failed. Reason {Reason}, client {Client}")]
    private partial void SignInFailedCore(string reason, string client);

    [LoggerMessage(EventId = CsrfRejectedId, EventName = "CsrfRejected", Level = LogLevel.Warning,
        Message = "Request rejected by the CSRF check. Reason {Reason}, client {Client}")]
    private partial void CsrfRejectedCore(string reason, string client);

    [LoggerMessage(EventId = SessionEndedId, EventName = "SessionEnded", Level = LogLevel.Information,
        Message = "Session ended. Reason {Reason}, session {Session}, client {Client}")]
    private partial void SessionEndedCore(string reason, string session, string client);

    [LoggerMessage(EventId = RateLimitedId, EventName = "RateLimited", Level = LogLevel.Warning,
        Message = "Request rate-limited. Policy {Policy}, client {Client}")]
    private partial void RateLimitedCore(string policy, string client);

    [LoggerMessage(EventId = RouteProbeId, EventName = "RouteProbe", Level = LogLevel.Information,
        Message = "Request outside the allow-list answered {Status}. Area {Area}, method {Method}, client {Client}")]
    private partial void RouteProbeCore(int status, string area, string method, string client);

    [LoggerMessage(EventId = RefreshOutcomeId, EventName = "RefreshOutcome", Level = LogLevel.Information,
        Message = "Token refresh finished. Outcome {Outcome}, session {Session}, client {Client}")]
    private partial void RefreshOutcomeCore(string outcome, string session, string client);
}

internal static class SecurityLogExtensions
{
    public static SecurityLog SecurityEvents(this HttpContext context) => context.RequestServices.GetRequiredService<SecurityLog>();
}