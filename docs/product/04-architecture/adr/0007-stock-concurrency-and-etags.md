# ADR-0007: Lock stock rows with SELECT … FOR UPDATE and use xmin for ETags

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Solution architect, developer |
| Consulted | dotnet-data-specialist, devils-advocate (pre-mortem F3) |

Revised 2026-10-27 (architecture review R-4): the order root is touched by an explicit `order.Touch(now)` in domain methods, not by the interceptor.

Revised 2026-10-27 (security S-D4, S-D7): `lock_timeout` is now 2 s; order create takes `FOR UPDATE` (was `FOR SHARE`) on the customer row and checks the cap of open Pending orders per customer (S-D4) under that lock, so two parallel creates cannot both pass the cap. Test: 10 parallel creates for a customer with 4 Pending orders → exactly 1 success.

## Context
Order create, cancel and item replace change stock on several products in one transaction (FR-083, FR-087, FR-089). Stock must never go below 0, and 20 parallel orders for stock 5 must give exactly 5 successes (FR-084). Products, customers and orders need an `ETag` with `If-Match` (FR-129). Every item or status change must change the order's ETag (P3). `ExecuteUpdate` is banned (D11), and only one retry layer is allowed (FR-084, NFR-03). The pre-mortem (F3) saw `xmin` retry loops clash with the execution strategy and produce flaky tests.

## Decision drivers
- Correct under concurrency, deterministic tests.
- Every stock change audited (old/new) through EF Core (FR-128).
- No retry loop in business code; one transient-error retry layer.
- No deadlocks by design.
- Cheap ETags without extra columns.

## Options considered

### Option 1: Pessimistic row locks, `SELECT … FOR UPDATE` in product-ID order (chosen)
- Good: waiting transactions re-read the latest stock (READ COMMITTED re-check), so the check is exact; no business retry; tracked entities, so audit works; deterministic test.
- Bad: holds row locks for the transaction; hot products serialize.
- Cost / effort: low.

### Option 2: Optimistic `xmin` on products + retry on `DbUpdateConcurrencyException`
- Good: no locks held.
- Bad: under contention most attempts fail and retry; a business retry loop on top of the execution strategy (F3); flaky.
- Cost / effort: medium.

### Option 3: Atomic `UPDATE products SET stock_quantity = stock_quantity - @q WHERE id = @id AND stock_quantity >= @q`
- Good: fastest; one statement per line.
- Bad: needs `ExecuteUpdate` (banned) or raw SQL, which skips the audit interceptor; audit would need manual rows.
- Cost / effort: medium plus an audit exception.

### Option 4: SERIALIZABLE isolation
- Good: no explicit locks.
- Bad: serialization failures (`40001`) need retries of the whole unit; more aborts under load.
- Cost / effort: medium.

## Decision
We will use **Option 1** for stock and **`xmin` for ETags**:

1. **Lock order** (global, prevents deadlocks): customer → order → products by ascending `id`.
   - Create: `SELECT … FROM customers WHERE id = @id FOR UPDATE` (blocks a concurrent erase, which also takes `FOR UPDATE` on the customer and then checks open orders), then products. Revised from `FOR SHARE`: two `FOR SHARE` locks do not block each other, and the Pending-order cap needs parallel creates for one customer to run one after the other.
   - Pending-order cap (S-D4): under the customer lock and before the product locks, create counts the customer's non-deleted `Pending` orders; at the cap (default 5, `Orders:MaxPendingPerCustomer`) → 409 `too-many-pending-orders`.
   - Cancel, item replace, status change: `SELECT … FROM orders WHERE id = @id FOR UPDATE` first, then (cancel, replace) products.
   - Products: `db.Products.FromSql($"SELECT * FROM products WHERE id = ANY({ids}) ORDER BY id FOR UPDATE").IgnoreQueryFilters(["SoftDelete"])`, then check `IsDeleted` in code (create/replace → 400; cancel restores stock even for deleted products, FR-089). `ORDER BY id` places the lock step after the sort, so rows are locked in ID order. Products are loaded **only** through this query in these handlers (no earlier tracked copy).
2. **Inside the lock**, the domain checks stock and changes `StockQuantity`; `SaveChangesAsync` updates rows (with the `xmin` check, which cannot fail while we hold the lock) and writes audit rows. Short products → 409 listing IDs.
3. **Transaction and retries:** the whole handler runs in `ITransactionRunner` (`CreateExecutionStrategy().ExecuteAsync` + `BeginTransaction`). The Npgsql execution strategy is the **only** retry layer: transient errors (`40P01`, `55P03`, connection loss) retry the whole unit with backoff and jitter. `demoapi_app` has `lock_timeout = 2s` (security S-D7, data model section 5). No business-level retry loop exists.
4. **Status changes** lock the order row, so of two concurrent confirms the second sees `Confirmed` and gets 409 (US-204 AC9).
5. **ETags from `xmin`.** `products`, `customers` and `orders` map `uint Version` → `xmin` (`IsRowVersion()`, explicit column name `xmin`, type `xid`). ETag = `"<xmin>"` (strong). `If-Match` missing → 428. On PUT, the handler sets the entry's original `Version` to the `If-Match` value; a stale value raises `DbUpdateConcurrencyException` → 412. For order item replace the handler compares `If-Match` with the locked row's `xmin` → 412 before any change.
6. **Aggregate root touch.** Every `Order` domain method that changes items or status (`ReplaceItems`, `ChangeStatus`, `Cancel`) ends with an explicit `Touch(now)` that sets `LastModifiedAt`. The order row is therefore always updated, and its `xmin` and ETag always change. The interceptor does not do this (simpler, visible in the domain, unit-testable).
7. **Last-Admin guard.** Lock, demote and delete of an Admin first lock all active Admin user rows with `SELECT … FOR UPDATE` (ordered by user `id`), then count. Two Admins locking each other at the same time cannot leave zero active Admins; the second request gets 409 `last-admin`.

## Consequences
- Good: FR-084 test is deterministic (exactly 5 successes, final stock 0); full audit of stock changes; no deadlock cycles by design.
- Good: no extra version columns; ETag works the same for all three resources.
- Bad / trade-offs: product ETags change on every order that touches the product, so an Admin PUT based on an older GET returns 412 (correct, because stock changed; the client re-reads).
- Bad / trade-offs: `xmin` exposes a transaction counter value (low risk) and wraps after ~4 billion transactions (a false "match" is practically impossible within one edit).
- Bad / trade-offs: a connection drop during `COMMIT` can make a retry repeat an order; accepted locally; stretch `Idempotency-Key` (FR-095) fixes it.
- Follow-up actions: tests: 20 parallel orders; every order write changes the order ETag (unit test on `Touch`, integration test on `xmin`); cancel vs replace on the same order; erase vs create for the same customer; two confirms; two Admins locking each other; 412/428 per resource; deadlock-free run of mixed operations in the suite.

## Revisit when
- Lock waits show up in p95 under real load (consider Option 3 with explicit audit rows, or a stock reservation table).
- Multiple database instances or read replicas are introduced.

## Links
- [Data model](../data-model.md) sections 3, 5, 6 · ADR-0004 · ADR-0006 · ADR-0011 resilience
- PRD FR-084, FR-087-FR-090, FR-129, NFR-03, D11, D15, P3; pre-mortem F3
- PostgreSQL explicit locking: https://www.postgresql.org/docs/18/explicit-locking.html · SELECT locking clause: https://www.postgresql.org/docs/18/sql-select.html#SQL-FOR-UPDATE-SHARE · Npgsql EF concurrency tokens: https://www.npgsql.org/efcore/modeling/concurrency.html
