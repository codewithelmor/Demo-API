# PRD: Orders API (working name)

| Field | Value |
|---|---|
| Status | Approved 2026-10-24 |
| Owner | Product owner (internal POC) |
| Client / sponsor | Internal proof of concept |
| Last updated | 2026-10-27 (security decisions S-D1..S-D8, section 18) |
| Mode | Standard |

Related: [concept](../01-concept/concept.md) · [endpoint list](endpoint-list.md) · [permission matrix](permission-matrix.md) · [summary](../00-summary.md)

## 1. Problem
Staff need one secure back-end API to manage products, customers and orders. Customers need safe self-service for their own data and orders. The bigger problem is that there is no proven, reusable secure .NET 10 API reference. Every client API starts from scratch and repeats mistakes such as BOLA, weak token handling and untestable data access.

## 2. Goals
- **North Star:** % of endpoints that meet the Definition of Done (section 15). Baseline 0%. Target 100% at the end of each slice.
- G1: Every endpoint enforces role and own-data rules, and tests prove it (100% authz and BOLA tests).
- G2: Rebuild from a fresh clone in ≤ 15 min with one compose command plus migrations, with no manual steps.
- G3: Product reads are fast with the cache and still work when Valkey is down (no 5xx).
- G4: 100% of writes are audited. No hard deletes.
- G5: The code is clean enough to copy as a template for the next client API.

## 3. Non-goals
- Front-end UI. API only.
- Payments, shipping, invoices, tax, discounts.
- Real email sending. A fake sender logs the links.
- Cloud deployment, high availability, multi-region.
- Multi-tenancy and multi-currency.
- Client-credentials flow, API keys, partner onboarding, external login (Entra ID, Google), MFA.
- Changing a customer's email through self-service (stretch at most).
- Automatic data purge or retention jobs (revisit before any production use).
- Supporting other databases or caches.

## 4. Users and roles
| Role | Description | Key needs |
|---|---|---|
| Anonymous | Not signed in | Register, confirm email, log in, refresh, reset password, health checks |
| Customer | Registered and confirmed. Has exactly one Customer record | Read the catalog. Manage own profile. Place, read and cancel own orders. Erase own account |
| Admin | Staff. Has no Customer record | Manage products, customers, orders, users and roles. Restore deleted items. Run reports. Read the audit log |
| API consumer (secondary) | Future web front end, M2M client, the developer reusing the template | Clear OpenAPI contract, `.http` and Postman files |

## 5. Scope by slice
| Slice | Content | Priority |
|---|---|---|
| S1 - Auth + products + cache | Register, confirm and resend, login, refresh with rotation and reuse detection, logout, logout-all, me, change password, seed Admin. Product CRUD, soft delete, restore. Valkey cache with fallback. Rate limiting, ProblemDetails, audit, health, paging conventions | Must |
| S2 - Customers + own-data rules | Forgot and reset password. Admin creates a customer. Customer CRUD (Admin) and `/me` (Customer). Delete with anonymization. Restore. Admin user management: list, get, lock/unlock, assign/remove Admin role, list roles, create Admin | Must |
| S3 - Orders | Orders with line items and price snapshot. Stock decrement and restore with optimistic concurrency. Status machine and history. Customer create, list, get and cancel own orders. Admin create, list, get, edit items while Pending, transition, cancel | Must |
| S4 - Dapper reads | Order summary view. Customer order-history function (paged). Sales report by date range. Low-stock report. Audit-log query (Should). Top products (Could) | Must / Should / Could |
| Stretch | Admin: revoke a user's sessions, send a reset link for a user. Customer edits own Pending order items. Stock-adjustment endpoint. Order soft delete. Idempotency-Key on order create | Could |

Each slice is shippable. Must items are in slices 1-4. Should items can slip. Stretch items are cut first.

## 6. Functional requirements
Release codes: S1-S4 are slices; St = stretch.

### 6.1 Auth and identity
| ID | Requirement | MoSCoW | Release |
|---|---|---|---|
| FR-001 | Register (anonymous) takes email, password, first name, last name and optional phone. In one transaction it creates an unconfirmed Identity user, gives it the Customer role, and creates the Customer record. If any step fails, nothing is saved. | Must | S1 |
| FR-002 | Register always returns 202 with the same body, so callers cannot learn which emails exist. If the email already exists, the fake sender logs an "account already exists" notice instead of a confirmation link. | Must | S1 |
| FR-003 | Password policy: 15-128 characters, no composition rules, all Unicode allowed (NIST SP 800-63B-4). Passwords on a local blocklist of common or breached passwords are rejected on register, change and reset (S-D7). A breaking value returns 400 with field errors. Hashes use PBKDF2 with 220,000 iterations (`PasswordHasherOptions.IterationCount`); older hashes are upgraded at the next successful login. | Must | S1 |
| FR-004 | Confirm email takes the user ID and a token. A valid token marks the email confirmed and returns 204. Confirming again returns 204. An invalid or expired token returns 400 with a generic message. Tokens live 24 h. | Must | S1 |
| FR-005 | Resend confirmation always returns 202. A new link is logged only if the account exists and is not confirmed. | Must | S1 |
| FR-006 | Login takes email and password. Success returns an access JWT (15 min), a refresh token (7 days) and both expiry times. | Must | S1 |
| FR-007 | Login failure: unknown email, wrong password, locked, or deleted all return the same 401. A correct password with an unconfirmed email returns 403 with problem type `email-not-confirmed`. The password is checked **before** the confirmed and locked checks, so an unconfirmed account with a wrong password gets the generic 401 (S-D6). | Must | S1 |
| FR-008 | 5 failed logins in a row lock the account for 15 min. | Must | S1 |
| FR-009 | The access token holds `sub` (user ID), role(s), `customer_id` (Customers only), `jti`, `iat`, `exp`, `iss` and `aud`. | Must | S1 |
| FR-010 | Refresh: a valid, unused refresh token returns a new pair. The old token is marked used (rotation). Each new token gets a 7-day sliding expiry, and a token family has a 30-day absolute limit. | Must | S1 |
| FR-011 | Reuse of a used or revoked refresh token revokes the whole family and returns 401. An expired or unknown token returns 401. | Must | S1 |
| FR-012 | Refresh tokens are stored only as SHA-256 hashes, with family ID, issued, expires, used and revoked times, and the revoke reason. | Must | S1 |
| FR-013 | Logout (authenticated) revokes the given refresh token if it belongs to the caller. Always 204 (idempotent). | Must | S1 |
| FR-014 | Logout-all revokes all of the caller's active refresh tokens. 204. | Must | S1 |
| FR-015 | All of a user's refresh tokens are revoked on: logout-all, password change, password reset, Admin lock (FR-032), role change, and account delete. The automatic failed-login lockout (FR-008) does **not** revoke sessions, so 5 bad passwords from someone else cannot log the user out (S-D2). | Must | S1 (S2 for the reset, Admin lock, role and delete triggers) |
| FR-016 | `GET me` returns user ID, email, email-confirmed flag, roles and `customerId` (null for Admins). | Must | S1 |
| FR-017 | Change password (EP-09) needs the current password. Rate limit `auth-strict`. Wrong current password returns 400 and counts toward the FR-008 lockout (S-D2). Success returns 204 and revokes all refresh tokens. | Must | S1 |
| FR-018 | Forgot password always returns 202. A reset link is logged only if the account exists and is not deleted. Reset tokens live **1 h**; email-confirmation tokens (FR-004) stay 24 h. The two use separate token providers, and the Data Protection key ring is persisted so links survive a restart (S-D3). | Must | S2 |
| FR-019 | Reset password takes email, token and new password. Success sets the password, marks the email confirmed, clears the failed-login count, clears an automatic lockout (FR-008) or a restored-customer lock (FR-066), revokes all refresh tokens and returns 204. It never clears a lock set by an Admin (FR-032). An invalid token returns a generic 400. | Must | S2 |
| FR-020 | The fake email sender logs recipient, subject and link at Information level. Links are built from a configured base URL. It never logs passwords. | Must | S1 |
| FR-021 | At startup, if no Admin exists, the system seeds one Admin from configuration (pre-confirmed). If no Admin exists and the configuration is missing, startup fails with a clear log message. | Must | S1 |

### 6.2 Admin: user and role management (Admin only)
| ID | Requirement | MoSCoW | Release |
|---|---|---|---|
| FR-030 | List users, paged. Filters: `email` (contains), `role`, `isLocked`, `emailConfirmed`, `includeDeleted`. Sort: `email`, `createdAt`. | Must | S2 |
| FR-031 | Get a user: ID, email, confirmed flag, roles, lockout end, failed-login count, customerId, createdAt, isDeleted. Unknown user returns 404. | Must | S2 |
| FR-032 | Lock a user. Optional `until` (default: no end) and `reason`. Revokes refresh tokens. Locking yourself or the last active Admin returns 409. Idempotent. | Must | S2 |
| FR-033 | Unlock a user. Clears the lockout and the failed-login count. A deleted user returns 409 (use customer restore instead). | Must | S2 |
| FR-034 | Assign the `Admin` role. Only `Admin` can be assigned. A user with a Customer record returns 409. Already has the role: 204. Revokes refresh tokens. | Must | S2 |
| FR-035 | Remove the `Admin` role. Removing your own Admin role or the last Admin returns 409. Revokes refresh tokens. | Must | S2 |
| FR-036 | List roles with name and user count. | Must | S2 |
| FR-037 | Create an Admin user from email and name. The user is unconfirmed, has no password and gets the Admin role. A reset link is logged. A duplicate email returns 409. | Must | S2 |
| FR-038 | Revoke all sessions of a user. | Could | St |
| FR-039 | Send a password-reset link for a user (Admin-triggered). | Could | St |

### 6.3 Products
| ID | Requirement | MoSCoW | Release |
|---|---|---|---|
| FR-040 | Create a product (Admin). Fields and rules: name 1-200; SKU 1-50, A-Z, 0-9 and `-`, stored upper-case, unique among non-deleted products; description ≤ 2000; price 0.01-9,999,999,999.99; stock quantity 0-1,000,000. Returns 201 with `Location` and `ETag`. Duplicate SKU returns 409. | Must | S1 |
| FR-041 | List products, paged (Admin, Customer). Filters: `search` (name or SKU, 2-100 chars), `minPrice`, `maxPrice`, `inStock`. Admin only: `includeDeleted` (ignored for Customers). Sort: `name` (default), `price`, `sku`, `stockQuantity`, `createdAt`. | Must | S1 |
| FR-042 | Get a product by ID. A deleted product returns 404, also for Admins unless `includeDeleted=true`. Returns an `ETag`. | Must | S1 |
| FR-043 | Update a product (Admin). Full replace of the editable fields. Needs `If-Match` (412 or 428). Same validation as create. | Must | S1 |
| FR-044 | Delete a product (Admin): soft delete, 204. Already deleted returns 404. Existing order lines are unchanged. Deleted products cannot be ordered. | Must | S1 |
| FR-045 | Restore a product (Admin), 204. Not deleted returns 409. SKU now used by an active product returns 409. | Must | S1 |
| FR-046 | Product get-by-ID and list responses are cached. Any product write, stock change, soft delete or restore removes the affected cache entries before the response returns. | Must | S1 |
| FR-047 | Stock adjustment (Admin). Delta ≠ 0, reason 1-200 chars. A result below 0 returns 409. | Could | St |

### 6.4 Customers
| ID | Requirement | MoSCoW | Release |
|---|---|---|---|
| FR-060 | Create a customer (Admin): email, first name, last name, optional phone. In one transaction it creates the Identity user (Customer role, unconfirmed, no password) and the Customer record, then logs a reset link. Duplicate email returns 409. 201. | Must | S2 |
| FR-061 | List customers, paged (Admin). Filters: `search` (name or email), `createdFrom`, `createdTo`, `includeDeleted`. Sort: `lastName` (default), `email`, `createdAt`. | Must | S2 |
| FR-062 | Get a customer by ID (Admin): profile, userId, isLocked, emailConfirmed, isDeleted. Returns an `ETag`. | Must | S2 |
| FR-063 | Update a customer (Admin): first name, last name, phone. `If-Match` required. Email can be changed only on a restored, anonymized record (see FR-066). | Must | S2 |
| FR-064 | Delete a customer (Admin), in one transaction: soft delete the customer; anonymize name, email and phone; soft delete and lock the Identity user; revoke all refresh tokens; (audit rows never hold PII values, see P1, so no audit row is changed). Orders stay unchanged. If the customer has an order that is Pending, Confirmed or Shipped, return 409. 204. | Must | S2 |
| FR-065 | After delete, no structured PII column in any table (`AuditLogs` included) contains the old email, phone or full name. Free-text fields (reasons, descriptions) and log files are out of scope for this check. | Must | S2 |
| FR-066 | (Stretch, moved by pre-mortem decision P2) Restore a customer (Admin): un-delete the Customer record and the Identity user. Data stays anonymized and the user stays locked. The Admin may then set a new email, which logs a reset link. Reset unlocks and confirms the user. Not deleted returns 409. | Could | St |
| FR-067 | `GET /customers/me` returns the caller's profile. The customer ID always comes from the token, never from the route or body. | Must | S2 |
| FR-068 | `PUT /customers/me` updates first name, last name and phone. `If-Match` required. | Must | S2 |
| FR-069 | `POST /customers/me/erase` needs the current password (wrong password returns 400 and counts toward the FR-008 lockout). Rate limit `auth-strict`. It has the same effect and the same open-order rule as FR-064. Returns 204. After that, refresh tokens return 401 (access tokens: see R1). | Must | S2 |

### 6.5 Orders
| ID | Requirement | MoSCoW | Release |
|---|---|---|---|
| FR-080 | Order rules for every create and every item edit: 1-50 lines; each product at most once per order; quantity 1-1000; products must exist and not be deleted (otherwise 400 with a field error); enough stock or 409 listing the short product IDs. | Must | S3 |
| FR-081 | Admin creates an order for a `customerId` in the body. The customer must exist and not be deleted (otherwise 400). 201. | Must | S3 |
| FR-082 | Customer creates their own order. The customer comes from the token, and the body has no customer field. 201. | Must | S3 |
| FR-083 | On create: unit price is copied from the product (snapshot); line total = quantity × unit price; order total = sum of line totals (2 decimals); status = Pending; first history row (none → Pending); stock decremented. All in one transaction. | Must | S3 |
| FR-084 | Stock changes lock the affected product rows (`SELECT … FOR UPDATE`, in product ID order) inside the order transaction, so no retry loop is needed. Exactly one retry layer exists for transient DB errors (decided in Architecture). Stock never goes below 0. Test: 20 parallel orders for stock 5 give exactly 5 successes and final stock 0. | Must | S3 |
| FR-085 | List orders, paged (Admin). Filters: `status` (multi), `customerId`, `createdFrom`, `createdTo`, `minTotal`, `maxTotal`. Sort: `createdAt` (default descending), `total`, `status`. | Must | S3 |
| FR-086 | Get an order (Admin): header, line items and status history. Returns an `ETag`. | Must | S3 |
| FR-087 | Replace line items (Admin), only while Pending (otherwise 409). `If-Match` required. Same rules as FR-080. Stock is adjusted by the difference. Lines that stay keep their price snapshot; new products take the current price. Totals are recalculated. | Must | S3 |
| FR-088 | Status change (Admin). Allowed: Pending → Confirmed, Confirmed → Shipped, Shipped → Completed. Anything else returns 409 with the allowed next statuses. Optional reason ≤ 500 chars. Each change adds a history row (from, to, by, at, reason). | Must | S3 |
| FR-089 | Admin cancel is allowed from Pending or Confirmed (otherwise 409). Restores stock for all lines, including products deleted since the order. Adds a history row. | Must | S3 |
| FR-090 | Customer cancels their own order only while Pending (otherwise 409). Another customer's or a missing order returns 404. Same stock and history effects as FR-089. | Must | S3 |
| FR-091 | A Customer can list and get only their own orders. Filters: `status`, `createdFrom`, `createdTo`. Sort: `createdAt`, `total`. Another customer's or a missing order returns 404. | Must | S3 |
| FR-092 | Product price changes never change existing order lines. | Must | S3 |
| FR-096 | A customer can have at most 5 open `Pending` orders (configurable, `Orders:MaxPendingPerCustomer`). Order create (Admin EP-38 or Customer EP-45) for a customer at the cap returns 409 `too-many-pending-orders`. Parallel creates for one customer cannot pass the cap (S-D4). | Must | S3 |
| FR-093 | Order soft delete (Admin), only when Cancelled (otherwise 409). | Could | St |
| FR-094 | Customer replaces items on their own Pending order. | Could | St |
| FR-095 | `Idempotency-Key` header on order create: the same key within 24 h returns the first result. | Could | St |

### 6.6 Reports and Dapper reads (Admin only, read-only)
| ID | Requirement | MoSCoW | Release |
|---|---|---|---|
| FR-100 | Order summary (view): order ID, customer ID, customer name (anonymized if deleted), status, item count, total units, total, createdAt. Same paging, filters and sort as FR-085. | Must | S4 |
| FR-101 | Customer order history (function, paged): for a given customer, the orders with totals and the total count. Unknown or deleted customer returns 404. | Must | S4 |
| FR-102 | Sales report (function). `from` and `to` are required; `from < to`; range ≤ 366 days (otherwise 400). `groupBy` = day, week or month (default day), using UTC periods. Each period returns order count, units and revenue, including empty periods with zeros, plus grand totals. Only Confirmed, Shipped and Completed orders count; Pending and Cancelled are excluded. | Must | S4 |
| FR-103 | Low-stock report: non-deleted products with stock ≤ `threshold` (default from configuration, 10; range 0-1,000,000). Sorted by stock ascending, paged. | Must | S4 |
| FR-104 | Audit-log query: filters `entityType`, `entityId`, `userId`, `action`, `from`, `to`. Newest first, paged. No endpoint can change audit logs. All Admins may read it; every query is logged as a security event (who, when, filters as IDs and codes) (S-D5). | Should | S4 |
| FR-105 | Top products by units and revenue for `from`/`to`, `top` 1-50. | Could | S4 |
| FR-106 | Every view and function excludes soft-deleted orders and order lines explicitly (`is_deleted = false`). Soft-deleted customers (shown anonymized) and soft-deleted products stay in report results so history and revenue are complete. Sales are counted on the order `createdAt` (UTC). Each has an integration test with deleted data present. All are created by migrations. | Must | S4 |

### 6.7 Cross-cutting
| ID | Requirement | MoSCoW | Release |
|---|---|---|---|
| FR-120 | **Paging:** `page` (≥ 1, default 1) and `pageSize` (1-100, default 20). Out of range returns 400. A page past the end returns 200 with empty `items`. | Must | S1 |
| FR-121 | **Paged response shape:** `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 137, "totalPages": 7 }`. | Must | S1 |
| FR-122 | **Sorting:** `sort=field,-field` (`-` means descending). Each resource has an allow-list. At most 3 fields. An unknown field returns 400. ID is always added as the last tiebreaker. | Must | S1 |
| FR-123 | **Filtering:** named query parameters per resource. `min`/`max` bounds are inclusive. Dates use `xxxFrom` (inclusive) and `xxxTo` (exclusive) in ISO 8601 with an offset, converted to UTC. `search` is a case-insensitive contains match, 2-100 chars. Unknown parameters are ignored. | Must | S1 |
| FR-124 | **ProblemDetails (RFC 9457)** on every 4xx and 5xx: `type`, `title`, `status`, `detail`, `instance`, `traceId`. Validation errors add `errors: { field: [messages] }`. Outside Development there are no stack traces or exception text. | Must | S1 |
| FR-125 | **Contracts:** request and response DTOs only, never entities. JSON in camelCase. Enums as strings. Money as a number with 2 decimals plus the configured currency code on order responses. Dates in ISO 8601 UTC (`Z`). | Must | S1 |
| FR-126 | Creates return 201 with `Location`. Commands without a body return 204. | Must | S1 |
| FR-127 | **Soft delete:** DELETE is always a soft delete. Deleted rows are hidden from all reads by default. Admins can use `includeDeleted` on product, customer and user lists. Restore exists for products and customers. Nothing is hard deleted (except the Identity join rows decided in the Q1 answer). | Must | S1 |
| FR-128 | **Audit:** every business write and every meaningful account change (user insert, email confirmed, password changed or reset, role assigned or removed, lock or unlock, delete or restore) creates an `AuditLogs` row in the same transaction: table, key, action (Insert, Update, SoftDelete, Restore, RoleAssigned, RoleRemoved, SessionsRevoked for stretch EP-20), changed fields old and new (fields marked as personal data are written as `"changed"`, never with values), user ID (or `anonymous`/`system`), UTC time, trace ID. `AuditLogs` is append-only: the app role has INSERT and SELECT only. A rolled-back transaction leaves no audit row. Password hashes, security stamps and token hashes are never written to the audit log. Refresh-token rows and the failed-login counter are not audited; they are logged and measured instead. | Must | S1 |
| FR-129 | **Concurrency:** products, customers and orders return an `ETag`. PUT needs `If-Match`. Missing returns 428; stale returns 412. | Must | S1 |
| FR-130 | **Caching** applies only to product reads. Customer, order, user and report data are never cached. If the cache fails, the system reads from the database. | Must | S1 |
| FR-131 | **Rate limiting** policies: `auth-strict`, `refresh` and `general` (limits in NFR-09). A rejected call returns 429 ProblemDetails with `Retry-After`. | Must | S1 |
| FR-132 | All business routes are under `/api/v1`. A breaking change needs `/api/v2`. | Must | S1 |
| FR-133 | Every endpoint has an OpenAPI summary, description, schemas, every status code and its auth rule. Scalar UI only in Development. | Must | S1 |
| FR-134 | Every endpoint has a `.http` entry and a Postman request. Environments contain no secrets. | Must | S1 |
| FR-135 | All timestamps come from `IDateTimeProvider` (UTC). | Must | S1 |
| FR-136 | Health: `live` checks only the process. `ready` checks PostgreSQL and Valkey: Valkey down = Degraded (200), PostgreSQL down = 503. Bodies have no internal detail outside Development. | Must | S1 |
| FR-137 | Request bodies over 1 MB return 413. Only JSON is accepted on bodies (otherwise 415). | Must | S1 |

## 7. Non-functional requirements
| ID | Category | Requirement (measurable) | Priority |
|---|---|---|---|
| NFR-01 | Performance | Local, warm, 50 concurrent users for 2 min: p95 for product get-by-ID and list (pageSize 20) is ≤ 20 ms cached and ≤ 100 ms uncached. With Valkey down: ≤ 300 ms and 0 responses with status 5xx | Must |
| NFR-02 | Performance | Same load, seed of 1,000 products, 1,000 customers and 10,000 orders: other reads p95 ≤ 200 ms, writes p95 ≤ 300 ms, reports p95 ≤ 1 s | Should |
| NFR-03 | Resilience | Transient DB and cache errors are retried up to 3 times with exponential backoff and jitter. Cache calls time out after ≤ 250 ms. The circuit breaker opens at ≥ 50% failures over ≥ 10 calls in 30 s and stays open for 30 s. DB unavailable returns 503 ProblemDetails within 5 s | Must |
| NFR-04 | Security | OWASP API Security Top 10 (2023): each item maps to at least one control and one test in the threat model. Authz tests cover 100% of endpoints × 3 roles. BOLA "other customer → 404" tests cover 100% of `/me` resource endpoints | Must |
| NFR-05 | Security (tokens) | JWT validation checks signature, an algorithm allow-list, `iss`, `aud` and `exp`, with clock skew ≤ 30 s. Signing key ≥ 256 bits, from secrets only. Access 15 min, refresh 7 days (30-day family limit) | Must |
| NFR-06 | Security (transport) | HTTPS only, with HSTS outside Development. Headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store` on auth and personal-data responses. No `Server` header. CORS allows only configured origins; an empty list means no CORS | Must |
| NFR-07 | Security (secrets) | 0 secrets in the repo, checked by a secret scan on every build. Configuration comes from user-secrets or environment variables | Must |
| NFR-08 | Security (least privilege) | The app's DB role has no DDL rights and no DELETE (except the Identity join tables decided in the Q1 answer). Migrations run as a separate role. An architecture test bans `ExecuteDelete` | Must |
| NFR-09 | Abuse protection | `auth-strict`: 10 requests/min per IP for register, login, confirm, resend, forgot, reset, change password and erase (one shared bucket, D16, S-D2). Reports: at most 4 at a time (S-D7). `refresh`: 30/min per IP. `general`: 300/min per user when signed in, 60/min per IP when anonymous. Lockout per FR-008 | Must |
| NFR-10 | Privacy | Logs never contain passwords, tokens, refresh tokens or token hashes. Emails are masked, except the fake-sender lines in Development. Erasure test: 0 matches for a deleted customer's email, phone or full name in any table | Must |
| NFR-11 | Compliance | GDPR-style right to erasure is a design goal. No formal certification this phase | Should |
| NFR-12 | Observability | Structured logs with trace ID and user ID (ID only). OpenTelemetry traces for HTTP, DB and cache. Metrics: request rate, duration and errors; cache hit ratio; rate-limit rejections; login failures; refresh-token reuse events | Must |
| NFR-13 | Supportability | 100% of error responses carry a `traceId` that matches the logs. README covers setup, run, test and seed Admin | Must |
| NFR-14 | Availability | Local only, no SLA. Health endpoints respond in ≤ 1 s | Must |
| NFR-15 | Rebuild | Fresh clone to a running API with the seed Admin: ≤ 15 min, one compose command plus migrations, 0 manual steps | Must |
| NFR-16 | Testability | Full test suite with Testcontainers runs in ≤ 5 min locally. 100% of write endpoints have an integration test that checks the audit row | Must |
| NFR-17 | Maintainability | Architecture tests (layer dependencies, no `ExecuteDelete`, every endpoint has a validator and an auth rule) pass at 100% | Must |
| NFR-18 | Licensing | 100% of dependencies are under a permissive license (MIT, Apache-2.0, BSD, PostgreSQL), checked in the build | Must |
| NFR-19 | Data retention | No automatic purge this phase. Audit logs, refresh tokens and status history are kept forever. Before any production use: decide retention (risk R4) | Should |
| NFR-20 | Data integrity | Money stored as `numeric(12,2)`. All timestamps stored as UTC `timestamptz`. Stock is never negative (DB check constraint plus test) | Must |
| NFR-21 | Accessibility | Not applicable (no UI). OpenAPI descriptions are in plain English | n/a |
| NFR-22 | Clients and devices | Any HTTP client. JSON UTF-8 only. No browser-specific behavior | Must |
| NFR-23 | Documentation | 100% of endpoints are in OpenAPI, `.http` and Postman (North Star check) | Must |

## 8. Integrations and data
| System | Direction | Method | Owner |
|---|---|---|---|
| PostgreSQL 18 (Docker) | Read/write | EF Core (writes, CRUD); Dapper (views, functions) | Developer |
| Valkey 9.1 (Docker) | Read/write | Distributed cache for product reads | Developer |
| Fake email sender | Out | Structured log line | Developer |
| Configuration / secrets | In | User-secrets, environment variables | Developer |

## 9. Answers to the 13 open questions
| # | Question | Answer |
|---|---|---|
| 1 | Customers place and cancel orders? | Yes. Create own orders; cancel own order while Pending. Admins create and update any order |
| 2 | Order line items? | Yes, `OrderItems` |
| 3 | Customer create | Anonymous register creates the user, the Customer role and the Customer record in one transaction. Admins can create customers too; the customer sets a password through the reset link |
| 4 | Customer delete | Soft delete plus PII anonymization (audit rows never hold PII values, P1). User soft deleted, locked, tokens revoked. Orders kept |
| 5 | Own data only | Yes. `/me` routes; another customer's resource returns 404. Admins see everything |
| 6 | Product delete | Soft delete; Admin restore |
| 7 | Stock and price | `StockQuantity`; decremented on order and restored on cancel; 409 when short. Unit price snapshot. `xmin` concurrency |
| 8 | Statuses | Pending → Confirmed → Shipped → Completed, plus Cancelled. Customer cancels own while Pending. Admin cancels up to Confirmed. Items editable only while Pending. `OrderStatusHistory` |
| 9 | Email confirmation | Required. Fake sender. Seeded Admin is pre-confirmed. Confirm and resend in S1 |
| 10 | First Admin | Seeded from configuration if no Admin exists. Admins can create Admins and manage roles, but cannot remove their own Admin role or the last Admin |
| 11 | Mediator | FastEndpoints command bus |
| 12 | Tokens | Access 15 min. Refresh 7 days, rotated, SHA-256 hash, reuse revokes the family. Revoked on logout-all, password change or reset, Admin lock, role change, delete (automatic lockout no longer revokes, S-D2) |
| 13 | Tenancy, currency, time | Single tenant. One configured ISO 4217 currency (default USD). `numeric(12,2)`. UTC `timestamptz` |

## 10. Success metrics
| Metric | Baseline | Target | How measured |
|---|---|---|---|
| North Star: endpoints meeting the DoD | 0% | 100% per slice | DoD checklist per endpoint in the endpoint catalog |
| BOLA tests passing | 0 | 100% of `/me` resource endpoints | Test report |
| Clean rebuild time | unknown | ≤ 15 min | Timed run on a fresh clone |
| Product read p95 (cached / uncached / Valkey down) | unknown | 20 / 100 / 300 ms | Load test, recorded per slice |
| Audited writes | unknown | 100% | Integration tests |
| Test suite time | unknown | ≤ 5 min | CI or local run |

## 11. Risks and dependencies
| Risk | Impact | Mitigation | Owner |
|---|---|---|---|
| R1 Access tokens stay valid up to 15 min after lock, role change or delete | Removed user keeps access | Check the security stamp per request (cached briefly), or document the 15 min window as accepted. Decide in Security phase | Architect / security |
| R2 No-DELETE DB rule conflicts with Identity's role and token removal | Runtime failures or a weakened rule | Narrow DELETE grant on the Identity join tables plus audit, recorded in an ADR | Architect |
| R3 Anonymization depends on a list of PII fields, also inside jsonb audit values | A new PII field leaks after erasure | Mark PII fields in one place. The erasure test seeds every PII field and searches all tables | Developer |
| R4 Tables grow forever (audit logs, refresh tokens, history) | Slow queries, storage | Indexes on query paths. Plan retention before production | Product owner |
| R5 Scope: 52 core endpoints for one developer; S1 carries most of the auth risk | Slice slips | Time-box S1, cut stretch first, write token tests first | Product owner |
| R6 FastEndpoints funding risk | Library goes bugfix-only | Keep handlers framework-light; ADR notes the exit path | Architect |

## 12. Assumptions
- Assumption: IDs are opaque (GUID) in routes. The architect decides the format.
- Assumption: refresh tokens travel in the JSON body this phase. An HttpOnly cookie option comes with the web front end.
- Assumption: register, forgot-password and resend return 202 to stop account enumeration.
- Assumption: a correct password with an unconfirmed email returns 403 `email-not-confirmed`. All other login failures return a generic 401.
- Assumption: on an item edit, lines that stay keep their snapshot price and new products take the current price.
- Assumption: Customers cannot edit their order items in the core scope (cancel and re-order instead).
- Assumption: performance targets are local indicative numbers, not an SLA.
- Assumption: customer email cannot be changed by the customer this phase.

## 13. Open questions
- [x] R1: accept the 15-min access-token window, or add a per-request security-stamp check? Answered: stamp check (S-D1, section 18).
- [x] Grace window for parallel refresh requests. Answered: strict now; "benign race" option recorded (S-D6).
- [x] Timing differences on register/forgot-password that could reveal registered emails. Answered: equal work + response floor (S-D6).

## 14. Definition of Ready (story)
- [ ] Linked to FR, NFR and EP IDs.
- [ ] Roles and expected status codes per role are in the permission matrix.
- [ ] Given/When/Then criteria include at least one error or edge case, and the 401, 403 and 404 cases where relevant.
- [ ] Validation rules and limits are written down.
- [ ] Estimate given; no open blockers.

## 15. Definition of Done (endpoint, the North Star)
- [ ] Request and response DTOs; FluentValidation rules; explicit auth rule.
- [ ] Authz tests for Anonymous, Customer and Admin. BOLA test (other customer → 404) on `/me` resources.
- [ ] Errors return ProblemDetails with `traceId`; validation errors list the fields.
- [ ] Writes produce an audit row (integration test); no hard delete.
- [ ] OpenAPI description, `.http` entry and Postman request.
- [ ] Unit tests (handler, validator) and integration tests (Testcontainers) pass.
- [ ] Rate-limit policy and cache rule applied as listed in the endpoint catalog.
- [ ] Code reviewed by a fresh-context agent (`dotnet-code-reviewer`) and merged; architecture tests green; Azure Pipelines build green; no secrets.
- [ ] Security review by `dotnet-security-reviewer` if the endpoint touches auth, personal data or tokens.

## 16. Decisions from the user-story review (2026-10-24)
These settle gaps found while writing the stories. Stories in `user-stories/` already follow them.

| # | Topic | Decision |
|---|---|---|
| D1 | Password reset and locks | Reset clears the failed-login lockout. It never clears an Admin lock (only EP-15 does). Lock type is stored (`failed-logins`, `admin`; `restored-customer` only if stretch restore is built). |
| D2 | Reports and deleted data | Views and functions exclude soft-deleted orders and order lines only. Deleted customers (anonymized) and deleted products stay in results. Sales count on order `createdAt` (UTC), ISO weeks start Monday, edge periods cut to the range. |
| D3 | Self-delete route | EP-37 is `POST /api/v1/customers/me/erase` (not DELETE with a body). Rate limit `auth-strict`. Wrong password → 400 and counts toward lockout. |
| D4 | Audit scope for identity | Audited: user insert, email confirmed, password changed/reset, role assigned/removed, lock/unlock, delete/restore, Admin session revoke. Not audited: refresh-token rows and the failed-login counter (logged and measured instead). |
| D5 | Field limits | Email ≤ 256 chars; first and last name 1-100; phone E.164 (`+` and digits) ≤ 20. |
| D6 | Admin names | Stored on the Identity user (first and last name), because Admins have no Customer record. |
| D7 | Response codes | PUT returns 200 with the updated resource and a new `ETag`. POST commands with a body (status change, cancel, restore, lock) return 204. A malformed route ID returns 400. |
| D8 | Filter edge rules | `xxxFrom ≥ xxxTo` → 400; `min > max` → 400; dates without an offset → 400; `inStock=true` means stock > 0. `sort` is not accepted on fixed-order reports (EP-51, EP-53, EP-55) and is ignored there. |
| D9 | Small rules | Cancel `reason` optional (≤ 500). Extra `customerId` in a customer's order body is ignored. Customers do not see `byUserId` in order history. Admins may create orders for locked or unconfirmed customers. Price with more than 2 decimals → 400. `isDeleted` appears only in Admin responses. |
| D10 | Health checks | No rate limit. Own simple status body (not ProblemDetails). Detailed per-check output only in Development. |
| D11 | Bulk writes | `ExecuteUpdate` is banned together with `ExecuteDelete` (both skip the audit interceptor). Architecture test enforces it. |
| D12 | Audit-log redaction | Superseded by P1: personal-data fields are never stored with values in audit rows, so audit rows are never changed. |
| D13 | Customer get by ID | `includeDeleted=true` works on Admin get-by-ID for products and customers, mirroring the list endpoints. |
| D14 | Email change | Moved to stretch with customer restore (P2). In core scope, customer email cannot be changed (409 `email-change-not-allowed`). |
| D15 | Stock and cache | Order create, cancel and item replace also invalidate the affected product cache entries. |
| D16 | Rate-limit buckets | `auth-strict` is one shared bucket per IP across register, login, confirm, resend, forgot, reset and erase. |

Moved to the Security phase: R1 (15-min access-token window), a grace window for parallel refreshes, and response-timing differences on register/forgot that could reveal registered emails.

## 17. Decisions from the pre-mortem (2026-10-24)
Source: [pre-mortem](pre-mortem.md). These override earlier text where they differ.

| # | Topic | Decision |
|---|---|---|
| P1 | PII in audit | Properties marked as personal data are written to `AuditLogs` as `"changed"`, never with old/new values. `AuditLogs` is append-only (app role: INSERT, SELECT). Replaces D12. |
| P2 | Customer restore | EP-34 / FR-066 move to stretch, with D14 (email change on restored records) and the `restored-customer` lock type. Product restore stays in S1. |
| P3 | Stock locking | Order create, cancel and item replace lock product rows with `SELECT … FOR UPDATE` in product ID order. One retry layer only for transient DB errors. Orders: every change to items or status updates the order row, so the order `ETag` always changes. ADR in Architecture. |
| P4 | Local setup | `docker compose up` runs the API as `Development`. Missing dev-only secrets (JWT key, seed Admin password) are generated on first run; `.env.example` documents them. HTTPS, HSTS and production headers are tested in a separate production-like profile. |
| P5 | Reviews and CI | "Code review" and "security review" in the DoD mean a fresh-context agent review (`dotnet-code-reviewer`, `dotnet-security-reviewer`) plus a checklist. An architecture test checks every endpoint has an auth rule, a validator and an OpenAPI summary. CI runs on Azure Pipelines: build, tests, secret scan (NFR-07) and licence check (NFR-18). |
| P6 | Time-boxes | Each slice gets a time-box in Delivery. Cut order when over: stretch first, then Should/Could items. Kill or pivot criteria are listed in the pre-mortem. |
| P7 | Template extraction | New deliverable after S4: shared parts (building blocks with no domain references) packaged as a `dotnet new` template plus a short "start a new API" guide. Outcome metric: hours to bootstrap the next API. |
| P8 | Rate-limit tests | Built-in limiters have no `TimeProvider`, so tests use short real windows (1-2 s) set by configuration. `auth-strict` includes `erase` (D16). |
| P9 | Stored procedure | Architecture adds at least one real PostgreSQL procedure used through Dapper and documents when to use a function vs a procedure. |
| P10 | Kept on purpose | Role assign/remove, list roles, Postman collection and the database circuit breaker stay, because the brief requires them. |

## 18. Security decisions S-D1..S-D8 (2026-10-27)
Source: [threat model](../05-security/threat-model.md) section 9. These override earlier text where they differ.

| # | Topic | Decision | Changed here |
|---|---|---|---|
| S-D1 | Access token after lock, demote or erase (R1) | Per-request security-stamp check: `sst` claim, checked on every request, in-process 30 s cache cleared on change, fails closed. Stamp rotated on lock/unlock, role change, erase/delete, logout-all, revoke-sessions | R1 closed (ADR-0008) |
| S-D2 | Lockout and change password | Automatic failed-login lockout no longer revokes sessions (Admin lock still does). EP-09 on `auth-strict`; a wrong current password counts toward lockout | FR-015, FR-017, NFR-09 |
| S-D3 | Token lifetimes | Reset token 1 h, confirm token 24 h, separate providers; Data Protection key ring persisted | FR-018 |
| S-D4 | Inventory hoarding | At most 5 open Pending orders per customer (configurable) → 409 `too-many-pending-orders` | FR-096 (new, S3) |
| S-D5 | Audit-log readers | All Admins; each audit-log query is a security event | FR-104 |
| S-D6 | Refresh races and timing | Strict refresh now ("benign race" option recorded for a browser client). Equal work + response floor on register, forgot, resend and login; password checked before confirmed/locked | FR-007; open questions in section 13 closed |
| S-D7 | Hardening set | Localhost-only ports; DB, Valkey and OTLP not published; Valkey ACL password; per-service secret mounts; password blocklist + 220,000 PBKDF2 iterations; query strings out of logs; tighter DB timeouts; grant snapshot test; enforced Dapper SQL allow-list; cache bounds; report concurrency limit; security events; ADR-0014 authorization model | FR-003, NFR-09 |
| S-D8 | Supply chain | Section 5 of the threat model; dependency updates via Renovate in a scheduled pipeline (build tool, not shipped) | — |
