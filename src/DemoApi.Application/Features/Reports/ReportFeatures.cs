namespace DemoApi.Application.Features.Reports
{
    /// <summary>Dapper reads timestamptz as UTC <see cref="DateTime"/>; responses use <see cref="DateTimeOffset"/>.</summary>
    internal static class Utc
    {
        public static DateTimeOffset From(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    public sealed record OrderSummaryItem(
        Guid OrderId, Guid CustomerId, string CustomerName, OrderStatus Status, int ItemCount, int TotalUnits, decimal Total, string Currency, DateTimeOffset CreatedAt);

    public sealed record CustomerOrderHistoryItem(Guid OrderId, OrderStatus Status, int ItemCount, decimal Total, string Currency, DateTimeOffset CreatedAt);

    public sealed record SalesPeriod(DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, long OrderCount, long Units, decimal Revenue);

    public sealed record SalesTotals(long OrderCount, long Units, decimal Revenue);

    public sealed record SalesReportResponse(
        DateTimeOffset From, DateTimeOffset To, string GroupBy, string Currency, IReadOnlyList<SalesPeriod> Periods, SalesTotals Totals);

    public sealed record LowStockItem(Guid Id, string Sku, string Name, int StockQuantity);

    public sealed record TopProductItem(Guid ProductId, string Sku, string Name, long Units, decimal Revenue);

    public sealed record TopProductsResponse(DateTimeOffset From, DateTimeOffset To, string Currency, IReadOnlyList<TopProductItem> Items);

    public sealed record AuditChange(string Field, string? Old, string? New);

    public sealed record AuditLogItem(
        Guid Id, string EntityType, string EntityId, string Action, IReadOnlyList<AuditChange> Changes, string UserId, DateTimeOffset Timestamp, string? TraceId);
}

namespace DemoApi.Application.Features.Reports.OrderSummaries
{
    public sealed record OrderSummariesQuery(PageRequest Page, IReadOnlyList<SortField> Sort, OrderFilter Filter);

    /// <summary>
    /// EP-50 over <c>v_order_summaries</c>. SQL is built only from the constant fragments below;
    /// every value is a parameter (ADR-0011).
    /// </summary>
    public sealed class OrderSummariesHandler(ISqlRunner sql) : IQueryHandler<OrderSummariesQuery, PagedResponse<OrderSummaryItem>>
    {
        private static readonly Dictionary<string, string> SortColumns = new(StringComparer.Ordinal)
        {
            ["createdAt"] = "created_at",
            ["total"] = "total",
            ["status"] = "status",
        };

        public Task<PagedResponse<OrderSummaryItem>> HandleAsync(OrderSummariesQuery q, CancellationToken ct)
        {
            var where = new StringBuilder(" WHERE true");
            var f = q.Filter;
            if (f.Statuses.Count > 0)
            {
                where.Append(" AND status = ANY(@Statuses)");
            }

            if (f.CustomerId is not null)
            {
                where.Append(" AND customer_id = @CustomerId");
            }

            if (f.CreatedFrom is not null)
            {
                where.Append(" AND created_at >= @CreatedFrom");
            }

            if (f.CreatedTo is not null)
            {
                where.Append(" AND created_at < @CreatedTo");
            }

            if (f.MinTotal is not null)
            {
                where.Append(" AND total >= @MinTotal");
            }

            if (f.MaxTotal is not null)
            {
                where.Append(" AND total <= @MaxTotal");
            }

            var orderBy = string.Join(", ", q.Sort.Select(s => SortColumns[s.Name] + (s.Descending ? " DESC" : " ASC"))) + ", order_id";
            var args = new
            {
                Statuses = f.Statuses.Select(s => s.ToString()).ToArray(),
                f.CustomerId,
                f.CreatedFrom,
                f.CreatedTo,
                f.MinTotal,
                f.MaxTotal,
                Take = q.Page.PageSize,
                Skip = q.Page.Offset,
            };

            return sql.RunAsync(
                async (conn, tx, token) =>
                {
                    var total = await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition("SELECT count(*)::int FROM public.v_order_summaries" + where, args, tx, cancellationToken: token));
                    var rows = total == 0
                        ? []
                        : await conn.QueryAsync<Row>(new CommandDefinition(
                            "SELECT order_id, customer_id, customer_first_name, customer_last_name, status, item_count, total_units, total, currency_code, created_at " +
                            "FROM public.v_order_summaries" + where + " ORDER BY " + orderBy + " LIMIT @Take OFFSET @Skip",
                            args,
                            tx,
                            cancellationToken: token));
                    var items = rows.Select(r => new OrderSummaryItem(
                        r.OrderId, r.CustomerId, $"{r.CustomerFirstName} {r.CustomerLastName}", Enum.Parse<OrderStatus>(r.Status),
                        r.ItemCount, r.TotalUnits, r.Total, r.CurrencyCode, Reports.Utc.From(r.CreatedAt))).ToList();
                    return new PagedResponse<OrderSummaryItem>(items, q.Page.Page, q.Page.PageSize, total);
                },
                ct);
        }

        private sealed class Row
        {
            public Guid OrderId { get; set; }

            public Guid CustomerId { get; set; }

            public string CustomerFirstName { get; set; } = string.Empty;

            public string CustomerLastName { get; set; } = string.Empty;

            public string Status { get; set; } = string.Empty;

            public int ItemCount { get; set; }

            public int TotalUnits { get; set; }

            public decimal Total { get; set; }

            public string CurrencyCode { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; }
        }
    }
}

namespace DemoApi.Application.Features.Reports.CustomerOrderHistory
{
    public sealed record CustomerOrderHistoryQuery(Guid CustomerId, PageRequest Page);

    /// <summary>EP-51 over <c>fn_customer_order_history</c>; 404 for unknown or deleted customers.</summary>
    public sealed class CustomerOrderHistoryHandler(IAppDbContext db, ISqlRunner sql)
        : IQueryHandler<CustomerOrderHistoryQuery, Result<PagedResponse<CustomerOrderHistoryItem>>>
    {
        public async Task<Result<PagedResponse<CustomerOrderHistoryItem>>> HandleAsync(CustomerOrderHistoryQuery q, CancellationToken ct)
        {
            if (!await db.Customers.AnyAsync(c => c.Id == q.CustomerId, ct))
            {
                return Error.NotFound();
            }

            return await sql.RunAsync(
                async (conn, tx, token) =>
                {
                    var rows = (await conn.QueryAsync<Row>(new CommandDefinition(
                        "SELECT total_count, order_id, status, item_count, total, currency_code, created_at " +
                        "FROM public.fn_customer_order_history(@CustomerId, @Page, @PageSize)",
                        new { q.CustomerId, q.Page.Page, q.Page.PageSize },
                        tx,
                        cancellationToken: token))).ToList();
                    var total = rows.Count == 0 ? 0 : (int)rows[0].TotalCount;
                    var items = rows.Where(r => r.OrderId is not null).Select(r => new CustomerOrderHistoryItem(
                        r.OrderId!.Value, Enum.Parse<OrderStatus>(r.Status!), r.ItemCount ?? 0, r.Total ?? 0, r.CurrencyCode!, Utc.From(r.CreatedAt!.Value))).ToList();
                    return new PagedResponse<CustomerOrderHistoryItem>(items, q.Page.Page, q.Page.PageSize, total);
                },
                ct);
        }

        private sealed class Row
        {
            public long TotalCount { get; set; }

            public Guid? OrderId { get; set; }

            public string? Status { get; set; }

            public int? ItemCount { get; set; }

            public decimal? Total { get; set; }

            public string? CurrencyCode { get; set; }

            public DateTime? CreatedAt { get; set; }
        }
    }
}

namespace DemoApi.Application.Features.Reports.Sales
{
    public static class SalesGroupBy
    {
        public static readonly string[] Allowed = ["day", "week", "month"];
    }

    public sealed record SalesReportQuery(DateTimeOffset From, DateTimeOffset To, string GroupBy);

    public sealed class SalesReportHandler(ISqlRunner sql, AppSettings settings) : IQueryHandler<SalesReportQuery, SalesReportResponse>
    {
        public Task<SalesReportResponse> HandleAsync(SalesReportQuery q, CancellationToken ct) =>
            sql.RunAsync(
                async (conn, tx, token) =>
                {
                    var rows = await conn.QueryAsync<Row>(new CommandDefinition(
                        "SELECT period_start, period_end, order_count, units, revenue FROM public.fn_sales_report(@From, @To, @GroupBy)",
                        new { From = q.From.UtcDateTime, To = q.To.UtcDateTime, q.GroupBy },
                        tx,
                        cancellationToken: token));
                    var periods = rows.Select(r => new SalesPeriod(Utc.From(r.PeriodStart), Utc.From(r.PeriodEnd), r.OrderCount, r.Units, r.Revenue)).ToList();
                    var totals = new SalesTotals(periods.Sum(p => p.OrderCount), periods.Sum(p => p.Units), periods.Sum(p => p.Revenue));
                    return new SalesReportResponse(q.From, q.To, q.GroupBy, settings.CurrencyCode, periods, totals);
                },
                ct);

        private sealed class Row
        {
            public DateTime PeriodStart { get; set; }

            public DateTime PeriodEnd { get; set; }

            public long OrderCount { get; set; }

            public long Units { get; set; }

            public decimal Revenue { get; set; }
        }
    }
}

namespace DemoApi.Application.Features.Reports.LowStock
{
    public sealed record LowStockQuery(int? Threshold, PageRequest Page);

    /// <summary>EP-53 (FR-103): non-deleted products with stock ≤ threshold, stock ascending.</summary>
    public sealed class LowStockHandler(ISqlRunner sql, AppSettings settings) : IQueryHandler<LowStockQuery, PagedResponse<LowStockItem>>
    {
        public Task<PagedResponse<LowStockItem>> HandleAsync(LowStockQuery q, CancellationToken ct)
        {
            var args = new { Threshold = q.Threshold ?? settings.LowStockThreshold, Take = q.Page.PageSize, Skip = q.Page.Offset };
            return sql.RunAsync(
                async (conn, tx, token) =>
                {
                    const string Where = " FROM public.products WHERE is_deleted = false AND stock_quantity <= @Threshold";
                    var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT count(*)::int" + Where, args, tx, cancellationToken: token));
                    var items = total == 0
                        ? []
                        : (await conn.QueryAsync<LowStockItem>(new CommandDefinition(
                            "SELECT id, sku, name, stock_quantity AS stockquantity" + Where + " ORDER BY stock_quantity, id LIMIT @Take OFFSET @Skip",
                            args,
                            tx,
                            cancellationToken: token))).ToList();
                    return new PagedResponse<LowStockItem>(items, q.Page.Page, q.Page.PageSize, total);
                },
                ct);
        }
    }
}

namespace DemoApi.Application.Features.Reports.TopProducts
{
    public sealed record TopProductsQuery(DateTimeOffset From, DateTimeOffset To, int Top);

    public sealed class TopProductsHandler(ISqlRunner sql, AppSettings settings) : IQueryHandler<TopProductsQuery, TopProductsResponse>
    {
        public Task<TopProductsResponse> HandleAsync(TopProductsQuery q, CancellationToken ct) =>
            sql.RunAsync(
                async (conn, tx, token) =>
                {
                    var items = (await conn.QueryAsync<TopProductItem>(new CommandDefinition(
                        "SELECT product_id AS productid, sku, name, units, revenue FROM public.fn_top_products(@From, @To, @Top)",
                        new { From = q.From.UtcDateTime, To = q.To.UtcDateTime, q.Top },
                        tx,
                        cancellationToken: token))).ToList();
                    return new TopProductsResponse(q.From, q.To, settings.CurrencyCode, items);
                },
                ct);
    }
}

namespace DemoApi.Application.Features.Reports.AuditLogs
{
    public sealed record AuditLogQuery(
        PageRequest Page, string? EntityType, string? EntityId, string? UserId, string? Action, DateTimeOffset? From, DateTimeOffset? To);

    /// <summary>EP-55: newest first. Logs security event 9030 with IDs and codes only (S-D5).</summary>
    public sealed partial class AuditLogQueryHandler(ISqlRunner sql, ICurrentUser user, ILogger<AuditLogQueryHandler> logger)
        : IQueryHandler<AuditLogQuery, PagedResponse<AuditLogItem>>
    {
        public Task<PagedResponse<AuditLogItem>> HandleAsync(AuditLogQuery q, CancellationToken ct)
        {
            LogQueried(logger, user.AuditName, q.EntityType, q.EntityId, q.UserId, q.Action);
            var where = new StringBuilder(" WHERE true");
            if (q.EntityType is not null)
            {
                where.Append(" AND entity_type = @EntityType");
            }

            if (q.EntityId is not null)
            {
                where.Append(" AND entity_id = @EntityId");
            }

            if (q.UserId is not null)
            {
                where.Append(" AND user_id = @UserId");
            }

            if (q.Action is not null)
            {
                where.Append(" AND action = @Action");
            }

            if (q.From is not null)
            {
                where.Append(" AND occurred_at >= @From");
            }

            if (q.To is not null)
            {
                where.Append(" AND occurred_at < @To");
            }

            var args = new { q.EntityType, q.EntityId, q.UserId, q.Action, q.From, q.To, Take = q.Page.PageSize, Skip = q.Page.Offset };
            return sql.RunAsync(
                async (conn, tx, token) =>
                {
                    var total = await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition("SELECT count(*)::int FROM public.audit_logs" + where, args, tx, cancellationToken: token));
                    var rows = total == 0
                        ? []
                        : await conn.QueryAsync<Row>(new CommandDefinition(
                            "SELECT id, entity_type, entity_id, action, changes::text AS changes, user_id, occurred_at, trace_id FROM public.audit_logs" +
                            where + " ORDER BY occurred_at DESC, id DESC LIMIT @Take OFFSET @Skip",
                            args,
                            tx,
                            cancellationToken: token));
                    var items = rows.Select(r => new AuditLogItem(
                        r.Id, r.EntityType, r.EntityId, r.Action, ParseChanges(r.Changes), r.UserId, Utc.From(r.OccurredAt), r.TraceId)).ToList();
                    return new PagedResponse<AuditLogItem>(items, q.Page.Page, q.Page.PageSize, total);
                },
                ct);
        }

        private static List<AuditChange> ParseChanges(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateObject()
                .Select(p => new AuditChange(p.Name, Value(p.Value, "old"), Value(p.Value, "new")))
                .ToList();
        }

        private static string? Value(JsonElement change, string name) =>
            change.ValueKind == JsonValueKind.Object && change.TryGetProperty(name, out var v)
                ? v.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => v.GetString(),
                    _ => v.GetRawText(),
                }
                : null;

        [LoggerMessage(EventId = 9030, EventName = "AuditLogQueried", Level = LogLevel.Information,
            Message = "AuditLogQueried by {ActorId} entityType={EntityType} entityId={EntityId} userId={FilterUserId} action={Action}")]
        private static partial void LogQueried(ILogger logger, string actorId, string? entityType, string? entityId, string? filterUserId, string? action);

        private sealed class Row
        {
            public Guid Id { get; set; }

            public string EntityType { get; set; } = string.Empty;

            public string EntityId { get; set; } = string.Empty;

            public string Action { get; set; } = string.Empty;

            public string Changes { get; set; } = "{}";

            public string UserId { get; set; } = string.Empty;

            public DateTime OccurredAt { get; set; }

            public string? TraceId { get; set; }
        }
    }
}