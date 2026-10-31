namespace DemoApi.Bff.Proxy;

/// <summary>
/// The YARP route allow-list from the endpoint catalog (ADR-0015, NFR-F32). One route per endpoint, an exact
/// method, typed route constraints. Never proxied: login, refresh, logout, logout-all (only /bff handles them).
/// Stretch endpoints EP-20, 21, 28, 34, 44, 49 are not listed. Admin routes are proxied for any signed-in user;
/// the API enforces roles.
/// </summary>
internal static class AllowList
{
    public const string ClusterId = "orders-api";
    public const string AnonymousMetadata = "Anonymous";
    public const string EndsSessionMetadata = "EndsSession";

    internal sealed record Entry(int Endpoint, string Method, string Path, Access Access = Access.Authenticated);

    internal enum Access
    {
        /// <summary>No token is sent (register, confirm, resend, forgot, reset).</summary>
        Anonymous,

        Authenticated,

        /// <summary>Authenticated, shares the auth-strict bucket, and a 2xx ends the session (EP-09, EP-37).</summary>
        StrictEndsSession,
    }

    private const string V1 = "/api/v1";

    public static IReadOnlyList<Entry> Entries { get; } =
    [
        new(1, "POST", $"{V1}/auth/register", Access.Anonymous),
        new(2, "POST", $"{V1}/auth/confirm-email", Access.Anonymous),
        new(3, "POST", $"{V1}/auth/resend-confirmation", Access.Anonymous),
        new(8, "GET", $"{V1}/auth/me"),
        new(9, "POST", $"{V1}/auth/change-password", Access.StrictEndsSession),
        new(10, "POST", $"{V1}/auth/forgot-password", Access.Anonymous),
        new(11, "POST", $"{V1}/auth/reset-password", Access.Anonymous),
        new(12, "GET", $"{V1}/admin/users"),
        new(13, "GET", $"{V1}/admin/users/{{userId:guid}}"),
        new(14, "POST", $"{V1}/admin/users/{{userId:guid}}/lock"),
        new(15, "POST", $"{V1}/admin/users/{{userId:guid}}/unlock"),
        new(16, "PUT", $"{V1}/admin/users/{{userId:guid}}/roles/{{role:alpha:length(1,32)}}"),
        new(17, "DELETE", $"{V1}/admin/users/{{userId:guid}}/roles/{{role:alpha:length(1,32)}}"),
        new(18, "GET", $"{V1}/admin/roles"),
        new(19, "POST", $"{V1}/admin/users"),
        new(22, "GET", $"{V1}/products"),
        new(23, "GET", $"{V1}/products/{{productId:guid}}"),
        new(24, "POST", $"{V1}/products"),
        new(25, "PUT", $"{V1}/products/{{productId:guid}}"),
        new(26, "DELETE", $"{V1}/products/{{productId:guid}}"),
        new(27, "POST", $"{V1}/products/{{productId:guid}}/restore"),
        new(29, "POST", $"{V1}/customers"),
        new(30, "GET", $"{V1}/customers"),
        new(31, "GET", $"{V1}/customers/{{customerId:guid}}"),
        new(32, "PUT", $"{V1}/customers/{{customerId:guid}}"),
        new(33, "DELETE", $"{V1}/customers/{{customerId:guid}}"),
        new(35, "GET", $"{V1}/customers/me"),
        new(36, "PUT", $"{V1}/customers/me"),
        new(37, "POST", $"{V1}/customers/me/erase", Access.StrictEndsSession),
        new(38, "POST", $"{V1}/orders"),
        new(39, "GET", $"{V1}/orders"),
        new(40, "GET", $"{V1}/orders/{{orderId:guid}}"),
        new(41, "PUT", $"{V1}/orders/{{orderId:guid}}/items"),
        new(42, "POST", $"{V1}/orders/{{orderId:guid}}/status"),
        new(43, "POST", $"{V1}/orders/{{orderId:guid}}/cancel"),
        new(45, "POST", $"{V1}/customers/me/orders"),
        new(46, "GET", $"{V1}/customers/me/orders"),
        new(47, "GET", $"{V1}/customers/me/orders/{{orderId:guid}}"),
        new(48, "POST", $"{V1}/customers/me/orders/{{orderId:guid}}/cancel"),
        new(50, "GET", $"{V1}/reports/order-summaries"),
        new(51, "GET", $"{V1}/customers/{{customerId:guid}}/order-history"),
        new(52, "GET", $"{V1}/reports/sales"),
        new(53, "GET", $"{V1}/reports/low-stock"),
        new(54, "GET", $"{V1}/reports/top-products"),
        new(55, "GET", $"{V1}/audit-logs"),
    ];

    public static IReadOnlyList<RouteConfig> Routes() =>
    [
        .. Entries.Select(static entry => new RouteConfig
        {
            RouteId = $"ep-{entry.Endpoint:00}",
            ClusterId = ClusterId,
            Match = new RouteMatch { Path = entry.Path, Methods = [entry.Method] },
            RateLimiterPolicy = entry.Access is Access.Anonymous or Access.StrictEndsSession ? RateLimitPolicies.AuthStrict : null,
            Metadata = MetadataFor(entry.Access),
        }),
    ];

    private static Dictionary<string, string>? MetadataFor(Access access) => access switch
    {
        Access.Anonymous => new Dictionary<string, string>(StringComparer.Ordinal) { [AnonymousMetadata] = "true" },
        Access.StrictEndsSession => new Dictionary<string, string>(StringComparer.Ordinal) { [EndsSessionMetadata] = "true" },
        _ => null,
    };
}