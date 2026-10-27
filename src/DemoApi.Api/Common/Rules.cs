namespace DemoApi.Api.Common;

/// <summary>Field rules shared by request validators (endpoint catalog §1.5, D5).</summary>
internal static partial class Rules
{
    public const int NameMax = 100;
    public const int EmailMax = 256;
    public const int PhoneMax = 20;
    public const int PasswordMin = 15;
    public const int PasswordMax = 128;
    public const int PasswordInputMax = 1024;
    public const int TokenMax = 2000;
    public const int RefreshTokenMax = 200;
    public const int ReasonMax = 500;
    public const int SearchMin = 2;
    public const int SearchMax = 100;

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(EmailMax).EmailAddress().WithMessage("Enter a valid email address.");

    public static IRuleBuilderOptions<T, string> Name<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("The value is required.")
            .Must(v => v is null || v.Trim().Length <= NameMax).WithMessage($"Use at most {NameMax} characters.");

    public static IRuleBuilderOptions<T, string?> Phone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || (v.Length <= PhoneMax && PhonePattern().IsMatch(v)))
            .WithMessage("Use the E.164 format, for example +15551234567.");

    /// <summary>15-128 Unicode code points (FR-003). The blocklist runs in the Identity validator.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(PasswordInputMax)
            .Must(v => v is null || CodePoints(v) is >= PasswordMin and <= PasswordMax)
            .WithMessage($"Use {PasswordMin} to {PasswordMax} characters.");

    public static IRuleBuilderOptions<T, string?> Reason<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(ReasonMax);

    public static IRuleBuilderOptions<T, string?> IsoDate<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || IsoDates.TryParse(v, out _))
            .WithMessage("Use an ISO 8601 date and time with an offset, for example 2026-10-24T09:15:00Z.");

    public static IRuleBuilderOptions<T, string?> Search<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || v.Trim().Length is >= SearchMin and <= SearchMax)
            .WithMessage($"Use {SearchMin} to {SearchMax} characters.");

    public static IRuleBuilderOptions<T, List<OrderItemRequest>> Items<T>(this IRuleBuilder<T, List<OrderItemRequest>> rule) =>
        rule.NotNull()
            .Must(v => v is { Count: >= 1 and <= 50 }).WithMessage("An order has 1 to 50 lines.")
            .Must(v => v is null || v.Select(i => i.ProductId).Distinct().Count() == v.Count).WithMessage("Each product may appear only once.");

    public static DateTimeOffset? ParseIso(string? value) => IsoDates.TryParse(value, out var parsed) ? parsed : null;

    /// <summary>True unless both dates parse and <paramref name="from"/> is not before <paramref name="to"/> (catalog §1.4).</summary>
    public static bool FromBeforeTo(string? from, string? to) =>
        ParseIso(from) is not { } f || ParseIso(to) is not { } t || f < t;

    /// <summary>True unless both dates parse and the range is longer than <paramref name="days"/>.</summary>
    public static bool RangeAtMost(string? from, string? to, int days) =>
        ParseIso(from) is not { } f || ParseIso(to) is not { } t || t - f <= TimeSpan.FromDays(days);

    public static bool MinNotAboveMax(decimal? min, decimal? max) => min is null || max is null || min <= max;

    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(AppClaims.Subject)?.Value, out var id) ? id : Guid.Empty;

    public static Guid CustomerId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(AppClaims.CustomerId)?.Value, out var id) ? id : Guid.Empty;

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

    private static int CodePoints(string value)
    {
        var count = 0;
        var e = value.EnumerateRunes();
        while (e.MoveNext())
        {
            count++;
        }

        return count;
    }

    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PhonePattern();
}

/// <summary>One order line in a request body (catalog §1.5).</summary>
public sealed class OrderItemRequest
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public static List<DemoApi.Application.Features.Orders.OrderLineInput> ToInput(List<OrderItemRequest> items) =>
        items.Select(i => new DemoApi.Application.Features.Orders.OrderLineInput(i.ProductId, i.Quantity)).ToList();
}

/// <summary>Validates one order line.</summary>
public sealed class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(i => i.ProductId).NotEmpty();
        RuleFor(i => i.Quantity).InclusiveBetween(1, 1000);
    }
}

/// <summary>Query paging and sorting (catalog §1.4).</summary>
public abstract class PagedRequest
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? Sort { get; set; }

    public PageRequest ToPage() => new(Page ?? 1, PageSize ?? PageRequest.DefaultPageSize);

    public IReadOnlyList<SortField> ToSort(IReadOnlyCollection<string> allowed, string defaultSort) =>
        SortParser.TryParse(Sort, allowed, defaultSort, out var fields, out _) ? fields : [];
}

/// <summary>Paging rules; the sort allow-list comes from the endpoint.</summary>
public abstract class PagedValidator<T> : Validator<T>
    where T : PagedRequest
{
    protected PagedValidator(IReadOnlyCollection<string>? sortFields = null, string? defaultSort = null)
    {
        RuleFor(r => r.Page).InclusiveBetween(1, PageRequest.MaxPage).When(r => r.Page is not null);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).When(r => r.PageSize is not null);
        if (sortFields is not null)
        {
            RuleFor(r => r.Sort).Custom((sort, ctx) =>
            {
                if (!SortParser.TryParse(sort, sortFields, defaultSort!, out _, out var error))
                {
                    ctx.AddFailure("sort", error!);
                }
            });
        }
    }
}