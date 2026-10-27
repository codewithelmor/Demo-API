[assembly: AssemblyFixture(typeof(DemoApi.IntegrationTests.Infrastructure.IntegrationFixture))]

// Tests share one database and Respawn resets it between classes, so classes must not overlap.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace DemoApi.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL 18.6 and one Valkey 9.1.2 container per run (test-strategy §3). The compose role script
/// creates <c>demoapi_migrator</c> and <c>demoapi_app</c>; migrations run as the migrator; the API connects as
/// the app role, so a hidden DELETE fails as in production.
/// </summary>
public sealed class IntegrationFixture : IAsyncLifetime
{
    public const string Database = "demoapi";
    public const string SeedAdminEmail = "seed-admin@example.com";
    public const string SeedAdminPassword = "seed admin passphrase for tests";

    private static readonly string MigratorPassword = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + "aA1";
    private static readonly string AppPassword = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + "bB2";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-trixie")
        .WithEnvironment("DEMOAPI_MIGRATOR_PASSWORD", MigratorPassword)
        .WithEnvironment("DEMOAPI_APP_PASSWORD", AppPassword)
        .WithEnvironment("DEMOAPI_DB", Database)
        .WithResourceMapping(RolesScript(), "/docker-entrypoint-initdb.d/01-roles.sh", fileMode: Unix.FileMode755)
        .Build();

    private readonly IContainer _valkey = new ContainerBuilder("valkey/valkey:9.1.2-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

    private Respawner? _respawner;

    public string MigratorConnectionString { get; private set; } = string.Empty;

    public string AppConnectionString { get; private set; } = string.Empty;

    public string ValkeyConnectionString { get; private set; } = string.Empty;

    public ApiFactory Factory { get; private set; } = null!;

    public IContainer Valkey => _valkey;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _valkey.StartAsync());

        var builder = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = Database };
        builder.Username = "demoapi_migrator";
        builder.Password = MigratorPassword;
        MigratorConnectionString = builder.ConnectionString;
        builder.Username = "demoapi_app";
        builder.Password = AppPassword;
        AppConnectionString = builder.ConnectionString;
        ValkeyConnectionString = $"{_valkey.Hostname}:{_valkey.GetMappedPublicPort(6379)},abortConnect=false";

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(MigratorConnectionString).UseSnakeCaseNamingConvention().Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using (var connection = new NpgsqlConnection(MigratorConnectionString))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory"), new Table("public", "asp_net_roles")],
            });
        }

        Factory = new ApiFactory(this);
        _ = Factory.Server; // starts the host (seeds the Admin)
    }

    /// <summary>Deletes all data as the migrator (the app role cannot DELETE), then re-seeds the Admin.</summary>
    public async Task ResetAsync()
    {
        await using (var connection = new NpgsqlConnection(MigratorConnectionString))
        {
            await connection.OpenAsync();
            await _respawner!.ResetAsync(connection);
        }

        await TestData.SeedAdminAsync(Factory);
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _valkey.DisposeAsync();
    }

    /// <summary>The compose role script, with LF line endings (it runs in the container).</summary>
    private static byte[] RolesScript()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "01-roles.sh");
        return Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}