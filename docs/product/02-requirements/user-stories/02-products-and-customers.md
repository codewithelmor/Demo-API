# User stories: Products and Customers (EP-22 to EP-37)

Date: 2026-10-24 | Status: Approved 2026-10-24
Scope: product catalog (list, get, create, update, soft delete, restore, stock adjustment), Admin customer management (create, list, get, update, delete with anonymization, restore) and Customer self-service (`/customers/me`).
Story count: 16 (15 Must in S1/S2, 1 Could/Stretch).
Sources: [PRD](../prd.md) · [endpoint list](../endpoint-list.md) · [permission matrix](../permission-matrix.md) · [summary](../../00-summary.md)

## Contents
| US | EP | Title | Slice |
|---|---|---|---|
| US-100 | EP-22 | List products | S1 |
| US-101 | EP-23 | Get a product | S1 |
| US-102 | EP-24 | Create a product | S1 |
| US-103 | EP-25 | Update a product | S1 |
| US-104 | EP-26 | Soft delete a product | S1 |
| US-105 | EP-27 | Restore a product | S1 |
| US-106 | EP-28 | Adjust product stock | Stretch |
| US-107 | EP-29 | Admin creates a customer | S2 |
| US-108 | EP-30 | List customers | S2 |
| US-109 | EP-31 | Get a customer | S2 |
| US-110 | EP-32 | Update a customer | S2 |
| US-111 | EP-33 | Delete and anonymize a customer | S2 |
| US-112 | EP-34 | Restore a customer | Stretch |
| US-113 | EP-35 | Read own profile | S2 |
| US-114 | EP-36 | Update own profile | S2 |
| US-115 | EP-37 | Erase own account (POST /customers/me/erase) | S2 |

## Story map
| Activity → | Browse catalog | Manage catalog (Admin) | Manage customers (Admin) | Manage own account (Customer) |
|---|---|---|---|---|
| **Steps** | Find, view | Add, change, remove, recover | Onboard, find, view, change, erase, recover | View, change, erase |
| **S1** | US-100, US-101 | US-102, US-103, US-104, US-105 | | |
| **S2** | | | US-107, US-108, US-109, US-110, US-111 | US-113, US-114, US-115 |
| **Stretch** | | US-106 | US-112 | |

## Common criteria (apply to every story below; not repeated)
- C1 - Given the caller exceeds the rate-limit policy, When they call the endpoint, Then 429 ProblemDetails with `Retry-After` (checked before 401).
- C2 - Every 4xx/5xx is RFC 9457 ProblemDetails with `type`, `title`, `status`, `detail`, `instance`, `traceId`; validation errors add `errors: { field: [messages] }`; no stack traces outside Development.
- C3 - Write endpoints with a body: non-JSON → 415; body > 1 MB → 413.
- C4 - Every write creates exactly one `AuditLogs` row per changed entity in the same transaction (table, key, action, changed fields old/new, user ID, UTC time, trace ID). A failed or rolled-back request leaves no audit row and no data change.
- C5 - Customer data responses carry `Cache-Control: no-store` and are never cached (FR-130, NFR-06).
- C6 - Role check (403) happens before body validation (400), so a forbidden caller learns nothing about the data.
- C7 - Unknown query parameters are ignored (FR-123). Dates in responses are ISO 8601 UTC (`Z`), JSON camelCase.
- C8 - Each endpoint has OpenAPI docs, a `.http` entry and a Postman request (DoD).

---

### US-100 - List products (EP-22, Slice S1, Must)
**As a** signed-in Customer or Admin **I want** to page, search, filter and sort the product catalog **so that** I can find products quickly.
Traces: FR-041, FR-046, FR-120, FR-121, FR-122, FR-123, FR-127, FR-130, NFR-01, NFR-03, NFR-09
Route: GET /api/v1/products  ·  Rate limit: general  ·  Cache: cached per normalized query and caller scope (Customer / Admin / Admin + includeDeleted); DB fallback
**Acceptance criteria**
- AC1 (happy path) - Given 45 non-deleted products and a Customer token, When GET `/products?page=2&pageSize=20`, Then 200 with 20 `items` (id, name, sku, description, price, stockQuantity, createdAt), `page` 2, `pageSize` 20, `totalCount` 45, `totalPages` 3, sorted by `name` ascending then `id`.
- AC2 (defaults) - Given no query parameters, When GET `/products`, Then `page` 1, `pageSize` 20, sort `name` ascending.
- AC3 (filtering) - Given products "Blue Widget" (SKU BW-1, 9.99, stock 0) and "Red Gadget" (SKU RG-2, 20.00, stock 5), When `search=widg` Then only Blue Widget (case-insensitive contains on name or SKU); When `search=rg-` Then only Red Gadget; When `minPrice=9.99&maxPrice=20.00` Then both (bounds inclusive); When `inStock=true` Then only Red Gadget; When `inStock=false` Then only Blue Widget.
- AC4 (sorting) - Given products with equal prices, When `sort=-price,name`, Then price descending, then name ascending, then `id` as final tiebreaker. Allowed fields: `name`, `price`, `sku`, `stockQuantity`, `createdAt`.
- AC5 (validation) - Given `page=0`, `pageSize=0`, `pageSize=101`, `search=a` (1 char), `search` of 101 chars, `sort=description` (not allowed), `sort=name,price,sku,createdAt` (4 fields), `minPrice=-1`, or `minPrice=50&maxPrice=10`, When GET, Then 400 ProblemDetails with `errors` for `page`, `pageSize`, `search`, `sort`, `minPrice`/`maxPrice` as applicable.
- AC6 (page past end) - Given 45 products, When `page=10`, Then 200 with empty `items`, `totalCount` 45, `totalPages` 3.
- AC7 (unauthenticated) - Given no token, or an expired or invalid token, When GET, Then 401.
- AC8 (forbidden) - Not applicable: Customer and Admin are both allowed.
- AC9 (soft-deleted / includeDeleted) - Given one soft-deleted product, When an Admin calls with `includeDeleted=true`, Then it is included with `isDeleted: true`; When an Admin calls without it, Then it is excluded; When a Customer calls with `includeDeleted=true`, Then the flag is ignored, the deleted product is excluded and the response is 200 (no error).
- AC10 (cache served) - Given the same query was just served, When it is called again, Then the response comes from Valkey (cache-hit metric increments) with the same body.
- AC11 (cache invalidated) - Given a cached list, When an Admin creates, updates, deletes or restores a product, or stock changes, Then the next list call reflects the change (entries removed before the write response returns).
- AC12 (Valkey down) - Given Valkey is stopped, When GET, Then 200 from PostgreSQL, 0 responses with status 5xx, p95 ≤ 300 ms under the NFR-01 load, and `/health/ready` reports Degraded.
Notes: Assumption: `inStock=true` means stock > 0, `false` means stock = 0. Assumption: `isDeleted` appears only in Admin responses. Assumption: `minPrice > maxPrice` → 400.

### US-101 - Get a product (EP-23, Slice S1, Must)
**As a** signed-in Customer or Admin **I want** to read one product by ID **so that** I can see its details and current stock.
Traces: FR-042, FR-046, FR-129, FR-130, NFR-01, NFR-03
Route: GET /api/v1/products/{productId}  ·  Rate limit: general  ·  Cache: cached per product ID; DB fallback
**Acceptance criteria**
- AC1 (happy path) - Given an existing non-deleted product, When a Customer or Admin calls GET, Then 200 with id, name, sku, description, price, stockQuantity, createdAt, and an `ETag` header.
- AC2 (validation) - Given a `productId` that is not a valid ID format, When GET, Then 400 with `errors` for `productId`.
- AC3 (unauthenticated) - Given no token, When GET, Then 401.
- AC4 (forbidden) - Not applicable: both roles are allowed.
- AC5 (not found) - Given an unknown ID, Then 404. Given a soft-deleted product, When a Customer calls (with or without `includeDeleted=true`), Then 404; When an Admin calls without `includeDeleted=true`, Then 404; When an Admin calls with `includeDeleted=true`, Then 200 with `isDeleted: true`.
- AC6 (cache served / invalidated) - Given the product was read before, When read again, Then it is served from cache; Given an Admin then updates it, When read again, Then the new values and a new `ETag` are returned.
- AC7 (Valkey down) - Given Valkey is stopped, When GET, Then 200 from PostgreSQL with no 5xx.

### US-102 - Create a product (EP-24, Slice S1, Must)
**As an** Admin **I want** to add a product to the catalog **so that** customers can see and order it.
Traces: FR-040, FR-046, FR-126, FR-128, FR-129, NFR-16, NFR-20
Route: POST /api/v1/products  ·  Rate limit: general  ·  Cache: invalidates product list entries
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and body `{ name: "Blue Widget", sku: "bw-100", description: "...", price: 9.99, stockQuantity: 50 }`, When POST, Then 201 with `Location: /api/v1/products/{id}`, an `ETag`, and the product with `sku` stored and returned as `BW-100`; an audit row (action Insert, new values, Admin user ID, trace ID) exists; the next list call includes the product.
- AC2 (validation) - Given name empty or 201 chars; sku empty, 51 chars, or with characters outside A-Z, 0-9 and `-` (for example `BW_1`, `BW 1`); description 2001 chars; price 0, 0.001, or 10,000,000,000.00; stockQuantity -1 or 1,000,001, When POST, Then 400 with `errors` for each failing field; nothing saved, no audit row.
- AC3 (unauthenticated) - Given no token, When POST, Then 401.
- AC4 (forbidden) - Given a Customer token (valid or invalid body), When POST, Then 403; nothing saved.
- AC5 (not found) - Not applicable.
- AC6 (duplicate SKU) - Given a non-deleted product with SKU `BW-100`, When POST with sku `bw-100`, Then 409; no audit row.
- AC7 (SKU reuse after delete) - Given only a soft-deleted product has SKU `BW-100`, When POST with `BW-100`, Then 201.
- AC8 (rollback) - Given the database fails during save, Then no product and no audit row exist (503 per NFR-03 if the DB is unavailable).
Notes: Assumption: price with more than 2 decimals → 400.

### US-103 - Update a product (EP-25, Slice S1, Must)
**As an** Admin **I want** to replace a product's editable fields safely **so that** the catalog stays correct without overwriting someone else's change.
Traces: FR-043, FR-040, FR-046, FR-092, FR-128, FR-129
Route: PUT /api/v1/products/{productId}  ·  Rate limit: general  ·  Cache: invalidates this product and list entries
**Acceptance criteria**
- AC1 (happy path) - Given an Admin, an existing product and `If-Match` equal to its current `ETag`, When PUT with a valid full body, Then 200 with the updated product and a new `ETag`; an audit row (action Update) lists only the changed fields with old and new values; the cache entries for this product and the lists are removed before the response returns.
- AC2 (validation) - Given any value that breaks the FR-040 rules (same cases as US-102 AC2), When PUT, Then 400 with `errors` per field; no change.
- AC3 (unauthenticated) - Given no token, When PUT, Then 401.
- AC4 (forbidden) - Given a Customer token, When PUT, Then 403.
- AC5 (not found) - Given an unknown or soft-deleted product ID, When PUT, Then 404.
- AC6 (missing If-Match) - Given no `If-Match` header, When PUT, Then 428; no change.
- AC7 (stale ETag) - Given another Admin updated the product after the ETag was read, When PUT with the old ETag, Then 412; no change, no audit row.
- AC8 (duplicate SKU) - Given another non-deleted product has SKU `RG-2`, When PUT sets sku `rg-2`, Then 409. Given the SKU is unchanged, Then no 409.
- AC9 (price snapshot) - Given an order line exists for this product (S3), When the price is changed, Then the order line's unit price and totals are unchanged.
Notes: Assumption: PUT returns 200 with body and new `ETag`.

### US-104 - Soft delete a product (EP-26, Slice S1, Must)
**As an** Admin **I want** to remove a product from the catalog without losing history **so that** it can no longer be ordered but old orders stay intact.
Traces: FR-044, FR-046, FR-127, FR-128, NFR-08
Route: DELETE /api/v1/products/{productId}  ·  Rate limit: general  ·  Cache: invalidates this product and list entries
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and an existing non-deleted product, When DELETE, Then 204; the row still exists with `IsDeleted` true and `DeletedAt`/`DeletedBy` set; an audit row (action SoftDelete) exists; the cache is invalidated; a following Customer GET returns 404 and the product is missing from lists.
- AC2 (validation) - Given a malformed `productId`, When DELETE, Then 400 with `errors` for `productId`.
- AC3 (unauthenticated) - Given no token, When DELETE, Then 401.
- AC4 (forbidden) - Given a Customer token, When DELETE, Then 403; product unchanged.
- AC5 (not found) - Given an unknown ID, Then 404. Given a product that is already soft-deleted, Then 404 and no new audit row.
- AC6 (existing orders) - Given the product is on existing order lines, When DELETE, Then 204 and the order lines are unchanged; a later order that includes it returns 400 with a field error (S3, FR-080).
- AC7 (no hard delete) - Given any DELETE call, Then no product row is physically removed (the app DB role has no DELETE grant on business tables).

### US-105 - Restore a product (EP-27, Slice S1, Must)
**As an** Admin **I want** to restore a soft-deleted product **so that** I can recover from a mistaken delete.
Traces: FR-045, FR-046, FR-127, FR-128
Route: POST /api/v1/products/{productId}/restore  ·  Rate limit: general  ·  Cache: invalidates this product and list entries
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a soft-deleted product whose SKU is not used by an active product, When POST restore, Then 204; `IsDeleted` is false; an audit row (action Restore) exists; the cache is invalidated; the product appears again in Customer lists and GET.
- AC2 (validation) - Given a malformed `productId`, When POST, Then 400.
- AC3 (unauthenticated) - Given no token, When POST, Then 401.
- AC4 (forbidden) - Given a Customer token, When POST, Then 403.
- AC5 (not found) - Given an unknown ID, When POST, Then 404.
- AC6 (not deleted) - Given a product that is not deleted, When POST restore, Then 409; no audit row.
- AC7 (SKU taken) - Given the deleted product has SKU `BW-100` and a new active product now uses `BW-100`, When POST restore, Then 409 with a detail that names the SKU conflict; the product stays deleted.
Notes: Assumption: `DeletedAt`/`DeletedBy` are cleared on restore; the history stays in the audit log.

### US-106 - Adjust product stock (EP-28, Stretch, Could)
**As an** Admin **I want** to add or remove stock with a reason **so that** stock matches reality after deliveries, damage or counts.
Traces: FR-047, FR-046, FR-084, FR-128, NFR-20
Route: POST /api/v1/products/{productId}/stock-adjustments  ·  Rate limit: general  ·  Cache: invalidates this product and list entries
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a product with stock 10, When POST `{ delta: -3, reason: "Damaged in storage" }`, Then 200 with `productId`, `stockQuantity` 7 and a new `ETag`; an audit row (action Update, stockQuantity 10 → 7) exists; the cache is invalidated.
- AC2 (validation) - Given `delta` 0 or missing, or `reason` empty or 201 chars, When POST, Then 400 with `errors` for `delta`/`reason`.
- AC3 (unauthenticated) - Given no token, When POST, Then 401.
- AC4 (forbidden) - Given a Customer token, When POST, Then 403.
- AC5 (not found) - Given an unknown or soft-deleted product, When POST, Then 404.
- AC6 (below zero) - Given stock 2, When `delta: -3`, Then 409; stock stays 2; no audit row.
- AC7 (above maximum) - Given stock 999,999, When `delta: 2`, Then 409; stock unchanged.
- AC8 (concurrency) - Given an order and an adjustment change the same product at the same time, Then the `xmin` conflict is retried up to 3 times, then 409; stock never goes below 0.
Notes: Assumption: 200 with body; result above 1,000,000 → 409; where the reason is stored is open.

### US-107 - Admin creates a customer (EP-29, Slice S2, Must)
**As an** Admin **I want** to create a customer account for someone **so that** they can set a password and start ordering without self-registering.
Traces: FR-060, FR-018, FR-019, FR-020, FR-126, FR-128, FR-129, NFR-10
Route: POST /api/v1/customers  ·  Rate limit: general  ·  Cache: never cached (no-store)
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and body `{ email: "jane.doe@example.test", firstName: "Jane", lastName: "Doe", phone: "+15550100" }`, When POST, Then 201 with `Location: /api/v1/customers/{id}`, an `ETag`, and id, userId, email, firstName, lastName, phone, emailConfirmed false, isLocked false, createdAt. In one transaction: an Identity user (Customer role, unconfirmed, no password) and the Customer record are created; audit rows exist for the Customer insert, the user insert and the role assignment (no password hash or security stamp in them). After commit the fake sender logs a reset link.
- AC2 (validation) - Given email missing, not a valid address or over 256 chars; firstName or lastName empty or over 100 chars; phone not in E.164 format or over 20 chars, When POST, Then 400 with `errors` for each field; nothing saved; no link logged.
- AC3 (unauthenticated) - Given no token, When POST, Then 401.
- AC4 (forbidden) - Given a Customer token, When POST, Then 403.
- AC5 (not found) - Not applicable.
- AC6 (duplicate email) - Given any non-deleted user (Customer or Admin) has `Jane.Doe@Example.test`, When POST with `jane.doe@example.test`, Then 409; nothing saved; no link logged.
- AC7 (rollback) - Given the Customer insert fails after the user insert, Then no user, role, customer or audit row exists, and no link is logged.
- AC8 (first sign-in) - Given the new customer, When they log in before using the link, Then 401 generic; When they reset the password with the logged token (EP-11) and log in, Then 200 tokens and `emailConfirmed` is true.
Notes: Assumption: field limits (names 1-100, phone E.164 ≤ 20, email ≤ 256) until confirmed.

### US-108 - List customers (EP-30, Slice S2, Must)
**As an** Admin **I want** to page, search, filter and sort customers **so that** I can find the right customer quickly.
Traces: FR-061, FR-120, FR-121, FR-122, FR-123, FR-127, FR-130, NFR-02, NFR-06
Route: GET /api/v1/customers  ·  Rate limit: general  ·  Cache: never cached (no-store)
**Acceptance criteria**
- AC1 (happy path) - Given 30 non-deleted customers and an Admin, When GET `/customers?page=1&pageSize=20`, Then 200 with 20 `items` (id, userId, firstName, lastName, email, phone, createdAt), `totalCount` 30, `totalPages` 2, sorted by `lastName` ascending then `id`; `Cache-Control: no-store`.
- AC2 (filtering) - Given customers "Jane Doe" (jane.doe@example.test) and "Sam Roe" (sam@example.test), When `search=DOE`, Then only Jane (case-insensitive contains on first name, last name or email); When `search=sam@`, Then only Sam. Given `createdFrom=2026-10-01T00:00:00+02:00&createdTo=2026-10-02T00:00:00+02:00`, Then customers created from 2026-09-30T22:00:00Z (inclusive) to 2026-10-01T22:00:00Z (exclusive).
- AC3 (sorting) - When `sort=email` or `sort=-createdAt,lastName`, Then the order follows the fields with `id` as final tiebreaker. Allowed: `lastName`, `email`, `createdAt`.
- AC4 (validation) - Given `page=0`, `pageSize=101`, `search=a`, `sort=phone` (not allowed), 4 sort fields, a date without an offset, or `createdFrom` ≥ `createdTo`, When GET, Then 400 with `errors` for the failing parameters.
- AC5 (page past end) - Given 30 customers, When `page=5`, Then 200 with empty `items` and `totalCount` 30.
- AC6 (includeDeleted) - Given one soft-deleted customer, When GET without `includeDeleted`, Then it is excluded; When `includeDeleted=true`, Then it is included with `isDeleted: true` and only anonymized values.
- AC7 (unauthenticated) - Given no token, When GET, Then 401.
- AC8 (forbidden) - Given a Customer token, When GET, Then 403.
- AC9 (not found) - Not applicable for a list.
Notes: Assumption: dates without an offset → 400; `createdFrom ≥ createdTo` → 400.

### US-109 - Get a customer (EP-31, Slice S2, Must)
**As an** Admin **I want** to read one customer with account status **so that** I can support them.
Traces: FR-062, FR-129, FR-130, NFR-06
Route: GET /api/v1/customers/{customerId}  ·  Rate limit: general  ·  Cache: never cached (no-store)
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and an existing customer, When GET, Then 200 with id, userId, email, firstName, lastName, phone, isLocked, emailConfirmed, isDeleted false, createdAt, an `ETag`, and `Cache-Control: no-store`.
- AC2 (validation) - Given a malformed `customerId`, When GET, Then 400.
- AC3 (unauthenticated) - Given no token, When GET, Then 401.
- AC4 (forbidden) - Given a Customer token, When GET with their own ID, another customer's ID or an unknown ID, Then 403 in all three cases (no existence leak).
- AC5 (not found) - Given an unknown ID, Then 404. Given a soft-deleted customer, When GET without `includeDeleted=true`, Then 404; When an Admin uses `includeDeleted=true`, Then 200 with anonymized values and `isDeleted: true`.
- AC6 (not cached) - Given an Admin updates the customer, When GET right after, Then the new values are returned.
Notes: Assumption: `includeDeleted=true` on get, mirroring FR-042.

### US-110 - Update a customer (EP-32, Slice S2, Must)
**As an** Admin **I want** to correct a customer's name and phone, and set a new email on a restored record **so that** customer data stays accurate.
Traces: FR-063, FR-066, FR-128, FR-129, FR-020
Route: PUT /api/v1/customers/{customerId}  ·  Rate limit: general  ·  Cache: never cached (no-store)
**Acceptance criteria**
- AC1 (happy path) - Given an Admin, an existing customer and a matching `If-Match`, When PUT `{ firstName, lastName, phone }`, Then 200 with the updated customer and a new `ETag`; an audit row (action Update) lists only the changed fields.
- AC2 (validation) - Given firstName or lastName empty or over 100 chars, or an invalid phone, When PUT, Then 400 with `errors` per field.
- AC3 (unauthenticated) - Given no token, When PUT, Then 401.
- AC4 (forbidden) - Given a Customer token (any ID), When PUT, Then 403.
- AC5 (not found) - Given an unknown or soft-deleted customer, When PUT, Then 404.
- AC6 (missing If-Match) - Given no `If-Match`, When PUT, Then 428.
- AC7 (stale ETag) - Given the record changed since the ETag was read, When PUT, Then 412; no change, no audit row.
- AC8 (email on a normal record) - Given a customer that was never anonymized, When PUT includes `email`, Then 409 (problem type `email-change-not-allowed`); no change.
- AC9 (Stretch, only with US-112) (email on a restored record) - Given a restored, anonymized customer (US-112), When PUT sets `email: "jane.new@example.test"` and new names, Then 200; the Identity email is updated and unconfirmed; the user stays locked; the fake sender logs a reset link; audit rows exist. When the reset link is used (EP-11), Then the user is unlocked and confirmed and can log in.
- AC10 (duplicate email) - Given the new email belongs to another non-deleted user, When PUT, Then 409.
- AC11 (full replace) - Given `phone` is omitted or null, When PUT, Then phone is cleared.
Notes: Assumption: email change on a normal record → 409; PUT returns 200 with body.

### US-111 - Delete and anonymize a customer (EP-33, Slice S2, Must)
**As an** Admin **I want** to erase a customer's personal data while keeping their orders **so that** we honor erasure requests without losing order history.
Traces: FR-064, FR-065, FR-015, FR-007, FR-018, FR-127, FR-128, NFR-10, NFR-11
Route: DELETE /api/v1/customers/{customerId}  ·  Rate limit: general  ·  Cache: never cached
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a customer with no orders, or only Completed/Cancelled orders, When DELETE, Then 204, and in one transaction: the Customer is soft deleted; first name, last name, email and phone are anonymized; the Identity user is soft deleted and locked, with its email, user name and normalized fields anonymized; all refresh tokens are revoked; a new audit row (action SoftDelete) is written with personal-data fields as `"changed"`; existing audit rows are not touched because they never hold personal-data values (P1); orders and order lines are unchanged.
- AC2 (erasure check) - Given a customer seeded with a value in every PII field, When deleted, Then a search of all structured columns (including `AuditLogs` jsonb, Identity tables and refresh tokens) finds 0 matches for the old email, phone or full name. Free-text fields and log files are out of scope (FR-065).
- AC3 (after delete) - Given the deleted customer, When they log in with the old password, Then 401 generic; When they use an old refresh token, Then 401; When forgot-password is called with the old email, Then 202 and no link is logged.
- AC4 (validation) - Given a malformed `customerId`, When DELETE, Then 400.
- AC5 (unauthenticated) - Given no token, When DELETE, Then 401.
- AC6 (forbidden) - Given a Customer token (including their own ID), When DELETE, Then 403; nothing changes.
- AC7 (not found) - Given an unknown ID, Then 404. Given a customer that is already deleted, Then 404 and no new audit row.
- AC8 (open orders) - Given the customer has an order in Pending, Confirmed or Shipped, When DELETE, Then 409 (problem type `customer-has-open-orders`); nothing is anonymized; no audit row.
- AC9 (rollback) - Given any step fails, Then nothing is anonymized, deleted or revoked, and no audit row is written.
- AC10 (orders kept) - Given the deleted customer had orders, When an Admin reads those orders (S3) or the order summary (S4), Then they are returned with the anonymized customer name.
Notes: The anonymized email must be unique per record (for example derived from the record ID on a reserved domain) so restore cannot break the partial unique index.

### US-112 - Restore a customer (EP-34, Slice Stretch, Could)
**As an** Admin **I want** to restore a deleted customer without bringing back their personal data **so that** I can recover a mistaken delete and reconnect the account safely.
Traces: FR-066, FR-127, FR-128, FR-019
Route: POST /api/v1/customers/{customerId}/restore  ·  Rate limit: general  ·  Cache: never cached
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a soft-deleted customer, When POST restore, Then 204; the Customer record and the Identity user are un-deleted; the data stays anonymized; the user stays locked; audit rows (action Restore) exist with no PII.
- AC2 (state after restore) - Given the restored customer, When an Admin GETs it, Then 200 with anonymized values, `isLocked: true`, `isDeleted: false`; When anyone tries to log in, Then 401 generic; old refresh tokens stay revoked.
- AC3 (reconnect) - Given the restored customer, When an Admin sets a new email (US-110 AC9) and the reset link is used, Then the user is unlocked and confirmed.
- AC4 (validation) - Given a malformed `customerId`, When POST, Then 400.
- AC5 (unauthenticated) - Given no token, When POST, Then 401.
- AC6 (forbidden) - Given a Customer token, When POST, Then 403.
- AC7 (not found) - Given an unknown ID, When POST, Then 404.
- AC8 (not deleted) - Given a customer that is not deleted, When POST restore, Then 409; no audit row.
Notes: Assumption: `emailConfirmed` is false after restore until the reset link is used.

### US-113 - Read own profile (EP-35, Slice S2, Must)
**As a** Customer **I want** to read my own profile **so that** I can check the data the company holds about me.
Traces: FR-067, FR-129, FR-130, NFR-04, NFR-06
Route: GET /api/v1/customers/me  ·  Rate limit: general  ·  Cache: never cached (no-store)
**Acceptance criteria**
- AC1 (happy path) - Given a Customer token, When GET, Then 200 with the caller's id, email, firstName, lastName, phone, emailConfirmed, createdAt, an `ETag`, and `Cache-Control: no-store`. The customer ID comes only from the token's `customer_id` claim.
- AC2 (validation / tampering) - Given a query such as `?customerId={otherId}`, When GET, Then the parameter is ignored and the caller's own profile is returned.
- AC3 (unauthenticated) - Given no token, When GET, Then 401.
- AC4 (forbidden) - Given an Admin token, When GET, Then 403.
- AC5 (not found) - Given the caller's Customer record was soft deleted while their access token is still valid, When GET, Then 404 (or 401 if R1 adds a per-request check).
- AC6 (BOLA) - Given customers A and B, When A calls GET, Then the response never contains B's data (automated BOLA test).

### US-114 - Update own profile (EP-36, Slice S2, Must)
**As a** Customer **I want** to update my name and phone **so that** my details stay correct.
Traces: FR-068, FR-128, FR-129, NFR-04
Route: PUT /api/v1/customers/me  ·  Rate limit: general  ·  Cache: never cached (no-store)
**Acceptance criteria**
- AC1 (happy path) - Given a Customer token and `If-Match` equal to the ETag from GET `/customers/me`, When PUT `{ firstName, lastName, phone }`, Then 200 with the updated profile and a new `ETag`; an audit row (action Update) has the caller's user ID and only the changed fields.
- AC2 (validation) - Given firstName or lastName empty or over 100 chars, or an invalid phone, When PUT, Then 400 with `errors` per field.
- AC3 (unauthenticated) - Given no token, When PUT, Then 401.
- AC4 (forbidden) - Given an Admin token, When PUT, Then 403.
- AC5 (not found) - Given the caller's record was soft deleted while the access token is still valid, When PUT, Then 404 (or 401 per R1).
- AC6 (missing If-Match) - Given no `If-Match`, When PUT, Then 428.
- AC7 (stale ETag) - Given an Admin changed the record after the customer read it, When PUT with the old ETag, Then 412; no change.
- AC8 (mass assignment) - Given the body also has `email`, `id`, `customerId` or `userId`, When PUT, Then those fields are ignored: the email is unchanged and only the caller's own record is updated.
- AC9 (BOLA) - Given customers A and B, When A sends PUT, Then B's record is never changed (automated BOLA test).

### US-115 - Erase own account (EP-37, Slice S2, Must)
**As a** Customer **I want** to delete my account after confirming my password **so that** my personal data is erased.
Traces: FR-069, FR-064, FR-065, FR-015, FR-128, NFR-04, NFR-10, NFR-11
Route: POST /api/v1/customers/me/erase  ·  Rate limit: auth-strict  ·  Cache: never cached
**Acceptance criteria**
- AC1 (happy path) - Given a Customer token, the correct `currentPassword` and no open orders, When POST, Then 204 with the same effects as US-111 AC1 (soft delete, anonymization, user soft deleted and locked, refresh tokens revoked, orders kept; audit rows hold no PII values); the new audit row has the caller's user ID.
- AC2 (validation) - Given `currentPassword` is missing or empty, When POST, Then 400 with `errors` for `currentPassword`.
- AC3 (wrong password) - Given a wrong `currentPassword`, When POST, Then 400 with a generic message; nothing changes; no audit row. The failure counts toward the login lockout (FR-008): after 5 wrong passwords in a row the account is locked for 15 min.
- AC4 (unauthenticated) - Given no token, When POST, Then 401.
- AC5 (forbidden) - Given an Admin token, When POST, Then 403.
- AC6 (not found) - Given the caller's record is already deleted while the access token is still valid, When POST, Then 404 (or 401 per R1).
- AC7 (open orders) - Given the caller has an order in Pending, Confirmed or Shipped, When POST with the correct password, Then 409 (problem type `customer-has-open-orders`); nothing changes.
- AC8 (erasure check) - Given the deletion succeeded, Then a search across all tables finds 0 matches for the old email, phone or full name.
- AC9 (old tokens) - Given the deletion succeeded, When the old refresh token is used, Then 401; When the old password is used to log in, Then 401 generic; When the old access token calls a protected endpoint, Then 401 if R1 adds a per-request check, otherwise `/me` routes return 404 until the token expires (≤ 15 min).
- AC10 (rollback) - Given any step fails, Then nothing is anonymized or revoked, and no audit row is written.
Notes: Check order is password (400) before open orders (409). Route changed from `DELETE /customers/me` to `POST /customers/me/erase` (decision 2026-10-24) because some clients and proxies drop DELETE bodies. Rate limit auth-strict (10/min per IP).

---

## Definition of Ready / Done
Use PRD section 14 (Ready) and section 15 (Done) as-is.
