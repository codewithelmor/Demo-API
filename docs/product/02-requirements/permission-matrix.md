# Permission matrix

Date: 2026-10-24 | Status: Approved 2026-10-24 | Revised 2026-10-27 (security S-D2, S-D4, S-D5; authorization model in ADR-0014)
Routes are prefixed with `/api/v1` (except health). Endpoint IDs match `endpoint-list.md`.

## Legend
- **Allow** - permitted.
- **Own** - permitted on the caller's own data only. The customer ID always comes from the token. Another customer's resource, a missing resource or a soft-deleted resource → **404**.
- **401** - denied: no, invalid or expired access token.
- **403** - denied: signed in but the role is not allowed. Returned the same way whether or not the ID exists, so it leaks nothing.
- **Self** - acts on the caller's own account only.

## Route rule
ID routes (`/customers/{id}`, `/orders/{id}`) are Admin only; a Customer gets 403 there. Customers use `/customers/me/...` routes, where the ownership check applies and BOLA tests run.

## Status code rules (checked in this order)
1. **429** - rate limit exceeded (`Retry-After`).
2. **401** - protected endpoint and no valid token. Also login failures (generic) and bad or reused refresh tokens.
3. **403** - wrong role. Also login with a correct password but an unconfirmed email (`email-not-confirmed`).
4. **400** - request shape or validation error (ProblemDetails with `errors`). Shape checks only, so nothing leaks about whether data exists.
5. **404** - not found, soft-deleted, or not owned on `/me` routes.
6. **428 / 412** - `If-Match` missing or stale.
7. **409** - business rule: wrong order state, insufficient stock, too many open Pending orders, duplicate SKU or email, last-Admin or self rules, restore of an item that is not deleted, open orders on delete.
8. **2xx** - 200 read, 201 create (with `Location`), 202 accepted (anti-enumeration endpoints), 204 command done.

| EP | Endpoint | Anonymous | Customer | Admin | Notes |
|---|---|---|---|---|---|
| EP-01 | POST /auth/register | Allow | Allow | Allow | Always 202. auth-strict limit |
| EP-02 | POST /auth/confirm-email | Allow | Allow | Allow | Bad token → 400 |
| EP-03 | POST /auth/resend-confirmation | Allow | Allow | Allow | Always 202 |
| EP-04 | POST /auth/login | Allow | Allow | Allow | 401 generic; 403 only if the password is correct and the email unconfirmed; lockout after 5 failures (does not revoke sessions) |
| EP-05 | POST /auth/refresh | Allow | Allow | Allow | Token is the credential. Reuse → family revoked + 401 |
| EP-06 | POST /auth/logout | 401 | Self | Self | Token not the caller's → 204, no change |
| EP-07 | POST /auth/logout-all | 401 | Self | Self | |
| EP-08 | GET /auth/me | 401 | Self | Self | |
| EP-09 | POST /auth/change-password | 401 | Self | Self | auth-strict limit. Wrong current password → 400 and counts toward lockout. Revokes all refresh tokens |
| EP-10 | POST /auth/forgot-password | Allow | Allow | Allow | Always 202 |
| EP-11 | POST /auth/reset-password | Allow | Allow | Allow | Confirms email; revokes tokens |
| EP-12 | GET /admin/users | 401 | 403 | Allow | |
| EP-13 | GET /admin/users/{id} | 401 | 403 | Allow | |
| EP-14 | POST /admin/users/{id}/lock | 401 | 403 | Allow | Self or last active Admin → 409 |
| EP-15 | POST /admin/users/{id}/unlock | 401 | 403 | Allow | Deleted user → 409 |
| EP-16 | PUT /admin/users/{id}/roles/{role} | 401 | 403 | Allow | Admin role only. User with a Customer record → 409 |
| EP-17 | DELETE /admin/users/{id}/roles/{role} | 401 | 403 | Allow | Self or last Admin → 409 |
| EP-18 | GET /admin/roles | 401 | 403 | Allow | |
| EP-19 | POST /admin/users | 401 | 403 | Allow | Duplicate email → 409 |
| EP-20 | POST /admin/users/{id}/revoke-sessions | 401 | 403 | Allow | Stretch |
| EP-21 | POST /admin/users/{id}/password-reset-link | 401 | 403 | Allow | Stretch |
| EP-22 | GET /products | 401 | Allow | Allow | Customers see non-deleted only; `includeDeleted` is Admin only |
| EP-23 | GET /products/{id} | 401 | Allow | Allow | Deleted → 404 (Customer always) |
| EP-24 | POST /products | 401 | 403 | Allow | Duplicate SKU → 409 |
| EP-25 | PUT /products/{id} | 401 | 403 | Allow | If-Match |
| EP-26 | DELETE /products/{id} | 401 | 403 | Allow | Soft delete |
| EP-27 | POST /products/{id}/restore | 401 | 403 | Allow | Not deleted or SKU taken → 409 |
| EP-28 | POST /products/{id}/stock-adjustments | 401 | 403 | Allow | Stretch. Result < 0 → 409 |
| EP-29 | POST /customers | 401 | 403 | Allow | Duplicate email → 409 |
| EP-30 | GET /customers | 401 | 403 | Allow | |
| EP-31 | GET /customers/{id} | 401 | 403 | Allow | Customers use EP-35 |
| EP-32 | PUT /customers/{id} | 401 | 403 | Allow | If-Match |
| EP-33 | DELETE /customers/{id} | 401 | 403 | Allow | Open orders → 409. Anonymizes |
| EP-34 | POST /customers/{id}/restore | 401 | 403 | Allow | Stretch. Stays anonymized and locked |
| EP-35 | GET /customers/me | 401 | Own | 403 | Admins have no Customer record |
| EP-36 | PUT /customers/me | 401 | Own | 403 | If-Match |
| EP-37 | POST /customers/me/erase | 401 | Own | 403 | Current password required (wrong → 400, counts toward lockout). auth-strict limit. Open orders → 409 |
| EP-38 | POST /orders | 401 | 403 | Allow | Customer in body must exist. Stock → 409. Customer already has 5 open Pending orders → 409 `too-many-pending-orders` |
| EP-39 | GET /orders | 401 | 403 | Allow | |
| EP-40 | GET /orders/{id} | 401 | 403 | Allow | |
| EP-41 | PUT /orders/{id}/items | 401 | 403 | Allow | Pending only, else 409. If-Match |
| EP-42 | POST /orders/{id}/status | 401 | 403 | Allow | Only P→C, C→S, S→Completed; else 409 |
| EP-43 | POST /orders/{id}/cancel | 401 | 403 | Allow | Pending or Confirmed only; else 409 |
| EP-44 | DELETE /orders/{id} | 401 | 403 | Allow | Stretch. Cancelled only |
| EP-45 | POST /customers/me/orders | 401 | Own | 403 | Customer from token. Stock → 409. 5 open Pending orders already → 409 `too-many-pending-orders` |
| EP-46 | GET /customers/me/orders | 401 | Own | 403 | Own orders only |
| EP-47 | GET /customers/me/orders/{id} | 401 | Own (404) | 403 | Other customer's ID → 404 |
| EP-48 | POST /customers/me/orders/{id}/cancel | 401 | Own (404) | 403 | Pending only, else 409 |
| EP-49 | PUT /customers/me/orders/{id}/items | 401 | Own (404) | 403 | Stretch. Pending only |
| EP-50 | GET /reports/order-summaries | 401 | 403 | Allow | |
| EP-51 | GET /customers/{id}/order-history | 401 | 403 | Allow | Deleted customer → 404 |
| EP-52 | GET /reports/sales | 401 | 403 | Allow | Range ≤ 366 days |
| EP-53 | GET /reports/low-stock | 401 | 403 | Allow | |
| EP-54 | GET /reports/top-products | 401 | 403 | Allow | Could |
| EP-55 | GET /audit-logs | 401 | 403 | Allow | Read-only. Each query is logged as a security event |
| EP-56 | GET /health/live | Allow | Allow | Allow | No details outside Development |
| EP-57 | GET /health/ready | Allow | Allow | Allow | Valkey down = Degraded 200; DB down = 503 |
