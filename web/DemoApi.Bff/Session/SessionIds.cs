namespace DemoApi.Bff.Session;

internal static class SessionIds
{
    private const int ByteLength = 32; // 256 bits from the CSPRNG (NFR-F30 asks for at least 128)

    public const int EncodedLength = 43;

    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));

    /// <summary>Checks the shape before a cookie value is used in a cache key.</summary>
    public static bool IsValid(string? id)
    {
        if (id is null || id.Length != EncodedLength)
        {
            return false;
        }

        foreach (char c in id)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}