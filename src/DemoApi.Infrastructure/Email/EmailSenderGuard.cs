namespace DemoApi.Infrastructure.Email;

/// <summary>
/// Fails startup when the fake sender (which logs full confirm and reset links) would run outside Development or Testing.
/// Replace <see cref="IEmailSender"/> with a real sender before deploying anywhere else.
/// </summary>
internal sealed class EmailSenderGuard(IEmailSender sender, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (sender is FakeEmailSender && !(environment.IsDevelopment() || environment.IsEnvironment("Testing")))
        {
            throw new InvalidOperationException(
                $"FakeEmailSender logs account links and must not run in the '{environment.EnvironmentName}' environment. Register a real IEmailSender.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}