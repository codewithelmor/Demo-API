namespace DemoApi.Application.Common;

public static class QueryHelpers
{
    public static async Task<PagedResponse<T>> ToPageAsync<T>(this IQueryable<T> query, PageRequest page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = total == 0 ? [] : await query.Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
        return new PagedResponse<T>(items, page.Page, page.PageSize, total);
    }

    /// <summary>Escapes <c>\ % _</c> for a contains search with <c>ILIKE … ESCAPE '\'</c>.</summary>
    public static string ContainsPattern(string search) =>
        "%" + search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";

    /// <summary>428 when If-Match is missing, 412 when stale (FR-129).</summary>
    public static Error? CheckIfMatch(string? ifMatch, uint version) => ETag.Check(ifMatch, version) switch
    {
        ETagCheck.Missing => Error.PreconditionRequired,
        ETagCheck.NoMatch => Error.PreconditionFailed,
        _ => null,
    };
}