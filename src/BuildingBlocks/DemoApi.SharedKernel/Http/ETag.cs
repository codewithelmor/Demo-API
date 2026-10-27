namespace DemoApi.SharedKernel.Http;

public enum ETagCheck
{
    Missing,
    Match,
    NoMatch,
}

/// <summary>Strong ETags from the xmin row version (FR-129, ADR-0007).</summary>
public static class ETag
{
    public static string Format(uint version) => $"\"{version}\"";

    /// <summary><c>If-Match: *</c> counts as missing because it would skip the check.</summary>
    public static ETagCheck Check(string? ifMatch, uint version)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*")
        {
            return ETagCheck.Missing;
        }

        var current = Format(version);
        return ifMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(tag => string.Equals(tag, current, StringComparison.Ordinal))
            ? ETagCheck.Match
            : ETagCheck.NoMatch;
    }
}