namespace DemoApi.IntegrationTests.Infrastructure;

/// <summary>
/// Base class: Respawn reset once per test class (test-strategy §3), the seeded Admin, helpers.
/// Inside a class, tests use unique data and do not depend on order.
/// </summary>
[Trait("Category", "Docker")]
public abstract class IntegrationTest(IntegrationFixture fixture) : IAsyncLifetime
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Type? _lastResetFor;

    protected IntegrationFixture Fixture { get; } = fixture;

    protected ApiFactory Factory => Fixture.Factory;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (_lastResetFor != GetType())
            {
                await Fixture.ResetAsync();
                _lastResetFor = GetType();
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected Task<TestUser> AdminAsync(string? password = null) => TestData.AdminAsync(Factory, password);

    protected Task<TestUser> CustomerAsync(string? password = null) => TestData.CustomerAsync(Factory, password);

    protected HttpClient Anonymous() => Factory.CreateApiClient();
}