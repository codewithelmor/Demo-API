namespace DemoApi.IntegrationTests.Infrastructure;

/// <summary>A signed-in test user with a client that sends its access token.</summary>
public sealed record TestUser(Guid UserId, Guid? CustomerId, string Email, string? Password, TokenResponse Tokens, HttpClient Client);

/// <summary>Builders for users and data. Users are created through Identity (audited as system); tokens come from the real issuer.</summary>
public static class TestData
{
    public const string Password = "correct horse battery staple x";

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    public static async Task SeedAdminAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var created = await identity.CreateUserAsync(
            new NewUser(IntegrationFixture.SeedAdminEmail, IntegrationFixture.SeedAdminPassword, "Seed", "Admin", "Admin", true), CancellationToken.None);
        created.IsSuccess.ShouldBeTrue();
    }

    /// <summary>A confirmed customer (user + Customer role + customer record). No password unless asked (fast).</summary>
    public static async Task<TestUser> CustomerAsync(ApiFactory factory, string? password = null, string firstName = "Ada", string lastName = "Lovelace")
    {
        var email = UniqueEmail("customer");
        Guid userId;
        Guid customerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = (await identity.CreateUserAsync(new NewUser(email, password, null, null, "Customer", true), CancellationToken.None)).Value;
            var customer = Customer.Create(userId, firstName, lastName, null);
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            customerId = customer.Id;
        }

        var tokens = await StartSessionAsync(factory, userId);
        return new TestUser(userId, customerId, email, password, tokens, factory.CreateApiClient(tokens.AccessToken));
    }

    public static async Task<TestUser> AdminAsync(ApiFactory factory, string? password = null)
    {
        var email = UniqueEmail("admin");
        Guid userId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            userId = (await identity.CreateUserAsync(new NewUser(email, password, "Test", "Admin", "Admin", true), CancellationToken.None)).Value;
        }

        var tokens = await StartSessionAsync(factory, userId);
        return new TestUser(userId, null, email, password, tokens, factory.CreateApiClient(tokens.AccessToken));
    }

    public static async Task<TokenResponse> StartSessionAsync(ApiFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITokenService>().StartSessionAsync(userId, CancellationToken.None);
    }

    public static async Task<JsonObject> ProductAsync(HttpClient admin, int stock = 10, decimal price = 9.99m, string? sku = null)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Product " + Guid.NewGuid().ToString("N")[..8],
            sku = sku ?? "T-" + Guid.NewGuid().ToString("N")[..12],
            description = "Test product",
            price,
            stockQuantity = stock,
        });
        await response.ShouldHaveStatusAsync(System.Net.HttpStatusCode.Created);
        return (await response.JsonAsync()).AsObject();
    }

    public static async Task<JsonObject> AdminOrderAsync(HttpClient admin, Guid customerId, params (Guid ProductId, int Quantity)[] items)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/orders", new
        {
            customerId,
            items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity }),
        });
        await response.ShouldHaveStatusAsync(System.Net.HttpStatusCode.Created);
        return (await response.JsonAsync()).AsObject();
    }

    public static async Task<JsonObject> MyOrderAsync(HttpClient customer, params (Guid ProductId, int Quantity)[] items)
    {
        var response = await customer.PostAsJsonAsync("/api/v1/customers/me/orders", new
        {
            items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity }),
        });
        await response.ShouldHaveStatusAsync(System.Net.HttpStatusCode.Created);
        return (await response.JsonAsync()).AsObject();
    }

    public static Guid Id(this JsonNode node, string property = "id") => Guid.Parse(node[property]!.GetValue<string>());
}

/// <summary>Reads the database as the migrator (sees soft-deleted rows; bypasses EF filters).</summary>
public static class Db
{
    public static async Task<List<T>> QueryAsync<T>(IntegrationFixture fixture, string sql, object? args = null)
    {
        await using var connection = new NpgsqlConnection(fixture.MigratorConnectionString);
        return (await connection.QueryAsync<T>(sql, args)).ToList();
    }

    public static async Task<T> ScalarAsync<T>(IntegrationFixture fixture, string sql, object? args = null)
    {
        await using var connection = new NpgsqlConnection(fixture.MigratorConnectionString);
        return (await connection.ExecuteScalarAsync<T>(sql, args))!;
    }

    public static Task<List<AuditRow>> AuditAsync(IntegrationFixture fixture, string entityType, Guid entityId) =>
        QueryAsync<AuditRow>(
            fixture,
            "SELECT entity_type AS EntityType, entity_id AS EntityId, action AS Action, user_id AS UserId, changes::text AS Changes, trace_id AS TraceId " +
            "FROM audit_logs WHERE entity_type = @entityType AND entity_id = @entityId ORDER BY occurred_at, id",
            new { entityType, entityId = entityId.ToString() });
}

public sealed record AuditRow(string EntityType, string EntityId, string Action, string UserId, string Changes, string? TraceId)
{
    public JsonObject ChangesJson => JsonNode.Parse(Changes)!.AsObject();
}