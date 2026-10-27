namespace DemoApi.IntegrationTests.Products;

public sealed class ProductTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    [Endpoint("EP-24")]
    [Audit("EP-24")]
    [Story("US-102", "AC1")]
    public async Task Create_returns_201_location_etag_uppercase_sku_and_audit_row()
    {
        var admin = await AdminAsync();
        var sku = "t-" + Guid.NewGuid().ToString("N")[..10];

        var response = await admin.Client.PostAsJsonAsync("/api/v1/products", new { name = " Mug ", sku, price = 12.50m, stockQuantity = 3 }, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var body = await response.JsonAsync();
        var id = body.Id();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/products/{id}");
        response.ETag().ShouldStartWith("\"");
        body["sku"]!.GetValue<string>().ShouldBe(sku.ToUpperInvariant());
        body["name"]!.GetValue<string>().ShouldBe("Mug");
        body["price"]!.GetValue<decimal>().ShouldBe(12.50m);
        body["createdAt"]!.GetValue<string>().ShouldEndWith("Z");
        body["isDeleted"]!.GetValue<bool>().ShouldBeFalse();

        var audit = (await Db.AuditAsync(Fixture, "Product", id)).ShouldHaveSingleItem();
        audit.Action.ShouldBe("Insert");
        audit.UserId.ShouldBe(admin.UserId.ToString());
        audit.ChangesJson["price"]!["new"]!.GetValue<string>().ShouldBe("12.50");
    }

    [Fact]
    [Endpoint("EP-24")]
    [Story("US-102", "AC2")]
    public async Task Create_with_invalid_body_returns_400_with_field_errors()
    {
        var admin = await AdminAsync();

        var response = await admin.Client.PostAsJsonAsync("/api/v1/products", new { name = "", sku = "bad sku", price = 1.234m, stockQuantity = -1 }, Ct);

        await response.ShouldBeValidationProblemAsync("name", "sku", "price", "stockQuantity");
    }

    [Fact]
    [Endpoint("EP-24")]
    public async Task Malformed_json_returns_400_without_parser_text()
    {
        var admin = await AdminAsync();

        var response = await admin.Client.PostAsync("/api/v1/products", new StringContent("{ \"name\": ", Encoding.UTF8, "application/json"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation-failed");
        problem.ToJsonString().ShouldNotContain("System.Text.Json");
    }

    [Fact]
    [Endpoint("EP-24")]
    public async Task Non_json_body_returns_415()
    {
        var admin = await AdminAsync();

        var response = await admin.Client.PostAsync("/api/v1/products", new StringContent("name=x", Encoding.UTF8, "text/plain"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "unsupported-media-type");
    }

    [Fact]
    [Endpoint("EP-24")]
    [Story("US-102", "AC6")]
    public async Task Create_with_duplicate_active_sku_returns_409()
    {
        var admin = await AdminAsync();
        var first = await TestData.ProductAsync(admin.Client);

        var response = await admin.Client.PostAsJsonAsync("/api/v1/products", new { name = "Other", sku = first["sku"]!.GetValue<string>().ToLowerInvariant(), price = 1m, stockQuantity = 1 }, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "duplicate-sku");
        problem["sku"]!.GetValue<string>().ShouldBe(first["sku"]!.GetValue<string>());
    }

    [Fact]
    [Endpoint("EP-24")]
    [Story("US-102", "AC7")]
    public async Task Create_reusing_deleted_sku_returns_201()
    {
        var admin = await AdminAsync();
        var first = await TestData.ProductAsync(admin.Client);
        (await admin.Client.DeleteAsync($"/api/v1/products/{first.Id()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await admin.Client.PostAsJsonAsync("/api/v1/products", new { name = "Again", sku = first["sku"]!.GetValue<string>(), price = 1m, stockQuantity = 1 }, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    [Endpoint("EP-23")]
    public async Task Get_returns_product_with_etag_and_private_no_cache()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = await TestData.ProductAsync(admin.Client);

        var response = await customer.Client.GetAsync($"/api/v1/products/{product.Id()}", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.ETag().ShouldNotBeNullOrWhiteSpace();
        response.ShouldHaveProductCacheControl();
        (await response.JsonAsync()).AsObject().ContainsKey("isDeleted").ShouldBeFalse("isDeleted is Admin only (D9)");
    }

    [Fact]
    [Endpoint("EP-23")]
    public async Task Get_unknown_returns_404_and_malformed_id_returns_400()
    {
        var customer = await CustomerAsync();

        await (await customer.Client.GetAsync($"/api/v1/products/{Guid.NewGuid()}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "not-found");
        await (await customer.Client.GetAsync("/api/v1/products/not-a-guid", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation-failed");
    }

    [Fact]
    [Endpoint("EP-25")]
    [Audit("EP-25")]
    [Story("US-103", "AC1")]
    public async Task Update_requires_if_match_428_then_412_then_200_with_new_etag()
    {
        var admin = await AdminAsync();
        var product = await TestData.ProductAsync(admin.Client, price: 5m);
        var id = product.Id();
        var etag = (await admin.Client.GetAsync($"/api/v1/products/{id}", Ct)).ETag();
        var body = new { name = "Renamed", sku = product["sku"]!.GetValue<string>(), price = 6m, stockQuantity = 4 };

        await (await admin.Client.PutAsJsonAsync($"/api/v1/products/{id}", body, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired, "precondition-required");

        using var star = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{id}") { Content = JsonContent.Create(body) };
        star.Headers.TryAddWithoutValidation("If-Match", "*");
        await (await admin.Client.SendAsync(star, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired);

        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{id}") { Content = JsonContent.Create(body) };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        await (await admin.Client.SendAsync(stale, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "precondition-failed");

        using var ok = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{id}") { Content = JsonContent.Create(body) };
        ok.Headers.TryAddWithoutValidation("If-Match", etag);
        var response = await admin.Client.SendAsync(ok, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.ETag().ShouldNotBe(etag);
        (await response.JsonAsync())["name"]!.GetValue<string>().ShouldBe("Renamed");

        var audit = (await Db.AuditAsync(Fixture, "Product", id)).Last();
        audit.Action.ShouldBe("Update");
        audit.ChangesJson["price"]!["old"]!.GetValue<string>().ShouldBe("5.00");
        audit.ChangesJson["price"]!["new"]!.GetValue<string>().ShouldBe("6.00");
        audit.ChangesJson.ContainsKey("sku").ShouldBeFalse("unchanged fields are not audited");
    }

    [Fact]
    [Endpoint("EP-25")]
    public async Task Update_unknown_product_returns_404_before_if_match()
    {
        var admin = await AdminAsync();

        var response = await admin.Client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}", new { name = "x", sku = "X-1", price = 1m, stockQuantity = 1 }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    [Endpoint("EP-26")]
    [Audit("EP-26")]
    [Story("US-104", "AC1")]
    public async Task Delete_soft_deletes_keeps_the_row_and_hides_it()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var id = (await TestData.ProductAsync(admin.Client)).Id();
        (await customer.Client.GetAsync($"/api/v1/products/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK); // warms the cache

        (await admin.Client.DeleteAsync($"/api/v1/products/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Db.ScalarAsync<bool>(Fixture, "SELECT is_deleted FROM products WHERE id = @id", new { id })).ShouldBeTrue("rows are never deleted (D11)");
        (await Db.ScalarAsync<DateTime?>(Fixture, "SELECT deleted_at FROM products WHERE id = @id", new { id })).ShouldNotBeNull();
        await (await customer.Client.GetAsync($"/api/v1/products/{id}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        await (await admin.Client.DeleteAsync($"/api/v1/products/{id}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        var admins = await admin.Client.GetAsync($"/api/v1/products/{id}?includeDeleted=true", Ct);
        await admins.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await admins.JsonAsync())["isDeleted"]!.GetValue<bool>().ShouldBeTrue();
        (await Db.AuditAsync(Fixture, "Product", id)).Last().Action.ShouldBe("SoftDelete");
    }

    [Fact]
    [Endpoint("EP-23")]
    [Security("ST-15")]
    public async Task Customer_include_deleted_is_ignored()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var id = (await TestData.ProductAsync(admin.Client)).Id();
        await admin.Client.DeleteAsync($"/api/v1/products/{id}", Ct);

        await (await customer.Client.GetAsync($"/api/v1/products/{id}?includeDeleted=true", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    [Endpoint("EP-27")]
    [Audit("EP-27")]
    public async Task Restore_returns_204_then_409_not_deleted()
    {
        var admin = await AdminAsync();
        var id = (await TestData.ProductAsync(admin.Client)).Id();
        await admin.Client.DeleteAsync($"/api/v1/products/{id}", Ct);

        (await admin.Client.PostAsync($"/api/v1/products/{id}/restore", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await admin.Client.PostAsync($"/api/v1/products/{id}/restore", null, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "not-deleted");
        (await Db.AuditAsync(Fixture, "Product", id)).Last().Action.ShouldBe("Restore");
        (await Db.ScalarAsync<DateTime?>(Fixture, "SELECT deleted_at FROM products WHERE id = @id", new { id })).ShouldBeNull();
    }

    [Fact]
    [Endpoint("EP-27")]
    public async Task Restore_when_sku_is_taken_returns_409_duplicate_sku()
    {
        var admin = await AdminAsync();
        var first = await TestData.ProductAsync(admin.Client);
        await admin.Client.DeleteAsync($"/api/v1/products/{first.Id()}", Ct);
        await TestData.ProductAsync(admin.Client, sku: first["sku"]!.GetValue<string>());

        await (await admin.Client.PostAsync($"/api/v1/products/{first.Id()}/restore", null, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "duplicate-sku");
    }

    [Fact]
    [Endpoint("EP-22")]
    [Security("ST-15")]
    public async Task List_is_paged_filtered_and_reflects_writes_after_cache_invalidation()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var marker = "zz" + Guid.NewGuid().ToString("N")[..6];
        var first = await TestData.ProductAsync(admin.Client, price: 3m);

        var before = await (await customer.Client.GetAsync("/api/v1/products?pageSize=100&sort=-createdAt", Ct)).JsonAsync();
        before["items"]!.AsArray().Select(i => i!.Id()).ShouldContain(first.Id());

        var second = await TestData.ProductAsync(admin.Client, price: 4m, sku: marker.ToUpperInvariant());
        var after = await customer.Client.GetAsync("/api/v1/products?pageSize=100&sort=-createdAt", Ct);
        await after.ShouldHaveStatusAsync(HttpStatusCode.OK);
        after.ShouldHaveProductCacheControl();
        (await after.JsonAsync())["items"]!.AsArray().Select(i => i!.Id()).ShouldContain(second.Id(), "create bumps the list generation (ADR-0009)");

        var search = await (await customer.Client.GetAsync($"/api/v1/products?search={marker}", Ct)).JsonAsync();
        search["totalCount"]!.GetValue<int>().ShouldBe(1);
        search["totalPages"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    [Endpoint("EP-22")]
    [Security("ST-11")]
    public async Task Search_with_like_wildcards_and_sql_text_is_safe()
    {
        var customer = await CustomerAsync();

        foreach (var term in new[] { "%%", "__", "\\\\", "';--", "ünï" })
        {
            var response = await customer.Client.GetAsync($"/api/v1/products?search={Uri.EscapeDataString(term)}", Ct);
            await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
            (await response.JsonAsync())["totalCount"]!.GetValue<int>().ShouldBe(0);
        }
    }

    [Fact]
    [Endpoint("EP-22")]
    public async Task List_with_unknown_sort_or_page_size_out_of_range_returns_400()
    {
        var customer = await CustomerAsync();

        await (await customer.Client.GetAsync("/api/v1/products?sort=colour", Ct)).ShouldBeValidationProblemAsync("sort");
        await (await customer.Client.GetAsync("/api/v1/products?pageSize=101", Ct)).ShouldBeValidationProblemAsync("pageSize");
        await (await customer.Client.GetAsync("/api/v1/products?page=abc", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        await (await customer.Client.GetAsync("/api/v1/products?page=2147483647", Ct)).ShouldBeValidationProblemAsync("page");
    }

    [Fact]
    [Endpoint("EP-25")]
    [Security("ST-15")]
    public async Task Update_invalidates_the_cached_item()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = await TestData.ProductAsync(admin.Client, price: 5m);
        var id = product.Id();
        (await (await customer.Client.GetAsync($"/api/v1/products/{id}", Ct)).JsonAsync())["price"]!.GetValue<decimal>().ShouldBe(5m);
        var etag = (await admin.Client.GetAsync($"/api/v1/products/{id}", Ct)).ETag();

        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{id}")
        {
            Content = JsonContent.Create(new { name = "x", sku = product["sku"]!.GetValue<string>(), price = 7m, stockQuantity = 1 }),
        };
        put.Headers.TryAddWithoutValidation("If-Match", etag);
        await (await admin.Client.SendAsync(put, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await (await customer.Client.GetAsync($"/api/v1/products/{id}", Ct)).JsonAsync())["price"]!.GetValue<decimal>().ShouldBe(7m);
    }
}