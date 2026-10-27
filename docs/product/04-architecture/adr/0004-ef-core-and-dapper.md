# ADR-0004: Use EF Core for writes and Dapper for views, functions and the procedure

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Solution architect, developer |
| Consulted | dotnet-data-specialist, product owner (stack fixed in the brief) |

Revised 2026-10-27 (architecture review R-1, R-3): rule 5 follows the new ADR-0011 (plain `EnableRetryOnFailure`, connection-level breaker, `ITransactionRunner` in SharedKernel).

## Context
The stack is fixed: PostgreSQL 18, EF Core 10 + Npgsql 10, Dapper 2.1. The open questions are where each tool is used, who owns the schema, how both share a transaction, and how retries work. Writes must be audited and soft-deleted by EF Core interceptors (ADR-0006). Slice S4 needs a view, set-returning functions and one real procedure (P9). Order commands lock product rows (ADR-0007). The pre-mortem (F3) warned about stacked retry layers.

## Decision drivers
- Every business write is audited in the same transaction (G4, FR-128).
- Reports need SQL that LINQ does not express well: `generate_series`, `date_trunc`, `LATERAL`, empty periods.
- One schema owner, reviewed migrations, least-privilege app role (NFR-08).
- One retry layer only (FR-084, NFR-03).
- Easy to copy as a template (G5): few concepts, clear rules.

## Options considered

### Option 1: EF Core only (views and functions through `FromSql`/keyless entities)
- Good: one tool; migrations and queries in one model.
- Bad: keyless entity types for every report shape; `FromSql` on functions still needs raw SQL; the brief asks to show Dapper.
- Cost / effort: low, but misses the brief.

### Option 2: Dapper only
- Good: full SQL control, lowest overhead.
- Bad: no change tracking, no interceptors, so audit and soft delete must be hand-written in every command; no migrations (needs DbUp or similar); Identity needs EF Core anyway.
- Cost / effort: high; breaks G4.

### Option 3: EF Core for writes, CRUD and migrations; Dapper for views, functions and the procedure (chosen)
- Good: each tool where it is strongest; audit interceptors see every business write; report SQL is plain and testable.
- Bad: two styles of data access; Dapper SQL has no compile-time check; EF query filters do not apply to Dapper.
- Cost / effort: moderate; rules below keep it contained.

## Decision
We will use **EF Core** for all writes, all CRUD reads (`AsNoTracking` + projection), Identity, and **all schema changes** (migrations, including views, functions, the procedure, extensions and grants as versioned SQL files). We will use **Dapper** only for the order-summary view, the report functions, the low-stock and audit-log list queries, and the `revoke_refresh_tokens` procedure. Data model section 4 lists each object.

Rules:
1. **One schema owner.** EF Core migrations create everything. Dapper never runs DDL.
2. **Dapper returns DTOs, never entities.** Commands never update entities from Dapper results.
3. **Dapper writes:** only `CALL public.revoke_refresh_tokens(...)` in `RefreshTokenStore` (a non-audited table, FR-128). An architecture test fails on any other Dapper `Execute*` or `CALL`.
4. **Shared connection and transaction.** `ISqlRunner` (SharedKernel) → `SqlRunner` (BuildingBlocks) uses `DbContext.Database.GetDbConnection()` and `Database.CurrentTransaction?.GetDbTransaction()` in each `CommandDefinition`. Never a second connection inside a unit of work.
5. **One retry layer.** EF Core's Npgsql execution strategy (plain `EnableRetryOnFailure`, ADR-0011) retries transient errors. User transactions run inside `CreateExecutionStrategy().ExecuteAsync(...)` via `ITransactionRunner` (SharedKernel), which clears the change tracker at the start of each attempt. `SqlRunner` opens connections through EF (so the connection-level breaker interceptor applies), wraps standalone reads in the same strategy and runs directly inside a transaction. Microsoft.Extensions.Resilience does not retry database calls.
6. **Where Dapper code lives.** SQL sits in the Application slice that uses it (`Application/Features/Reports/GetSalesReport/GetSalesReport.sql`, embedded resource, or a `const` raw string). DDL for views/functions/procedure sits in `Infrastructure/Persistence/Sql/{Views,Functions,Procedures}/<name>.vN.sql`, applied by migrations.
7. **Safety.** Every value is a parameter; sort columns and directions come from allow-lists; `LIKE` input is escaped; every SQL statement has `is_deleted = false` written out where D2/FR-106 demand it; `CommandDefinition` carries timeout and `CancellationToken`.
8. **Tests.** Each Dapper query and DB object has an integration test on Testcontainers PostgreSQL 18.6 with soft-deleted rows present, connected as the app role.

## Consequences
- Good: audit and soft delete stay automatic for all business writes; reports are readable SQL; the procedure shows the PostgreSQL CALL pattern; no retries stacked on retries.
- Good: Dapper inside a command sees the command's uncommitted changes (same connection).
- Bad / trade-offs: reviewers must check soft-delete filters by hand in Dapper SQL; SQL errors appear only at test time; two mapping styles to learn.
- Bad / trade-offs: function signature changes need `DROP` + `CREATE` and re-granting (ADR-0005).
- Follow-up actions: build `SqlRunner` and `TransactionRunner` in S1; architecture tests for rule 3; a sample sliced report in S4 as the template example.

## Revisit when
- A report needs more than ~1 s at NFR-02 volume (consider a summary table refreshed by a procedure).
- A second write path outside EF Core is proposed for an audited table (that would need a new ADR).

## Links
- [Data model](../data-model.md) sections 4-6 · ADR-0005 · ADR-0006 · ADR-0007 · ADR-0011 · [solution structure](../solution-structure.md)
- PRD FR-084, FR-100-FR-106, FR-128, NFR-03, NFR-08, D11, P3, P9
- EF Core connection resiliency and user transactions: https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency
