


using AuthPolicies = DemoApi.BuildingBlocks.Security.Policies;

namespace DemoApi.Api.Features.Products;

// EP-22
public sealed class ListProductsRequest : PagedRequest
{
    public string? Search { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool? InStock { get; set; }

    public bool? IncludeDeleted { get; set; }
}

public sealed class ListProductsValidator : PagedValidator<ListProductsRequest>
{
    public ListProductsValidator()
        : base(ProductSortFields.Allowed, ProductSortFields.Default)
    {
        RuleFor(r => r.Search).Search();
        RuleFor(r => r.MinPrice).GreaterThanOrEqualTo(0).When(r => r.MinPrice is not null);
        RuleFor(r => r.MaxPrice).GreaterThanOrEqualTo(0).When(r => r.MaxPrice is not null);
        RuleFor(r => r.MaxPrice).Must((r, max) => Rules.MinNotAboveMax(r.MinPrice, max)).WithMessage("maxPrice must not be less than minPrice.");
    }
}

public sealed class ListProductsEndpoint(IQueryHandler<ListProductsQuery, PagedResponse<ProductResponse>> handler) : Endpoint<ListProductsRequest, PagedResponse<ProductResponse>>
{
    public override void Configure()
    {
        Get("/products");
        Policies(AuthPolicies.Authenticated);
        Summary(s =>
        {
            s.Summary = "List products";
            s.Description = "Paged product list. Filters: search (name or SKU), minPrice, maxPrice, inStock; includeDeleted is honoured for Admins only. Cached per role scope; Cache-Control: private, no-cache.";
        });
    }

    public override async Task HandleAsync(ListProductsRequest r, CancellationToken ct)
    {
        var query = new ListProductsQuery(
            r.ToPage(), r.ToSort(ProductSortFields.Allowed, ProductSortFields.Default), r.Search?.Trim(), r.MinPrice, r.MaxPrice, r.InStock, User.IsAdmin(), r.IncludeDeleted ?? false);
        var page = await handler.HandleAsync(query, ct);
        HttpContext.Response.Headers.CacheControl = HttpResults.ProductCacheControl;
        await Send.OkAsync(page, ct);
    }
}

// EP-23
public sealed class GetProductRequest
{
    public Guid ProductId { get; set; }

    public bool? IncludeDeleted { get; set; }
}

public sealed class GetProductByIdEndpoint(IQueryHandler<GetProductByIdQuery, Result<ProductWithETag>> handler) : Endpoint<GetProductRequest, ProductResponse>
{
    public override void Configure()
    {
        Get("/products/{productId}");
        Policies(AuthPolicies.Authenticated);
        Summary(s =>
        {
            s.Summary = "Get a product";
            s.Description = "Returns one product with its ETag. Deleted products are 404 unless an Admin sends includeDeleted=true. Cache-Control: private, no-cache.";
        });
    }

    public override async Task HandleAsync(GetProductRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await handler.HandleAsync(new GetProductByIdQuery(r.ProductId, User.IsAdmin(), r.IncludeDeleted ?? false), ct))
            .ToOk(HttpContext, HttpResults.ProductCacheControl));
}

// EP-24, EP-25
public abstract class ProductBodyRequest
{
    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }
}

public abstract class ProductBodyValidator<T> : Validator<T>
    where T : ProductBodyRequest
{
    protected ProductBodyValidator()
    {
        RuleFor(r => r.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("The value is required.")
            .Must(v => v is null || v.Trim().Length <= ProductLimits.NameMax).WithMessage($"Use at most {ProductLimits.NameMax} characters.");
        RuleFor(r => r.Sku).Must(v => v is not null && ProductLimits.SkuPattern().IsMatch(v.Trim()) && v.Trim().Length <= ProductLimits.SkuMax)
            .WithMessage($"Use 1 to {ProductLimits.SkuMax} letters, digits or hyphens.");
        RuleFor(r => r.Description).MaximumLength(ProductLimits.DescriptionMax);
        RuleFor(r => r.Price).InclusiveBetween(ProductLimits.MinPrice, ProductLimits.MaxPrice)
            .Must(v => decimal.Round(v, 2) == v).WithMessage("Use at most 2 decimal places.");
        RuleFor(r => r.StockQuantity).InclusiveBetween(0, ProductLimits.MaxStock);
    }
}

public sealed class CreateProductRequest : ProductBodyRequest;

public sealed class CreateProductValidator : ProductBodyValidator<CreateProductRequest>;

public sealed class CreateProductEndpoint : Endpoint<CreateProductRequest, ProductResponse>
{
    public override void Configure()
    {
        Post("/products");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Create a product";
            s.Description = "Creates a product (SKU stored upper-case). Returns 201 with Location and ETag; 409 duplicate-sku when an active product has the SKU.";
        });
    }

    public override async Task HandleAsync(CreateProductRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new CreateProductCommand(r.Name, r.Sku, r.Description, r.Price, r.StockQuantity).ExecuteAsync(ct)).ToCreated(HttpContext));
}

public sealed class UpdateProductRequest : ProductBodyRequest
{
    public Guid ProductId { get; set; }

    [FromHeader("If-Match", false)]
    public string? IfMatch { get; set; }
}

public sealed class UpdateProductValidator : ProductBodyValidator<UpdateProductRequest>;

public sealed class UpdateProductEndpoint : Endpoint<UpdateProductRequest, ProductResponse>
{
    public override void Configure()
    {
        Put("/products/{productId}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Update a product";
            s.Description = "Full replace. Requires If-Match with the current ETag: 428 when missing, 412 when stale. Returns the product with a new ETag; 409 duplicate-sku.";
        });
    }

    public override async Task HandleAsync(UpdateProductRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new UpdateProductCommand(r.ProductId, r.IfMatch, r.Name, r.Sku, r.Description, r.Price, r.StockQuantity).ExecuteAsync(ct))
            .ToOk(HttpContext));
}

// EP-26, EP-27
public sealed class ProductRouteRequest
{
    public Guid ProductId { get; set; }
}

public sealed class DeleteProductEndpoint : Endpoint<ProductRouteRequest>
{
    public override void Configure()
    {
        Delete("/products/{productId}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Delete a product";
            s.Description = "Soft deletes the product; existing order lines are unchanged. 404 when unknown or already deleted.";
        });
    }

    public override async Task HandleAsync(ProductRouteRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new DeleteProductCommand(r.ProductId).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

public sealed class RestoreProductEndpoint : Endpoint<ProductRouteRequest>
{
    public override void Configure()
    {
        Post("/products/{productId}/restore");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Restore a product";
            s.Description = "Restores a soft-deleted product. 409 not-deleted when it is active; 409 duplicate-sku when another active product now has the SKU.";
        });
    }

    public override async Task HandleAsync(ProductRouteRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new RestoreProductCommand(r.ProductId).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

/// <summary>ProductRequest limits from the endpoint catalog §5 (the domain checks them again).</summary>
internal static partial class ProductLimits
{
    public const int NameMax = 200;
    public const int SkuMax = 50;
    public const int DescriptionMax = 2000;
    public const decimal MinPrice = 0.01m;
    public const decimal MaxPrice = 9_999_999_999.99m;
    public const int MaxStock = 1_000_000;

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9-]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, 100)]
    public static partial System.Text.RegularExpressions.Regex SkuPattern();
}