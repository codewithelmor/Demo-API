namespace DemoApi.Application.Features.Products.GetProductById
{
    public sealed record GetProductByIdQuery(Guid ProductId, bool Admin, bool IncludeDeleted);

    /// <summary>EP-23. Cached per scope; deleted products and Admin includeDeleted bypass the cache.</summary>
    public sealed class GetProductByIdHandler(IAppDbContext db, IAppCache cache) : IQueryHandler<GetProductByIdQuery, Result<ProductWithETag>>
    {
        public async Task<Result<ProductWithETag>> HandleAsync(GetProductByIdQuery query, CancellationToken ct)
        {
            if (query.Admin && query.IncludeDeleted)
            {
                var any = await db.Products.IgnoreQueryFilters().AsNoTracking()
                    .Where(p => p.Id == query.ProductId).Select(ProductMappings.Projection(true)).FirstOrDefaultAsync(ct);
                return any is null ? Error.NotFound() : any;
            }

            ProductWithETag? missing = null;
            var found = await cache.GetOrCreateAsync(
                ProductCache.ItemKey(query.Admin, query.ProductId),
                async token => await db.Products.AsNoTracking().Where(p => p.Id == query.ProductId)
                    .Select(ProductMappings.Projection(query.Admin)).FirstOrDefaultAsync(token) ?? missing,
                ProductCache.ItemL2,
                ProductCache.ItemL1,
                ct);
            if (found is null)
            {
                // No negative caching: drop the null entry right away.
                await cache.RemoveAsync([ProductCache.ItemKey(query.Admin, query.ProductId)], ct);
                return Error.NotFound();
            }

            return found;
        }
    }
}

namespace DemoApi.Application.Features.Products.ListProducts
{
    public sealed record ListProductsQuery(
        PageRequest Page,
        IReadOnlyList<SortField> Sort,
        string? Search,
        decimal? MinPrice,
        decimal? MaxPrice,
        bool? InStock,
        bool Admin,
        bool IncludeDeleted);

    public static class ProductSortFields
    {
        public const string Default = "name";
        public static readonly string[] Allowed = ["name", "price", "sku", "stockQuantity", "createdAt"];
    }

    /// <summary>EP-22. Lists are cached by scope + generation + query hash; search lists are never cached (S-D7).</summary>
    public sealed class ListProductsHandler(IAppDbContext db, IAppCache cache) : IQueryHandler<ListProductsQuery, PagedResponse<ProductResponse>>
    {
        public async Task<PagedResponse<ProductResponse>> HandleAsync(ListProductsQuery query, CancellationToken ct)
        {
            var includeDeleted = query.Admin && query.IncludeDeleted;
            if (includeDeleted || !string.IsNullOrWhiteSpace(query.Search))
            {
                return await LoadAsync(query, includeDeleted, ct);
            }

            var generation = await cache.GetListGenerationAsync(ProductCache.GenerationKey, ct);
            var key = ProductCache.ListKey(query.Admin, generation, Normalize(query));
            return await cache.GetOrCreateAsync(key, token => LoadAsync(query, false, token), ProductCache.ListL2, ProductCache.ListL1, ct);
        }

        private static string Normalize(ListProductsQuery q) => string.Join(
            '|',
            q.Page.Page.ToString(CultureInfo.InvariantCulture),
            q.Page.PageSize.ToString(CultureInfo.InvariantCulture),
            string.Join(',', q.Sort.Select(s => (s.Descending ? "-" : string.Empty) + s.Name)),
            q.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            q.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            q.InStock?.ToString() ?? string.Empty);

        private async Task<PagedResponse<ProductResponse>> LoadAsync(ListProductsQuery q, bool includeDeleted, CancellationToken ct)
        {
            var products = includeDeleted ? db.Products.IgnoreQueryFilters() : db.Products;
            var query = products.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var pattern = QueryHelpers.ContainsPattern(q.Search).ToLowerInvariant();
                query = query.Where(p => EF.Functions.Like(p.Name.ToLower(), pattern, "\\") || EF.Functions.Like(p.Sku.ToLower(), pattern, "\\"));
            }

            if (q.MinPrice is { } min)
            {
                query = query.Where(p => p.Price >= min);
            }

            if (q.MaxPrice is { } max)
            {
                query = query.Where(p => p.Price <= max);
            }

            if (q.InStock is { } inStock)
            {
                query = inStock ? query.Where(p => p.StockQuantity > 0) : query.Where(p => p.StockQuantity == 0);
            }

            var page = await Sort(query, q.Sort).Select(ProductMappings.Projection(q.Admin)).ToPageAsync(q.Page, ct);
            return new PagedResponse<ProductResponse>(page.Items.Select(i => i.Product).ToList(), page.Page, page.PageSize, page.TotalCount);
        }

        private static IQueryable<Product> Sort(IQueryable<Product> query, IReadOnlyList<SortField> sort)
        {
            IOrderedQueryable<Product>? ordered = null;
            foreach (var field in sort)
            {
                ordered = field.Name switch
                {
                    "price" => Order(query, ordered, p => p.Price, field.Descending),
                    "sku" => Order(query, ordered, p => p.Sku, field.Descending),
                    "stockQuantity" => Order(query, ordered, p => p.StockQuantity, field.Descending),
                    "createdAt" => Order(query, ordered, p => p.CreatedAt, field.Descending),
                    _ => Order(query, ordered, p => p.Name, field.Descending),
                };
            }

            return ordered is null ? query.OrderBy(p => p.Id) : ordered.ThenBy(p => p.Id);
        }

        private static IOrderedQueryable<Product> Order<TKey>(
            IQueryable<Product> query, IOrderedQueryable<Product>? ordered, System.Linq.Expressions.Expression<Func<Product, TKey>> key, bool descending) =>
            ordered is null
                ? (descending ? query.OrderByDescending(key) : query.OrderBy(key))
                : (descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
    }
}