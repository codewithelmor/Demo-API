namespace DemoApi.IntegrationTests.Orders;

public sealed class OrderTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    private static StringContent EmptyJson() => new("{}", Encoding.UTF8, "application/json");

    private static Task<int> StockAsync(IntegrationFixture fixture, Guid productId) =>
        Db.ScalarAsync<int>(fixture, "SELECT stock_quantity FROM products WHERE id = @productId", new { productId });

    [Fact]
    [Endpoint("EP-38")]
    [Audit("EP-38")]
    [Story("US-401", "AC1")]
    public async Task Admin_create_reserves_stock_snapshots_price_and_audits()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 5, price: 2.50m)).Id();

        var response = await admin.Client.PostAsJsonAsync("/api/v1/orders", new { customerId = customer.CustomerId, items = new[] { new { productId = product, quantity = 2 } } }, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var order = await response.JsonAsync();
        var id = order.Id();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/orders/{id}");
        response.ETag().ShouldNotBeNullOrWhiteSpace();
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        order["status"]!.GetValue<string>().ShouldBe("Pending");
        order["total"]!.GetValue<decimal>().ShouldBe(5.00m);
        order["customerId"]!.GetValue<string>().ShouldBe(customer.CustomerId.ToString());
        (await StockAsync(Fixture, product)).ShouldBe(3);
        (await Db.AuditAsync(Fixture, "Order", id)).First().Action.ShouldBe("Insert");
        (await Db.AuditAsync(Fixture, "Product", product)).Last().ChangesJson["stockQuantity"]!["new"]!.GetValue<string>().ShouldBe("3");

        // The price is a snapshot: a later price change does not alter the order.
        var etag = (await admin.Client.GetAsync($"/api/v1/products/{product}", Ct)).ETag();
        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{product}")
        {
            Content = JsonContent.Create(new { name = "p", sku = "SNAP-" + Guid.NewGuid().ToString("N")[..8], price = 99m, stockQuantity = 3 }),
        };
        put.Headers.TryAddWithoutValidation("If-Match", etag);
        (await admin.Client.SendAsync(put, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await admin.Client.GetAsync($"/api/v1/orders/{id}", Ct)).JsonAsync())["total"]!.GetValue<decimal>().ShouldBe(5.00m);
    }

    [Fact]
    [Endpoint("EP-38")]
    public async Task Admin_create_with_unknown_customer_or_product_returns_400_on_the_field()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();

        await (await admin.Client.PostAsJsonAsync("/api/v1/orders", new { customerId = Guid.NewGuid(), items = new[] { new { productId = product, quantity = 1 } } }, Ct))
            .ShouldBeValidationProblemAsync("customerId");
        await (await admin.Client.PostAsJsonAsync("/api/v1/orders", new { customerId = customer.CustomerId, items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } } }, Ct))
            .ShouldBeValidationProblemAsync("items[0].productId");
    }

    [Fact]
    [Endpoint("EP-45")]
    [Story("US-405", "AC3")]
    public async Task Insufficient_stock_returns_409_with_product_ids_and_changes_nothing()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var plenty = (await TestData.ProductAsync(admin.Client, stock: 10)).Id();
        var scarce = (await TestData.ProductAsync(admin.Client, stock: 1)).Id();

        var response = await customer.Client.PostAsJsonAsync("/api/v1/customers/me/orders", new { items = new[] { new { productId = plenty, quantity = 2 }, new { productId = scarce, quantity = 2 } } }, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "insufficient-stock");
        problem["productIds"]!.AsArray().Select(p => p!.GetValue<string>()).ShouldBe([scarce.ToString()]);
        (await StockAsync(Fixture, plenty)).ShouldBe(10, "the transaction rolled back");
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM orders WHERE customer_id = @c", new { c = customer.CustomerId })).ShouldBe(0);
    }

    [Fact]
    [Endpoint("EP-45")]
    [Story("US-405", "AC4")]
    [Trait("Category", "Slow")]
    public async Task Parallel_orders_never_oversell_the_stock()
    {
        var admin = await AdminAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 5)).Id();
        var customers = new List<TestUser>();
        for (var i = 0; i < 10; i++)
        {
            customers.Add(await CustomerAsync());
        }

        var responses = await Task.WhenAll(customers.SelectMany(c => new[] { c, c }).Select(c =>
            c.Client.PostAsJsonAsync("/api/v1/customers/me/orders", new { items = new[] { new { productId = product, quantity = 1 } } }, Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(5);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(15);
        (await StockAsync(Fixture, product)).ShouldBe(0);
        (await Db.ScalarAsync<long>(Fixture, "SELECT coalesce(sum(quantity), 0) FROM order_items WHERE product_id = @product", new { product })).ShouldBe(5);
    }

    [Fact]
    [Endpoint("EP-45")]
    [Story("US-405", "AC5")]
    public async Task Sixth_pending_order_returns_409_too_many_pending_orders()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 100)).Id();
        for (var i = 0; i < 5; i++)
        {
            await TestData.MyOrderAsync(customer.Client, (product, 1));
        }

        var problem = await (await customer.Client.PostAsJsonAsync("/api/v1/customers/me/orders", new { items = new[] { new { productId = product, quantity = 1 } } }, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "too-many-pending-orders");
        problem["maxPending"]!.GetValue<int>().ShouldBe(5);
        await (await admin.Client.PostAsJsonAsync("/api/v1/orders", new { customerId = customer.CustomerId, items = new[] { new { productId = product, quantity = 1 } } }, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "too-many-pending-orders");
    }

    [Fact]
    [Endpoint("EP-45")]
    [Security("ST-03")]
    public async Task Customer_id_in_the_body_is_ignored_mass_assignment()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var victim = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();

        var response = await customer.Client.PostAsJsonAsync("/api/v1/customers/me/orders", new { customerId = victim.CustomerId, items = new[] { new { productId = product, quantity = 1 } } }, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var id = (await response.JsonAsync()).Id();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/customers/me/orders/{id}");
        (await Db.ScalarAsync<Guid>(Fixture, "SELECT customer_id FROM orders WHERE id = @id", new { id })).ShouldBe(customer.CustomerId!.Value);
        (await Db.AuditAsync(Fixture, "Order", id)).First().UserId.ShouldBe(customer.UserId.ToString());
    }

    [Fact]
    [Endpoint("EP-47")]
    [Security("ST-02")]
    [Story("US-406", "AC2")]
    public async Task Bola_another_customers_order_is_404_with_the_same_body_as_unknown()
    {
        var admin = await AdminAsync();
        var owner = await CustomerAsync();
        var attacker = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();
        var order = (await TestData.MyOrderAsync(owner.Client, (product, 1))).Id();

        var foreign = await (await attacker.Client.GetAsync($"/api/v1/customers/me/orders/{order}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "not-found");
        var unknown = await (await attacker.Client.GetAsync($"/api/v1/customers/me/orders/{Guid.NewGuid()}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "not-found");
        foreign["title"]!.GetValue<string>().ShouldBe(unknown["title"]!.GetValue<string>());
        foreign["detail"]?.GetValue<string>().ShouldBe(unknown["detail"]?.GetValue<string>());

        await (await attacker.Client.PostAsync($"/api/v1/customers/me/orders/{order}/cancel", EmptyJson(), Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "not-found");
        (await Db.ScalarAsync<string>(Fixture, "SELECT status FROM orders WHERE id = @order", new { order })).ShouldBe("Pending");

        var list = await (await attacker.Client.GetAsync("/api/v1/customers/me/orders", Ct)).JsonAsync();
        list["totalCount"]!.GetValue<int>().ShouldBe(0);
        (await owner.Client.GetAsync($"/api/v1/customers/me/orders/{order}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Endpoint("EP-46")]
    [Security("ST-02")]
    public async Task List_own_ignores_a_customer_id_query()
    {
        var admin = await AdminAsync();
        var owner = await CustomerAsync();
        var attacker = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();
        await TestData.MyOrderAsync(owner.Client, (product, 1));

        var list = await attacker.Client.GetAsync($"/api/v1/customers/me/orders?customerId={owner.CustomerId}", Ct);

        // Either rejected as unknown or ignored; never the owner's data.
        if (list.StatusCode == HttpStatusCode.OK)
        {
            (await list.JsonAsync())["totalCount"]!.GetValue<int>().ShouldBe(0);
        }
        else
        {
            await list.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("empty")]
    [InlineData("reason")]
    [Endpoint("EP-48")]
    [Story("US-407", "AC1")]
    public async Task Customer_cancel_accepts_no_body_empty_body_or_reason_and_restores_stock(string body)
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 4)).Id();
        var order = (await TestData.MyOrderAsync(customer.Client, (product, 3))).Id();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/customers/me/orders/{order}/cancel")
        {
            Content = body switch
            {
                "empty" => new StringContent("{}", Encoding.UTF8, "application/json"),
                "reason" => JsonContent.Create(new { reason = "changed my mind" }),
                _ => null,
            },
        };

        await (await customer.Client.SendAsync(request, Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await StockAsync(Fixture, product)).ShouldBe(4);
        (await Db.ScalarAsync<string>(Fixture, "SELECT status FROM orders WHERE id = @order", new { order })).ShouldBe("Cancelled");
        await (await customer.Client.PostAsync($"/api/v1/customers/me/orders/{order}/cancel", EmptyJson(), Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid-status-transition");
    }

    /// <summary>Stock must come back even when the product was soft-deleted after the order, so a later restore shows the real stock.</summary>
    [Fact]
    [Endpoint("EP-48")]
    public async Task Cancel_returns_stock_to_a_product_deleted_after_the_order()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 5)).Id();
        var order = (await TestData.MyOrderAsync(customer.Client, (product, 2))).Id();
        (await StockAsync(Fixture, product)).ShouldBe(3);

        (await admin.Client.DeleteAsync($"/api/v1/products/{product}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await customer.Client.PostAsync($"/api/v1/customers/me/orders/{order}/cancel", EmptyJson(), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await StockAsync(Fixture, product)).ShouldBe(5);
    }

    [Fact]
    [Endpoint("EP-43")]
    [Audit("EP-43")]
    public async Task Admin_cancel_without_body_works_for_confirmed_orders_and_writes_history()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 4)).Id();
        var order = (await TestData.AdminOrderAsync(admin.Client, customer.CustomerId!.Value, (product, 2))).Id();
        (await admin.Client.PostAsJsonAsync($"/api/v1/orders/{order}/status", new { status = "Confirmed" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.Client.PostAsync($"/api/v1/orders/{order}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await StockAsync(Fixture, product)).ShouldBe(4);
        var history = (await (await admin.Client.GetAsync($"/api/v1/orders/{order}", Ct)).JsonAsync())["history"]!.AsArray();
        history.Count.ShouldBe(3);
        (await Db.AuditAsync(Fixture, "Order", order)).Last().Action.ShouldBe("Update");
    }

    [Fact]
    [Endpoint("EP-48")]
    public async Task Customer_cannot_cancel_a_confirmed_order()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();
        var order = (await TestData.MyOrderAsync(customer.Client, (product, 1))).Id();
        (await admin.Client.PostAsJsonAsync($"/api/v1/orders/{order}/status", new { status = "Confirmed" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await customer.Client.PostAsync($"/api/v1/customers/me/orders/{order}/cancel", EmptyJson(), Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid-status-transition");
    }

    [Fact]
    [Endpoint("EP-42")]
    [Story("US-404", "AC1")]
    public async Task Status_moves_forward_only_and_reports_allowed_next_statuses()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();
        var order = (await TestData.AdminOrderAsync(admin.Client, customer.CustomerId!.Value, (product, 1))).Id();

        var skip = await (await admin.Client.PostAsJsonAsync($"/api/v1/orders/{order}/status", new { status = "Shipped" }, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid-status-transition");
        skip["allowedNextStatuses"]!.AsArray().Select(s => s!.GetValue<string>()).ShouldBe(["Confirmed"]);
        await (await admin.Client.PostAsJsonAsync($"/api/v1/orders/{order}/status", new { status = "Cancelled" }, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid-status-transition");

        foreach (var status in new[] { "Confirmed", "Shipped", "Completed" })
        {
            (await admin.Client.PostAsJsonAsync($"/api/v1/orders/{order}/status", new { status, reason = "next" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await (await admin.Client.PostAsync($"/api/v1/orders/{order}/cancel", EmptyJson(), Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid-status-transition");
        await (await admin.Client.PostAsJsonAsync($"/api/v1/orders/{order}/status", new { status = "Bogus" }, Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Endpoint("EP-41")]
    [Audit("EP-41")]
    public async Task Replace_items_needs_if_match_and_adjusts_stock_by_the_delta()
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var a = (await TestData.ProductAsync(admin.Client, stock: 10)).Id();
        var b = (await TestData.ProductAsync(admin.Client, stock: 10)).Id();
        var created = await admin.Client.PostAsJsonAsync("/api/v1/orders", new { customerId = customer.CustomerId, items = new[] { new { productId = a, quantity = 3 } } }, Ct);
        var order = (await created.JsonAsync()).Id();
        var etag = created.ETag();
        var body = new { items = new[] { new { productId = a, quantity = 1 }, new { productId = b, quantity = 4 } } };

        await (await admin.Client.PutAsJsonAsync($"/api/v1/orders/{order}/items", body, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired, "precondition-required");

        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/orders/{order}/items") { Content = JsonContent.Create(body) };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        await (await admin.Client.SendAsync(stale, Ct)).ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "precondition-failed");

        using var ok = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/orders/{order}/items") { Content = JsonContent.Create(body) };
        ok.Headers.TryAddWithoutValidation("If-Match", etag);
        var response = await admin.Client.SendAsync(ok, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.ETag().ShouldNotBe(etag);

        (await StockAsync(Fixture, a)).ShouldBe(9);
        (await StockAsync(Fixture, b)).ShouldBe(6);
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM order_items WHERE order_id = @order", new { order })).ShouldBeGreaterThanOrEqualTo(2, "removed lines are soft deleted, never deleted");
    }

    [Fact]
    [Endpoint("EP-39")]
    public async Task Admin_list_filters_by_customer_and_status()
    {
        var admin = await AdminAsync();
        var one = await CustomerAsync();
        var two = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 50)).Id();
        await TestData.MyOrderAsync(one.Client, (product, 1));
        await TestData.MyOrderAsync(one.Client, (product, 1));
        await TestData.MyOrderAsync(two.Client, (product, 1));

        var page = await (await admin.Client.GetAsync($"/api/v1/orders?customerId={one.CustomerId}&status=Pending&sort=-total", Ct)).JsonAsync();

        page["totalCount"]!.GetValue<int>().ShouldBe(2);
        page["items"]!.AsArray().ShouldAllBe(i => i!["customerId"]!.GetValue<string>() == one.CustomerId.ToString());
        await (await admin.Client.GetAsync("/api/v1/orders?sort=customerName", Ct)).ShouldBeValidationProblemAsync("sort");
    }
}