namespace DemoApi.BuildingBlocks.Persistence;

public static class ModelBuilderExtensions
{
    public const string SoftDeleteFilter = "SoftDelete";

    /// <summary>Adds the named "SoftDelete" query filter to every root entity implementing IDeletable.</summary>
    public static ModelBuilder ApplySoftDeleteFilters(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IDeletable).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(IDeletable.IsDeleted)));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(SoftDeleteFilter, Expression.Lambda(body, parameter));
        }

        return modelBuilder;
    }

    /// <summary>Copies [PersonalData] from CLR properties to the EF model annotation.</summary>
    public static ModelBuilder ApplyPersonalDataAnnotations(this ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.PropertyInfo?.GetCustomAttributes(typeof(PersonalDataAttribute), true).Length > 0)
            {
                property.SetAnnotation(AuditAnnotations.PersonalData, true);
            }
        }

        return modelBuilder;
    }
}

/// <summary>ADR-0011: one retry layer (EF execution strategy); ChangeTracker.Clear() per attempt.</summary>
public sealed class TransactionRunner<TContext>(TContext db) : ITransactionRunner
    where TContext : DbContext
{
    public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            (db, work),
            async (_, state, token) =>
            {
                state.db.ChangeTracker.Clear();
                await using var tx = await state.db.Database.BeginTransactionAsync(token);
                var result = await state.work(token);
                if (result.IsSuccess)
                {
                    await state.db.SaveChangesAsync(token);
                    await tx.CommitAsync(token);
                }
                else
                {
                    await tx.RollbackAsync(token);
                    state.db.ChangeTracker.Clear();
                }

                return result;
            },
            verifySucceeded: null,
            ct);
    }
}

/// <summary>Dapper on the DbContext's connection and current transaction (ADR-0011).</summary>
public sealed class SqlRunner<TContext>(TContext db) : ISqlRunner
    where TContext : DbContext
{
    public async Task<T> RunAsync<T>(Func<DbConnection, DbTransaction?, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is { } tx)
        {
            await db.Database.OpenConnectionAsync(ct); // counted open; the transaction keeps the connection alive
            try
            {
                return await work(db.Database.GetDbConnection(), tx.GetDbTransaction(), ct);
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            work,
            async (_, w, token) =>
            {
                await db.Database.OpenConnectionAsync(token);
                try
                {
                    return await w(db.Database.GetDbConnection(), null, token);
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }
            },
            verifySucceeded: null,
            ct);
    }
}

public sealed class DatabaseUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Connection-level circuit breaker (ADR-0011, P10). Counts only failures to open a connection, never
/// business errors, and never retries. While open, connection attempts fail fast with 503.
/// </summary>
public sealed class PostgresCircuitBreaker(TimeProvider time)
{
    private readonly Lock _gate = new();
    private int _failures;
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;

    public int FailureThreshold { get; init; } = 5;

    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(15);

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return time.GetUtcNow() < _openUntil;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _failures = 0;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            if (++_failures >= FailureThreshold)
            {
                _openUntil = time.GetUtcNow() + BreakDuration;
                _failures = 0;
            }
        }
    }
}

public sealed class PostgresCircuitBreakerInterceptor(PostgresCircuitBreaker breaker) : DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        ThrowIfOpen();
        return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        ThrowIfOpen();
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        breaker.RecordSuccess();
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Exception is NpgsqlException or TimeoutException or System.Net.Sockets.SocketException)
        {
            breaker.RecordFailure();
        }

        return base.ConnectionFailedAsync(connection, eventData, cancellationToken);
    }

    private void ThrowIfOpen()
    {
        if (breaker.IsOpen)
        {
            throw new DatabaseUnavailableException("The database circuit breaker is open.");
        }
    }
}

public static class PostgresErrors
{
    public const string UniqueViolation = "23505";
    public const string LockNotAvailable = "55P03";

    public static bool IsUniqueViolation(this DbUpdateException ex, string? constraint = null) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolation } pg
        && (constraint is null || string.Equals(pg.ConstraintName, constraint, StringComparison.Ordinal));
}