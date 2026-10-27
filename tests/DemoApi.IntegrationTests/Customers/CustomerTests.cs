namespace DemoApi.IntegrationTests.Customers;

public sealed class CustomerTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    private CapturingEmailSender Emails => (CapturingEmailSender)Factory.Services.GetRequiredService<DemoApi.Application.Common.IEmailSender>();

    [Fact]
    [Endpoint("EP-29")]
    [Audit("EP-29")]
    [Story("US-301", "AC1")]
    public async Task Admin_create_returns_201_and_audits_personal_data_as_changed()
    {
        var admin = await AdminAsync();
        var email = TestData.UniqueEmail("new");

        var response = await admin.Client.PostAsJsonAsync("/api/v1/customers", new { email, firstName = "Alan", lastName = "Turing", phone = "+441234567890" }, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var id = (await response.JsonAsync()).Id();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/customers/{id}");
        response.ETag().ShouldNotBeNullOrWhiteSpace();
        Emails.Last("reset", email).ShouldNotBeNull("the reset link is logged after commit");

        var audit = (await Db.AuditAsync(Fixture, "Customer", id)).ShouldHaveSingleItem();
        audit.Action.ShouldBe("Insert");
        audit.ChangesJson["firstName"]!.GetValue<string>().ShouldBe("changed");
        audit.ChangesJson["lastName"]!.GetValue<string>().ShouldBe("changed");
        audit.Changes.ShouldNotContain("Turing");
        audit.Changes.ShouldNotContain("+441234567890");
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM audit_logs WHERE changes::text LIKE @e", new { e = "%" + email + "%" })).ShouldBe(0, "no email value anywhere in the audit log");
    }

    [Fact]
    [Endpoint("EP-29")]
    public async Task Admin_create_with_existing_email_returns_409()
    {
        var admin = await AdminAsync();
        var existing = await CustomerAsync();

        await (await admin.Client.PostAsJsonAsync("/api/v1/customers", new { email = existing.Email, firstName = "A", lastName = "B" }, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "duplicate-email");
    }

    [Fact]
    [Endpoint("EP-32")]
    [Audit("EP-32")]
    public async Task Admin_update_needs_if_match_and_audits_pii_as_changed()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var id = customer.CustomerId!.Value;
        var etag = (await admin.Client.GetAsync($"/api/v1/customers/{id}", Ct)).ETag();
        var body = new { firstName = "Renamed", lastName = "Person", phone = (string?)null };

        await (await admin.Client.PutAsJsonAsync($"/api/v1/customers/{id}", body, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{id}") { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        var response = await admin.Client.SendAsync(request, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await Db.ScalarAsync<string>(Fixture, "SELECT first_name FROM customers WHERE id = @id", new { id })).ShouldBe("Renamed", "the change is persisted");
        response.ETag().ShouldNotBe(etag);

        var audit = (await Db.AuditAsync(Fixture, "Customer", id)).Last();
        audit.Action.ShouldBe("Update");
        audit.UserId.ShouldBe(admin.UserId.ToString());
        audit.ChangesJson["firstName"]!.GetValue<string>().ShouldBe("changed");
        audit.Changes.ShouldNotContain("Renamed");
    }

    [Fact]
    [Endpoint("EP-36")]
    [Security("ST-02")]
    public async Task Me_profile_is_the_callers_own_and_update_needs_if_match()
    {
        var customer = await CustomerAsync();
        var other = await CustomerAsync();

        var me = await customer.Client.GetAsync($"/api/v1/customers/me?customerId={other.CustomerId}", Ct);
        await me.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await me.JsonAsync())["email"]!.GetValue<string>().ShouldBe(customer.Email, StringCompareShould.IgnoreCase);
        var etag = me.ETag();

        await (await customer.Client.PutAsJsonAsync("/api/v1/customers/me", new { firstName = "A", lastName = "B" }, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired);
        using var stale = new HttpRequestMessage(HttpMethod.Put, "/api/v1/customers/me") { Content = JsonContent.Create(new { firstName = "A", lastName = "B" }) };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        await (await customer.Client.SendAsync(stale, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed);
        using var ok = new HttpRequestMessage(HttpMethod.Put, "/api/v1/customers/me") { Content = JsonContent.Create(new { firstName = "A", lastName = "B" }) };
        ok.Headers.TryAddWithoutValidation("If-Match", etag);
        var updated = await customer.Client.SendAsync(ok, Ct);
        await updated.ShouldHaveStatusAsync(HttpStatusCode.OK);
        updated.ETag().ShouldNotBe(etag);
        (await Db.ScalarAsync<string>(Fixture, "SELECT last_name FROM customers WHERE id = @id", new { id = customer.CustomerId })).ShouldBe("B", "the change is persisted");
        (await Db.AuditAsync(Fixture, "Customer", customer.CustomerId!.Value)).Last().Action.ShouldBe("Update");

        (await (await other.Client.GetAsync("/api/v1/customers/me", Ct)).JsonAsync())["firstName"]!.GetValue<string>().ShouldBe("Ada", "the other customer is untouched");
    }

    [Fact]
    [Endpoint("EP-33")]
    [Audit("EP-33")]
    [Story("US-305", "AC1")]
    public async Task Admin_delete_soft_deletes_anonymizes_locks_and_revokes_but_keeps_rows()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var id = customer.CustomerId!.Value;

        (await admin.Client.DeleteAsync($"/api/v1/customers/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Db.ScalarAsync<bool>(Fixture, "SELECT is_deleted FROM customers WHERE id = @id", new { id })).ShouldBeTrue();
        (await Db.ScalarAsync<string>(Fixture, "SELECT first_name FROM customers WHERE id = @id", new { id })).ShouldNotBe("Ada");
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM asp_net_users WHERE id = @u", new { u = customer.UserId })).ShouldBe(1, "rows are never deleted");
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM asp_net_users WHERE lower(email) = lower(@e)", new { e = customer.Email })).ShouldBe(0, "email anonymized");
        await (await admin.Client.GetAsync($"/api/v1/customers/{id}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        await (await customer.Client.GetAsync("/api/v1/customers/me", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        await (await Anonymous().PostAsJsonAsync("/api/v1/auth/refresh", Body.Of(("refreshToken", customer.Tokens.RefreshToken)), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        (await Db.AuditAsync(Fixture, "Customer", id)).Select(a => a.Action).ShouldContain("SoftDelete");
        await (await admin.Client.GetAsync($"/api/v1/customers/{id}/order-history", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    [Endpoint("EP-33")]
    public async Task Delete_with_an_open_order_returns_409()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();
        await TestData.MyOrderAsync(customer.Client, (product, 1));

        await (await admin.Client.DeleteAsync($"/api/v1/customers/{customer.CustomerId}", Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    [Endpoint("EP-37")]
    [Story("US-306", "AC1")]
    public async Task Erase_needs_the_current_secret_then_anonymizes_and_ends_the_session()
    {
        var customer = await CustomerAsync(TestData.Password);

        await (await customer.Client.PostAsJsonAsync("/api/v1/customers/me/erase", Body.Of(("currentPassword", "not the right one")), Ct)).ShouldBeValidationProblemAsync("currentPassword");

        (await customer.Client.PostAsJsonAsync("/api/v1/customers/me/erase", Body.Of(("currentPassword", TestData.Password)), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Db.ScalarAsync<bool>(Fixture, "SELECT is_deleted FROM customers WHERE id = @id", new { id = customer.CustomerId })).ShouldBeTrue();
        (await Db.ScalarAsync<string?>(Fixture, "SELECT password_hash FROM asp_net_users WHERE id = @u", new { u = customer.UserId })).ShouldBeNull();
        await (await Anonymous().PostAsJsonAsync("/api/v1/auth/login", Body.Of(("email", customer.Email), ("password", TestData.Password)), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        await (await customer.Client.GetAsync("/api/v1/customers/me", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Endpoint("EP-30")]
    public async Task Admin_list_searches_and_hides_deleted_unless_asked()
    {
        var admin = await AdminAsync();
        var keep = await TestData.CustomerAsync(Factory, lastName: "Searchable" + Guid.NewGuid().ToString("N")[..6]);
        var gone = await TestData.CustomerAsync(Factory);
        (await admin.Client.DeleteAsync($"/api/v1/customers/{gone.CustomerId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var all = await (await admin.Client.GetAsync("/api/v1/customers?pageSize=100", Ct)).JsonAsync();
        var ids = all["items"]!.AsArray().Select(i => i!.Id()).ToList();
        ids.ShouldContain(keep.CustomerId!.Value);
        ids.ShouldNotContain(gone.CustomerId!.Value);

        var withDeleted = await (await admin.Client.GetAsync("/api/v1/customers?pageSize=100&includeDeleted=true", Ct)).JsonAsync();
        withDeleted["items"]!.AsArray().Select(i => i!.Id()).ShouldContain(gone.CustomerId!.Value);
    }
}