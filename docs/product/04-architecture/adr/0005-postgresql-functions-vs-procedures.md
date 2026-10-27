# ADR-0005: Use PostgreSQL functions for reads and a procedure for set-based commands

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Solution architect, developer |
| Consulted | dotnet-data-specialist, db-relational-specialist (skills) |

## Context
The brief promises "Dapper for views, functions and procedures", and pre-mortem decision P9 asks for at least one real procedure plus a rule for when to use which. Many .NET developers come from SQL Server, where one "stored procedure" returns result sets and runs commands. PostgreSQL splits this: a **function** returns a value or a set of rows and is called inside a query (`SELECT * FROM fn(...)`); a **procedure** (since PostgreSQL 11) is run with `CALL`, returns no result set (only `INOUT` values or a `refcursor`), and may `COMMIT`/`ROLLBACK` when it is not called inside an explicit transaction block. Getting this wrong leads to procedures that try to return rows through cursors, or functions that hide writes inside `SELECT`.

## Decision drivers
- Reads must be pageable and composable (`WHERE`, `ORDER BY`, `LIMIT`) and map easily to Dapper DTOs.
- Commands must run inside the caller's EF transaction (all-or-nothing, e.g. erase, FR-064 AC9).
- One clear rule for the template; a real, not a token, procedure (P9).
- Least privilege: `EXECUTE` granted per routine.

## Options considered

### Option 1: Functions for everything (reads and writes)
- Good: one concept; a function can run `UPDATE` and return a count.
- Bad: a write hidden in a `SELECT` is surprising and easy to call by mistake (also from a read-only path); does not show procedures (fails P9).
- Cost / effort: low.

### Option 2: Procedures for everything, rows returned through `refcursor`
- Good: looks like SQL Server stored procedures.
- Bad: cursors need an open transaction and a second `FETCH`; awkward with Dapper; cannot be joined, filtered or paged by the caller.
- Cost / effort: medium, poor ergonomics.

### Option 3: Functions for reads, procedures for commands (chosen)
- Good: each object type does what PostgreSQL designed it for; read shapes are plain `QueryAsync<T>`; commands are explicit `CALL`s.
- Bad: two kinds of objects and two calling styles to document.
- Cost / effort: low.

## Decision
We will use:
- **Views** for reusable row shapes that the API filters, sorts and pages (`v_order_summaries`).
- **Functions** (`RETURNS TABLE`, `STABLE`, `SECURITY INVOKER`, `SET search_path = public, pg_temp`) for parameterised reads and reports: `fn_customer_order_history`, `fn_sales_report`, `fn_top_products`. Called as `SELECT * FROM public.fn_x(@a, @b)`.
- **Procedures** for set-based commands that return at most a few scalars through `INOUT`: `revoke_refresh_tokens(p_user_id, p_family_id, p_reason, p_at, INOUT p_revoked_count)`, called as `CALL public.revoke_refresh_tokens(@UserId, @FamilyId, @Reason, @At, NULL)` with Dapper `QuerySingleAsync<int>` on the shared EF connection and transaction. Inside our transaction the procedure does not `COMMIT`.
- **Plain inline SQL** when no reuse or encapsulation is gained (low-stock list, audit-log query).

Rule for the template: *returns rows → function (or view); changes data → procedure; only `COMMIT` inside a procedure for batch jobs called outside a transaction* (future retention purge).

Versioning: SQL files `Sql/{Views,Functions,Procedures}/<name>.vN.sql`, never edited after merge; migrations `Up` apply `vN`, `Down` re-apply `vN-1` (or `DROP` for v1). `CREATE OR REPLACE` only when the signature and return type are unchanged; otherwise `DROP ... (arg types)` then `CREATE`, and every file ends with its `GRANT EXECUTE ... TO demoapi_app`. `EXECUTE` is revoked from `PUBLIC` by default (data model section 5).

## Consequences
- Good: SQL Server developers get a clear mapping; Dapper code stays simple; the procedure has a real job (revoke-all on a non-audited table without `ExecuteUpdate`, D11).
- Good: functions validate parameters as a guard (`SQLSTATE 22023`) behind the API validators.
- Bad / trade-offs: the procedure writes outside the EF interceptor; allowed only because `refresh_tokens` is not audited (FR-128). Any future procedure that touches an audited table needs its own audit design and an ADR.
- Bad / trade-offs: `RETURNS TABLE` changes force `DROP` + `CREATE` and a re-grant.
- Follow-up actions: S1 builds the procedure (auth needs it); S4 builds the view and functions; integration tests with deleted data for each (FR-106).

## Revisit when
- A batch job (retention purge, summary refresh) is approved: add a procedure that commits in batches and a role with only the grants it needs.
- PostgreSQL adds result sets for procedures in a way Npgsql supports.

## Links
- [Data model](../data-model.md) section 4 · ADR-0004 · ADR-0006
- PostgreSQL docs: CREATE PROCEDURE https://www.postgresql.org/docs/18/sql-createprocedure.html · CREATE FUNCTION https://www.postgresql.org/docs/18/sql-createfunction.html · Transaction management in procedures https://www.postgresql.org/docs/18/plpgsql-transactions.html
