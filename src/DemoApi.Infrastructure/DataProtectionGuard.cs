namespace DemoApi.Infrastructure;

/// <summary>
/// Fails startup outside Development or Testing when no Data Protection key ring path is configured: with ephemeral keys,
/// every restart invalidates confirm and reset links (ST-17). Protect the key ring at rest (for example Key Vault) before production.
/// </summary>
internal sealed class DataProtectionGuard(IConfiguration configuration, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!(environment.IsDevelopment() || environment.IsEnvironment("Testing")) && string.IsNullOrWhiteSpace(configuration["DataProtection:KeysPath"]))
        {
            throw new InvalidOperationException(
                $"DataProtection:KeysPath is required in the '{environment.EnvironmentName}' environment so account links survive a restart.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}