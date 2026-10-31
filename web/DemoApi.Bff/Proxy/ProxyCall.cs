namespace DemoApi.Bff.Proxy;

/// <summary>State of one proxied request: the token it used, the buffered body for one replay, and the cookie expiry.</summary>
internal sealed class ProxyCall
{
    public const string ItemsKey = "bff.proxycall";

    public static readonly HttpRequestOptionsKey<ProxyCall> OptionsKey = new(ItemsKey);

    public required string SessionId { get; init; }

    /// <summary>The access token this request is sent with. Updated when a replay uses a new one.</summary>
    public required string AccessToken { get; set; }

    public required string ClientIp { get; init; }

    public required DateTimeOffset RefreshTokenExpiresAt { get; set; }

    /// <summary>Request body, buffered (at most 1 MB) so the request can be replayed once after an API 401.</summary>
    public byte[]? Body { get; init; }

    public bool Replayed { get; set; }

    /// <summary>Set when the session ended during this request; the cookie must not be refreshed.</summary>
    public bool Ended { get; set; }
}

/// <summary>Raised inside the proxy pipeline when a refresh after an API 401 did not give a usable session.</summary>
internal sealed class ProxyAuthException(RefreshOutcome outcome) : Exception("The API rejected the session.")
{
    public RefreshOutcome Outcome { get; } = outcome;
}