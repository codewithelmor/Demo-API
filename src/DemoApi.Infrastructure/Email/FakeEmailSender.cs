namespace DemoApi.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>Links are built only from configuration, never from the Host header (S-D3).</summary>
    public string LinkBaseUrl { get; set; } = "http://localhost:8080";
}

/// <summary>Logs the link instead of sending mail (assumption: no real email this phase). Emails are masked.</summary>
internal sealed class FakeEmailSender(IOptions<EmailOptions> options, ILogger<FakeEmailSender> logger) : IEmailSender
{
    public Task SendConfirmationAsync(string email, Guid userId, string token, CancellationToken ct)
    {
        var link = $"{options.Value.LinkBaseUrl.TrimEnd('/')}/confirm-email?userId={userId}&token={Uri.EscapeDataString(token)}";
        logger.LogInformation("[FakeEmail] Confirm email for {Email}: {Link}", Mask(email), link);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct)
    {
        var link = $"{options.Value.LinkBaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        logger.LogInformation("[FakeEmail] Password reset for {Email}: {Link}", Mask(email), link);
        return Task.CompletedTask;
    }

    public Task SendAccountExistsAsync(string email, CancellationToken ct)
    {
        logger.LogInformation("[FakeEmail] Account already exists notice for {Email}", Mask(email));
        return Task.CompletedTask;
    }

    public static string Mask(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}