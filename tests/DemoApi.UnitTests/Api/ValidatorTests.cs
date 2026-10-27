namespace DemoApi.UnitTests.Api;

internal static class ValidationExtensions
{
    public static ValidationResult Check<T>(this IValidator<T> validator, T request) => validator.Validate(request);

    public static IEnumerable<string> Fields(this ValidationResult result) => result.Errors.Select(e => e.PropertyName);
}

[Endpoint("EP-24")]
public sealed class CreateProductValidatorTests
{
    private static CreateProductRequest Valid() => new() { Name = "Mug", Sku = "T-1", Price = 9.99m, StockQuantity = 5 };

    [Fact]
    public void Valid_request_passes() => new CreateProductValidator().Check(Valid()).IsValid.ShouldBeTrue();

    [Theory]
    [Story("US-102", "AC2")]
    [InlineData("", "T-1", "1", 0, "Name")]
    [InlineData("   ", "T-1", "1", 0, "Name")]
    [InlineData("Mug", "", "1", 0, "Sku")]
    [InlineData("Mug", "T 1", "1", 0, "Sku")]
    [InlineData("Mug", "T_1", "1", 0, "Sku")]
    [InlineData("Mug", "T-1", "0", 0, "Price")]
    [InlineData("Mug", "T-1", "1.001", 0, "Price")]
    [InlineData("Mug", "T-1", "10000000000", 0, "Price")]
    [InlineData("Mug", "T-1", "1", -1, "StockQuantity")]
    [InlineData("Mug", "T-1", "1", 1_000_001, "StockQuantity")]
    public void Invalid_field_is_reported(string name, string sku, string price, int stock, string field)
    {
        var request = new CreateProductRequest
        {
            Name = name,
            Sku = sku,
            Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture),
            StockQuantity = stock,
        };

        new CreateProductValidator().Check(request).Fields().ShouldContain(field);
    }

    [Fact]
    public void Name_over_200_and_sku_over_50_and_description_over_2000_fail()
    {
        var request = Valid();
        request.Name = new string('a', 201);
        request.Sku = new string('A', 51);
        request.Description = new string('d', 2001);

        new CreateProductValidator().Check(request).Fields().ShouldBe(["Name", "Sku", "Description"], ignoreOrder: true);
    }

    [Fact]
    public void Limits_are_inclusive()
    {
        var request = Valid();
        request.Name = new string('a', 200);
        request.Sku = new string('A', 50);
        request.Description = new string('d', 2000);
        request.Price = 9_999_999_999.99m;
        request.StockQuantity = 1_000_000;

        new CreateProductValidator().Check(request).IsValid.ShouldBeTrue();
    }
}

[Endpoint("EP-01")]
public sealed class RegisterValidatorTests
{
    private static RegisterRequest Valid() => new()
    {
        Email = "ada@example.com",
        Password = "a long enough passphrase",
        FirstName = "Ada",
        LastName = "Lovelace",
        Phone = "+12025550101",
    };

    [Fact]
    public void Valid_request_passes() => new RegisterValidator().Check(Valid()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Bad_email_fails(string email)
    {
        var request = Valid();
        request.Email = email;

        new RegisterValidator().Check(request).Fields().ShouldContain("Email");
    }

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Password_length_is_15_to_128_code_points(int length, bool valid)
    {
        var request = Valid();
        request.Password = new string('x', length);

        new RegisterValidator().Check(request).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Password_counts_unicode_code_points_not_utf16_units()
    {
        var request = Valid();
        request.Password = string.Concat(Enumerable.Repeat("😀", 15)); // 30 UTF-16 units, 15 code points

        new RegisterValidator().Check(request).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("12025550101")]
    [InlineData("+0123")]
    [InlineData("+1 202 555 0101")]
    [InlineData("+123456789012345678")]
    public void Phone_must_be_e164(string phone)
    {
        var request = Valid();
        request.Phone = phone;

        new RegisterValidator().Check(request).Fields().ShouldContain("Phone");
    }

    [Fact]
    public void Name_over_100_characters_fails()
    {
        var request = Valid();
        request.LastName = new string('n', 101);

        new RegisterValidator().Check(request).Fields().ShouldContain("LastName");
    }
}

[Endpoint("EP-38")]
public sealed class CreateOrderValidatorTests
{
    [Fact]
    public void Valid_request_passes() =>
        new CreateOrderValidator().Check(new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = [new OrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }],
        }).IsValid.ShouldBeTrue();

    [Fact]
    public void Empty_customer_and_no_items_fail()
    {
        var result = new CreateOrderValidator().Check(new CreateOrderRequest { CustomerId = Guid.Empty, Items = [] });

        result.Fields().ShouldContain("CustomerId");
        result.Fields().ShouldContain("Items");
    }

    [Fact]
    public void Duplicate_product_and_bad_quantity_fail()
    {
        var id = Guid.NewGuid();
        var result = new CreateOrderValidator().Check(new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = [new OrderItemRequest { ProductId = id, Quantity = 1 }, new OrderItemRequest { ProductId = id, Quantity = 1001 }],
        });

        result.Fields().ShouldContain("Items");
        result.Fields().ShouldContain("Items[1].Quantity");
    }

    [Fact]
    public void Fifty_one_lines_fail() =>
        new CreateOrderValidator().Check(new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = Enumerable.Range(0, 51).Select(_ => new OrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }).ToList(),
        }).IsValid.ShouldBeFalse();
}

public sealed class PagingAndFilterValidatorTests
{
    [Theory]
    [InlineData(0, null, null, "Page")]
    [InlineData(null, 0, null, "PageSize")]
    [InlineData(null, 101, null, "PageSize")]
    [InlineData(null, null, "colour", "sort")]
    public void Paging_and_sort_limits(int? page, int? pageSize, string? sort, string field) =>
        new ListProductsValidator().Check(new ListProductsRequest { Page = page, PageSize = pageSize, Sort = sort }).Fields().ShouldContain(field);

    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    public void Search_needs_two_characters(string search) =>
        new ListProductsValidator().Check(new ListProductsRequest { Search = search }).Fields().ShouldContain("Search");

    [Fact]
    public void Min_price_above_max_fails() =>
        new ListProductsValidator().Check(new ListProductsRequest { MinPrice = 5, MaxPrice = 4 }).Fields().ShouldContain("MaxPrice");

    [Fact]
    public void Order_filter_rejects_dates_without_offset_and_reversed_ranges()
    {
        new ListOrdersValidator().Check(new ListOrdersRequest { CreatedFrom = "2026-10-24T00:00:00" }).Fields().ShouldContain("CreatedFrom");
        new ListOrdersValidator().Check(new ListOrdersRequest { CreatedFrom = "2026-10-25T00:00:00Z", CreatedTo = "2026-10-24T00:00:00Z" })
            .Fields().ShouldContain("CreatedTo");
        new ListOrdersValidator().Check(new ListOrdersRequest { Sort = "-total,status" }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Customer_order_sort_does_not_allow_status() =>
        new ListMyOrdersValidator().Check(new ListMyOrdersRequest { Sort = "status" }).Fields().ShouldContain("sort");

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Customer", true)]
    [InlineData("Root", false)]
    public void List_users_role_filter(string role, bool valid) =>
        new ListUsersValidator().Check(new ListUsersRequest { Role = role }).IsValid.ShouldBe(valid);
}

[Endpoint("EP-52")]
public sealed class SalesReportValidatorTests
{
    [Fact]
    public void From_and_to_are_required() =>
        new SalesReportValidator().Check(new SalesReportRequest()).Fields().ShouldBe(["From", "To"], ignoreOrder: true);

    [Theory]
    [InlineData("2026-01-01T00:00:00Z", "2027-01-03T00:00:00Z", "day", false)]
    [InlineData("2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z", "WEEK", true)]
    [InlineData("2026-01-01T00:00:00Z", "2027-01-02T00:00:00Z", "month", true)]
    [InlineData("2026-01-02T00:00:00Z", "2026-01-01T00:00:00Z", "day", false)]
    [InlineData("2026-01-01T00:00:00Z", "2026-02-01T00:00:00Z", "year", false)]
    public void Range_and_group_by_rules(string from, string to, string groupBy, bool valid) =>
        new SalesReportValidator().Check(new SalesReportRequest { From = from, To = to, GroupBy = groupBy }).IsValid.ShouldBe(valid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Top_products_top_is_1_to_50(int top, bool valid) =>
        new TopProductsValidator().Check(new TopProductsRequest { From = "2026-01-01T00:00:00Z", To = "2026-02-01T00:00:00Z", Top = top })
            .IsValid.ShouldBe(valid);
}

public sealed class OtherValidatorTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("admin", true)]
    [InlineData("Customer", false)]
    public void Only_admin_role_can_be_assigned(string role, bool valid) =>
        new UserRoleValidator().Check(new UserRoleRequest { UserId = Guid.NewGuid(), Role = role }).IsValid.ShouldBe(valid);

    [Fact]
    public void Update_customer_email_is_optional_but_must_be_valid()
    {
        var request = new UpdateCustomerRequest { FirstName = "Ada", LastName = "Lovelace" };
        new UpdateCustomerValidator().Check(request).IsValid.ShouldBeTrue();

        request.Email = "nope";
        new UpdateCustomerValidator().Check(request).Fields().ShouldContain("Email");
    }

    [Fact]
    public void Cancel_reason_over_500_characters_fails() =>
        new CancelOrderValidator().Check(new CancelOrderRequest { Reason = new string('r', 501) }).IsValid.ShouldBeFalse();

    [Fact]
    public void Change_status_requires_a_status() =>
        new ChangeOrderStatusValidator().Check(new ChangeOrderStatusRequest()).Fields().ShouldContain("Status");

    [Fact]
    public void Login_password_is_capped_at_1024() =>
        new LoginValidator().Check(new LoginRequest { Email = "a@example.com", Password = new string('p', 1025) }).Fields().ShouldContain("Password");

    [Fact]
    public void Refresh_token_is_capped_at_200() =>
        new RefreshTokenValidator().Check(new RefreshTokenRequest { RefreshToken = new string('t', 201) }).IsValid.ShouldBeFalse();

    [Fact]
    public void Lock_until_needs_an_offset() =>
        new LockUserValidator().Check(new LockUserRequest { Until = "2030-01-01T00:00:00" }).Fields().ShouldContain("Until");

    [Fact]
    public void Erase_requires_current_password() =>
        new EraseMyAccountValidator().Check(new EraseMyAccountRequest()).Fields().ShouldContain("CurrentPassword");
}