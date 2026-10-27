# ADR-0011: One retry layer per dependency with Microsoft.Extensions.Resilience and the EF Core execution strategy

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Architect |
| Consulted | PRD NFR-03, FR-084, FR-130, P3, P10; pre-mortem F3; architecture review C1, C3 |

Revised 2026-10-27 (architecture review R-1, R-3): the database breaker is now a connection-level `DbConnectionInterceptor`; the custom execution strategy and the AsyncLocal nesting flag are removed; connect timeout is 1 s; `ChangeTracker.Clear()` runs at the start of each attempt.

## Context
NFR-03: transient DB and cache errors retried up to 3 times with exponential backoff and jitter; cache calls time out after ≤ 250 ms; circuit breaker opens at ≥ 50% failures over ≥ 10 calls in 30 s and stays open 30 s; DB unavailable returns 503 within 5 s. P3/FR-084: exactly one retry layer for transient DB errors; stock uses `SELECT … FOR UPDATE`, not retry loops. P10 keeps a DB circuit breaker. The pre-mortem (F3) failed on retries on retries: EF's retrying execution strategy plus a resilience pipeline retry plus a concurrency retry. Polly must not be referenced directly (fee on the next major); Microsoft.Extensions.Resilience 10.11.0 (MIT) brings it in transitively.

The architecture review found two defects in the first version. C1: `PostgresException` (unique violation `23505`, check violation `23514`, lock timeout `55P03`) derives from `NpgsqlException`, so a burst of 409s could open the breaker and return 503 everywhere for 30 s. C3: EF Core does not clear tracked entities between execution-strategy attempts, so audit rows or Identity users from a failed attempt would be saved again.

## Decision drivers
- Exactly one retry layer per dependency (P3, F3).
- User transactions (order create, register, erase) must retry as a whole or not at all, with no leftovers from the failed attempt.
- Business errors (409, 412) must never open the breaker.
- Product reads must survive Valkey being down with no 5xx (G3, NFR-01).
- Fail fast with 503 when PostgreSQL is down (NFR-03).
- No direct Polly reference. Least custom code.

## Options considered (database)

### Option 1: EF Core execution strategy only (`EnableRetryOnFailure`)
- Good: knows Npgsql transient errors; retries whole user transactions when wrapped in `CreateExecutionStrategy().ExecuteAsync`; jittered exponential backoff.
- Bad: no circuit breaker (required by P10).

### Option 2: Resilience pipeline retry + breaker around DB calls; EF strategy off
- Good: one library for all policies.
- Bad: must reimplement Npgsql transient detection; a retry inside a user transaction replays half a transaction unless every call site is wrapped by hand. High risk of F3.

### Option 3: Custom execution strategy that runs the outermost call inside a timeout + breaker pipeline (first version)
- Good: one retry layer; breaker around whole operations.
- Bad: custom strategy subclass plus an AsyncLocal nesting flag; counts every failed operation, including business errors (C1). Rejected by the review.

### Option 4: Plain `EnableRetryOnFailure` + a connection-level breaker in a `DbConnectionInterceptor` (chosen)
- Good: one retry layer; the breaker sees only connection opens, so SQL errors (constraints, locks) can never trip it; no custom strategy; Dapper uses the same connection path.
- Bad: a DB that drops mid-query is not counted until the next open fails (acceptable: the next open does fail).

## Decision
**Database (Option 4).**
- **Retry:** `UseNpgsql(dataSource, o => o.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: 250 ms))`. This is the only DB retry layer. Assumption: Npgsql's delay calculation is exponential with random jitter, capped by `maxRetryDelay`; verify in S1a.
- **Connect timeout:** Npgsql `Timeout=1` (1 s). Worst case with an unreachable host: 4 attempts × 1 s + 3 delays × ≤ 250 ms ≈ 4.75 s, so "DB down → 503 within 5 s" holds. A stopped container refuses connections at once, which is faster. Once the breaker is open, requests fail in milliseconds.
- **Breaker:** `PostgresCircuitBreakerInterceptor : DbConnectionInterceptor` (BuildingBlocks). In `ConnectionOpeningAsync` it opens the connection itself through the resilience pipeline `postgres-connection` (circuit breaker only: failure ratio 0.5, minimum throughput 10, sampling 30 s, break 30 s) and returns `InterceptionResult.Suppress()`. While the breaker is open, the open fails at once with `BrokenCircuitException`. EF does not treat that exception as transient, so it does not retry.
  - Counted as failures: `NpgsqlException` that is **not** a `PostgresException` (socket, DNS, connection refused), `TimeoutException`, and `PostgresException` with SQLSTATE class `08` (connection exception) or `57P01`-`57P03` (server shutdown or starting).
  - Never counted: all other `PostgresException` SQLSTATEs, in particular `23xxx` (constraint), `40001`/`40P01` (serialization, deadlock), `55P03` (lock not available), `57014` (statement or lock timeout cancel), and `28xxx` (auth; a config error, not an outage).
- **User transactions:** `ITransactionRunner` (interface in SharedKernel next to `ISqlRunner`; implementation `TransactionRunner` in BuildingBlocks) = `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`. Each attempt starts with `db.ChangeTracker.Clear()`, then `BeginTransaction`, the handler's work, `SaveChanges` and `Commit`. So a retried attempt starts with no tracked entities and no pending audit rows. The runner commits when the work returns a success `Result` and rolls back on a failure `Result` or an exception (a flow that must persist something and still fail, such as refresh-token reuse, returns a success outcome and maps it to an error after the commit; ADR-0008). Handler code inside must be re-runnable: it reloads and re-locks everything it needs.
- **Dapper:** `ISqlRunner` opens the connection through `db.Database.OpenConnectionAsync()` (so the interceptor applies) and runs standalone reads inside the same execution strategy. Inside a transaction it runs directly on the current connection and transaction.
- **Stock concurrency:** row locks (`SELECT … FOR UPDATE` in product ID order) inside the transaction; no app-level concurrency retry. `xmin` is only for ETags (412).
- **Errors:** `BrokenCircuitException`, `RetryLimitExceededException` and transient `NpgsqlException` map to 503 ProblemDetails (`GlobalExceptionHandler`), with `Retry-After` when the breaker is open.

**Valkey (cache).**
- `ResilientDistributedCache` decorates the StackExchangeRedis `IDistributedCache` used as HybridCache L2. Pipeline `valkey` = total timeout 250 ms (outer) → circuit breaker (same thresholds) → retry (up to 3, exponential + jitter, base 20 ms) → attempt timeout 100 ms. The outer timeout keeps a hung Valkey inside the 300 ms p95 budget (ADR-0009).
- Any failure after the pipeline = cache miss (read) or a logged, measured warning (remove). The product query then reads PostgreSQL (FR-130).
- StackExchange.Redis: `AbortOnConnectFail=false`, short `ConnectTimeout`/`SyncTimeout`; its own reconnect is not a request retry, so no double retry.
- If Valkey is down during a product write, the L2 entry is not removed. The TTLs bound the staleness (ADR-0009: items 10 min L2 / 1 min L1, lists 2 min / 30 s, plus the list generation key). No replay list.

**Outgoing HTTP.** None this phase (fake email). Any future `HttpClient` uses `AddStandardResilienceHandler()` from Microsoft.Extensions.Http.Resilience and no other retry.

**Config.** All numbers in `Resilience` options (appsettings), validated on start. Integration tests use smaller break durations.

## Consequences
- Good: one retry layer per dependency, so no multiplication (F3); breaker present as required (P10) and blind to business errors (C1); one connection path for EF and Dapper.
- Good: no custom execution strategy and no AsyncLocal state; only a small interceptor.
- Good: no direct Polly reference; only Microsoft.Extensions.Resilience.
- Bad / trade-offs: handlers inside `ITransactionRunner` must be re-runnable per attempt.
- Bad / trade-offs: on a single local PostgreSQL the DB breaker adds little value (Advice in solution-structure).
- Follow-up actions (tests):
  - "10 duplicate SKUs leave the breaker closed": 10+ create-product calls with a duplicate SKU return 409, and the next valid request returns 201.
  - "DB down → 503 within 5 s": stop or pause the PostgreSQL container; the first request returns 503 ProblemDetails in < 5 s; after the breaker opens, requests return 503 with `Retry-After` in < 100 ms.
  - Forced transient failure (C3): a test interceptor throws a transient `NpgsqlException` once on the first commit of register; the retry succeeds with exactly one user, one customer and one set of audit rows.
  - Valkey paused → product reads 200, p95 ≤ 300 ms.
  - S3: 20 parallel orders for stock 5 → exactly 5 successes.
  - Architecture test: no `Polly` namespace used directly in our code.

## Revisit when
- A managed or replicated PostgreSQL is used (breaker becomes more useful; tune thresholds).
- Real outgoing HTTP integrations appear.

## Links
- [solution-structure.md](../solution-structure.md) section 5 · [data-model.md](../data-model.md) section 6 · [architecture review](../architecture-review.md) C1, C3
- PRD NFR-03, FR-084, P3, P10; pre-mortem F3, F10
- EF Core connection resiliency: https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency · Interceptors: https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors
