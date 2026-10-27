namespace DemoApi.Application.Common;

/// <summary>Order filters shared by the Admin order list, the customer order list and the order summaries report (EP-39, 46, 50).</summary>
public sealed record OrderFilter(
    IReadOnlyList<OrderStatus> Statuses, Guid? CustomerId, DateTimeOffset? CreatedFrom, DateTimeOffset? CreatedTo, decimal? MinTotal, decimal? MaxTotal);