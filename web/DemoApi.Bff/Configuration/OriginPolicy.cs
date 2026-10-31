namespace DemoApi.Bff.Configuration;

internal static class OriginPolicy
{
    /// <summary>
    /// Accepts only an exact origin: scheme, host and optional non-default port. No wildcard, no "null", no path,
    /// no query, no user info, no trailing slash.
    /// </summary>
    public static bool TryNormalize(string? raw, out string origin)
    {
        origin = string.Empty;
        if (string.IsNullOrWhiteSpace(raw) || raw is "*" or "null")
        {
            return false;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.AbsolutePath != "/")
        {
            return false;
        }

        string normalized = uri.GetLeftPart(UriPartial.Authority);
        if (!string.Equals(raw, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        origin = normalized;
        return true;
    }
}