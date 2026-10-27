# ADR-0014: Deny by default, four named policies, and `/me` ownership from the token

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-27 |
| Deciders | Product owner, architect |
| Consulted | Threat model (section 3, S-D1, S-D7), permission matrix, endpoint catalog section 1.10 |

## Context
The API has two roles: Admin (staff) and Customer. BOLA (OWASP API1) is the top API risk, and the threat model ranks BOLA regressions 5th (T-12). Broken function-level authorization (T-13) and Admin-only fields shown to Customers (T-14, T-24) are close behind. Today the rules are spread over the endpoint catalog, the permission matrix and each handler. The template must copy cleanly, and `CustomerOnly` with the `customer_id` claim is a strip point (solution-structure section 13). This ADR gives the authorization model one home.

## Decision drivers
- Deny by default; a missing rule must fail a test, not open an endpoint.
- One way to check ownership, so a new `/me` handler cannot forget it.
- No information leak: other customers' data looks exactly like missing data.
- Simple enough to read and copy; easy to remove the Customer concept.
- Revocation is fast (R1, S-D1).

## Options considered

### Option 1: Named role policies + ownership from the token in queries (chosen)
- Good: simple; ownership is a query predicate, so non-owned rows never load; 404 comes for free; easy to test from endpoint metadata.
- Bad: ownership lives in handler queries, so it needs a helper and tests to stay consistent.
- Cost / effort: low.

### Option 2: Resource-based `IAuthorizationHandler` (load the resource, then authorize)
- Good: standard ASP.NET Core pattern; good for sharing rules (owner, delegate, team).
- Bad: loads the row first, then decides; easy to return 403 (leaks existence) instead of 404; more code per resource.
- Cost / effort: medium.

### Option 3: Permissions instead of roles (`products:write`, `audit:read`)
- Good: fine-grained; supports an `Auditor` later.
- Bad: two fixed roles do not need it now; more claims and configuration.
- Cost / effort: medium.

### Option 4: PostgreSQL row-level security
- Good: enforced in the database, also for Dapper.
- Bad: needs a session variable per request; Admins bypass; hard to test and to copy; works against the pooled shared connection.
- Cost / effort: high.

## Decision
We will use **Option 1**.

1. **Deny by default.** The fallback policy requires an authenticated user. Every endpoint declares exactly one of four policies, by name from one `Policies` class:

| Policy | Rule |
|---|---|
| `Anonymous` | `AllowAnonymous()`; the endpoint summary says why |
| `Authenticated` | Valid access token, role Customer or Admin |
| `AdminOnly` | Role `Admin` |
| `CustomerOnly` | Role `Customer` **and** a valid `customer_id` claim |

2. **ID routes are Admin only.** Routes with a resource ID (`/customers/{id}`, `/orders/{id}`) are `AdminOnly`; a Customer gets 403.
3. **`/me` ownership comes only from the token.** `customer_id` is read from `ICurrentUser.CustomerId`, never from the route, query or body (extra fields are ignored). Every `/me` query goes through one helper, `OwnedBy(ICurrentUser)`, which adds `CustomerId == current`. Not owned = missing = deleted → **404** with the same body.
4. **Status order** follows the permission matrix: 429 (rate limit) → 401 (no or invalid token, failed stamp check) → 403 (wrong role) → 400 (validation) → 404 (not found or not owned) → 428/412 (`If-Match`) → 409 (business rule).
5. **Role-specific DTOs.** Admin and Customer get separate response types (`isDeleted`, `userId`, `byUserId` are Admin only). Customers' `includeDeleted` is ignored. Cache keys carry the role scope (ADR-0009).
6. **Current account state.** The per-request security-stamp check (ADR-0008, S-D1) rejects tokens of locked, demoted, erased or deleted users; `/me` handlers also treat a missing or deleted customer as 404, and order create rejects an erased customer.
7. **Audit-log reads.** All Admins may read audit logs (S-D5); each query is logged as a security event (ADR-0013).

## Tests
- **ST-01 authz matrix:** built from `EndpointDataSource` × anonymous / Customer / Admin and compared with the permission matrix. An endpoint missing from the matrix fails the test.
- **ST-02 BOLA:** every `/me/{id}` route with another customer's ID → 404. A new `/me/{id}` route without a BOLA case fails the test.
- **ST-03:** forbidden body fields have no effect; per-role DTO snapshots.
- **ST-06:** old access token → 401 after lock, role remove, erase, password change, logout-all.
- Architecture test A9: every endpoint has an explicit policy.

## Consequences
- Good: one model, one helper, one matrix; failures show up as test failures, not reviews.
- Good: 404 for non-owned data never reveals that a resource exists.
- Bad / trade-offs: roles are coarse; an `Auditor` or a "staff read-only" role needs new policies (Option 3 later).
- Bad / trade-offs: Dapper reports are Admin only, so they need no ownership filter; a future Customer report must add the predicate by hand and get a BOLA test.

## Strip guide (template)
To remove the Customer concept: delete the `CustomerOnly` policy and the `Customer` role; stop issuing the `customer_id` claim; remove `ICurrentUser.CustomerId` and `OwnedBy`; delete the `/me` customer and order endpoints and their BOLA cases. Keep `Anonymous`, `Authenticated`, `AdminOnly`, the matrix test and the stamp check. See solution-structure section 13.

## Revisit when
- Multi-tenancy, sharing between customers, or delegated access appears (consider Option 2 or tenant filters).
- A client needs an `Auditor` or finer staff roles (Option 3).

## Links
- [Threat model](../../05-security/threat-model.md) sections 2.3, 3, 9 · [permission matrix](../../02-requirements/permission-matrix.md) · [endpoint catalog](../endpoint-catalog.md) section 1.10
- ADR-0008 (tokens, stamp check), ADR-0009 (cache scope), ADR-0013 (security events)
- OWASP API1 and API5 (2023)
