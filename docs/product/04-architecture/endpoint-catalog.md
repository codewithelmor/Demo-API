# Endpoint catalog (Architecture)

| Field | Value |
|---|---|
| Status | Approved |
| Date | 2026-10-24 (revised 2026-10-27, architecture review R-5; security S-D2, S-D3, S-D4, S-D5, S-D6) |
| Inputs | [PRD](../02-requirements/prd.md) (FR-120..137, NFRs, D1-D16, P1-P10), [endpoint list](../02-requirements/endpoint-list.md), [permission matrix](../02-requirements/permission-matrix.md), [user stories](../02-requirements/user-stories/), [solution structure](solution-structure.md) |
| ADRs | [0008 JWT and refresh tokens](adr/0008-jwt-and-refresh-tokens.md), [0009 product caching](adr/0009-caching-products.md), [0010 rate limiting](adr/0010-rate-limiting.md) |

This is the contract for all 57 endpoints. The DoD (PRD section 15) is checked against it. Where a story left a choice open, the choice is made here and marked **Assumption:**.

---

## 1. Conventions

### 1.1 Routes, IDs, binding
- Business routes: `/api/v1/...`. FastEndpoints `RoutePrefix = "api/v1"` (literal; no versioning package). A breaking change adds a `v2` group (FR-132). Health routes use `RoutePrefixOverride(string.Empty)` and have no prefix.
- IDs are GUIDs (v7, created server-side). Route parameters are bound as `Guid` **without** a `:guid` route constraint, so a malformed ID returns **400** with `errors.<param>` instead of 404 (D7).
- `/customers/me...` is a literal segment and wins over `/customers/{customerId}` in routing.
- One FastEndpoints `Group` per area: `AuthGroup` (`auth`), `AdminUsersGroup` (`admin`), `ProductsGroup`, `CustomersGroup`, `CustomersMeGroup` (`customers/me`), `OrdersGroup`, `ReportsGroup`, `AuditLogsGroup`. The group sets the tag, the default policy and the default rate-limit policy. An endpoint may override.
- Unknown query parameters are ignored (FR-123). Unknown JSON body members are ignored (System.Text.Json default). Request DTOs hold only fields the caller may set (mass assignment, OWASP API3).

### 1.2 Bodies and content
- Request bodies: `application/json` (UTF-8) only, else **415**. Max 1 MB (Kestrel `MaxRequestBodySize`), else **413** (FR-137).
- Malformed JSON or a wrong JSON type → **400** `validation-failed`, with the field (or `body`) in `errors`. The parser message is not echoed.
- JSON is camelCase. Enums are strings (case-insensitive on input, PascalCase on output). Nulls are written (`phone: null`), so the shape is stable.

### 1.3 Money and dates
- Money: JSON number with exactly 2 decimals on output. Input with more than 2 decimals → 400 (D9). Order, report and sales responses add `currency` (ISO 4217 from configuration, default `USD`). Product responses have no currency field (one currency; FR-125).
- Dates out: ISO 8601 UTC with `Z` (`2026-10-24T09:15:00Z`).
- Dates in (query and body): ISO 8601 **with** an offset (`Z` or `±hh:mm`), converted to UTC. A date without an offset → 400 (D8). A custom parser does this, because the default binder accepts dates without an offset.
- All server times come from `IDateTimeProvider` (FR-135).

### 1.4 Paging, sorting, filtering (FR-120..123, D8)
| Parameter | Rule |
|---|---|
| `page` | Integer ≥ 1, default 1. Out of range or not a number → 400 `errors.page` |
| `pageSize` | Integer 1-100, default 20 → else 400 `errors.pageSize` |
| `sort` | `field,-field` (`-` = descending). Max 3 fields. Field must be in the endpoint's allow-list, else 400 `errors.sort` listing the allowed fields. `id` is always added as the last tiebreaker. Not accepted on fixed-order reports (EP-51, 53, 55): ignored there |
| `search` | Case-insensitive contains, trimmed, 2-100 chars |
| `xxxFrom` / `xxxTo` | `From` inclusive, `To` exclusive. `From ≥ To` → 400 |
| `minXxx` / `maxXxx` | Inclusive. `min > max` → 400. Negative → 400 |
| `includeDeleted` | Bool. Admin only; ignored for Customers. Deleted rows then carry `isDeleted: true` |
| Booleans | `true` / `false` (case-insensitive), else 400 |
| Multi-value | Repeat the parameter: `status=Pending&status=Confirmed` |

A page past the end returns 200 with empty `items`.

### 1.5 Shared DTOs
```text
PagedResponse<T>   { items: T[], page: int, pageSize: int, totalCount: int, totalPages: int }   // totalPages = ceil(totalCount / pageSize); 0 when totalCount = 0
AcceptedResponse   { message: string }      // fixed text, identical for every outcome (anti-enumeration)
TokenResponse      { tokenType: "Bearer", accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt }
OrderItemRequest   { productId: Guid (required), quantity: int 1-1000 }
```

Field rule shorthands used below:

| Shorthand | Rule |
|---|---|
| `Email` | Required, valid address, ≤ 256 chars (D5). Compared case-insensitively (normalized) |
| `Name` | Required, trimmed, 1-100 chars (D5) |
| `Phone?` | Optional; E.164 `^\+[1-9][0-9]{1,14}$`, ≤ 20 chars (D5) |
| `Password` | 15-128 chars, counted in Unicode code points; no composition rules; all Unicode allowed (FR-003). Identity `RequiredLength = 15`, other Identity character rules off |
| `Reason?` | Optional, ≤ 500 chars |
| `Items` | `OrderItemRequest[]`, 1-50 entries, each `productId` at most once (FR-080) |
| `Paging` | `page`, `pageSize` per 1.4 |

Role-specific response DTOs: `isDeleted` appears only in Admin responses (D9). Customers never see `byUserId` (D9).

### 1.6 Concurrency: ETag / If-Match (FR-129)
- Products, customers and orders return `ETag` on GET, POST create and PUT. Value: strong ETag from the PostgreSQL `xmin` row version, e.g. `"4711"`. Clients must treat it as opaque.
- Order ETag uses the **order** row; every item or status change also updates the order row (P3), so the ETag always changes.
- PUT on products, customers, `/customers/me` and order items requires `If-Match`:
  - Missing → **428** `precondition-required`. `If-Match: *` counts as missing (it would skip the check). **Assumption.**
  - No listed ETag matches (strong comparison) → **412** `precondition-failed`.
  - A race after the check (EF Core concurrency token on `xmin`) → also **412**.
- DELETE, restore and POST commands do not use `If-Match` (right-sized; the stories do not ask for it).
- Users have no ETag (story common rules).
- Conditional GET (`If-None-Match` → 304) is not implemented this phase. **Assumption.**

### 1.7 Response headers
| Header | Where |
|---|---|
| `Location` | Every 201 (absolute path, e.g. `/api/v1/products/{id}`) |
| `ETag` | See 1.6 |
| `Retry-After` | Every 429 (seconds, from the limiter lease; ADR-0010) and 503 from the DB breaker |
| `Cache-Control: no-store` | **Every** response except product GETs: auth, users, customers, `/me`, orders, reports, audit logs, health, and all errors (NFR-06) |
| `Cache-Control: private, no-cache` | EP-22 and EP-23 (product data is not personal; clients may store it but must refetch) |
| Security headers | All responses: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`; no `Server`; HSTS outside Development (NFR-06) |

### 1.8 ProblemDetails (RFC 9457, FR-124)
All 4xx/5xx use `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`, `traceId`; validation adds `errors: { field: [messages] }`.
- `type` is a relative URI `/problems/{slug}`. The slug is the stable error code clients switch on. The URIs need not resolve this phase. **Assumption.**
- `instance` = request path **without** the query string (queries can hold search text).
- `traceId` = W3C trace ID of the request; the same value is in the logs (NFR-13).
- `errors` keys are camelCase; collections use `items[2].productId`; route and query keys use the parameter name.
- No stack trace, SQL or exception text outside Development.

| Status | `type` | Used for | Extensions |
|---|---|---|---|
| 400 | `/problems/validation-failed` | Shape, limits, malformed JSON or ID, unknown referenced product/customer, wrong current password | `errors` |
| 400 | `/problems/invalid-token` | Confirm-email and reset-password: wrong, expired, used token, unknown user (one generic `detail`) | - |
| 401 | `/problems/unauthorized` | Missing, invalid or expired access token | - |
| 401 | `/problems/invalid-credentials` | Every login failure (unknown, wrong password, locked, deleted, no password). Identical body | - |
| 401 | `/problems/invalid-refresh-token` | Refresh: unknown, expired, used, revoked, family limit, user state. Identical body | - |
| 403 | `/problems/forbidden` | Role not allowed | - |
| 403 | `/problems/email-not-confirmed` | Login with correct password, email unconfirmed | - |
| 404 | `/problems/not-found` | Missing, soft-deleted, or not owned (`/me`). Same body in all cases | - |
| 405 | `/problems/method-not-allowed` | Wrong method on a known route (e.g. POST `/audit-logs`) | - |
| 409 | `/problems/duplicate-sku` | Create/update/restore with an SKU used by an active product | `sku` |
| 409 | `/problems/duplicate-email` | Admin creates a customer/Admin, or sets an email already in use | - |
| 409 | `/problems/insufficient-stock` | Order create or item replace | `productIds: Guid[]` |
| 409 | `/problems/too-many-pending-orders` | Order create when the customer already has the maximum number of open Pending orders (default 5, FR-096, S-D4) | `maxPending` |
| 409 | `/problems/invalid-status-transition` | Status change or cancel not allowed from the current status | `currentStatus`, `allowedNextStatuses` |
| 409 | `/problems/invalid-order-state` | Item replace or delete when the order is not in the required status | `currentStatus` |
| 409 | `/problems/customer-has-open-orders` | Delete/erase with a Pending, Confirmed or Shipped order | - |
| 409 | `/problems/email-change-not-allowed` | PUT customer with a different email on a normal record (D14) | - |
| 409 | `/problems/not-deleted` | Restore of an item that is not deleted | - |
| 409 | `/problems/admin-safeguard` | Lock or demote self; lock or demote the last active Admin | - |
| 409 | `/problems/user-is-customer` | Assign Admin to a user with a Customer record | - |
| 409 | `/problems/user-deleted` | Unlock or reset-link for a soft-deleted user | - |
| 409 | `/problems/stock-out-of-range` | Stock adjustment result < 0 or > 1,000,000 | - |
| 409 | `/problems/concurrency-conflict` | Write race not covered by `If-Match` (e.g. two confirms at once, `xmin` retry exhausted) | - |
| 412 | `/problems/precondition-failed` | Stale `If-Match` | - |
| 413 | `/problems/payload-too-large` | Body > 1 MB | - |
| 415 | `/problems/unsupported-media-type` | Body not JSON | - |
| 422 | `/problems/idempotency-key-reused` | Stretch: same `Idempotency-Key`, different body | - |
| 428 | `/problems/precondition-required` | Missing `If-Match` | - |
| 429 | `/problems/rate-limited` | Rate-limit policy exceeded | - (`Retry-After` header) |
| 500 | `/problems/internal-error` | Unhandled exception (generic text) | - |
| 503 | `/problems/service-unavailable` | Database unavailable / breaker open (NFR-03) | - (`Retry-After`) |

### 1.9 Error mapping order
Checked in this order; the first that applies wins (permission matrix):

| # | Status | Where it is decided |
|---|---|---|
| 1 | 429 | Rate limiter middleware (after authentication, before authorization; ADR-0010) |
| 2 | 401 | Authorization middleware (no valid principal) |
| 3 | 403 | Authorization middleware (policy). Runs **before** binding, so a Customer with a bad body or unknown ID gets 403, not 400/404 |
| 4 | 400 | Binding + FluentValidation (shape only). 413/415 also happen here |
| 5 | 404 | Handler: load by ID with owner filter |
| 6 | 428 / 412 | Handler: `If-Match` after the resource is found (US-203 AC6) |
| 7 | 400 (reference) | Handler: referenced product/customer missing or deleted, wrong current password. After 404/412, before 409 |
| 8 | 409 | Handler: business rule. Domain errors are `Result` values, mapped by `ResultHttpExtensions` |
| 9 | 2xx | 200 read/PUT, 201 create, 202 anti-enumeration, 204 command |

Not repeated per endpoint below: **429** (every endpoint except health), **413/415** (every endpoint with a body), **500/503** (every endpoint).

### 1.10 Authorization policies
| Policy | Rule | FastEndpoints |
|---|---|---|
| (fallback) | Authenticated user. Every endpoint without an explicit rule gets this | `FallbackPolicy` |
| `Anonymous` | No token needed. Each use has a reason in the endpoint `Summary` | `AllowAnonymous()` |
| `Authenticated` | Valid access token, role Customer or Admin | `Policies("Authenticated")` |
| `AdminOnly` | Role `Admin` | `Policies("AdminOnly")` |
| `CustomerOnly` | Role `Customer` **and** a valid `customer_id` claim | `Policies("CustomerOnly")` |

Resource rules:
- **Own (customer):** customer ID comes only from the `customer_id` claim (`ICurrentUser.CustomerId`). Every query adds `CustomerId == current`. Not owned = missing = deleted → 404 with the same body. BOLA test per `/me` resource endpoint (NFR-04).
- **Self (user):** user ID comes only from `sub`.
- Policy names live in one `Policies` class. Endpoints use `Policies(...)`, not `Roles(...)`, so the rule changes in one place. [Solution structure](solution-structure.md) follows this (`Policies(AdminOnly)` in the worked slice).
- Template note: `CustomerOnly` and the `customer_id` claim are Customer leaks that the template strip guide removes ([solution structure](solution-structure.md) section 13).

### 1.11 Rate-limit policies (ADR-0010)
`auth-strict` 10/min per IP, one shared bucket (D16) for EP-01, 02, 03, 04, **09**, 10, 11, 37 · `refresh` 30/min per IP · `general` 300/min per user, 60/min per IP when anonymous · `none` for health · global concurrency limit: `/reports/*` at most 4 at a time, 5th → 429 (S-D7).

### 1.12 Cache rules (ADR-0009)
Only product reads are cached (FR-130). Keys:
- Item: `products:v1:item:{scope}:{productId}` (`scope` = `customer` | `admin`).
- List: `products:v1:list:{scope}:g{generation}:{queryHash}`; generation key `products:v1:list-gen`.
- Admin `includeDeleted=true` (EP-22, EP-23) **bypasses** the cache.
- "Invalidate P" below means: after the DB commit and before the response, remove the item keys of the affected product IDs (both scopes) and set a new list generation.
- Every other endpoint: no server cache, `Cache-Control: no-store`.

### 1.13 Audit (FR-128, P1, D4)
"Audit" lists the `AuditLogs` actions written in the same transaction. Actor: user ID from `sub`, `anonymous` or `system`. Personal-data fields are written as `"changed"`. Refresh-token rows and the failed-login counter are never audited (logged and measured instead).

---

## 2. Index

Legend: Pol = policy (`Anon`, `Auth`, `Admin`, `Cust`); RL = rate limit (`strict`, `refresh`, `general`, `-`); Cache = `P` product cache rule, `ns` = no-store; Audit = `-` none. Slice `St` = stretch.

| EP | Method | Route (`/api/v1`) | Endpoint class | Pol | RL | Cache | Audit | Slice |
|---|---|---|---|---|---|---|---|---|
| 01 | POST | /auth/register | `RegisterEndpoint` | Anon | strict | ns | Insert, RoleAssigned | S1 |
| 02 | POST | /auth/confirm-email | `ConfirmEmailEndpoint` | Anon | strict | ns | Update | S1 |
| 03 | POST | /auth/resend-confirmation | `ResendConfirmationEndpoint` | Anon | strict | ns | - | S1 |
| 04 | POST | /auth/login | `LoginEndpoint` | Anon | strict | ns | Update (lockout only) | S1 |
| 05 | POST | /auth/refresh | `RefreshTokenEndpoint` | Anon | refresh | ns | - | S1 |
| 06 | POST | /auth/logout | `LogoutEndpoint` | Auth | general | ns | - | S1 |
| 07 | POST | /auth/logout-all | `LogoutAllEndpoint` | Auth | general | ns | - | S1 |
| 08 | GET | /auth/me | `GetCurrentUserEndpoint` | Auth | general | ns | - | S1 |
| 09 | POST | /auth/change-password | `ChangePasswordEndpoint` | Auth | strict | ns | Update | S1 |
| 10 | POST | /auth/forgot-password | `ForgotPasswordEndpoint` | Anon | strict | ns | - | S2 |
| 11 | POST | /auth/reset-password | `ResetPasswordEndpoint` | Anon | strict | ns | Update | S2 |
| 12 | GET | /admin/users | `ListUsersEndpoint` | Admin | general | ns | - | S2 |
| 13 | GET | /admin/users/{userId} | `GetUserEndpoint` | Admin | general | ns | - | S2 |
| 14 | POST | /admin/users/{userId}/lock | `LockUserEndpoint` | Admin | general | ns | Update | S2 |
| 15 | POST | /admin/users/{userId}/unlock | `UnlockUserEndpoint` | Admin | general | ns | Update | S2 |
| 16 | PUT | /admin/users/{userId}/roles/{role} | `AssignUserRoleEndpoint` | Admin | general | ns | RoleAssigned | S2 |
| 17 | DELETE | /admin/users/{userId}/roles/{role} | `RemoveUserRoleEndpoint` | Admin | general | ns | RoleRemoved | S2 |
| 18 | GET | /admin/roles | `ListRolesEndpoint` | Admin | general | ns | - | S2 |
| 19 | POST | /admin/users | `CreateAdminUserEndpoint` | Admin | general | ns | Insert, RoleAssigned | S2 |
| 20 | POST | /admin/users/{userId}/revoke-sessions | `RevokeUserSessionsEndpoint` | Admin | general | ns | SessionsRevoked | **St** |
| 21 | POST | /admin/users/{userId}/password-reset-link | `SendUserPasswordResetLinkEndpoint` | Admin | general | ns | - | **St** |
| 22 | GET | /products | `ListProductsEndpoint` | Auth | general | P read | - | S1 |
| 23 | GET | /products/{productId} | `GetProductByIdEndpoint` | Auth | general | P read | - | S1 |
| 24 | POST | /products | `CreateProductEndpoint` | Admin | general | P inval | Insert | S1 |
| 25 | PUT | /products/{productId} | `UpdateProductEndpoint` | Admin | general | P inval | Update | S1 |
| 26 | DELETE | /products/{productId} | `DeleteProductEndpoint` | Admin | general | P inval | SoftDelete | S1 |
| 27 | POST | /products/{productId}/restore | `RestoreProductEndpoint` | Admin | general | P inval | Restore | S1 |
| 28 | POST | /products/{productId}/stock-adjustments | `AdjustProductStockEndpoint` | Admin | general | P inval | Update | **St** |
| 29 | POST | /customers | `CreateCustomerEndpoint` | Admin | general | ns | Insert, RoleAssigned | S2 |
| 30 | GET | /customers | `ListCustomersEndpoint` | Admin | general | ns | - | S2 |
| 31 | GET | /customers/{customerId} | `GetCustomerByIdEndpoint` | Admin | general | ns | - | S2 |
| 32 | PUT | /customers/{customerId} | `UpdateCustomerEndpoint` | Admin | general | ns | Update | S2 |
| 33 | DELETE | /customers/{customerId} | `DeleteCustomerEndpoint` | Admin | general | ns | SoftDelete | S2 |
| 34 | POST | /customers/{customerId}/restore | `RestoreCustomerEndpoint` | Admin | general | ns | Restore | **St** |
| 35 | GET | /customers/me | `GetMyProfileEndpoint` | Cust | general | ns | - | S2 |
| 36 | PUT | /customers/me | `UpdateMyProfileEndpoint` | Cust | general | ns | Update | S2 |
| 37 | POST | /customers/me/erase | `EraseMyAccountEndpoint` | Cust | **strict** | ns | SoftDelete | S2 |
| 38 | POST | /orders | `CreateOrderEndpoint` | Admin | general | P inval | Insert, Update | S3 |
| 39 | GET | /orders | `ListOrdersEndpoint` | Admin | general | ns | - | S3 |
| 40 | GET | /orders/{orderId} | `GetOrderByIdEndpoint` | Admin | general | ns | - | S3 |
| 41 | PUT | /orders/{orderId}/items | `ReplaceOrderItemsEndpoint` | Admin | general | P inval | Insert, Update, SoftDelete | S3 |
| 42 | POST | /orders/{orderId}/status | `ChangeOrderStatusEndpoint` | Admin | general | ns | Update, Insert | S3 |
| 43 | POST | /orders/{orderId}/cancel | `CancelOrderEndpoint` | Admin | general | P inval | Update, Insert | S3 |
| 44 | DELETE | /orders/{orderId} | `DeleteOrderEndpoint` | Admin | general | ns | SoftDelete | **St** |
| 45 | POST | /customers/me/orders | `PlaceMyOrderEndpoint` | Cust | general | P inval | Insert, Update | S3 |
| 46 | GET | /customers/me/orders | `ListMyOrdersEndpoint` | Cust | general | ns | - | S3 |
| 47 | GET | /customers/me/orders/{orderId} | `GetMyOrderEndpoint` | Cust | general | ns | - | S3 |
| 48 | POST | /customers/me/orders/{orderId}/cancel | `CancelMyOrderEndpoint` | Cust | general | P inval | Update, Insert | S3 |
| 49 | PUT | /customers/me/orders/{orderId}/items | `ReplaceMyOrderItemsEndpoint` | Cust | general | P inval | Insert, Update, SoftDelete | **St** |
| 50 | GET | /reports/order-summaries | `GetOrderSummariesEndpoint` | Admin | general | ns | - | S4 |
| 51 | GET | /customers/{customerId}/order-history | `GetCustomerOrderHistoryEndpoint` | Admin | general | ns | - | S4 |
| 52 | GET | /reports/sales | `GetSalesReportEndpoint` | Admin | general | ns | - | S4 |
| 53 | GET | /reports/low-stock | `GetLowStockReportEndpoint` | Admin | general | ns | - | S4 |
| 54 | GET | /reports/top-products | `GetTopProductsReportEndpoint` | Admin | general | ns | - | S4 (Could) |
| 55 | GET | /audit-logs | `ListAuditLogsEndpoint` | Admin | general | ns | - | S4 (Should) |
| 56 | GET | /health/live *(no prefix)* | `LivenessEndpoint` | Anon | - | ns | - | S1 |
| 57 | GET | /health/ready *(no prefix)* | `ReadinessEndpoint` | Anon | - | ns | - | S1 |

Totals: 57 endpoints; 51 in S1-S4, 6 stretch (EP-20, 21, 28, 34, 44, 49).

---

## 3. Auth and identity (EP-01..11)

All: `Cache-Control: no-store`. Tokens and passwords never logged.

**EP-01 Register** · `Anonymous` (sign-up) · S1
- Body `RegisterRequest`: `email` Email · `password` Password · `firstName` Name · `lastName` Name · `phone` Phone?
- 202 `AcceptedResponse` (identical for new and existing email, FR-002) · 400 `validation-failed`.
- One transaction: user (unconfirmed) + Customer role + Customer record. Existing email: no write; fake sender logs "account already exists".
- Audit (new only): Insert user, RoleAssigned, Insert customer; actor `anonymous`.

**EP-02 Confirm email** · `Anonymous` (link from email) · S1
- Body: `userId` Guid required · `token` string required, ≤ 2000.
- 204 (also when already confirmed) · 400 `validation-failed` · 400 `invalid-token` (generic for bad/expired token, unknown or deleted user).
- Audit: Update (`emailConfirmed`), actor `anonymous`. None when already confirmed.

**EP-03 Resend confirmation** · `Anonymous` · S1
- Body: `email` Email. → 202 `AcceptedResponse` always · 400. No write, no audit.

**EP-04 Login** · `Anonymous` · S1
- Body `LoginRequest`: `email` required, ≤ 256 · `password` required, ≤ 1024 (DoS cap; policy not checked here).
- 200 `TokenResponse` · 400 · 401 `invalid-credentials` · 403 `email-not-confirmed` (correct password only; the password is checked before the confirmed and locked checks, S-D6).
- 5 failures in a row → lock 15 min (lock type `failed-logins`). Existing sessions are **not** revoked (S-D2).
- Equal work for unknown emails (dummy hash) so timing does not reveal accounts (S-D6). Register, forgot and resend also have a response floor (500 / 250 / 250 ms).
- Audit: only when the lockout starts (Update user). Token issue is logged, not audited.

**EP-05 Refresh** · `Anonymous` (the refresh token is the credential) · `refresh` · S1
- Body: `refreshToken` string required, ≤ 200.
- 200 `TokenResponse` (new pair, same family) · 400 · 401 `invalid-refresh-token` (reuse also revokes the family). Details: ADR-0008. No audit.

**EP-06 Logout** · `Authenticated` · Self · S1
- Body: `refreshToken` required, ≤ 200. → 204 always (unknown, revoked or another user's token: no change) · 400 · 401. No audit.

**EP-07 Logout all** · `Authenticated` · Self · S1
- No body. → 204 · 401. Revokes all the caller's active tokens (`logout-all`). No audit.

**EP-08 Get current user** · `Authenticated` · Self · S1
- 200 `CurrentUserResponse { userId, email, emailConfirmed, roles: string[], customerId: Guid? }` · 401. Read from the DB, so values are current. **Assumption.**

**EP-09 Change password** · `Authenticated` · Self · **`auth-strict`** · S1
- Body: `currentPassword` required, ≤ 1024 · `newPassword` Password (blocklist applies, FR-003).
- 204 · 400 (`errors.currentPassword` when wrong; counts toward the FR-008 lockout, S-D2) · 401 · 429. Revokes all refresh tokens (`password-changed`) and rotates the security stamp.
- Audit: Update user (no hash or stamp).

**EP-10 Forgot password** · `Anonymous` · S2
- Body: `email` Email. → 202 `AcceptedResponse` always · 400. Link only for existing, non-deleted accounts. Reset token lives **1 h** (confirm token: 24 h; separate providers, S-D3). No audit.

**EP-11 Reset password** · `Anonymous` · S2
- Body: `email` Email · `token` required, ≤ 2000 · `newPassword` Password.
- 204 · 400 `validation-failed` · 400 `invalid-token` (generic).
- Sets password, confirms email, clears failed count and a `failed-logins` lock; never an `admin` lock (D1). Revokes all tokens (`password-reset`).
- Audit: Update user.

## 4. Admin: users and roles (EP-12..21)

All: `AdminOnly`; `general`; `no-store`; no ETag. Customer → 403 for any ID and any body.

```text
UserListItem  { id, email, firstName, lastName, emailConfirmed, roles[], isLocked, customerId?, createdAt, isDeleted }
UserResponse  { id, email, firstName, lastName, emailConfirmed, roles[], lockoutEnd?, lockType?, failedLoginCount, customerId?, createdAt, isDeleted }
```
Assumption: `firstName`/`lastName` come from the Identity user for Admins (D6) and from the Customer record for Customers. `lockType` = `failed-logins` | `admin` | null.

| EP | Request | Success | Errors | Audit |
|---|---|---|---|---|
| 12 List users | Query: Paging · `sort` ∈ {`email` (default), `createdAt`} · `email` 2-100 · `role` ∈ {Admin, Customer} · `isLocked` · `emailConfirmed` · `includeDeleted` | 200 `PagedResponse<UserListItem>` | 400, 401, 403 | - |
| 13 Get user | Route `userId` | 200 `UserResponse` (deleted users: 200, `isDeleted: true`, anonymized) | 400, 401, 403, 404 | - |
| 14 Lock | Route `userId`; body `until?` DateTimeOffset (future, with offset; null = no end) · `reason` Reason? | 204 (idempotent) | 400, 401, 403, 404, 409 `admin-safeguard` | Update (lockout end, lock type `admin`, reason) if changed. Revokes tokens (`admin-lock`) |
| 15 Unlock | Route `userId`; no body | 204 (no-op if not locked) | 400, 401, 403, 404, 409 `user-deleted` | Update if changed |
| 16 Assign role | Route `userId`, `role` (must equal `Admin`, case-insensitive) | 204 (no-op if already Admin) | 400 `errors.role`, 401, 403, 404, 409 `user-is-customer` | RoleAssigned. Revokes tokens (`role-changed`) |
| 17 Remove role | Route `userId`, `role` = `Admin` | 204 (no-op if not Admin) | 400, 401, 403, 404, 409 `admin-safeguard` | RoleRemoved. Revokes tokens (`role-changed`) |
| 18 List roles | - | 200 `RoleResponse[] { name, userCount }`, sorted by name, not paged | 401, 403 | - |
| 19 Create Admin | Body `email` Email · `firstName` Name · `lastName` Name | 201 `UserResponse`, `Location: /api/v1/admin/users/{id}` | 400, 401, 403, 409 `duplicate-email` | Insert user, RoleAssigned; reset link logged after commit |
| 20 Revoke sessions **(St)** | Route `userId`; no body | 204 (also for self) | 400, 401, 403, 404 | SessionsRevoked (new action, Assumption) |
| 21 Reset link **(St)** | Route `userId`; no body | 204 | 400, 401, 403, 404, 409 `user-deleted` | - (no DB write) |

Last-Admin rules (EP-14, EP-17): the handler locks the active Admin user rows (`SELECT … FOR UPDATE`, ordered by ID) before it counts them, so two Admins acting on each other at once cannot both succeed (US-014 AC8, US-017 AC3). **Assumption:** ADR-0007 does not cover this yet and should add it.

## 5. Products (EP-22..28)

```text
ProductResponse  { id, name, sku, description?, price, stockQuantity, createdAt, isDeleted (Admin only) }
ProductRequest   { name: 1-200 · sku: 1-50, ^[A-Za-z0-9-]+$ (stored upper-case) · description?: ≤ 2000 · price: 0.01-9,999,999,999.99, ≤ 2 decimals · stockQuantity: int 0-1,000,000 }
```

| EP | Pol | Request | Success + headers | Errors | Cache | Audit |
|---|---|---|---|---|---|---|
| 22 List | Authenticated | Query: Paging · `sort` ∈ {`name` (default), `price`, `sku`, `stockQuantity`, `createdAt`} · `search` 2-100 (name or SKU) · `minPrice`, `maxPrice` ≥ 0 · `inStock` (true = stock > 0, false = 0) · `includeDeleted` (Admin only) | 200 `PagedResponse<ProductResponse>`; `Cache-Control: private, no-cache` | 400, 401 | Read: list key by scope + generation + query hash. Admin `includeDeleted=true` bypasses | - |
| 23 Get | Authenticated | Route `productId`; query `includeDeleted` (Admin only, D13) | 200 `ProductResponse` + `ETag`; `private, no-cache` | 400, 401, 404 | Read: item key by scope. Deleted never cached. Admin `includeDeleted=true` bypasses | - |
| 24 Create | AdminOnly | Body `ProductRequest` | 201 + `Location`, `ETag` | 400, 401, 403, 409 `duplicate-sku` | Inval: new list generation | Insert |
| 25 Update | AdminOnly | Route `productId`; header `If-Match`; body `ProductRequest` (full replace) | 200 `ProductResponse` + new `ETag` | 400, 401, 403, 404, 428, 412, 409 `duplicate-sku` | Inval P | Update (changed fields) |
| 26 Delete | AdminOnly | Route `productId` | 204 | 400, 401, 403, 404 (also already deleted) | Inval P | SoftDelete |
| 27 Restore | AdminOnly | Route `productId`; no body | 204 | 400, 401, 403, 404, 409 `not-deleted`, 409 `duplicate-sku` | Inval P | Restore |
| 28 Stock adjust **(St)** | AdminOnly | Route `productId`; body `delta` int ≠ 0, −1,000,000..1,000,000 · `reason` 1-200 | 200 `{ productId, stockQuantity }` + new `ETag` | 400, 401, 403, 404, 409 `stock-out-of-range`, 409 `concurrency-conflict` | Inval P | Update (`stockQuantity`) |

Deleted products can never be ordered. Products with order lines can still be deleted (lines unchanged).

## 6. Customers (EP-29..37)

```text
CustomerResponse      { id, userId, email, firstName, lastName, phone?, emailConfirmed, isLocked, isDeleted, createdAt }    // Admin
CustomerListItem      { id, userId, firstName, lastName, email, phone?, createdAt, isDeleted }                             // Admin
MyProfileResponse     { id, email, firstName, lastName, phone?, emailConfirmed, createdAt }                                // Customer
```

All: `no-store`; never cached; `ETag` on single-customer responses.

| EP | Pol | Request | Success + headers | Errors | Audit |
|---|---|---|---|---|---|
| 29 Create | AdminOnly | Body `email` Email · `firstName` Name · `lastName` Name · `phone` Phone? | 201 `CustomerResponse` + `Location: /api/v1/customers/{id}`, `ETag` | 400, 401, 403, 409 `duplicate-email` | Insert customer, Insert user, RoleAssigned; reset link logged after commit |
| 30 List | AdminOnly | Query: Paging · `sort` ∈ {`lastName` (default), `email`, `createdAt`} · `search` 2-100 (first, last name, email) · `createdFrom`, `createdTo` · `includeDeleted` | 200 `PagedResponse<CustomerListItem>` | 400, 401, 403 | - |
| 31 Get | AdminOnly | Route `customerId`; query `includeDeleted` (D13) | 200 `CustomerResponse` + `ETag` | 400, 401, 403, 404 | - |
| 32 Update | AdminOnly | Route `customerId`; `If-Match`; body `firstName` Name · `lastName` Name · `phone` Phone? (null clears) · `email?` | 200 `CustomerResponse` + new `ETag` | 400, 401, 403, 404, 428, 412, 409 `email-change-not-allowed` | Update (PII fields as `"changed"`) |
| 33 Delete | AdminOnly | Route `customerId` | 204 | 400, 401, 403, 404, 409 `customer-has-open-orders` | SoftDelete customer and user (no PII values) |
| 34 Restore **(St)** | AdminOnly | Route `customerId`; no body | 204 (stays anonymized and locked) | 400, 401, 403, 404, 409 `not-deleted` | Restore |
| 35 Get own | CustomerOnly · Own | - (any `customerId` query is ignored) | 200 `MyProfileResponse` + `ETag` | 401, 403, 404 | - |
| 36 Update own | CustomerOnly · Own | `If-Match`; body `firstName` Name · `lastName` Name · `phone` Phone? | 200 `MyProfileResponse` + new `ETag` | 400, 401, 403, 404, 428, 412 | Update |
| 37 Erase own | CustomerOnly · Own · **`auth-strict`** | Body `currentPassword` required, ≤ 1024 | 204 | 400 (`errors.currentPassword`; wrong password counts toward lockout), 401, 403, 404, 409 `customer-has-open-orders` | SoftDelete customer and user |

Notes:
- EP-32 `email`: absent or equal to the current email → ignored. Different → 409 `email-change-not-allowed` (D14). With stretch EP-34, a restored record may change email (US-110 AC9). **Assumption** (equal email is ignored so GET-modify-PUT clients do not fail).
- EP-33/EP-37: one transaction: soft delete, anonymize (email derived from the record ID on a reserved domain), soft delete and lock the user, revoke tokens (`account-deleted`). Check order for EP-37: password (400) before open orders (409).
- EP-35/36/37 after the caller's own record is deleted but the access token is still valid: 404 (R1, ADR-0008).
- EP-31 test (C10): Admin get of a deleted customer with `includeDeleted=true` returns 200 with the anonymized `email` present. The user join must also ignore the soft-delete filter, or the row or the email is dropped silently.

## 7. Orders (EP-38..49)

```text
OrderLineResponse      { productId, quantity, unitPrice, lineTotal }
OrderHistoryResponse   { from?, to, byUserId (Admin only), at, reason? }
OrderResponse          { id, customerId, status, total, currency, createdAt, lastModifiedAt?, items: OrderLineResponse[], history: OrderHistoryResponse[], allowedNextStatuses: string[] }   // Admin
MyOrderResponse        { id, status, total, currency, createdAt, lastModifiedAt?, items[], history[] (no byUserId) }                                                                        // Customer
OrderListItem          { id, customerId, status, total, currency, createdAt }       // Admin
MyOrderListItem        { id, status, total, currency, createdAt }                   // Customer
```
`status` ∈ Pending, Confirmed, Shipped, Completed, Cancelled. `allowedNextStatuses` is an additive helper for clients. **Assumption.**

All: `no-store`; order data never cached; order writes that change stock run "Inval P" for the affected products (D15). Stock rows are locked `SELECT … FOR UPDATE` in product ID order (P3).

| EP | Pol | Request | Success + headers | Errors | Audit |
|---|---|---|---|---|---|
| 38 Create | AdminOnly | Body `customerId` Guid required · `items` Items. Header `Idempotency-Key` (St) | 201 `OrderResponse` + `Location: /api/v1/orders/{id}`, `ETag` | 400 (shape; unknown/deleted customer → `errors.customerId`; unknown/deleted product → `errors.items[i].productId`), 401, 403, 409 `too-many-pending-orders` (customer already has 5 open Pending orders, FR-096), 409 `insufficient-stock`, 422 (St) | Insert order, items, history; Update products |
| 39 List | AdminOnly | Query: Paging · `sort` ∈ {`createdAt` (default `-createdAt`), `total`, `status`} · `status` (multi) · `customerId` · `createdFrom`, `createdTo` · `minTotal`, `maxTotal` | 200 `PagedResponse<OrderListItem>` | 400, 401, 403 | - |
| 40 Get | AdminOnly | Route `orderId` | 200 `OrderResponse` + `ETag` | 400, 401, 403, 404 | - |
| 41 Replace items | AdminOnly | Route `orderId`; `If-Match`; body `items` Items | 200 `OrderResponse` + new `ETag` | 400, 401, 403, 404, 428, 412, 400 (reference), 409 `invalid-order-state`, 409 `insufficient-stock` | Insert/Update/SoftDelete items; Update order, products |
| 42 Change status | AdminOnly | Route `orderId`; body `status` (enum, required) · `reason` Reason? | 204 | 400, 401, 403, 404, 409 `invalid-status-transition`, 409 `concurrency-conflict` | Update order; Insert history |
| 43 Cancel | AdminOnly | Route `orderId`; body (optional) `reason` Reason? | 204 | 400, 401, 403, 404, 409 `invalid-status-transition` (only Pending/Confirmed) | Update order, products; Insert history |
| 44 Delete **(St)** | AdminOnly | Route `orderId` | 204 | 400, 401, 403, 404, 409 `invalid-order-state` (only Cancelled) | SoftDelete |
| 45 Place own | CustomerOnly · Own | Body `items` Items (any `customerId` ignored). Header `Idempotency-Key` (St) | 201 `MyOrderResponse` + `Location: /api/v1/customers/me/orders/{id}`, `ETag` | 400, 401, 403, 409 `too-many-pending-orders` (FR-096), 409 `insufficient-stock`, 422 (St) | As EP-38 (actor = customer's user) |
| 46 List own | CustomerOnly · Own | Query: Paging · `sort` ∈ {`createdAt` (default `-createdAt`), `total`} · `status` (multi) · `createdFrom`, `createdTo` | 200 `PagedResponse<MyOrderListItem>` | 400, 401, 403 | - |
| 47 Get own | CustomerOnly · Own | Route `orderId` | 200 `MyOrderResponse` + `ETag` | 400, 401, 403, 404 (other customer's = unknown) | - |
| 48 Cancel own | CustomerOnly · Own | Route `orderId`; body (optional) `reason` Reason? | 204 | 400, 401, 403, 404 (before any 409), 409 `invalid-status-transition` (only Pending) | As EP-43 |
| 49 Replace own items **(St)** | CustomerOnly · Own | Route `orderId`; `If-Match`; body `items` Items | 200 `MyOrderResponse` + new `ETag` | 400, 401, 403, 404, 428, 412, 409 `invalid-order-state`, 409 `insufficient-stock` | As EP-41 |

Notes:
- EP-42: allowed only Pending → Confirmed → Shipped → Completed. `Cancelled` or `Pending` as target → 409 with `allowedNextStatuses`; for `Cancelled` the `detail` points to EP-43.
- EP-43/48 body is optional: no body, `{}` or `{ "reason": "..." }`. `Content-Type` is required only when a body is sent.
- Stretch `Idempotency-Key` (FR-095): 1-100 printable ASCII chars, stored per user for 24 h with a body hash. Same key + same body → the first response (status, body, `Location`). Same key + other body → 422.

## 8. Reports and audit (EP-50..55)

All: `AdminOnly`; `general`; `no-store`; Dapper reads; read-only; never cached. Every view and function excludes soft-deleted orders and lines and keeps deleted customers (anonymized) and products (FR-106, D2).

| EP | Request | Success | Errors |
|---|---|---|---|
| 50 Order summaries | Same query as EP-39 | 200 `PagedResponse<OrderSummaryItem { orderId, customerId, customerName, status, itemCount, totalUnits, total, currency, createdAt }>` | 400, 401, 403 |
| 51 Customer order history | Route `customerId`; query Paging (`sort` ignored; fixed `createdAt` desc, then id) | 200 `PagedResponse<CustomerOrderHistoryItem { orderId, status, itemCount, total, currency, createdAt }>` | 400, 401, 403, 404 (unknown or deleted customer) |
| 52 Sales | Query `from`, `to` required, `from < to`, ≤ 366 days · `groupBy` ∈ {`day` (default), `week`, `month`} (UTC; ISO weeks start Monday; edge periods cut to the range) | 200 `SalesReportResponse { from, to, groupBy, currency, periods: [{ periodStart, periodEnd, orderCount, units, revenue }], totals: { orderCount, units, revenue } }` (empty periods = zeros) | 400, 401, 403 |
| 53 Low stock | Query `threshold` int 0-1,000,000 (default from config, 10); Paging (`sort` ignored; stock asc, then id) | 200 `PagedResponse<LowStockItem { id, sku, name, stockQuantity }>` | 400, 401, 403 |
| 54 Top products (Could) | Query `from`, `to` (as EP-52) · `top` int 1-50, default 10 | 200 `TopProductsResponse { from, to, currency, items: [{ productId, sku, name, units, revenue }] }` (not paged; capped by `top`; units desc, revenue desc, productId) | 400, 401, 403 |
| 55 Audit logs (Should) | Query: Paging (`sort` ignored; newest first, then id) · `entityType` ≤ 100 · `entityId` ≤ 100 · `userId` ≤ 64 (GUID, `anonymous` or `system`) · `action` ∈ {Insert, Update, SoftDelete, Restore, RoleAssigned, RoleRemoved, SessionsRevoked} · `from`, `to` | 200 `PagedResponse<AuditLogItem { id, entityType, entityId, action, changes: [{ field, old, new }], userId, timestamp, traceId }>` | 400, 401, 403; other methods → 405. Each call logs security event `AuditLogQueried` (S-D5, ADR-0013) |

EP-52/54 count only Confirmed, Shipped and Completed orders, by order `createdAt` (UTC).

## 9. Health (EP-56..57, D10)

| EP | Route | Checks | Responses | Body |
|---|---|---|---|---|
| 56 Liveness | GET `/health/live` | Process only (no dependencies) | 200 `Healthy` | `{ status }` |
| 57 Readiness | GET `/health/ready` | `postgres` (`SELECT 1`), `valkey` (`PING`); each check times out at 500 ms so the total stays ≤ 1 s (NFR-14) | 200 `Healthy`; 200 `Degraded` (Valkey down); 503 `Unhealthy` (PostgreSQL down) | `{ status }`; Development adds `checks: [{ name, status, durationMs }]` |

- `Anonymous` (probes must work without a token; an expired bearer token is ignored, never 401). Rate limit: none (`DisableRateLimiting()`). `Cache-Control: no-store`. Security headers apply.
- Own small status body, not ProblemDetails (D10). Outside Development: no exception text, host names, versions or connection strings.
- Built as FastEndpoints endpoints that call `HealthCheckService` with a tag filter, so they appear in OpenAPI (FR-133, NFR-23). **Assumption** (the alternative, `MapHealthChecks`, does not show in the OpenAPI document).

---

## 10. Open points
1. Conditional GET (`If-None-Match` → 304) for products: not built now (answered by A-D12); `Cache-Control: private, no-cache` keeps the door open.
2. R1 and the parallel-refresh grace window: decided (S-D1 stamp check; S-D6 strict refresh), ADR-0008.
3. `SessionsRevoked` audit action (stretch EP-20) extends the FR-128 action list (answered by A-D17).
4. Resolved: solution structure uses `Policies(...)` and the list generation key.
5. Resolved: ADR-0011 has the outer 250 ms total timeout and the ADR-0009 TTLs.
