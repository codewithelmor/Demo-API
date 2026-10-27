namespace DemoApi.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    [Story("US-102", "AC1")]
    public void Create_trims_name_and_stores_sku_upper_case()
    {
        var result = Product.Create("  Coffee mug ", " t-100-ab ", "  ", 9.99m, 5);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Coffee mug");
        result.Value.Sku.ShouldBe("T-100-AB");
        result.Value.Description.ShouldBeNull();
        result.Value.Id.Version.ShouldBe(7);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.001")]
    [InlineData("10.005")]
    [InlineData("-1")]
    [InlineData("10000000000")]
    public void Create_with_invalid_price_returns_validation_error(string price)
    {
        var result = Product.Create("Mug", "T-1", null, decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), 1);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("validation-failed");
        result.Error.ValidationErrors!.Keys.ShouldContain("price");
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("9999999999.99")]
    public void Create_accepts_price_limits(string price) =>
        Product.Create("Mug", "T-1", null, decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), 0).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    public void Create_with_stock_out_of_range_returns_validation_error(int stock) =>
        Product.Create("Mug", "T-1", null, 1m, stock).Error!.ValidationErrors!.Keys.ShouldContain("stockQuantity");

    [Fact]
    public void Update_replaces_all_fields()
    {
        var product = Product.Create("Mug", "T-1", "old", 1m, 1).Value;

        product.Update("Cup", "t-2", "new", 2.5m, 7).IsSuccess.ShouldBeTrue();

        product.Name.ShouldBe("Cup");
        product.Sku.ShouldBe("T-2");
        product.Description.ShouldBe("new");
        product.Price.ShouldBe(2.5m);
        product.StockQuantity.ShouldBe(7);
    }

    [Fact]
    public void Update_with_invalid_price_leaves_product_unchanged()
    {
        var product = Product.Create("Mug", "T-1", null, 1m, 1).Value;

        product.Update("Cup", "T-2", null, 0m, 1).IsFailure.ShouldBeTrue();

        product.Name.ShouldBe("Mug");
    }

    [Fact]
    public void Restore_of_active_product_returns_not_deleted()
    {
        var product = Product.Create("Mug", "T-1", null, 1m, 1).Value;

        product.Restore().Error.ShouldBe(ProductErrors.NotDeleted);
    }

    [Fact]
    public void RemoveStock_decrements_and_never_goes_negative()
    {
        var product = Product.Create("Mug", "T-1", null, 1m, 5).Value;

        product.HasStock(5).ShouldBeTrue();
        product.HasStock(6).ShouldBeFalse();
        product.RemoveStock(5);
        product.StockQuantity.ShouldBe(0);
        Should.Throw<InvalidOperationException>(() => product.RemoveStock(1));
    }

    [Fact]
    public void ReturnStock_caps_at_maximum()
    {
        var product = Product.Create("Mug", "T-1", null, 1m, Product.MaxStock - 1).Value;

        product.ReturnStock(10);

        product.StockQuantity.ShouldBe(Product.MaxStock);
        Should.Throw<ArgumentOutOfRangeException>(() => product.ReturnStock(0));
    }

    [Fact]
    public void DuplicateSku_error_carries_sku_extension() =>
        ProductErrors.DuplicateSku("T-1").Extensions!["sku"].ShouldBe("T-1");
}