namespace DemoApi.Bff.Configuration;

internal sealed class BffOptionsValidator : IValidateOptions<BffOptions>
{
    public ValidateOptionsResult Validate(string? name, BffOptions options)
    {
        List<string> failures = [];

        if (!options.ApiBaseUrl.IsAbsoluteUri
            || (options.ApiBaseUrl.Scheme != Uri.UriSchemeHttp && options.ApiBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("Bff:ApiBaseUrl must be an absolute http or https URL.");
        }

        if (options.AllowedOrigins.Length == 0)
        {
            failures.Add("Bff:AllowedOrigins must contain at least one origin.");
        }

        for (int i = 0; i < options.AllowedOrigins.Length; i++)
        {
            if (!OriginPolicy.TryNormalize(options.AllowedOrigins[i], out _))
            {
                failures.Add($"Bff:AllowedOrigins:{i.ToString(CultureInfo.InvariantCulture)} must be an exact origin such as http://localhost:8081 (no wildcard, no null, no path).");
            }
        }

        if (options.AuthStrictPermitLimit < 1 || options.RefreshPermitLimit < 1 || options.AnonymousPermitLimit < 1 || options.RateLimitWindowSeconds < 1)
        {
            failures.Add("Bff rate limit values must be at least 1.");
        }

        if (options.ReadyPermitLimit < 1 || options.ReadyCacheSeconds < 0)
        {
            failures.Add("Bff ready limit must be at least 1 and the cache time not negative.");
        }

        if (options.AdminIdleMinutes < 1 || options.AdminAbsoluteHours < 1)
        {
            failures.Add("Bff admin session limits must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

internal sealed class ValkeyOptionsValidator(IHostEnvironment environment) : IValidateOptions<ValkeyOptions>
{
    public ValidateOptionsResult Validate(string? name, ValkeyOptions options)
    {
        // Sessions fail closed: no store, no sign-in. Process memory needs an explicit flag (default: Development only).
        return string.IsNullOrWhiteSpace(options.ConnectionString) && !(options.AllowInMemory ?? environment.IsDevelopment())
            ? ValidateOptionsResult.Fail("Valkey:ConnectionString is required. Set Valkey:AllowInMemory=true only for a single local instance.")
            : ValidateOptionsResult.Success;
    }
}

internal sealed class KeyStoreOptionsValidator(IHostEnvironment environment) : IValidateOptions<KeyStoreOptions>
{
    public ValidateOptionsResult Validate(string? name, KeyStoreOptions options)
    {
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.KeysPath))
        {
            return ValidateOptionsResult.Success;
        }

        // An ephemeral key ring would end every session at each restart (T-52), so outside Development the
        // key folder must exist and be writable.
        if (string.IsNullOrWhiteSpace(options.KeysPath))
        {
            return ValidateOptionsResult.Fail("DataProtection:KeysPath is required outside Development.");
        }

        if (!Directory.Exists(options.KeysPath))
        {
            return ValidateOptionsResult.Fail("DataProtection:KeysPath must be an existing directory.");
        }

        string probe = Path.Combine(options.KeysPath, ".write-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ValidateOptionsResult.Fail("DataProtection:KeysPath must be writable.");
        }
    }
}