namespace DemoApi.Infrastructure.Identity;

/// <summary>
/// FR-021: creates the pre-confirmed Admin from configuration only if no active Admin exists. Runs as
/// <c>system</c>, so the audit rows say so. Missing config with no Admin stops startup.
/// </summary>
internal sealed class SeedAdminHostedService(IServiceProvider services, IConfiguration configuration, ILogger<SeedAdminHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("SeedAdmin:Enabled", true))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasAdmin = await db.UserRoles.AnyAsync(
            ur => ur.RoleId == AppRole.AdminId && db.Users.Any(u => u.Id == ur.UserId),
            cancellationToken);
        if (hasAdmin)
        {
            return;
        }

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("No Admin exists and SeedAdmin:Email / SeedAdmin:Password are not configured.");
        }

        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var created = await identity.CreateUserAsync(new NewUser(email, password, "Seed", "Admin", Roles.Admin, EmailConfirmed: true), cancellationToken);
        if (created.IsFailure && created.Error!.Code == CustomerErrors.DuplicateEmail.Code)
        {
            logger.LogInformation("Seed Admin already created by another instance");
            return;
        }

        if (created.IsFailure)
        {
            throw new InvalidOperationException($"Seeding the Admin failed: {created.Error!.Code}.");
        }

        logger.LogInformation("Seed Admin created");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}