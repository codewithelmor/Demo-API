namespace DemoApi.IntegrationTests.Infrastructure;

/// <summary>Captures the links the fake sender would log, so tests can confirm and reset (P-032).</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<SentEmail> Sent { get; } = new();

    public Task SendConfirmationAsync(string email, Guid userId, string token, CancellationToken ct)
    {
        Sent.Enqueue(new SentEmail("confirm", email, userId, token));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct)
    {
        Sent.Enqueue(new SentEmail("reset", email, null, token));
        return Task.CompletedTask;
    }

    public Task SendAccountExistsAsync(string email, CancellationToken ct)
    {
        Sent.Enqueue(new SentEmail("exists", email, null, null));
        return Task.CompletedTask;
    }

    public SentEmail Last(string kind, string email) =>
        Sent.Last(e => e.Kind == kind && string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase));
}

public sealed record SentEmail(string Kind, string Email, Guid? UserId, string? Token);

/// <summary>Business clock (IDateTimeProvider) backed by a FakeTimeProvider; the framework TimeProvider stays real.</summary>
public sealed class FakeDateTimeProvider(FakeTimeProvider time) : IDateTimeProvider
{
    public DateTimeOffset UtcNow => time.GetUtcNow();
}

/// <summary>
/// The API in-process: environment Testing, app role connection, high rate limits, no response floors,
/// fast password hashing, captured emails. Optional settings and a fake business clock per factory.
/// </summary>
public sealed class ApiFactory(IntegrationFixture fixture, IReadOnlyDictionary<string, string?>? overrides = null, FakeTimeProvider? clock = null)
    : WebApplicationFactory<Program>
{
    public static readonly string SigningKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    public CapturingEmailSender Email { get; } = new();

    public FakeTimeProvider? Clock => clock;

    public HttpClient CreateApiClient(string? accessToken = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Postgres"] = fixture.AppConnectionString,
            ["ConnectionStrings:Valkey"] = fixture.ValkeyConnectionString,
            ["Jwt:SigningKey"] = SigningKey,
            ["SeedAdmin:Enabled"] = "true",
            ["SeedAdmin:Email"] = IntegrationFixture.SeedAdminEmail,
            ["SeedAdmin:Password"] = IntegrationFixture.SeedAdminPassword,
            ["RateLimiting:AuthStrict:PermitLimit"] = "100000",
            ["RateLimiting:Refresh:PermitLimit"] = "100000",
            ["RateLimiting:General:PermitLimit"] = "100000",
            ["RateLimiting:General:AnonymousPermitLimit"] = "100000",
            ["RateLimiting:ReportsConcurrency"] = "100",
            ["App:RegisterResponseFloor"] = "00:00:00",
            ["App:ForgotResponseFloor"] = "00:00:00",
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        // Quiet by default; set DEMOAPI_TEST_LOGS=1 to see server errors on the console when a test fails with 500.
        builder.ConfigureLogging(l =>
        {
            l.ClearProviders().SetMinimumLevel(LogLevel.Warning);
            if (Environment.GetEnvironmentVariable("DEMOAPI_TEST_LOGS") == "1")
            {
                l.AddSimpleConsole();
            }
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            services.Configure<PasswordHasherOptions>(o => o.IterationCount = 1_000);
            if (clock is not null)
            {
                services.RemoveAll<IDateTimeProvider>();
                services.AddSingleton<IDateTimeProvider>(new FakeDateTimeProvider(clock));
            }
        });
    }
}