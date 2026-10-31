namespace DemoApi.Bff.Security;

internal static class RequestPaths
{
    public static bool IsApi(PathString path) => HasPrefix(path.Value, "/api");

    public static bool IsBff(PathString path) => HasPrefix(path.Value, "/bff");

    public static bool IsApiOrBff(PathString path) => IsApi(path) || IsBff(path);

    public static bool IsHealth(PathString path) => path.StartsWithSegments("/bff/health", StringComparison.OrdinalIgnoreCase);

    public static bool IsSafeMethod(string method) => HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

    private static bool HasPrefix(string? path, string prefix) =>
        path is not null
        && (path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
}