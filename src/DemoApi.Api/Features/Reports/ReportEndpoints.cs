


using AuthPolicies = DemoApi.BuildingBlocks.Security.Policies;

namespace DemoApi.Api.Features.Reports;

// EP-50
public sealed class OrderSummariesRequest : OrderFilterRequest;

public sealed class OrderSummariesValidator : OrderFilterValidator<OrderSummariesRequest>;

public sealed class GetOrderSummariesEndpoint(IQueryHandler<OrderSummariesQuery, PagedResponse<OrderSummaryItem>> handler)
    : Endpoint<OrderSummariesRequest, PagedResponse<OrderSummaryItem>>
{
    public override void Configure()
    {
        Get("/reports/order-summaries");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Order summaries report";
            s.Description = "Paged order summaries with customer name, line and unit counts. Same filters and sort as the order list. Deleted customers stay in the report (anonymized).";
        });
    }

    public override async Task HandleAsync(OrderSummariesRequest r, CancellationToken ct) =>
        await Send.OkAsync(await handler.HandleAsync(new OrderSummariesQuery(r.ToPage(), r.ToSort(), r.ToFilter()), ct), ct);
}

// EP-51
public sealed class CustomerOrderHistoryRequest : PagedRequest
{
    public Guid CustomerId { get; set; }
}

public sealed class CustomerOrderHistoryValidator : PagedValidator<CustomerOrderHistoryRequest>;

public sealed class GetCustomerOrderHistoryEndpoint(IQueryHandler<CustomerOrderHistoryQuery, Result<PagedResponse<CustomerOrderHistoryItem>>> handler)
    : Endpoint<CustomerOrderHistoryRequest, PagedResponse<CustomerOrderHistoryItem>>
{
    public override void Configure()
    {
        Get("/customers/{customerId}/order-history");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Customer order history";
            s.Description = "Paged order history of one customer, newest first (sort is ignored). 404 for an unknown or deleted customer.";
        });
    }

    public override async Task HandleAsync(CustomerOrderHistoryRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await handler.HandleAsync(new CustomerOrderHistoryQuery(r.CustomerId, r.ToPage()), ct)).ToOk(HttpContext));
}

// EP-52, EP-54
public abstract class DateRangeRequest
{
    public const int MaxDays = 366;

    public string? From { get; set; }

    public string? To { get; set; }
}

public abstract class DateRangeValidator<T> : Validator<T>
    where T : DateRangeRequest
{
    protected DateRangeValidator()
    {
        RuleFor(r => r.From).NotEmpty().IsoDate();
        RuleFor(r => r.To).NotEmpty().IsoDate()
            .Must((r, to) => Rules.FromBeforeTo(r.From, to)).WithMessage("to must be after from.")
            .Must((r, to) => Rules.RangeAtMost(r.From, to, DateRangeRequest.MaxDays)).WithMessage($"The range may be at most {DateRangeRequest.MaxDays} days.");
    }
}

public sealed class SalesReportRequest : DateRangeRequest
{
    public string? GroupBy { get; set; }
}

public sealed class SalesReportValidator : DateRangeValidator<SalesReportRequest>
{
    public SalesReportValidator() =>
        RuleFor(r => r.GroupBy).Must(g => g is null || SalesGroupBy.Allowed.Contains(g, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Use one of: {string.Join(", ", SalesGroupBy.Allowed)}.");
}

public sealed class GetSalesReportEndpoint(IQueryHandler<SalesReportQuery, SalesReportResponse> handler) : Endpoint<SalesReportRequest, SalesReportResponse>
{
    public override void Configure()
    {
        Get("/reports/sales");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Sales report";
            s.Description = "Order count, units and revenue per day, week (ISO, Monday) or month in UTC for Confirmed, Shipped and Completed orders. from and to are required, from < to, at most 366 days.";
        });
    }

    public override async Task HandleAsync(SalesReportRequest r, CancellationToken ct)
    {
        var query = new SalesReportQuery(Rules.ParseIso(r.From)!.Value, Rules.ParseIso(r.To)!.Value, r.GroupBy?.ToLowerInvariant() ?? "day");
        await Send.OkAsync(await handler.HandleAsync(query, ct), ct);
    }
}

// EP-53
public sealed class LowStockRequest : PagedRequest
{
    public int? Threshold { get; set; }
}

public sealed class LowStockValidator : PagedValidator<LowStockRequest>
{
    public LowStockValidator() => RuleFor(r => r.Threshold).InclusiveBetween(0, 1_000_000).When(r => r.Threshold is not null);
}

public sealed class GetLowStockReportEndpoint(IQueryHandler<LowStockQuery, PagedResponse<LowStockItem>> handler) : Endpoint<LowStockRequest, PagedResponse<LowStockItem>>
{
    public override void Configure()
    {
        Get("/reports/low-stock");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Low stock report";
            s.Description = "Paged active products with stock at or below the threshold (default from configuration), lowest stock first (sort is ignored).";
        });
    }

    public override async Task HandleAsync(LowStockRequest r, CancellationToken ct) =>
        await Send.OkAsync(await handler.HandleAsync(new LowStockQuery(r.Threshold, r.ToPage()), ct), ct);
}

// EP-54
public sealed class TopProductsRequest : DateRangeRequest
{
    public const int DefaultTop = 10;

    public int? Top { get; set; }
}

public sealed class TopProductsValidator : DateRangeValidator<TopProductsRequest>
{
    public TopProductsValidator() => RuleFor(r => r.Top).InclusiveBetween(1, 50).When(r => r.Top is not null);
}

public sealed class GetTopProductsReportEndpoint(IQueryHandler<TopProductsQuery, TopProductsResponse> handler) : Endpoint<TopProductsRequest, TopProductsResponse>
{
    public override void Configure()
    {
        Get("/reports/top-products");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Top products report";
            s.Description = "Best-selling products by units, then revenue, for Confirmed, Shipped and Completed orders in the range. top is 1-50 (default 10); not paged.";
        });
    }

    public override async Task HandleAsync(TopProductsRequest r, CancellationToken ct)
    {
        var query = new TopProductsQuery(Rules.ParseIso(r.From)!.Value, Rules.ParseIso(r.To)!.Value, r.Top ?? TopProductsRequest.DefaultTop);
        await Send.OkAsync(await handler.HandleAsync(query, ct), ct);
    }
}

// EP-55
public sealed class ListAuditLogsRequest : PagedRequest
{
    public static readonly string[] Actions = ["Insert", "Update", "SoftDelete", "Restore", "RoleAssigned", "RoleRemoved", "SessionsRevoked"];

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? UserId { get; set; }

    public string? Action { get; set; }

    public string? From { get; set; }

    public string? To { get; set; }

    /// <summary>Audit rows store user IDs as lower-case "D" GUIDs, or anonymous/system.</summary>
    public string? CanonicalUserId() =>
        UserId is null ? null : Guid.TryParse(UserId, out var id) ? id.ToString() : UserId.ToLowerInvariant();

    public string? CanonicalAction() =>
        Action is null ? null : Actions.FirstOrDefault(a => string.Equals(a, Action, StringComparison.OrdinalIgnoreCase));
}

public sealed class ListAuditLogsValidator : PagedValidator<ListAuditLogsRequest>
{
    public ListAuditLogsValidator()
    {
        RuleFor(r => r.EntityType).MaximumLength(100);
        RuleFor(r => r.EntityId).MaximumLength(100);
        RuleFor(r => r.UserId).MaximumLength(64)
            .Must(u => u is null || Guid.TryParse(u, out _) || string.Equals(u, "anonymous", StringComparison.OrdinalIgnoreCase) || string.Equals(u, "system", StringComparison.OrdinalIgnoreCase)).WithMessage("Use a user ID, anonymous or system.");
        RuleFor(r => r.Action).Must(a => a is null || ListAuditLogsRequest.Actions.Contains(a, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Use one of: {string.Join(", ", ListAuditLogsRequest.Actions)}.");
        RuleFor(r => r.From).IsoDate();
        RuleFor(r => r.To).IsoDate().Must((r, to) => Rules.FromBeforeTo(r.From, to)).WithMessage("to must be after from.");
    }
}

public sealed class ListAuditLogsEndpoint(IQueryHandler<AuditLogQuery, PagedResponse<AuditLogItem>> handler) : Endpoint<ListAuditLogsRequest, PagedResponse<AuditLogItem>>
{
    public override void Configure()
    {
        Get("/audit-logs");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "List audit logs";
            s.Description = "Paged audit entries, newest first (sort is ignored). Filters: entityType, entityId, userId, action, from (inclusive), to (exclusive). Personal data appears only as \"changed\". Each call is logged as a security event.";
        });
    }

    public override async Task HandleAsync(ListAuditLogsRequest r, CancellationToken ct)
    {
        var query = new AuditLogQuery(
            r.ToPage(), r.EntityType, r.EntityId, r.CanonicalUserId(), r.CanonicalAction(), Rules.ParseIso(r.From), Rules.ParseIso(r.To));
        await Send.OkAsync(await handler.HandleAsync(query, ct), ct);
    }
}