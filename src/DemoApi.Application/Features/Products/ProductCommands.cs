namespace DemoApi.Application.Features.Products.CreateProduct
{
    public sealed record CreateProductCommand(string Name, string Sku, string? Description, decimal Price, int StockQuantity)
        : ICommand<Result<ProductWithETag>>;

    public sealed class CreateProductHandler(IAppDbContext db, IAppCache cache) : ICommandHandler<CreateProductCommand, Result<ProductWithETag>>
    {
        public async Task<Result<ProductWithETag>> ExecuteAsync(CreateProductCommand command, CancellationToken ct)
        {
            var created = Product.Create(command.Name, command.Sku, command.Description, command.Price, command.StockQuantity);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            var product = created.Value;
            if (await db.Products.AnyAsync(p => p.Sku == product.Sku, ct))
            {
                return ProductErrors.DuplicateSku(product.Sku);
            }

            db.Products.Add(product);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (db.UniqueViolation(ex) is not null)
            {
                return ProductErrors.DuplicateSku(product.Sku);
            }

            await cache.BumpListGenerationAsync(ProductCache.GenerationKey, ct);
            return product.ToResponse(admin: true);
        }
    }
}

namespace DemoApi.Application.Features.Products.UpdateProduct
{
    public sealed record UpdateProductCommand(Guid ProductId, string? IfMatch, string Name, string Sku, string? Description, decimal Price, int StockQuantity)
        : ICommand<Result<ProductWithETag>>;

    public sealed class UpdateProductHandler(IAppDbContext db, IAppCache cache) : ICommandHandler<UpdateProductCommand, Result<ProductWithETag>>
    {
        public async Task<Result<ProductWithETag>> ExecuteAsync(UpdateProductCommand command, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.ProductId, ct);
            if (product is null)
            {
                return Error.NotFound();
            }

            if (QueryHelpers.CheckIfMatch(command.IfMatch, product.Version) is { } precondition)
            {
                return precondition;
            }

            var sku = Product.NormalizeSku(command.Sku);
            if (await db.Products.AnyAsync(p => p.Sku == sku && p.Id != product.Id, ct))
            {
                return ProductErrors.DuplicateSku(sku);
            }

            var updated = product.Update(command.Name, command.Sku, command.Description, command.Price, command.StockQuantity);
            if (updated.IsFailure)
            {
                return updated.Error!;
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.PreconditionFailed;
            }
            catch (DbUpdateException ex) when (db.UniqueViolation(ex) is not null)
            {
                return ProductErrors.DuplicateSku(sku);
            }

            await ProductCache.InvalidateAsync(cache, [product.Id], ct);
            return product.ToResponse(admin: true);
        }
    }
}

namespace DemoApi.Application.Features.Products.DeleteProduct
{
    public sealed record DeleteProductCommand(Guid ProductId) : ICommand<Result>;

    public sealed class DeleteProductHandler(IAppDbContext db, IAppCache cache) : ICommandHandler<DeleteProductCommand, Result>
    {
        public async Task<Result> ExecuteAsync(DeleteProductCommand command, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.ProductId, ct);
            if (product is null)
            {
                return Error.NotFound();
            }

            db.Products.Remove(product); // soft delete by the interceptor
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.ConcurrencyConflict;
            }

            await ProductCache.InvalidateAsync(cache, [product.Id], ct);
            return Result.Success();
        }
    }
}

namespace DemoApi.Application.Features.Products.RestoreProduct
{
    public sealed record RestoreProductCommand(Guid ProductId) : ICommand<Result>;

    public sealed class RestoreProductHandler(IAppDbContext db, IAppCache cache) : ICommandHandler<RestoreProductCommand, Result>
    {
        public async Task<Result> ExecuteAsync(RestoreProductCommand command, CancellationToken ct)
        {
            var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == command.ProductId, ct);
            if (product is null)
            {
                return Error.NotFound();
            }

            var restored = product.Restore();
            if (restored.IsFailure)
            {
                return restored;
            }

            if (await db.Products.AnyAsync(p => p.Sku == product.Sku && p.Id != product.Id, ct))
            {
                return ProductErrors.DuplicateSku(product.Sku);
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.ConcurrencyConflict;
            }
            catch (DbUpdateException ex) when (db.UniqueViolation(ex) is not null)
            {
                return ProductErrors.DuplicateSku(product.Sku);
            }

            await ProductCache.InvalidateAsync(cache, [product.Id], ct);
            return Result.Success();
        }
    }
}