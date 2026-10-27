namespace DemoApi.SharedKernel.Paging;

public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Keeps <c>(Page - 1) * PageSize</c> inside <see cref="int"/> and the OFFSET scan bounded.</summary>
    public const int MaxPage = 100_000;

    public int Offset => (Page - 1) * PageSize;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record SortField(string Name, bool Descending);

/// <summary>
/// Parses <c>sort=field,-field</c> against an allow-list (FR-122). Max 3 fields; the caller adds <c>id</c>
/// as the final tiebreaker.
/// </summary>
public static class SortParser
{
    public const int MaxFields = 3;

    public static bool TryParse(
        string? sort,
        IReadOnlyCollection<string> allowed,
        string defaultSort,
        out IReadOnlyList<SortField> fields,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        var input = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort;
        var parts = input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var result = new List<SortField>(parts.Length);
        fields = result;
        error = null;

        if (parts.Length == 0 || parts.Length > MaxFields)
        {
            error = $"Use 1 to {MaxFields} sort fields. Allowed: {string.Join(", ", allowed)}.";
            return false;
        }

        foreach (var part in parts)
        {
            var descending = part.StartsWith('-');
            var name = descending ? part[1..] : part;
            var match = allowed.FirstOrDefault(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            if (match is null || result.Any(f => f.Name == match))
            {
                error = $"Unknown or repeated sort field '{name}'. Allowed: {string.Join(", ", allowed)}.";
                return false;
            }

            result.Add(new SortField(match, descending));
        }

        return true;
    }
}