namespace DemoApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Dapper reads snake_case columns into PascalCase members (ADR-0011).
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. Run the stack with 'docker compose up -d', or for 'dotnet run' execute deploy/scripts/Initialize-DotnetRun.ps1 first.");
        }

        services.AddSingleton(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            if (!connectionString.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                builder.ConnectionStringBuilder.Timeout = 2; // fail fast when the database is down; an explicit Timeout wins
            }

            return builder.Build();
        });
        services.AddDbContext<AppDbContext>((sp, o) => o
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql =>
            {
                // The only PostgreSQL retry layer (ADR-0011).
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromMilliseconds(250), null);
                npgsql.CommandTimeout(5);
            })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>(), sp.GetRequiredService<PostgresCircuitBreakerInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddTransactionalPersistence<AppDbContext>();

        services.AddMemoryCache();
        var keysPath = configuration["DataProtection:KeysPath"];
        var dataProtection = services.AddDataProtection().SetApplicationName("demoapi");
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 15;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 1;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.SignIn.RequireConfirmedEmail = false;
                o.Tokens.EmailConfirmationTokenProvider = EmailConfirmationTokenProvider.ProviderName;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<EmailConfirmationTokenProvider>(EmailConfirmationTokenProvider.ProviderName)
            .AddPasswordValidator<PasswordPolicyValidator>();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
        services.Configure<EmailConfirmationTokenProviderOptions>(o =>
        {
            o.TokenLifespan = TimeSpan.FromHours(24);
            o.Name = EmailConfirmationTokenProvider.ProviderName;
        });
        services.Configure<PasswordHasherOptions>(o => o.IterationCount = 220_000);

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserSessionValidator, UserSessionValidator>();
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.Section);
        services.AddSingleton<IEmailSender, FakeEmailSender>();
        services.AddHostedService<EmailSenderGuard>();
        services.AddHostedService<DataProtectionGuard>();
        services.AddHostedService<SeedAdminHostedService>();
        return services;
    }
}

/// <summary>Used by <c>dotnet ef</c> and the migration bundle (runs as the migrator role).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresMigrator")
            ?? "Host=localhost;Database=demoapi;Username=demoapi_migrator";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}

public static class SqlResource
{
    public static string Read(string relativePath)
    {
        var name = "DemoApi.Infrastructure.Persistence.Sql." + relativePath.Replace('/', '.');
        using var stream = typeof(SqlResource).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"SQL resource {name} not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}