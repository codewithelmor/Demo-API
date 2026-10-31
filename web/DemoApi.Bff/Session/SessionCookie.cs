namespace DemoApi.Bff.Session;

/// <summary>
/// The one session cookie (NFR-F10): <c>__Host-</c> prefix, HttpOnly, Secure, SameSite=Strict, Path=/, no Domain.
/// The flags are the same in every environment. The value is only an opaque session ID.
/// </summary>
internal static class SessionCookie
{
    public const string Name = "__Host-demoapi";

    public static string? Read(HttpRequest request) => request.Cookies.TryGetValue(Name, out string? value) ? value : null;

    public static void Write(HttpResponse response, string sessionId, DateTimeOffset expires) =>
        response.Cookies.Append(Name, sessionId, Options(expires));

    public static void Delete(HttpResponse response) =>
        response.Cookies.Append(Name, string.Empty, Options(DateTimeOffset.UnixEpoch));

    private static CookieOptions Options(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };
}