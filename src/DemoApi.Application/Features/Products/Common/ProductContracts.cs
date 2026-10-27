namespace DemoApi.Application.Features.Products.Common;

/// <summary>Product as returned to callers. <see cref="IsDeleted"/> is Admin only (D9).</summary>
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    int StockQuantity,
    DateTimeOffset CreatedAt)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDeleted { get; init; }
}

/// <summary>A product with its ETag; this is also the cached item value.</summary>
public sealed record ProductWithETag(ProductResponse Product, string ETag);

public static class ProductMappings
{
    public static Expression<Func<Product, ProductWithETag>> Projection(bool admin) => p => new ProductWithETag(
        new ProductResponse(p.Id, p.Name, p.Sku, p.Description, p.Price, p.StockQuantity, p.CreatedAt) { IsDeleted = admin ? p.IsDeleted : null },
        ETag.Format(p.Version));

    public static ProductWithETag ToResponse(this Product p, bool admin) => new(
        new ProductResponse(p.Id, p.Name, p.Sku, p.Description, p.Price, p.StockQuantity, p.CreatedAt) { IsDeleted = admin ? p.IsDeleted : null },
        ETag.Format(p.Version));
}

/// <summary>ADR-0009 cache keys and invalidation ("Inval P").</summary>