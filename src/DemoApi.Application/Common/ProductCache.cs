namespace DemoApi.Application.Common;

/// <summary>Product cache keys and invalidation. In Common because order writes change stock (D15).</summary>
public static class ProductCache
{
    public const string GenerationKey = "products:v1:list-gen";
    public static readonly TimeSpan ItemL2 = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ItemL1 = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ListL2 = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ListL1 = TimeSpan.FromSeconds(30);

    public static string Scope(bool admin) => admin ? "admin" : "customer";

    public static string ItemKey(bool admin, Guid id) => $"products:v1:item:{Scope(admin)}:{id:N}";

    public static string ListKey(bool admin, string generation, string normalizedQuery) =>
        $"products:v1:list:{Scope(admin)}:g{generation}:{Hash(normalizedQuery)}";

    public static async Task InvalidateAsync(IAppCache cache, IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var keys = productIds.SelectMany(id => new[] { ItemKey(true, id), ItemKey(false, id) }).ToList();
        if (keys.Count > 0)
        {
            await cache.RemoveAsync(keys, ct);
        }

        await cache.BumpListGenerationAsync(GenerationKey, ct);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}