namespace DemoApi.Api.Common;

/// <summary>Admin order filter query shared by EP-39 (orders) and EP-50 (order summaries report).</summary>
public abstract class OrderFilterRequest : PagedRequest
{
    public List<OrderStatus>? Status { get; set; }

    public Guid? CustomerId { get; set; }

    public string? CreatedFrom { get; set; }

    public string? CreatedTo { get; set; }

    public decimal? MinTotal { get; set; }

    public decimal? MaxTotal { get; set; }

    public IReadOnlyList<SortField> ToSort() => ToSort(OrderSortFields.Admin, OrderSortFields.Default);

    public OrderFilter ToFilter() => new(
        Status?.Distinct().ToList() ?? [], CustomerId, Rules.ParseIso(CreatedFrom), Rules.ParseIso(CreatedTo), MinTotal, MaxTotal);
}

/// <summary>Rules for <see cref="OrderFilterRequest"/> (catalog §1.4, EP-39).</summary>
public abstract class OrderFilterValidator<T> : PagedValidator<T>
    where T : OrderFilterRequest
{
    protected OrderFilterValidator()
        : base(OrderSortFields.Admin, OrderSortFields.Default)
    {
        RuleForEach(r => r.Status).IsInEnum();
        RuleFor(r => r.CreatedFrom).IsoDate();
        RuleFor(r => r.CreatedTo).IsoDate()
            .Must((r, to) => Rules.FromBeforeTo(r.CreatedFrom, to)).WithMessage("createdTo must be after createdFrom.");
        RuleFor(r => r.MinTotal).GreaterThanOrEqualTo(0).When(r => r.MinTotal is not null);
        RuleFor(r => r.MaxTotal).GreaterThanOrEqualTo(0).When(r => r.MaxTotal is not null);
        RuleFor(r => r.MaxTotal).Must((r, max) => Rules.MinNotAboveMax(r.MinTotal, max)).WithMessage("maxTotal must not be less than minTotal.");
    }
}