namespace DemoApi.BuildingBlocks.Security;

/// <summary>JWT settings (ADR-0008). Keys come from secrets only.</summary>
public sealed class JwtOptions : IValidatableObject
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "demoapi";

    [Required]
    public string Audience { get; set; } = "demoapi";

    /// <summary>Base64, at least 32 bytes of random data.</summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    public string KeyId { get; set; } = "k1";

    public string? PreviousSigningKey { get; set; }

    public string? PreviousKeyId { get; set; }

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 30)]
    public int RefreshTokenDays { get; set; } = 7;

    [Range(1, 90)]
    public int RefreshFamilyDays { get; set; } = 30;

    public IReadOnlyList<SecurityKey> ValidationKeys()
    {
        var keys = new List<SecurityKey> { CreateKey(SigningKey, KeyId) };
        if (!string.IsNullOrWhiteSpace(PreviousSigningKey))
        {
            keys.Add(CreateKey(PreviousSigningKey, PreviousKeyId ?? "previous"));
        }

        return keys;
    }

    public SymmetricSecurityKey CurrentKey() => CreateKey(SigningKey, KeyId);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (key, name) in new[] { (SigningKey, nameof(SigningKey)), (PreviousSigningKey, nameof(PreviousSigningKey)) })
        {
            if (key is null || (name == nameof(PreviousSigningKey) && key.Length == 0))
            {
                continue;
            }

            byte[]? bytes = null;
            try
            {
                bytes = Convert.FromBase64String(key);
            }
            catch (FormatException)
            {
            }

            if (bytes is null || bytes.Length < 32)
            {
                yield return new ValidationResult($"Jwt:{name} must be base64 with at least 256 bits.", [name]);
            }
        }
    }

    private static SymmetricSecurityKey CreateKey(string base64, string keyId) =>
        new(Convert.FromBase64String(base64)) { KeyId = keyId };
}