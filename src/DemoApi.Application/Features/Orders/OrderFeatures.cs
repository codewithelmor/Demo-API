namespace DemoApi.Application.Features.Orders
{
    public sealed record OrderLineInput(Guid ProductId, int Quantity);

    public sealed record OrderLineResponse(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);

    public sealed record OrderHistoryResponse(OrderStatus? From, OrderStatus To, Guid? ByUserId, DateTimeOffset At, string? Reason);

    public sealed record MyOrderHistoryResponse(OrderStatus? From, OrderStatus To, DateTimeOffset At, string? Reason);

    public sealed record OrderResponse(
        Guid Id, Guid CustomerId, OrderStatus Status, decimal Total, string Currency, DateTimeOffset CreatedAt, DateTimeOffset? LastModifiedAt,
        IReadOnlyList<OrderLineResponse> Items, IReadOnlyList<OrderHistoryResponse> History, IReadOnlyList<OrderStatus> AllowedNextStatuses);

    public sealed record MyOrderResponse(
        Guid Id, OrderStatus Status, decimal Total, string Currency, DateTimeOffset CreatedAt, DateTimeOffset? LastModifiedAt,
        IReadOnlyList<OrderLineResponse> Items, IReadOnlyList<MyOrderHistoryResponse> History);

    public sealed record OrderListItem(Guid Id, Guid CustomerId, OrderStatus Status, decimal Total, string Currency, DateTimeOffset CreatedAt);

    public sealed record MyOrderListItem(Guid Id, OrderStatus Status, decimal Total, string Currency, DateTimeOffset CreatedAt);

    public static class OrderSortFields
    {
        public const string Default = "-createdAt";
        public static readonly string[] Admin = ["createdAt", "total", "status"];
        public static readonly string[] Customer = ["createdAt", "total"];
    }

    public static class OrderMappings
    {
        public static Tagged<OrderResponse> ToAdmin(Order o) => new(
            new OrderResponse(
                o.Id, o.CustomerId, o.Status, o.Total, o.CurrencyCode, o.CreatedAt, o.LastModifiedAt, Lines(o),
                o.History.OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                    .Select(h => new OrderHistoryResponse(h.FromStatus, h.ToStatus, h.CreatedBy, h.CreatedAt, h.Reason)).ToList(),
                Order.NextStatuses(o.Status)),
            ETag.Format(o.Version));

        public static Tagged<MyOrderResponse> ToCustomer(Order o) => new(
            new MyOrderResponse(
                o.Id, o.Status, o.Total, o.CurrencyCode, o.CreatedAt, o.LastModifiedAt, Lines(o),
                o.History.OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                    .Select(h => new MyOrderHistoryResponse(h.FromStatus, h.ToStatus, h.CreatedAt, h.Reason)).ToList()),
            ETag.Format(o.Version));

        private static List<OrderLineResponse> Lines(Order o) =>
            o.ActiveItems.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
                .Select(i => new OrderLineResponse(i.ProductId, i.Quantity, i.UnitPrice, i.LineTotal)).ToList();
    }

    /// <summary>Order write steps shared by the Admin and customer endpoints (P3, D15, FR-096).</summary>
    public sealed class OrderWriter(IAppDbContext db, IAppCache cache, IDateTimeProvider clock, AppSettings settings)
    {
        public static IQueryable<Order> WithDetails(IAppDbContext db) =>
            db.Orders.Include(o => o.Items).Include(o => o.History);

        public async Task<Result<Order>> PlaceAsync(Guid customerId, IReadOnlyList<OrderLineInput> items, CancellationToken ct)
        {
            await db.LockCustomerAsync(customerId, ct);
            var products = await LockAsync(items.Select(i => i.ProductId), ct);
            if (UnknownProducts(items, products) is { } unknown)
            {
                return unknown; // 400 reference errors come before 409 (catalog §1.9)
            }

            var pending = await db.Orders.CountAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending, ct);
            if (pending >= settings.MaxPendingOrders)
            {
                return OrderErrors.TooManyPendingOrders(settings.MaxPendingOrders);
            }

            var short_ = items.Where(i => !products[i.ProductId].HasStock(i.Quantity)).Select(i => i.ProductId).ToList();
            if (short_.Count > 0)
            {
                return OrderErrors.InsufficientStock(short_);
            }

            var created = Order.Create(
                customerId, settings.CurrencyCode, items.Select(i => new OrderLine(i.ProductId, i.Quantity, products[i.ProductId].Price)).ToList());
            if (created.IsFailure)
            {
                return created.Error!;
            }

            foreach (var item in items)
            {
                products[item.ProductId].RemoveStock(item.Quantity);
            }

            db.Orders.Add(created.Value);
            await db.SaveChangesAsync(ct);
            return created;
        }

        public async Task<Result<Order>> ReplaceItemsAsync(Order order, string? ifMatch, IReadOnlyList<OrderLineInput> items, CancellationToken ct)
        {
            if (QueryHelpers.CheckIfMatch(ifMatch, order.Version) is { } precondition)
            {
                return precondition;
            }

            var products = await LockAsync(items.Select(i => i.ProductId).Concat(order.ActiveItems.Select(i => i.ProductId)), ct);
            if (UnknownProducts(items, products) is { } unknown)
            {
                return unknown; // 400 reference errors come before 409 (catalog §1.9)
            }

            if (order.Status != OrderStatus.Pending)
            {
                return OrderErrors.InvalidOrderState(order.Status);
            }

            var replaced = order.ReplaceItems(
                items.Select(i => new OrderLine(i.ProductId, i.Quantity, products[i.ProductId].Price)).ToList(), clock.UtcNow);
            if (replaced.IsFailure)
            {
                return replaced.Error!;
            }

            var short_ = replaced.Value.Where(d => d.Value > 0 && !products[d.Key].HasStock(d.Value)).Select(d => d.Key).ToList();
            if (short_.Count > 0)
            {
                return OrderErrors.InsufficientStock(short_);
            }

            foreach (var (productId, delta) in replaced.Value)
            {
                if (!products.TryGetValue(productId, out var product))
                {
                    continue;
                }

                if (delta > 0)
                {
                    product.RemoveStock(delta);
                }
                else if (delta < 0)
                {
                    product.ReturnStock(-delta);
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.PreconditionFailed;
            }

            return order;
        }

        public async Task<Result> CancelAsync(Order order, bool byCustomer, string? reason, CancellationToken ct)
        {
            var cancelled = order.Cancel(byCustomer, reason, clock.UtcNow);
            if (cancelled.IsFailure)
            {
                return cancelled;
            }

            var products = await LockAsync(order.ActiveItems.Select(i => i.ProductId), ct);
            foreach (var item in order.ActiveItems)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    product.ReturnStock(item.Quantity);
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.ConcurrencyConflict;
            }

            return Result.Success();
        }

        /// <summary>After commit: drop cached product entries whose stock changed (D15).</summary>
        public Task InvalidateAsync(IEnumerable<Guid> productIds, CancellationToken ct) =>
            ProductCache.InvalidateAsync(cache, productIds.Distinct(), ct);

        private async Task<Dictionary<Guid, Product>> LockAsync(IEnumerable<Guid> productIds, CancellationToken ct)
        {
            var ids = productIds.Distinct().Order().ToList();
            // Deleted products are locked too so cancelling or editing an order gives their stock back; UnknownProducts
            // still rejects them for new lines.
            return ids.Count == 0 ? [] : (await db.LockProductsAsync(ids, includeDeleted: true, ct)).ToDictionary(p => p.Id);
        }

        private static Error? UnknownProducts(IReadOnlyList<OrderLineInput> items, Dictionary<Guid, Product> products)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                if (!products.TryGetValue(items[i].ProductId, out var product) || product.IsDeleted)
                {
                    errors[$"items[{i}].productId"] = ["The product does not exist."];
                }
            }

            return errors.Count == 0 ? null : Error.Validation(errors);
        }
    }

    internal static class OrderQueries
    {
        public static IQueryable<Order> Filter(IQueryable<Order> orders, OrderFilter f)
        {
            if (f.Statuses.Count > 0)
            {
                var statuses = f.Statuses.ToList();
                orders = orders.Where(o => statuses.Contains(o.Status));
            }

            if (f.CustomerId is { } customerId)
            {
                orders = orders.Where(o => o.CustomerId == customerId);
            }

            if (f.CreatedFrom is { } from)
            {
                orders = orders.Where(o => o.CreatedAt >= from);
            }

            if (f.CreatedTo is { } to)
            {
                orders = orders.Where(o => o.CreatedAt < to);
            }

            if (f.MinTotal is { } min)
            {
                orders = orders.Where(o => o.Total >= min);
            }

            if (f.MaxTotal is { } max)
            {
                orders = orders.Where(o => o.Total <= max);
            }

            return orders;
        }

        public static IOrderedQueryable<Order> Sort(IQueryable<Order> orders, IReadOnlyList<SortField> sort)
        {
            IOrderedQueryable<Order>? ordered = null;
            foreach (var s in sort)
            {
                ordered = s.Name switch
                {
                    "total" => Then(ordered, orders, o => o.Total, s.Descending),
                    "status" => Then(ordered, orders, o => o.Status, s.Descending),
                    _ => Then(ordered, orders, o => o.CreatedAt, s.Descending),
                };
            }

            return (ordered ?? orders.OrderByDescending(o => o.CreatedAt)).ThenBy(o => o.Id);
        }

        private static IOrderedQueryable<Order> Then<TKey>(
            IOrderedQueryable<Order>? ordered, IQueryable<Order> source, System.Linq.Expressions.Expression<Func<Order, TKey>> key, bool desc) =>
            ordered is null
                ? (desc ? source.OrderByDescending(key) : source.OrderBy(key))
                : (desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
    }
}

namespace DemoApi.Application.Features.Orders.Admin
{
    public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineInput> Items) : ICommand<Result<Tagged<OrderResponse>>>;

    public sealed class CreateOrderHandler(IAppDbContext db, ITransactionRunner tx, OrderWriter writer) : ICommandHandler<CreateOrderCommand, Result<Tagged<OrderResponse>>>
    {
        public async Task<Result<Tagged<OrderResponse>>> ExecuteAsync(CreateOrderCommand command, CancellationToken ct)
        {
            if (!await db.Customers.AnyAsync(c => c.Id == command.CustomerId, ct))
            {
                return Error.Validation("customerId", "The customer does not exist.");
            }

            var placed = await tx.ExecuteAsync(token => writer.PlaceAsync(command.CustomerId, command.Items, token), ct);
            if (placed.IsFailure)
            {
                return placed.Error!;
            }

            await writer.InvalidateAsync(command.Items.Select(i => i.ProductId), ct);
            return OrderMappings.ToAdmin(placed.Value);
        }
    }

    public sealed record ListOrdersQuery(PageRequest Page, IReadOnlyList<SortField> Sort, OrderFilter Filter);

    public sealed class ListOrdersHandler(IAppDbContext db) : IQueryHandler<ListOrdersQuery, PagedResponse<OrderListItem>>
    {
        public Task<PagedResponse<OrderListItem>> HandleAsync(ListOrdersQuery q, CancellationToken ct) =>
            OrderQueries.Sort(OrderQueries.Filter(db.Orders.AsNoTracking(), q.Filter), q.Sort)
                .Select(o => new OrderListItem(o.Id, o.CustomerId, o.Status, o.Total, o.CurrencyCode, o.CreatedAt))
                .ToPageAsync(q.Page, ct);
    }

    public sealed record GetOrderQuery(Guid OrderId);

    public sealed class GetOrderHandler(IAppDbContext db) : IQueryHandler<GetOrderQuery, Result<Tagged<OrderResponse>>>
    {
        public async Task<Result<Tagged<OrderResponse>>> HandleAsync(GetOrderQuery query, CancellationToken ct)
        {
            var order = await OrderWriter.WithDetails(db).AsNoTracking().FirstOrDefaultAsync(o => o.Id == query.OrderId, ct);
            return order is null ? Error.NotFound() : OrderMappings.ToAdmin(order);
        }
    }

    public sealed record ReplaceOrderItemsCommand(Guid OrderId, string? IfMatch, IReadOnlyList<OrderLineInput> Items) : ICommand<Result<Tagged<OrderResponse>>>;

    public sealed class ReplaceOrderItemsHandler(IAppDbContext db, ITransactionRunner tx, OrderWriter writer)
        : ICommandHandler<ReplaceOrderItemsCommand, Result<Tagged<OrderResponse>>>
    {
        public async Task<Result<Tagged<OrderResponse>>> ExecuteAsync(ReplaceOrderItemsCommand command, CancellationToken ct)
        {
            var touched = new List<Guid>();
            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var order = await OrderWriter.WithDetails(db).FirstOrDefaultAsync(o => o.Id == command.OrderId, token);
                    if (order is null)
                    {
                        return Result<Order>.Failure(Error.NotFound());
                    }

                    touched.AddRange(order.ActiveItems.Select(i => i.ProductId));
                    return await writer.ReplaceItemsAsync(order, command.IfMatch, command.Items, token);
                },
                ct);
            if (result.IsFailure)
            {
                return result.Error!;
            }

            await writer.InvalidateAsync(touched.Concat(command.Items.Select(i => i.ProductId)), ct);
            return OrderMappings.ToAdmin(result.Value);
        }
    }

    public sealed record ChangeOrderStatusCommand(Guid OrderId, OrderStatus Status, string? Reason) : ICommand<Result>;

    public sealed class ChangeOrderStatusHandler(IAppDbContext db, IDateTimeProvider clock) : ICommandHandler<ChangeOrderStatusCommand, Result>
    {
        public async Task<Result> ExecuteAsync(ChangeOrderStatusCommand command, CancellationToken ct)
        {
            var order = await db.Orders.Include(o => o.History).FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);
            if (order is null)
            {
                return Error.NotFound();
            }

            var changed = order.ChangeStatus(command.Status, command.Reason, clock.UtcNow);
            if (changed.IsFailure)
            {
                return changed;
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.ConcurrencyConflict;
            }

            return Result.Success();
        }
    }

    public sealed record CancelOrderCommand(Guid OrderId, string? Reason) : ICommand<Result>;

    public sealed class CancelOrderHandler(IAppDbContext db, ITransactionRunner tx, OrderWriter writer) : ICommandHandler<CancelOrderCommand, Result>
    {
        public async Task<Result> ExecuteAsync(CancelOrderCommand command, CancellationToken ct)
        {
            var touched = new List<Guid>();
            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var order = await OrderWriter.WithDetails(db).FirstOrDefaultAsync(o => o.Id == command.OrderId, token);
                    if (order is null)
                    {
                        return Result<bool>.Failure(Error.NotFound());
                    }

                    touched.AddRange(order.ActiveItems.Select(i => i.ProductId));
                    var cancelled = await writer.CancelAsync(order, byCustomer: false, command.Reason, token);
                    return cancelled.IsSuccess ? true : Result<bool>.Failure(cancelled.Error!);
                },
                ct);
            if (result.IsFailure)
            {
                return result.Error!;
            }

            await writer.InvalidateAsync(touched, ct);
            return Result.Success();
        }
    }
}

namespace DemoApi.Application.Features.Orders.Me
{
    public sealed record PlaceMyOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineInput> Items) : ICommand<Result<Tagged<MyOrderResponse>>>;

    public sealed class PlaceMyOrderHandler(IAppDbContext db, ITransactionRunner tx, OrderWriter writer)
        : ICommandHandler<PlaceMyOrderCommand, Result<Tagged<MyOrderResponse>>>
    {
        public async Task<Result<Tagged<MyOrderResponse>>> ExecuteAsync(PlaceMyOrderCommand command, CancellationToken ct)
        {
            if (!await db.Customers.AnyAsync(c => c.Id == command.CustomerId, ct))
            {
                return Error.NotFound();
            }

            var placed = await tx.ExecuteAsync(token => writer.PlaceAsync(command.CustomerId, command.Items, token), ct);
            if (placed.IsFailure)
            {
                return placed.Error!;
            }

            await writer.InvalidateAsync(command.Items.Select(i => i.ProductId), ct);
            return OrderMappings.ToCustomer(placed.Value);
        }
    }

    public sealed record ListMyOrdersQuery(Guid CustomerId, PageRequest Page, IReadOnlyList<SortField> Sort, IReadOnlyList<OrderStatus> Statuses, DateTimeOffset? CreatedFrom, DateTimeOffset? CreatedTo);

    public sealed class ListMyOrdersHandler(IAppDbContext db) : IQueryHandler<ListMyOrdersQuery, PagedResponse<MyOrderListItem>>
    {
        public Task<PagedResponse<MyOrderListItem>> HandleAsync(ListMyOrdersQuery q, CancellationToken ct)
        {
            var filter = new OrderFilter(q.Statuses, q.CustomerId, q.CreatedFrom, q.CreatedTo, null, null);
            return OrderQueries.Sort(OrderQueries.Filter(db.Orders.AsNoTracking(), filter), q.Sort)
                .Select(o => new MyOrderListItem(o.Id, o.Status, o.Total, o.CurrencyCode, o.CreatedAt))
                .ToPageAsync(q.Page, ct);
        }
    }

    public sealed record GetMyOrderQuery(Guid CustomerId, Guid OrderId);

    /// <summary>Another customer's order is "unknown": 404, never 403 (BOLA).</summary>
    public sealed class GetMyOrderHandler(IAppDbContext db) : IQueryHandler<GetMyOrderQuery, Result<Tagged<MyOrderResponse>>>
    {
        public async Task<Result<Tagged<MyOrderResponse>>> HandleAsync(GetMyOrderQuery query, CancellationToken ct)
        {
            var order = await OrderWriter.WithDetails(db).AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == query.OrderId && o.CustomerId == query.CustomerId, ct);
            return order is null ? Error.NotFound() : OrderMappings.ToCustomer(order);
        }
    }

    public sealed record CancelMyOrderCommand(Guid CustomerId, Guid OrderId, string? Reason) : ICommand<Result>;

    public sealed class CancelMyOrderHandler(IAppDbContext db, ITransactionRunner tx, OrderWriter writer) : ICommandHandler<CancelMyOrderCommand, Result>
    {
        public async Task<Result> ExecuteAsync(CancelMyOrderCommand command, CancellationToken ct)
        {
            var touched = new List<Guid>();
            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var order = await OrderWriter.WithDetails(db)
                        .FirstOrDefaultAsync(o => o.Id == command.OrderId && o.CustomerId == command.CustomerId, token);
                    if (order is null)
                    {
                        return Result<bool>.Failure(Error.NotFound());
                    }

                    touched.AddRange(order.ActiveItems.Select(i => i.ProductId));
                    var cancelled = await writer.CancelAsync(order, byCustomer: true, command.Reason, token);
                    return cancelled.IsSuccess ? true : Result<bool>.Failure(cancelled.Error!);
                },
                ct);
            if (result.IsFailure)
            {
                return result.Error!;
            }

            await writer.InvalidateAsync(touched, ct);
            return Result.Success();
        }
    }
}