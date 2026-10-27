namespace DemoApi.Domain.Products;

public sealed class Product : AuditableEntity
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;
    public const int DescriptionMaxLength = 2000;
    public const decimal MinPrice = 0.01m;
    public const decimal MaxPrice = 9_999_999_999.99m;
    public const int MaxStock = 1_000_000;

    private Product()
    {
        Name = string.Empty;
        Sku = string.Empty;
    }

    private Product(Guid id, string name, string sku, string? description, decimal price, int stockQuantity)
        : base(id)
    {
        Name = name;
        Sku = sku;
        Description = description;
        Price = price;
        StockQuantity = stockQuantity;
    }

    public string Name { get; private set; }

    public string Sku { get; private set; }

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    /// <summary>PostgreSQL xmin row version, used for the ETag (ADR-0007).</summary>
    public uint Version { get; private set; }

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static Result<Product> Create(string name, string sku, string? description, decimal price, int stockQuantity)
    {
        var check = Validate(price, stockQuantity);
        if (check is not null)
        {
            return check;
        }

        return new Product(Guid.CreateVersion7(), name.Trim(), NormalizeSku(sku), Normalize(description), price, stockQuantity);
    }

    public Result Update(string name, string sku, string? description, decimal price, int stockQuantity)
    {
        var check = Validate(price, stockQuantity);
        if (check is not null)
        {
            return check;
        }

        Name = name.Trim();
        Sku = NormalizeSku(sku);
        Description = Normalize(description);
        Price = price;
        StockQuantity = stockQuantity;
        return Result.Success();
    }

    public Result Restore()
    {
        if (!IsDeleted)
        {
            return ProductErrors.NotDeleted;
        }

        ClearDeleted();
        return Result.Success();
    }

    public bool HasStock(int quantity) => StockQuantity >= quantity;

    public void RemoveStock(int quantity)
    {
        if (quantity <= 0 || quantity > StockQuantity)
        {
            throw new InvalidOperationException("Stock must be checked before it is removed.");
        }

        StockQuantity -= quantity;
    }

    public void ReturnStock(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        StockQuantity = Math.Min(MaxStock, StockQuantity + quantity);
    }

    private static Error? Validate(decimal price, int stockQuantity)
    {
        if (price < MinPrice || price > MaxPrice || decimal.Round(price, 2) != price)
        {
            return Error.Validation("price", $"Price must be between {MinPrice} and {MaxPrice} with at most 2 decimals.");
        }

        if (stockQuantity is < 0 or > MaxStock)
        {
            return Error.Validation("stockQuantity", $"Stock must be between 0 and {MaxStock}.");
        }

        return null;
    }

    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

public static class ProductErrors
{
    public static Error DuplicateSku(string sku) =>
        Error.Conflict("duplicate-sku", "Duplicate SKU", "An active product already uses this SKU.") with
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sku"] = sku },
        };

    public static readonly Error NotDeleted =
        Error.Conflict("not-deleted", "Not deleted", "The product is not deleted.");
}