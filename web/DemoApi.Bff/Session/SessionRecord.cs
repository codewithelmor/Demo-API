namespace DemoApi.Bff.Session;

/// <summary>The user summary from EP-08. Never holds tokens.</summary>
internal sealed record UserSummary(Guid UserId, string Email, bool EmailConfirmed, IReadOnlyList<string> Roles, Guid? CustomerId)
{
    public bool IsAdmin => Roles.Contains("Admin", StringComparer.Ordinal);
}

/// <summary>
/// What the BFF keeps per session in Valkey (encrypted). <see cref="Version"/> changes only when the token set
/// changes, so a refresh result can be saved conditionally (NFR-F31). Activity updates keep the version.
/// </summary>
internal sealed record SessionRecord(
    string SessionId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserSummary User,
    DateTimeOffset LastActivityAt,
    DateTimeOffset CreatedAt,
    long Version)
{
    // The generated ToString would print both tokens into any log line that formats the record.
    public override string ToString() => "SessionRecord { redacted }";
}