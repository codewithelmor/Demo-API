namespace DemoApi.SharedKernel.Time
{
    /// <summary>The only source of the current time (FR-135). Always UTC.</summary>
    public interface IDateTimeProvider
    {
        DateTimeOffset UtcNow { get; }
    }
}

namespace DemoApi.SharedKernel.Security
{
    /// <summary>The caller, read from validated token claims only.</summary>
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }

        Guid? UserId { get; }

        /// <summary>From the <c>customer_id</c> claim. Template strip point (solution-structure §13).</summary>
        Guid? CustomerId { get; }

        bool IsAdmin { get; }

        /// <summary>User ID as text, or <c>anonymous</c> / <c>system</c> for the audit log.</summary>
        string AuditName { get; }
    }
}

namespace DemoApi.SharedKernel.Caching
{
    /// <summary>Product cache (ADR-0009). Failures behave as a miss; they never throw.</summary>
    public interface IAppCache
    {
        Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan l2Ttl, TimeSpan l1Ttl, CancellationToken ct);

        Task RemoveAsync(IEnumerable<string> keys, CancellationToken ct);

        Task<string> GetListGenerationAsync(string generationKey, CancellationToken ct);

        Task BumpListGenerationAsync(string generationKey, CancellationToken ct);
    }
}

namespace DemoApi.SharedKernel.Data
{
    /// <summary>Runs Dapper SQL on the DbContext's connection and transaction (ADR-0011).</summary>
    public interface ISqlRunner
    {
        Task<T> RunAsync<T>(Func<DbConnection, DbTransaction?, CancellationToken, Task<T>> work, CancellationToken ct);
    }

    /// <summary>
    /// Runs a unit of work in a transaction inside the EF execution strategy. Each attempt clears the
    /// change tracker. Commits on a success result, rolls back on a failure result.
    /// </summary>
    public interface ITransactionRunner
    {
        Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct);
    }
}

namespace DemoApi.SharedKernel.Queries
{
    /// <summary>Read side: endpoints inject query handlers directly (no bus).</summary>
    public interface IQueryHandler<in TQuery, TResult>
    {
        Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
    }
}