namespace DemoApi.Domain.Orders;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Completed,
    Cancelled,
}

/// <summary>A requested line: product, quantity and the current product price (snapshot).</summary>
public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);

/// <summary>Order aggregate: lines, total, status machine and status history (FR-080..096).</summary>
public sealed class Order : AuditableEntity
{
    public const int MaxLines = 50;
    public const int MaxQuantity = 1000;
    public const decimal MaxMoney = 9_999_999_999.99m;

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _history = [];

    private Order()
    {
        CurrencyCode = string.Empty;
    }

    private Order(Guid id, Guid customerId, string currencyCode)
        : base(id)
    {
        CustomerId = customerId;
        CurrencyCode = currencyCode;
        Status = OrderStatus.Pending;
    }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal Total { get; private set; }

    public string CurrencyCode { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public IReadOnlyCollection<OrderStatusHistory> History => _history;

    public IEnumerable<OrderItem> ActiveItems => _items.Where(i => !i.IsDeleted);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus status) => status switch
    {
        OrderStatus.Pending => [OrderStatus.Confirmed],
        OrderStatus.Confirmed => [OrderStatus.Shipped],
        OrderStatus.Shipped => [OrderStatus.Completed],
        _ => [],
    };

    public static Result<Order> Create(Guid customerId, string currencyCode, IReadOnlyList<OrderLine> lines)
    {
        var check = ValidateLines(lines);
        if (check is not null)
        {
            return check;
        }

        var order = new Order(Guid.CreateVersion7(), customerId, currencyCode);
        foreach (var line in lines)
        {
            order._items.Add(OrderItem.Create(order.Id, line.ProductId, line.Quantity, line.UnitPrice));
        }

        order.Total = order.ActiveItems.Sum(i => i.LineTotal);
        order._history.Add(OrderStatusHistory.Create(order.Id, null, OrderStatus.Pending, null));
        return order;
    }

    /// <summary>
    /// Replaces the lines of a Pending order (US-203). Returns the stock change per product:
    /// positive = more stock needed, negative = stock to return.
    /// </summary>
    public Result<IReadOnlyDictionary<Guid, int>> ReplaceItems(IReadOnlyList<OrderLine> lines, DateTimeOffset now)
    {
        if (Status != OrderStatus.Pending)
        {
            return OrderErrors.InvalidOrderState(Status);
        }

        var check = ValidateLines(lines);
        if (check is not null)
        {
            return check;
        }

        var delta = new Dictionary<Guid, int>();
        var wanted = lines.ToDictionary(l => l.ProductId);
        foreach (var item in ActiveItems.ToList())
        {
            if (wanted.TryGetValue(item.ProductId, out var line))
            {
                delta[item.ProductId] = line.Quantity - item.Quantity;
                item.ChangeQuantity(line.Quantity);
                wanted.Remove(item.ProductId);
            }
            else
            {
                delta[item.ProductId] = -item.Quantity;
                item.Remove();
            }
        }

        foreach (var line in wanted.Values)
        {
            delta[line.ProductId] = line.Quantity;
            _items.Add(OrderItem.Create(Id, line.ProductId, line.Quantity, line.UnitPrice));
        }

        Total = ActiveItems.Sum(i => i.LineTotal);
        Touch(now);
        return delta;
    }

    public Result ChangeStatus(OrderStatus target, string? reason, DateTimeOffset now)
    {
        if (!NextStatuses(Status).Contains(target))
        {
            return OrderErrors.InvalidStatusTransition(Status, target == OrderStatus.Cancelled);
        }

        SetStatus(target, reason, now);
        return Result.Success();
    }

    /// <summary>Admins cancel up to Confirmed; customers only while Pending (FR-089, FR-094).</summary>
    public Result Cancel(bool byCustomer, string? reason, DateTimeOffset now)
    {
        var allowed = byCustomer
            ? Status == OrderStatus.Pending
            : Status is OrderStatus.Pending or OrderStatus.Confirmed;
        if (!allowed)
        {
            return OrderErrors.InvalidStatusTransition(Status, false);
        }

        SetStatus(OrderStatus.Cancelled, reason, now);
        return Result.Success();
    }

    private void SetStatus(OrderStatus target, string? reason, DateTimeOffset now)
    {
        _history.Add(OrderStatusHistory.Create(Id, Status, target, reason));
        Status = target;
        Touch(now);
    }

    private static Error? ValidateLines(IReadOnlyList<OrderLine> lines)
    {
        if (lines.Count is 0 or > MaxLines)
        {
            return Error.Validation("items", $"An order has 1 to {MaxLines} lines.");
        }

        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
        {
            return Error.Validation("items", "Each product may appear only once.");
        }

        decimal total = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Quantity is < 1 or > MaxQuantity)
            {
                return Error.Validation($"items[{i}].quantity", $"Quantity must be between 1 and {MaxQuantity}.");
            }

            var lineTotal = lines[i].Quantity * lines[i].UnitPrice;
            total += lineTotal;
            if (lineTotal > MaxMoney || total > MaxMoney)
            {
                return new Error("order-total-too-large", "Order total too large", "The order total is above the maximum amount.", ErrorType.Validation)
                {
                    ValidationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["items"] = ["The order total is above the maximum amount."] },
                };
            }
        }

        return null;
    }
}

public sealed class OrderItem : AuditableEntity
{
    private OrderItem()
    {
    }

    private OrderItem(Guid id, Guid orderId, Guid productId, int quantity, decimal unitPrice)
        : base(id)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = quantity * unitPrice;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    internal static OrderItem Create(Guid orderId, Guid productId, int quantity, decimal unitPrice) =>
        new(Guid.CreateVersion7(), orderId, productId, quantity, unitPrice);

    internal void ChangeQuantity(int quantity)
    {
        Quantity = quantity;
        LineTotal = quantity * UnitPrice;
    }

    internal void Remove() => MarkDeleted();
}

public sealed class OrderStatusHistory : AuditableEntity
{
    private OrderStatusHistory()
    {
    }

    private OrderStatusHistory(Guid id, Guid orderId, OrderStatus? fromStatus, OrderStatus toStatus, string? reason)
        : base(id)
    {
        OrderId = orderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
    }

    public Guid OrderId { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    internal static OrderStatusHistory Create(Guid orderId, OrderStatus? from, OrderStatus to, string? reason) =>
        new(Guid.CreateVersion7(), orderId, from, to, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
}

public static class OrderErrors
{
    public static Error InsufficientStock(IReadOnlyList<Guid> productIds) =>
        Error.Conflict("insufficient-stock", "Insufficient stock", "One or more products do not have enough stock.") with
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["productIds"] = productIds },
        };

    public static Error TooManyPendingOrders(int max) =>
        Error.Conflict("too-many-pending-orders", "Too many pending orders", $"The customer already has {max} Pending orders.") with
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["maxPending"] = max },
        };

    public static Error InvalidStatusTransition(OrderStatus current, bool cancelRequested) =>
        Error.Conflict(
            "invalid-status-transition",
            "Invalid status transition",
            cancelRequested
                ? "Use the cancel endpoint to cancel an order."
                : $"The order cannot move to that status from {current}.") with
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["currentStatus"] = current.ToString(),
                ["allowedNextStatuses"] = Order.NextStatuses(current).Select(s => s.ToString()).ToArray(),
            },
        };

    public static Error InvalidOrderState(OrderStatus current) =>
        Error.Conflict("invalid-order-state", "Invalid order state", $"The order is {current}.") with
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["currentStatus"] = current.ToString() },
        };
}