namespace DemoApi.Bff.Security;

internal static class ClientIp
{
    /// <summary>
    /// Rate-limit key: the connection address only. Forwarded headers are never read (the BFF is the edge, T-31).
    /// IPv6 addresses are keyed by their /64 prefix (ADR-0010).
    /// </summary>
    public static string Key(HttpContext context)
    {
        IPAddress? address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        _ = address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();
        return new IPAddress(bytes).ToString();
    }
}