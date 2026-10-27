# ADR-0006: Enforce soft delete and append-only audit with EF Core interceptors and a no-DELETE database role

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Solution architect, developer |
| Consulted | dotnet-data-specialist, security reviewer (Security phase will re-check) |

Revised 2026-10-27 (architecture review R-4, R-5): the interceptor no longer touches the order root (explicit `order.Touch(now)`, ADR-0007); added the C10 test for Admin get of a deleted customer.

## Context
The product owner decided: nothing is hard-deleted (G4, FR-127); every business write and every meaningful account change is audited in the same transaction (FR-128, D4); personal data is never stored as values in the audit log (P1); `AuditLogs` is append-only. ASP.NET Core Identity saves through its own `UserStore`, and `RemoveFromRoleAsync` deletes the `AspNetUserRoles` row (R2). `ExecuteDelete` and `ExecuteUpdate` skip interceptors (D11). The pre-mortem (F5) warned that interceptors can fight Identity.

## Decision drivers
- "Never delete" enforced by the database, not only by code (NFR-08).
- 100% of writes audited, rollback leaves no audit row (NFR-16).
- No PII values in audit (P1, FR-065, R3).
- Works for Identity without forking it.
- Generic: lives in BuildingBlocks with no domain references (P7).

## Options considered

### Option 1: Database triggers for audit and soft delete
- Good: catches every write, even raw SQL.
- Bad: triggers do not know the app user or trace ID without session variables; PII marking lives in SQL, far from the model; harder to test and copy.
- Cost / effort: medium-high.

### Option 2: Explicit audit calls in each handler
- Good: obvious.
- Bad: easy to forget; Identity saves are not covered; fails "100% audited".
- Cost / effort: high over 50 endpoints.

### Option 3: EF Core `SaveChanges` interceptors + named query filters + no-DELETE role (chosen)
- Good: one place for all writes, including Identity (same `DbContext`); knows user, time and trace; PII marked on the model.
- Bad: bypassed by raw SQL and `ExecuteUpdate/Delete` (so those are banned or pinned); Identity join-table delete needs one grant exception.
- Cost / effort: medium, mostly in S1.

## Decision
We will implement Option 3:

1. **`SoftDeleteInterceptor`** (`SaveChangesInterceptor`, BuildingBlocks), registered first. For each `Deleted` entry implementing `IDeletable`: set `State = Modified`, `IsDeleted = true`, `DeletedAt = IDateTimeProvider.UtcNow`, `DeletedBy = ICurrentUser.UserId`. Owned/child entities are not cascaded automatically; the domain soft-deletes children it means to. A `Deleted` entry that is **not** `IDeletable` throws, **except** `IdentityUserRole<Guid>` (the allow-list).
2. **`AuditingInterceptor`** (registered second), in `SavingChangesAsync` after `ChangeTracker.DetectChanges()`:
   - Sets `CreatedAt/By` on `Added`, `LastModifiedAt/By` on `Modified` (`IAuditable`), from `IDateTimeProvider` and `ICurrentUser`.
   - Does not touch aggregate roots. The `Order` domain methods call `order.Touch(now)` themselves, so the order row is always updated and its `xmin` (ETag) changes (ADR-0007).
   - Adds one `AuditLog` entity per changed entry to the same context, so it is saved in the same command batch and transaction. Action: `Insert`, `Update`, `SoftDelete` (`IsDeleted` false→true), `Restore` (true→false), `RoleAssigned`/`RoleRemoved` (added/deleted `IdentityUserRole<Guid>`, entity = the user).
   - `changes` jsonb holds changed properties only. Properties marked `[PersonalData]` (or `.IsPersonalData()` for inherited Identity properties such as `Email`, `UserName`, `PhoneNumber`) are written as `"changed"`. Never written: `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, token hashes. Ignored: `AccessFailedCount`; if nothing else changed, no row (FR-128).
   - Skipped types: `AuditLog`, `RefreshToken`.
   - User = `ICurrentUser.AuditName`: user ID, `anonymous` (register, reset) or `system` (seed). Trace ID from `Activity.Current`.
3. **Named query filters (EF Core 10)**: a model convention adds `HasQueryFilter("SoftDelete", e => !e.IsDeleted)` to every `IDeletable`. Admin `includeDeleted` and restore use `IgnoreQueryFilters(["SoftDelete"])`, leaving other named filters on. Reads that must show deleted customers or products (order details, history) ignore the filter on purpose and re-apply `!o.IsDeleted` for the order itself.
4. **Identity goes through the same pipeline.** `AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>` is the only context; `UserManager`/`SignInManager` use it (scoped), so each store save runs both interceptors. Multi-step flows (register, admin create customer, erase) wrap all `UserManager` calls and domain saves in one `ITransactionRunner` transaction, so a failure rolls back users, customers, tokens and audit rows together. `AppUser` implements `IAuditable` and `IDeletable`; the soft-delete filter on `AppUser` makes deleted users invisible to `FindByEmailAsync` (login → generic 401). Spike in S1a (F5): register + lockout + audit, including a forced rollback and a forced transient retry (`ChangeTracker.Clear()` per attempt, ADR-0011).
5. **Database role without DELETE.** `demoapi_app` has SELECT/INSERT/UPDATE on business tables, **SELECT/INSERT only on `audit_logs`**, no DDL, and **DELETE only on `asp_net_user_roles`** (remove Admin role, FR-035). `asp_net_user_tokens` is not used (confirm/reset tokens are not stored; no MFA or external login), so it gets SELECT only and no DELETE. Every role removal is audited as `RoleRemoved`. Migrations run as `demoapi_migrator` from the one-shot migrator container.
6. **Bans.** `ExecuteDelete*` and `ExecuteUpdate*` are banned by BannedApiAnalyzers (build error) with an ArchUnit backup rule (A10, D11). Raw-SQL writes are limited to `CALL revoke_refresh_tokens` in `RefreshTokenStore` (non-audited table, ADR-0004/0005); another architecture test checks no `ExecuteSql*` is used outside migrations.

## Consequences
- Good: a hidden hard delete fails at the database (`42501`), not silently; audit is append-only by grant, so P1 holds without redaction.
- Good: the erasure test reads PII markers from the EF model, seeds every PII field, erases, and searches all tables (R3).
- Bad / trade-offs: audit rows have no `xmin`/generated values (all IDs are client-side v7, so this is fine); interceptor order matters and is tested.
- Bad / trade-offs: Identity role removal is a real DELETE (narrow, audited exception). Option "demote = lock" from the pre-mortem was not chosen because FR-035 is kept (P10).
- Bad / trade-offs: soft-deleted rows keep growing (retention, data model section 8).
- Follow-up actions: integration tests: app role cannot `DELETE`/`UPDATE audit_logs`/`TRUNCATE`; every write endpoint asserts its audit row (NFR-16); rollback leaves no audit row; role removal works and is audited; C10: Admin get of a deleted customer (`includeDeleted=true`) returns the customer with its (anonymized) email, so the `AppUser` soft-delete filter does not drop the user in the join.

## Revisit when
- MFA or external login is added (`asp_net_user_tokens`/`asp_net_user_logins` writes and deletes; extend the allow-list and grants by ADR).
- A write path outside EF Core is needed for an audited table.
- Audit volume makes per-entry JSON too slow (consider batching or triggers).

## Links
- [Data model](../data-model.md) sections 1, 3, 5 · ADR-0004 · ADR-0005 · ADR-0007
- PRD FR-064, FR-065, FR-127, FR-128, NFR-08, NFR-16, D4, D11, P1, R2, R3
- EF Core 10 named query filters: https://learn.microsoft.com/ef/core/querying/filters · Interceptors: https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors
