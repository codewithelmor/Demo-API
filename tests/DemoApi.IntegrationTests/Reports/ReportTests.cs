namespace DemoApi.IntegrationTests.Reports;

/// <summary>Dapper views and functions (EP-50..55) over real data: soft-deleted rows excluded, deleted customers kept.</summary>
public sealed class ReportTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    private static string Range =>
        $"from={Iso(DateTimeOffset.UtcNow.AddDays(-1))}&to={Iso(DateTimeOffset.UtcNow.AddDays(1))}";

    private static string Iso(DateTimeOffset value) =>
        Uri.EscapeDataString(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

    private async Task<(TestUser Admin, TestUser Customer, Guid Product, Guid Order)> ConfirmedOrderAsync(int quantity = 3, decimal price = 4m)
    {
        var admin = await AdminAsync();
        var customer = await CustomerAsync();
        var product = (await TestData.ProductAsync(admin.Client, stock: 50, price: price)).Id();
        var order = (await TestData.MyOrderAsync(customer.Client, (product, quantity))).Id();
        (await admin.Client.PostAsJsonAsync($"/api/v1/orders/{order}/status", new { status = "Confirmed" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return (admin, customer, product, order);
    }

    [Fact]
    [Endpoint("EP-50")]
    public async Task Order_summaries_return_counts_and_the_customer_name()
    {
        var (admin, customer, _, order) = await ConfirmedOrderAsync();

        var response = await admin.Client.GetAsync($"/api/v1/reports/order-summaries?customerId={customer.CustomerId}", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var item = (await response.JsonAsync())["items"]!.AsArray().ShouldHaveSingleItem()!;
        item["orderId"]!.GetValue<string>().ShouldBe(order.ToString());
        item["customerName"]!.GetValue<string>().ShouldBe("Ada Lovelace");
        item["totalUnits"]!.GetValue<int>().ShouldBe(3);
        item["total"]!.GetValue<decimal>().ShouldBe(12m);
    }

    [Fact]
    [Endpoint("EP-51")]
    public async Task Customer_order_history_is_paged_and_404_for_unknown()
    {
        var (admin, customer, product, _) = await ConfirmedOrderAsync();
        await TestData.MyOrderAsync(customer.Client, (product, 1));

        var page = await (await admin.Client.GetAsync($"/api/v1/customers/{customer.CustomerId}/order-history?pageSize=1", Ct)).JsonAsync();

        page["totalCount"]!.GetValue<int>().ShouldBe(2);
        page["items"]!.AsArray().Count.ShouldBe(1);
        await (await admin.Client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}/order-history", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    [Endpoint("EP-52")]
    public async Task Sales_report_counts_confirmed_orders_only()
    {
        // Other tests in this class also create orders (Respawn runs per class), so measure the change.
        var range = Range;
        var before = await SalesTotalsAsync(range);

        var (_, customer, product, _) = await ConfirmedOrderAsync(quantity: 3, price: 4m);
        await TestData.MyOrderAsync(customer.Client, (product, 5)); // Pending: not revenue

        var after = await SalesTotalsAsync(range);
        (Number(after, "revenue") - Number(before, "revenue")).ShouldBe(12m);
        (Number(after, "units") - Number(before, "units")).ShouldBe(3m);
    }

    private async Task<JsonNode> SalesTotalsAsync(string range)
    {
        var admin = await AdminAsync();
        var response = await admin.Client.GetAsync($"/api/v1/reports/sales?{range}&groupBy=day", Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return (await response.JsonAsync())["totals"]!;
    }

    [Fact]
    [Endpoint("EP-52")]
    public async Task Sales_report_rejects_ranges_over_366_days_and_unknown_grouping()
    {
        var admin = await AdminAsync();
        var from = Iso(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var to = Iso(new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero));

        await (await admin.Client.GetAsync($"/api/v1/reports/sales?from={from}&to={to}", Ct)).ShouldBeValidationProblemAsync("to");
        await (await admin.Client.GetAsync($"/api/v1/reports/sales?{Range}&groupBy=hour", Ct)).ShouldBeValidationProblemAsync("groupBy");
    }

    [Fact]
    [Endpoint("EP-53")]
    public async Task Low_stock_lists_active_products_under_the_threshold()
    {
        var admin = await AdminAsync();
        var low = (await TestData.ProductAsync(admin.Client, stock: 1)).Id();
        var deleted = (await TestData.ProductAsync(admin.Client, stock: 0)).Id();
        var high = (await TestData.ProductAsync(admin.Client, stock: 500)).Id();
        await admin.Client.DeleteAsync($"/api/v1/products/{deleted}", Ct);

        var ids = (await (await admin.Client.GetAsync("/api/v1/reports/low-stock?threshold=5&pageSize=100", Ct)).JsonAsync())["items"]!.AsArray().Select(i => i!.Id()).ToList();

        ids.ShouldContain(low);
        ids.ShouldNotContain(deleted);
        ids.ShouldNotContain(high);
    }

    [Fact]
    [Endpoint("EP-54")]
    public async Task Top_products_rank_by_units()
    {
        var (admin, _, product, _) = await ConfirmedOrderAsync(quantity: 7);

        var response = await admin.Client.GetAsync($"/api/v1/reports/top-products?{Range}&top=5", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var first = (await response.JsonAsync())["items"]!.AsArray().First()!;
        first["productId"]!.GetValue<string>().ShouldBe(product.ToString());
        Number(first, "units").ShouldBe(7m);
    }

    [Fact]
    [Endpoint("EP-55")]
    [Security("ST-14")]
    public async Task Audit_logs_are_filterable_and_never_show_personal_values()
    {
        var admin = await AdminAsync();
        var product = (await TestData.ProductAsync(admin.Client)).Id();
        await CustomerAsync();

        var response = await admin.Client.GetAsync($"/api/v1/audit-logs?entityType=Product&entityId={product}", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var item = (await response.JsonAsync())["items"]!.AsArray().ShouldHaveSingleItem()!;
        item["action"]!.GetValue<string>().ShouldBe("Insert");
        item["userId"]!.GetValue<string>().ShouldBe(admin.UserId.ToString());
        item["changes"]!.AsArray().ShouldContain(c => c!["field"]!.GetValue<string>() == "sku");

        var customers = await (await admin.Client.GetAsync("/api/v1/audit-logs?entityType=Customer&pageSize=100", Ct)).JsonAsync();
        customers["items"]!.AsArray()
            .SelectMany(i => i!["changes"]!.AsArray())
            .Where(c => c!["field"]!.GetValue<string>() is "firstName" or "lastName")
            .ShouldAllBe(c => NoValue(c!["old"]) && NoValue(c!["new"]), "personal data never has values (P1)");
    }

    private static bool NoValue(JsonNode? value) => value is null || value.GetValue<string>() == "changed";

    private static decimal Number(JsonNode node, string property) => decimal.Parse(node[property]!.ToJsonString(), CultureInfo.InvariantCulture);
}