namespace DemoApi.IntegrationTests.Security;

/// <summary>Least privilege for the API role (data-model §5, ADR-0006, D11). Never quarantine.</summary>
[Security("ST-12")]
public sealed class DatabaseGrantsTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task App_role_has_exactly_the_expected_grants()
    {
        var tables = await Db.QueryAsync<Grant>(
            Fixture,
            """
            SELECT table_name AS Object, string_agg(privilege_type, ',' ORDER BY privilege_type) AS Privileges
            FROM information_schema.role_table_grants
            WHERE grantee = 'demoapi_app' AND table_schema = 'public'
            GROUP BY table_name
            """);
        var routines = await Db.QueryAsync<Grant>(
            Fixture,
            """
            SELECT p.proname || '()' AS Object, 'EXECUTE' AS Privileges
            FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND has_function_privilege('demoapi_app', p.oid, 'EXECUTE')
            """);
        var actual = tables.Concat(routines).Select(g => $"{g.Object}: {g.Privileges}").Order(StringComparer.Ordinal).ToArray();

        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Security", "expected-grants.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task App_role_has_no_dangerous_attributes_and_owns_nothing()
    {
        var role = (await Db.QueryAsync<RoleRow>(
            Fixture,
            "SELECT rolsuper AS Super, rolcreatedb AS CreateDb, rolcreaterole AS CreateRole, rolbypassrls AS BypassRls FROM pg_roles WHERE rolname = 'demoapi_app'")).ShouldHaveSingleItem();
        role.ShouldBe(new RoleRow(false, false, false, false));
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM pg_class c JOIN pg_roles r ON r.oid = c.relowner WHERE r.rolname = 'demoapi_app'")).ShouldBe(0);
        (await Db.ScalarAsync<string>(Fixture, "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = current_database()")).ShouldBe("demoapi_migrator");
    }

    [Theory]
    [InlineData("DELETE FROM products")]
    [InlineData("DELETE FROM customers")]
    [InlineData("DELETE FROM audit_logs")]
    [InlineData("UPDATE audit_logs SET action = 'x'")]
    [InlineData("TRUNCATE products")]
    [InlineData("CREATE TABLE evil (id int)")]
    [InlineData("SELECT * FROM \"__EFMigrationsHistory\"")]
    [InlineData("DROP TABLE products")]
    public async Task App_role_cannot_delete_rewrite_audit_or_run_ddl(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.AppConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);

        var error = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));

        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege, error.MessageText);
    }

    [Fact]
    public async Task App_role_can_remove_a_role_row_the_only_delete()
    {
        await using var connection = new NpgsqlConnection(Fixture.AppConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("DELETE FROM asp_net_user_roles WHERE user_id = '00000000-0000-0000-0000-000000000000'", connection);

        (await command.ExecuteNonQueryAsync(Ct)).ShouldBe(0);
    }

    private sealed record Grant(string Object, string Privileges);

    private sealed record RoleRow(bool Super, bool CreateDb, bool CreateRole, bool BypassRls);
}