# User stories 03: Orders, reports and platform

Date: 2026-10-24 (revised 2026-10-27, security S-D4: Pending-order cap) | Status: Approved 2026-10-24 | Tracker: Azure DevOps Boards
Scope: EP-38 to EP-57 (orders for Admin and `/me`, Dapper reports, audit-log query, health) plus 3 cross-cutting enabler stories
Story count: 23 (20 endpoint stories, 2 of them Stretch; 3 enablers)
Sources: [PRD](../prd.md) · [endpoint list](../endpoint-list.md) · [permission matrix](../permission-matrix.md) · [summary](../../00-summary.md)

## Conventions used in every story
- Routes start with `/api/v1`. Health routes do not.
- "PD" means ProblemDetails (RFC 9457) with `type`, `title`, `status`, `detail`, `instance` and `traceId` (US-290).
- "Audit row" means an `AuditLogs` row written in the same transaction: table, key, action, changed fields old/new, the caller's user ID, UTC time and trace ID (US-291). Each write story checks it in an integration test.
- Status codes are checked in the permission-matrix order: 429 → 401 → 403 → 400 → 404 → 428/412 → 409 → 2xx.
- Assumption: a malformed route ID (not a valid ID) returns 400 PD, because the matrix treats 400 as a shape check.
- Cache is `none` on every endpoint in this file (FR-130). Every story also meets the PRD Definition of Done (section 15).
- Sample data: Customer A (`customer_id` = A) and Customer B. Products P1 (price 10.00, stock 5) and P2 (price 2.50, stock 10). The currency comes from configuration (default `USD`).

## Contents
| US | EP | Title | Slice |
|---|---|---|---|
| US-200 | EP-38 | Admin creates an order for a customer | S3 |
| US-201 | EP-39 | Admin lists orders | S3 |
| US-202 | EP-40 | Admin gets an order with items and history | S3 |
| US-203 | EP-41 | Admin replaces items of a Pending order | S3 |
| US-204 | EP-42 | Admin moves an order to the next status | S3 |
| US-205 | EP-43 | Admin cancels an order | S3 |
| US-206 | EP-44 | Admin soft deletes a Cancelled order | Stretch |
| US-207 | EP-45 | Customer places an own order | S3 |
| US-208 | EP-46 | Customer lists own orders | S3 |
| US-209 | EP-47 | Customer gets an own order | S3 |
| US-210 | EP-48 | Customer cancels an own Pending order | S3 |
| US-211 | EP-49 | Customer edits items of an own Pending order | Stretch |
| US-212 | EP-50 | Order summary report (view) | S4 |
| US-213 | EP-51 | Customer order history (function) | S4 |
| US-214 | EP-52 | Sales report by date range | S4 |
| US-215 | EP-53 | Low-stock report | S4 |
| US-216 | EP-54 | Top products report | S4 |
| US-217 | EP-55 | Query the audit log | S4 |
| US-218 | EP-56 | Liveness check | S1 |
| US-219 | EP-57 | Readiness check | S1 |
| US-290 | - | Enabler: paging, sorting, filtering and ProblemDetails | S1 |
| US-291 | - | Enabler: soft delete, audit interceptor, no hard delete | S1 |
| US-292 | - | Enabler: rate limiting, security headers and CORS | S1 |

---

## Orders (Admin)

### US-200 - Admin creates an order for a customer (EP-38, Slice S3, Must)
**As an** Admin **I want** to create an order for any active customer **so that** staff can take orders for customers and fix mistakes.
Traces: FR-080, FR-081, FR-083, FR-096, FR-084, FR-092, FR-046, FR-125, FR-126, FR-128, NFR-04, NFR-16, NFR-20
Route: POST /api/v1/orders  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin, an active customer C, P1 (10.00, stock 5) and P2 (2.50, stock 10). When the Admin posts `{customerId: C, items: [{P1, 2}, {P2, 3}]}`. Then the response is 201 with `Location: /api/v1/orders/{id}` and an `ETag`. The body shows status `Pending`, `customerId` C, line unit prices 10.00 and 2.50 (snapshot), line totals 20.00 and 7.50, total 27.50 and the currency code. P1 stock becomes 3 and P2 stock becomes 7. One history row is added (none → Pending, by the Admin's user ID, at the current UTC time). Audit rows are written: Insert for Order, OrderItems and history, and Update for P1 and P2 (`stockQuantity` old/new). Cache entries for P1 and P2 are removed before the response.
- AC2 (validation) - Given any of these bodies: 0 lines, 51 lines, the same product twice, quantity 0 or 1001, or no `customerId`. When the Admin posts. Then 400 PD with `errors` for `items`, `items[i].productId`, `items[i].quantity` or `customerId`. Nothing is saved and no audit row is written.
- AC3 (product rule) - Given a line with an unknown product or a soft-deleted product. When the Admin posts. Then 400 PD with a field error on `items[i].productId`. Stock does not change.
- AC4 (customer rule) - Given a `customerId` that is unknown or soft deleted. When the Admin posts. Then 400 PD with an error on `customerId`, not 404.
- AC5 (unauthenticated) - Given no token, or an invalid or expired token. When posting. Then 401 PD and nothing is saved.
- AC6 (forbidden) - Given a Customer token, even one whose own ID is in the body. When posting. Then 403 PD and nothing is saved.
- AC7 (not found) - Not applicable. There is no ID in the route; missing references return 400 (AC3, AC4).
- AC8 (insufficient stock) - Given P1 stock 1 and P2 stock 0. When the Admin orders P1 × 2 and P2 × 1. Then 409 PD (type `insufficient-stock`) that lists both product IDs [P1, P2]. No order is created, no stock changes and no audit row is written.
- AC9 (concurrency) - Given P1 stock 5. When 20 parallel creates each order P1 × 1. Then exactly 5 return 201 and 15 return 409. Final stock is 0 and is never negative. Product rows are locked with `SELECT … FOR UPDATE` in ID order, so the test is deterministic (P3).
- AC10 (price snapshot) - Given an order created at P1 = 10.00. When an Admin later changes P1's price to 99.00. Then the order's lines and total do not change.
- AC11 (atomic) - Given a failure is forced after the stock decrement. When the Admin posts. Then the whole transaction rolls back: no order, no history row, no stock change and no audit row.
- AC12 (Pending cap, FR-096, S-D4) - Given customer C already has 5 open `Pending` orders (default cap). When the Admin posts an order for C. Then 409 PD (type `too-many-pending-orders`). Nothing changes. When one of C's orders is confirmed or cancelled, a new order returns 201. Given C has 4 Pending orders, When 10 parallel creates for C arrive, Then exactly 1 returns 201.
Notes: Assumption: a locked or unconfirmed customer can still receive an Admin-created order. The `Idempotency-Key` header (FR-095) is Stretch and not part of this story.

### US-201 - Admin lists orders (EP-39, Slice S3, Must)
**As an** Admin **I want** to page, filter and sort all orders **so that** I can find and work through orders quickly.
Traces: FR-085, FR-120, FR-121, FR-122, FR-123, FR-125, NFR-02, NFR-04
Route: GET /api/v1/orders  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given 25 orders. When an Admin calls GET with no parameters. Then 200 with `{items, page: 1, pageSize: 20, totalCount: 25, totalPages: 2}`. Items are sorted by `createdAt` descending, then by ID. Each item has `id`, `customerId`, `status`, `total`, `currency` and `createdAt`.
- AC2 (paging) - `page=2` returns 5 items. `page=9` returns 200 with empty `items` and `totalCount` 25. `page=0`, `pageSize=0`, `pageSize=101` or a non-number return 400 PD with `errors.page` or `errors.pageSize`.
- AC3 (filtering) - `status=Pending&status=Confirmed` returns only orders in those states. `customerId`, `createdFrom` (inclusive), `createdTo` (exclusive), `minTotal` and `maxTotal` (inclusive) each filter correctly. Several filters combine with AND. An unknown status value returns 400. A date without an offset returns 400. Unknown parameters are ignored. Assumption: `createdFrom ≥ createdTo` or `minTotal > maxTotal` returns 400.
- AC4 (sorting) - `sort=total` and `sort=-total,status` are applied in order, with ID as the last tiebreaker. A field outside the allow-list (`createdAt`, `total`, `status`), such as `customerName`, returns 400 PD with `errors.sort`. More than 3 fields returns 400.
- AC5 (unauthenticated) - Given no token. Then 401.
- AC6 (forbidden) - Given a Customer token. Then 403.
- AC7 (not found) - Not applicable. No match returns 200 with empty `items`.
- AC8 (deleted data) - Orders of a soft-deleted customer are listed. The row shows `customerId` only and no personal data. Soft-deleted orders (EP-44, Stretch) are not listed.
- AC9 (read-only) - The call writes no audit row and nothing is cached.

### US-202 - Admin gets an order with items and history (EP-40, Slice S3, Must)
**As an** Admin **I want** to see one order with its lines and status history **so that** I can answer questions and decide the next step.
Traces: FR-086, FR-092, FR-125, FR-129, NFR-04
Route: GET /api/v1/orders/{orderId}  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an order that went Pending → Confirmed. When an Admin calls GET. Then 200 with an `ETag` header. The body has the header (`id`, `customerId`, `status`, `total`, `currency`, `createdAt`, `lastModifiedAt`), the lines (`productId`, `quantity`, `unitPrice`, `lineTotal`) and 2 history rows in time order (`from`, `to`, `byUserId`, `at`, `reason`).
- AC2 (validation) - Given a malformed `orderId`. Then 400 PD.
- AC3 (unauthenticated) - Given no token. Then 401.
- AC4 (forbidden) - Given a Customer token, even for the Customer's own order. Then 403. Customers use EP-47.
- AC5 (not found) - Given an unknown ID, or a soft-deleted order (Stretch). Then 404 PD.
- AC6 (deleted product) - Given a line product that was soft deleted after the order. Then the line is still returned unchanged.
- AC7 (ETag) - Given the ETag from a GET. When the items or the status change. Then the next GET returns a different ETag.

### US-203 - Admin replaces items of a Pending order (EP-41, Slice S3, Must)
**As an** Admin **I want** to replace the lines of a Pending order **so that** I can correct an order before it is confirmed.
Traces: FR-080, FR-084, FR-087, FR-092, FR-129, FR-046, FR-128, NFR-16, NFR-20
Route: PUT /api/v1/orders/{orderId}/items  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given a Pending order with P1 × 2 at 10.00 and P2 × 3 at 2.50. P1 now costs 12.00, and P3 costs 5.00 with stock 4. Stock is P1 3 and P2 7. The request has a valid `If-Match`. When the Admin replaces the lines with P1 × 4 and P3 × 1. Then 200 with the updated order and a new `ETag`. P1 keeps its 10.00 snapshot. P3 takes 5.00. The total becomes 45.00. Stock becomes P1 1, P2 10 and P3 3. The P2 line is soft deleted, not removed. Audit rows are written for line insert, update and soft delete, the order total, and each stock change. No status history row is added. Product cache entries are removed.
- AC2 (validation) - The same rules as US-200 AC2 apply: 1-50 lines, unique products, quantity 1-1000. Otherwise 400 PD with field errors and nothing changes.
- AC3 (product rule) - A new line with an unknown or soft-deleted product returns 400 with an error on `items[i].productId`. Assumption: a kept line whose product was deleted after the order was placed also returns 400.
- AC4 (unauthenticated) - Given no token. Then 401.
- AC5 (forbidden) - Given a Customer token. Then 403.
- AC6 (not found) - Given an unknown order ID. Then 404, before any `If-Match` check.
- AC7 (If-Match) - A missing `If-Match` returns 428 PD. A stale `If-Match` returns 412 PD. Nothing changes in either case.
- AC8 (wrong state) - Given an order that is Confirmed, Shipped, Completed or Cancelled, and a valid `If-Match`. Then 409 PD that states the current status. Nothing changes.
- AC9 (insufficient stock) - Given a quantity increase larger than the product's stock. Then 409 that lists the short product IDs. Nothing changes. Lowering a quantity never causes 409.
- AC10 (concurrency) - Given two Admins send the same ETag at the same time. Then one gets 200 and the other gets 412.

### US-204 - Admin moves an order to the next status (EP-42, Slice S3, Must)
**As an** Admin **I want** to confirm, ship and complete orders **so that** the order status matches the real-world process.
Traces: FR-088, FR-128, FR-135, NFR-16
Route: POST /api/v1/orders/{orderId}/status  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given a Pending order. When an Admin posts `{status: "Confirmed", reason: "Paid by invoice"}`. Then 204. The status is Confirmed. One history row is added (Pending → Confirmed, the Admin's user ID, the current UTC time, the reason). An audit row is written for the order update and the history insert. The ETag changes and stock does not change.
- AC2 (all allowed steps) - Confirmed → Shipped and Shipped → Completed each return 204 and add one history row each.
- AC3 (invalid transition) - These requests return 409 PD with `allowedNextStatuses`: Pending → Shipped, Pending → Completed, Confirmed → Pending, Confirmed → Confirmed, Completed → any status (`[]`), and Cancelled → any status (`[]`). Nothing changes and no history or audit row is written.
- AC4 (cancel via status) - Given `status: "Cancelled"`. Then 409 with `allowedNextStatuses`, and the `detail` points to the cancel endpoint (EP-43). Assumption.
- AC5 (validation) - A missing or unknown `status` value returns 400 with `errors.status`. A `reason` over 500 characters returns 400 with `errors.reason`.
- AC6 (unauthenticated) - Given no token. Then 401.
- AC7 (forbidden) - Given a Customer token. Then 403.
- AC8 (not found) - Given an unknown or soft-deleted order. Then 404.
- AC9 (concurrency) - Given two Admins confirm the same Pending order at the same time. Then exactly one gets 204 and the other gets 409. Only one history row exists.
Notes: Assumption: this command returns 204, even though it has a body.

### US-205 - Admin cancels an order (EP-43, Slice S3, Must)
**As an** Admin **I want** to cancel a Pending or Confirmed order **so that** reserved stock goes back on sale.
Traces: FR-089, FR-084, FR-046, FR-128, NFR-16, NFR-20
Route: POST /api/v1/orders/{orderId}/cancel  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path, Pending) - Given a Pending order with P1 × 2 and P2 × 3. When an Admin posts `{reason?}`. Then 204. The status becomes Cancelled. P1 stock goes up by 2 and P2 stock by 3. One history row is added (Pending → Cancelled, by the Admin, with the reason). Audit rows are written for the order, the history and both stock updates. Product cache entries are removed.
- AC2 (happy path, Confirmed) - Given a Confirmed order. Then 204 with the same effects as AC1.
- AC3 (deleted product) - Given P1 was soft deleted after the order. Then P1's stock is still restored by 2, and P1 stays deleted.
- AC4 (wrong state) - Given a Shipped, Completed or Cancelled order. Then 409 PD that states the current status, and stock does not change. Cancelling the same order twice returns 204 and then 409, and stock is restored only once.
- AC5 (validation) - A `reason` over 500 characters returns 400 with `errors.reason`. Assumption: `reason` is optional on cancel.
- AC6 (unauthenticated) - Given no token. Then 401.
- AC7 (forbidden) - Given a Customer token. Then 403. Customers use EP-48.
- AC8 (not found) - Given an unknown order. Then 404.
- AC9 (concurrency) - Given P1 stock 0 after this order. When this cancel (restores 2) runs at the same time as a new order for P1 × 1. Then final stock is 1 and no update is lost.

### US-206 - Admin soft deletes a Cancelled order (EP-44, Slice Stretch, Could)
**As an** Admin **I want** to hide Cancelled orders **so that** order lists show only relevant orders.
Traces: FR-093, FR-127, FR-128, NFR-08
Route: DELETE /api/v1/orders/{orderId}  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given a Cancelled order. When an Admin calls DELETE. Then 204. The row stays in the database with `isDeleted = true`, `deletedAt` and `deletedBy` set, and one SoftDelete audit row is written. The order then disappears from EP-39, EP-46 and EP-50, and returns 404 on EP-40 and EP-47.
- AC2 (wrong state) - Given a Pending, Confirmed, Shipped or Completed order. Then 409 PD that states the current status.
- AC3 (validation) - Given a malformed ID. Then 400.
- AC4 (unauthenticated) - Given no token. Then 401.
- AC5 (forbidden) - Given a Customer token. Then 403.
- AC6 (not found) - An unknown order returns 404. An already deleted order also returns 404.
- AC7 (no hard delete) - The order and line row counts do not change, and no SQL `DELETE` is issued (US-291).
Notes: Orders have no restore endpoint and no `includeDeleted` option (FR-127).

## Orders (Customer, `/me`)

### US-207 - Customer places an own order (EP-45, Slice S3, Must)
**As a** Customer **I want** to place an order for myself **so that** I can buy products without staff help.
Traces: FR-080, FR-082, FR-083, FR-096, FR-084, FR-092, FR-046, FR-128, NFR-04, NFR-16
Route: POST /api/v1/customers/me/orders  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given Customer A, P1 (10.00, stock 5) and P2 (2.50, stock 10). When A posts `{items: [{P1, 2}, {P2, 3}]}`. Then 201 with `Location: /api/v1/customers/me/orders/{id}` and an `ETag`. The order belongs to A (taken from the token). The snapshot prices, totals, stock decrement, Pending history row (by A's user ID), audit rows (user = A's user ID) and cache removal all match US-200 AC1.
- AC2 (BOLA) - Given a body that also contains `customerId: B`. Then that field is ignored. The order belongs to A, and B has no new order. Assumption: the extra field is ignored rather than rejected with 400.
- AC3 (validation) - The same line rules as US-200 AC2 apply. Otherwise 400 PD with field errors and nothing is saved.
- AC4 (product rule) - An unknown or soft-deleted product returns 400 with an error on `items[i].productId`.
- AC5 (unauthenticated) - Given no token, or the token of a customer who has erased their account (FR-069). Then 401.
- AC6 (forbidden) - Given an Admin token. Then 403, because Admins have no Customer record.
- AC7 (not found) - Not applicable. There is no ID in the route.
- AC8 (insufficient stock) - Given there is not enough stock. Then 409 that lists the short product IDs. Nothing changes.
- AC9 (concurrency) - Given P1 stock 5. When 20 parallel orders for P1 × 1 come from different customers. Then exactly 5 succeed and final stock is 0.
- AC10 (Pending cap, FR-096, S-D4) - Given A already has 5 open `Pending` orders. When A posts a new order. Then 409 PD (type `too-many-pending-orders`) and nothing changes. After A cancels one Pending order (US-210), a new order returns 201.

### US-208 - Customer lists own orders (EP-46, Slice S3, Must)
**As a** Customer **I want** to see my orders **so that** I can track what I bought.
Traces: FR-091, FR-120, FR-121, FR-122, FR-123, NFR-04
Route: GET /api/v1/customers/me/orders  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given A has 3 orders and B has 2. When A calls GET. Then 200 in the paged shape with exactly A's 3 orders and `totalCount` 3. The default sort is `createdAt` descending, then ID. Assumption: this default matches EP-39.
- AC2 (paging) - Out-of-range `page` or `pageSize` returns 400. A page past the end returns 200 with empty `items`.
- AC3 (filtering) - `status`, `createdFrom` (inclusive) and `createdTo` (exclusive) filter correctly. A `customerId=B` parameter is ignored, so A still sees only A's orders.
- AC4 (sorting) - `sort=total` and `sort=-createdAt` work. `sort=status` returns 400, because the Customer allow-list is only `createdAt` and `total`.
- AC5 (unauthenticated) - Given no token. Then 401.
- AC6 (forbidden) - Given an Admin token. Then 403.
- AC7 (not found / empty) - Given a customer with no orders. Then 200 with empty `items` and `totalCount` 0.

### US-209 - Customer gets an own order (EP-47, Slice S3, Must)
**As a** Customer **I want** to open one of my orders **so that** I can see its lines and progress.
Traces: FR-091, FR-086, FR-129, NFR-04
Route: GET /api/v1/customers/me/orders/{orderId}  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given A's order. When A calls GET. Then 200 with an `ETag`, the header, the lines and the status history (`from`, `to`, `at`, `reason`). Assumption: `byUserId` is not shown to Customers.
- AC2 (validation) - Given a malformed ID. Then 400.
- AC3 (unauthenticated) - Given no token. Then 401.
- AC4 (forbidden) - Given an Admin token. Then 403.
- AC5 (not found, BOLA) - B's order ID returns 404. The response has the same `type`, `title` and `detail` as for an unknown ID, so it reveals nothing about B's order.
- AC6 (not found) - An unknown order returns 404. A soft-deleted order (Stretch) also returns 404.

### US-210 - Customer cancels an own Pending order (EP-48, Slice S3, Must)
**As a** Customer **I want** to cancel my order while it is Pending **so that** I can change my mind before it is processed.
Traces: FR-090, FR-089, FR-084, FR-046, FR-128, NFR-04, NFR-16
Route: POST /api/v1/customers/me/orders/{orderId}/cancel  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given A's Pending order with P1 × 2. When A posts. Then 204. The status becomes Cancelled and P1 stock goes up by 2. One history row is added, by A's user ID. Audit rows are written and the product cache entry is removed.
- AC2 (wrong state) - Given A's order is Confirmed, Shipped, Completed or Cancelled. Then 409 PD that states the current status, and stock does not change.
- AC3 (validation) - Given a malformed ID, or a `reason` over 500 characters. Then 400. Assumption: `reason` is optional.
- AC4 (unauthenticated) - Given no token. Then 401.
- AC5 (forbidden) - Given an Admin token. Then 403.
- AC6 (not found, BOLA) - Given B's order, in any status. Then 404, not 409. B's status, stock and history do not change.
- AC7 (not found) - Given an unknown order. Then 404.
- AC8 (deleted product) - Given a line product that was soft deleted after the order. Then its stock is still restored.
- AC9 (race) - Given A cancels while an Admin confirms the same order at the same time. Then exactly one succeeds. If the confirm wins, the cancel returns 409. Stock is restored at most once.

### US-211 - Customer edits items of an own Pending order (EP-49, Slice Stretch, Could)
**As a** Customer **I want** to change the lines of my Pending order **so that** I do not have to cancel and order again.
Traces: FR-094, FR-080, FR-087, FR-129, FR-128, NFR-04
Route: PUT /api/v1/customers/me/orders/{orderId}/items  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given A's Pending order and a valid `If-Match`. When A replaces the lines. Then 200 with the same effects as US-203 AC1 (snapshot rule, stock difference, totals, soft-deleted removed lines, audit rows by A).
- AC2 (validation) - The FR-080 line rules apply. Otherwise 400 with field errors.
- AC3 (unauthenticated) - Given no token. Then 401.
- AC4 (forbidden) - Given an Admin token. Then 403.
- AC5 (not found, BOLA) - B's order or an unknown order returns 404, before any `If-Match` check. B's order does not change.
- AC6 (If-Match) - A missing `If-Match` returns 428. A stale one returns 412.
- AC7 (wrong state) - Given the order is not Pending. Then 409.
- AC8 (insufficient stock) - Given there is not enough stock. Then 409 that lists the short product IDs.

## Reports and Dapper reads (Admin only, read-only)

### US-212 - Order summary report (EP-50, Slice S4, Must)
**As an** Admin **I want** a fast summary list of orders with counts and totals **so that** I can review orders without loading every line.
Traces: FR-100, FR-106, FR-120 to FR-123, NFR-02
Route: GET /api/v1/reports/order-summaries  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given seeded orders. When an Admin calls GET. Then 200 in the paged shape. Each item has `orderId`, `customerId`, `customerName`, `status`, `itemCount`, `totalUnits`, `total` and `createdAt`. The values equal the order data read through EF Core.
- AC2 (paging) - The same rules as US-201 AC2 apply.
- AC3 (filtering) - `status` (multi), `customerId`, `createdFrom`/`createdTo` and `minTotal`/`maxTotal` behave as in US-201 AC3.
- AC4 (sorting) - The default is `-createdAt`. The allow-list is `createdAt`, `total` and `status`. An unknown field returns 400.
- AC5 (unauthenticated) - Given no token. Then 401.
- AC6 (forbidden) - Given a Customer token. Then 403.
- AC7 (deleted data) - An integration test runs with deleted data present. Soft-deleted lines (removed by an item replace) are not counted in `itemCount` or `totalUnits`. Soft-deleted orders are excluded. Orders of a soft-deleted customer appear, with the anonymized name.
- AC8 (setup) - The view is created by a migration and read with Dapper. p95 is ≤ 1 s on the NFR-02 seed.

### US-213 - Customer order history (EP-51, Slice S4, Must)
**As an** Admin **I want** a paged order history for one customer **so that** I can answer that customer's questions quickly.
Traces: FR-101, FR-106, FR-120, FR-121
Route: GET /api/v1/customers/{customerId}/order-history  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given customer C with 3 orders and other customers with orders. When an Admin calls GET. Then 200 in the paged shape with only C's 3 orders, each with its totals, and `totalCount` 3. Assumption: the order is `createdAt` descending.
- AC2 (paging) - Out-of-range values return 400. A page past the end returns 200 with empty `items`.
- AC3 (filtering and sorting) - FR-101 defines no filters or sort. Assumption: a `sort` parameter is ignored.
- AC4 (unauthenticated) - Given no token. Then 401.
- AC5 (forbidden) - Given a Customer token, even with the Customer's own ID. Then 403.
- AC6 (not found) - An unknown customer returns 404. A soft-deleted customer also returns 404.
- AC7 (edge) - Given an active customer with no orders. Then 200 with empty `items`.
- AC8 (deleted data) - Soft-deleted orders are excluded. The function is created by a migration and has an integration test with deleted data present.

### US-214 - Sales report by date range (EP-52, Slice S4, Must)
**As an** Admin **I want** order count, units and revenue per day, week or month **so that** I can see sales trends.
Traces: FR-102, FR-106, FR-123, FR-125, NFR-02
Route: GET /api/v1/reports/sales?from=&to=&groupBy=  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given these orders: Confirmed on 2026-01-05 (total 100.00, 3 units), Shipped on 2026-01-05 (50.00, 1 unit), Completed on 2026-01-07 (20.00, 2 units), plus one Pending order on 01-05 and one Cancelled order on 01-06. When an Admin calls with `from=2026-01-05T00:00:00Z`, `to=2026-01-08T00:00:00Z` and `groupBy=day`. Then 200 with 3 periods: 01-05 (count 2, units 4, revenue 150.00), 01-06 (0, 0, 0.00) and 01-07 (1, 2, 20.00). Grand totals are 3, 6 and 170.00, with the currency code.
- AC2 (groupBy) - Without `groupBy`, the default is `day`. `week` and `month` use UTC periods. Assumption: weeks start on Monday (ISO 8601), and the first and last periods are cut to the requested range.
- AC3 (validation) - These return 400 PD with errors on `from`, `to` or `groupBy`: missing `from` or `to`; `from ≥ to`; a range over 366 days; `groupBy=year`; a date without an offset. A range of exactly 366 days returns 200.
- AC4 (time zone) - `from=2026-01-05T00:00:00+02:00` is treated as 2026-01-04T22:00:00Z.
- AC5 (unauthenticated) - Given no token. Then 401.
- AC6 (forbidden) - Given a Customer token. Then 403.
- AC7 (not found) - Not applicable. A range with no sales returns 200 with every period at zero and zero totals.
- AC8 (deleted data) - Soft-deleted orders and lines are excluded. Orders whose product or customer was later deleted still count (decision 2026-10-24). An integration test runs with deleted data present.
- AC9 (status rule) - Only Confirmed, Shipped and Completed orders count. An order counts in the period of its `createdAt` (decision 2026-10-24).

### US-215 - Low-stock report (EP-53, Slice S4, Must)
**As an** Admin **I want** a list of products at or below a stock threshold **so that** I can reorder in time.
Traces: FR-103, FR-106, FR-120, FR-121, FR-130
Route: GET /api/v1/reports/low-stock?threshold=  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given the configured threshold is 10 and products have stock 0, 5, 10 and 11. When an Admin calls GET without `threshold`. Then 200, paged, with the products at 0, 5 and 10, sorted by stock ascending and then by ID. Each item has `id`, `sku`, `name` and `stockQuantity`.
- AC2 (filtering) - `threshold=0` returns only products with stock 0. `threshold=-1`, `threshold=1000001` or a non-number returns 400 with `errors.threshold`.
- AC3 (paging and sorting) - The standard paging rules apply. The sort order is fixed. Assumption: a `sort` parameter is ignored.
- AC4 (unauthenticated) - Given no token. Then 401.
- AC5 (forbidden) - Given a Customer token. Then 403.
- AC6 (not found / deleted) - A soft-deleted product with stock 0 is not listed. No match returns 200 with empty `items`.
- AC7 (fresh data) - Given an order lowers P1 from 11 to 9. When an Admin calls the report right after. Then P1 is listed, because the report is not cached.

### US-216 - Top products report (EP-54, Slice S4, Could)
**As an** Admin **I want** the best-selling products for a period **so that** I can see what sells.
Traces: FR-105, FR-106, FR-123
Route: GET /api/v1/reports/top-products?from=&to=&top=  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given sales in the range. When an Admin calls with `top=3`. Then 200 with at most 3 rows. Each row has `productId`, `sku`, `name`, `units` and `revenue`. Rows are sorted by units descending, then revenue descending, then product ID. Assumption.
- AC2 (validation) - `top=0` or `top=51` returns 400. Assumption: `from` and `to` follow the sales-report rules (required, `from < to`, ≤ 366 days), and `top` defaults to 10.
- AC3 (status rule) - Assumption: only Confirmed, Shipped and Completed orders count, as in US-214.
- AC4 (unauthenticated) - Given no token. Then 401.
- AC5 (forbidden) - Given a Customer token. Then 403.
- AC6 (not found / empty) - Given a range with no sales. Then 200 with an empty list.
- AC7 (deleted data) - Soft-deleted orders and lines are excluded. An integration test runs with deleted data present.

### US-217 - Query the audit log (EP-55, Slice S4, Should)
**As an** Admin **I want** to search the audit log **so that** I can see who changed what and when.
Traces: FR-104, FR-128, FR-065, NFR-10, FR-120 to FR-123
Route: GET /api/v1/audit-logs  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given audit rows exist. When an Admin calls GET. Then 200 in the paged shape, newest first (time descending, then ID). Each item has `id`, `entityType`, `entityId`, `action`, `changes` (old/new), `userId`, `timestamp` and `traceId`.
- AC2 (filtering) - `entityType`, `entityId`, `userId`, `action`, `from` (inclusive) and `to` (exclusive) each filter correctly and combine with AND. An `action` outside Insert, Update, SoftDelete, Restore, RoleAssigned and RoleRemoved returns 400. Assumption: `from ≥ to` returns 400.
- AC3 (paging and sorting) - The standard paging rules apply. The sort order is fixed to newest first. Assumption: a `sort` parameter is ignored.
- AC4 (unauthenticated) - Given no token. Then 401.
- AC5 (forbidden) - Given a Customer token. Then 403.
- AC6 (not found) - Not applicable. No match returns 200 with empty `items`.
- AC7 (privacy) - Given a deleted customer. Then their rows show redacted values and never the original email, phone or name. No row contains a password hash, security stamp or token hash.
- AC8 (read-only) - POST, PUT, PATCH and DELETE on `/audit-logs` return 405. The query itself writes no audit row.
- AC9 (security event, S-D5) - Every query logs security event `AuditLogQueried` with the Admin's user ID, the trace ID and the filters as IDs and codes only (no emails or names).

## Health

### US-218 - Liveness check (EP-56, Slice S1, Must)
**As an** operator **I want** a liveness check **so that** Docker can restart the process when it hangs.
Traces: FR-136, NFR-14, NFR-06
Route: GET /health/live  ·  Rate limit: none (Assumption)  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given the process is running. When anyone calls GET. Then 200 with status `Healthy` within 1 s.
- AC2 (dependencies) - Given PostgreSQL and Valkey are both down. Then the response is still 200, because liveness checks only the process.
- AC3 (auth) - Anonymous, Customer and Admin calls all get 200. A request with an expired bearer token also gets 200, not 401.
- AC4 (forbidden / not found) - Not applicable. The endpoint is open to everyone and has no route ID.
- AC5 (no detail) - Outside Development, the body contains only the overall status: no exception text, host names, versions or connection strings. The security headers from US-292 are present.

### US-219 - Readiness check (EP-57, Slice S1, Must)
**As an** operator **I want** a readiness check on the database and cache **so that** I know whether the API can serve traffic.
Traces: FR-136, NFR-03, NFR-14, G3
Route: GET /health/ready  ·  Rate limit: none (Assumption)  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given PostgreSQL and Valkey are up. Then 200 with status `Healthy` within 1 s.
- AC2 (Valkey down) - Given Valkey is stopped. Then 200 with status `Degraded` within 1 s. Product reads still return 200 from the database.
- AC3 (PostgreSQL down) - Given PostgreSQL is stopped. Then 503 with status `Unhealthy` within 1 s.
- AC4 (both down) - Given both are stopped. Then 503.
- AC5 (recovery) - Given PostgreSQL is started again. Then the next call returns 200 without a restart.
- AC6 (detail by environment) - In Development, the body lists each check (`postgres`, `valkey`) with its status and duration. Outside Development, it shows only the overall status.
- AC7 (auth) - Every caller, with or without a token, gets the same result. There is no 401 or 403. Not found does not apply.

## Cross-cutting enablers

### US-290 - Paging, sorting, filtering and ProblemDetails convention (Enabler, Slice S1, Must)
**As an** API consumer **I want** one consistent contract for lists and errors **so that** every endpoint behaves the same and clients are simple to write.
Traces: FR-120 to FR-126, FR-132, FR-133, FR-137, NFR-13, NFR-17
Route: all `/api/v1` endpoints  ·  Rate limit: as per endpoint  ·  Cache: as per endpoint
**Acceptance criteria**
- AC1 (defaults) - Given a list call without paging parameters. Then the response uses `page` 1 and `pageSize` 20 in the shape `{items, page, pageSize, totalCount, totalPages}`.
- AC2 (paging validation) - `page < 1`, `pageSize < 1`, `pageSize > 100` or a non-number returns 400 PD with `errors.page` or `errors.pageSize`. A page past the end returns 200 with empty `items` and the correct `totalCount` and `totalPages`.
- AC3 (sorting) - `sort=name,-price` is applied in order. ID is always the last tiebreaker, so paging through rows with equal values never repeats or skips a row. A field outside the allow-list returns 400, and the error lists the allowed fields. More than 3 fields returns 400.
- AC4 (filtering) - `min`/`max` bounds are inclusive. `xxxFrom` is inclusive and `xxxTo` is exclusive. A date without an offset returns 400. A date with `+02:00` is converted to UTC. `search` is a case-insensitive contains match, and a length of 1 or 101 returns 400. Unknown parameters are ignored.
- AC5 (ProblemDetails) - Every 4xx and 5xx response uses `application/problem+json` with `type`, `title`, `status`, `detail`, `instance` and `traceId`. The `traceId` matches the log entry for that request. Validation errors add `errors` keyed by camelCase field names.
- AC6 (no leaks) - Given an unhandled exception outside Development. Then 500 PD with no stack trace or exception message.
- AC7 (body limits) - A body over 1 MB returns 413 PD. A non-JSON `Content-Type` on a body returns 415 PD. An unknown `/api/v1` route returns 404 PD.
- AC8 (contract) - JSON uses camelCase. Enums are strings. Money has 2 decimals. Dates are ISO 8601 UTC with `Z`. Creates return 201 with `Location`. Commands without a body return 204 with an empty body.
- AC9 (architecture test) - Every endpoint has a validator (or is marked as having no input) and an explicit auth rule. The test fails otherwise.

### US-291 - Soft delete, audit interceptor and no hard delete (Enabler, Slice S1, Must)
**As the** product owner **I want** every write audited and no row ever hard deleted **so that** history is complete and mistakes can be recovered.
Traces: FR-127, FR-128, FR-135, NFR-08, NFR-16, NFR-17, NFR-19, NFR-20, G4
Route: all write endpoints  ·  Rate limit: n/a  ·  Cache: n/a
**Acceptance criteria**
- AC1 (soft delete) - Given an entity that implements `IDeletable` is removed through EF Core. When changes are saved. Then an UPDATE sets `isDeleted = true`, `deletedAt` (UTC from `IDateTimeProvider`) and `deletedBy` (the caller's user ID). The row still exists and the audit action is `SoftDelete`.
- AC2 (query filters) - Deleted rows are hidden from EF Core reads by default. Only the product, customer and user lists honor `includeDeleted`, and only for Admins.
- AC3 (audit fields) - With `FakeTimeProvider` set to a fixed time, an insert sets `createdAt`/`createdBy` and an update sets `lastModifiedAt`/`lastModifiedBy` to exactly that time and the caller.
- AC4 (audit row) - Each write creates an `AuditLogs` row in the same transaction. The row has the table, key, action, only the changed fields (old/new jsonb) and the user ID (`anonymous` for register, `system` for seeding). Time is in UTC, and `traceId` equals the request's trace ID.
- AC5 (rollback) - Given a forced failure after SaveChanges, inside the transaction. Then neither the data nor the audit row is stored.
- AC6 (secrets and PII excluded) - Given a password change, a token refresh and a customer profile update. Then no audit row contains a password hash, security stamp or token hash, and personal-data fields appear as `"changed"` without values (P1). `UPDATE` and `DELETE` on `AuditLogs` fail under the app role (append-only).
- AC7 (DB role) - Raw SQL `DELETE FROM products` run under the app role fails with PostgreSQL permission denied (42501). `CREATE TABLE` also fails. DELETE on `asp_net_user_roles` succeeds (the only DELETE grant, A-D8; `asp_net_user_tokens` is unused and has no DELETE grant), and a role removal writes a `RoleRemoved` audit row. Migrations run under a separate role.
- AC8 (architecture test) - Any use of `ExecuteDelete` or `ExecuteDeleteAsync` in the solution fails the build.
- AC9 (partial unique index) - Given a soft-deleted product with SKU `ABC-1`. When a new product is created with `ABC-1`. Then 201. A duplicate among active products returns 409.
- AC10 (coverage) - The test report shows an audit-row assertion for 100% of write endpoints.

### US-292 - Rate limiting, security headers and CORS (Enabler, Slice S1, Must)
**As an** operator **I want** abuse limits, safe response headers and strict CORS **so that** the API resists brute force and browser-based attacks.
Traces: FR-131, NFR-06, NFR-09, NFR-12, NFR-04
Route: all endpoints  ·  Rate limit: auth-strict, refresh, general  ·  Cache: n/a
**Acceptance criteria**
- AC1 (auth-strict) - Given 10 requests in one minute from one IP to the auth-strict endpoints (register, login, confirm, resend, forgot, reset, change password, erase). When the 11th arrives. Then 429 PD with `Retry-After` in seconds. After the window, calls succeed again. One shared bucket per IP across these endpoints (D16, S-D2).
- AC2 (refresh) - The 31st refresh call in a minute from one IP returns 429.
- AC3 (general) - A signed-in user's 301st call in a minute returns 429. Two users behind the same IP have separate limits. An anonymous caller's 61st call in a minute from one IP returns 429.
- AC4 (order of checks) - Given an anonymous caller over the limit on a protected endpoint. Then 429, not 401.
- AC5 (observability) - Each rejection increments the rate-limit metric and writes a log entry that contains no token.
- AC6 (security headers) - Every response has `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` and `Referrer-Policy: no-referrer`, and has no `Server` header. Outside Development, `Strict-Transport-Security` is present and plain HTTP is not served.
- AC7 (no-store) - `Cache-Control: no-store` is set on `/auth/*`, `/customers/*`, `/orders/*`, `/admin/*`, `/reports/*` and `/audit-logs`. Assumption: this is the list of personal-data routes.
- AC8 (reports concurrency, S-D7) - Given 4 report requests in progress. When a 5th report request arrives. Then 429 PD with `Retry-After`.
- AC9 (forwarded headers, S-D7) - Given an `X-Forwarded-For` header and no proxies configured. Then the rate-limit partition is the connection IP. Given forwarded headers enabled with no known proxies or networks, Then startup fails.
- AC10 (CORS) - Given an origin on the configured list. Then the preflight returns that exact origin in `Access-Control-Allow-Origin`, never `*`. An origin not on the list gets no CORS headers. An empty list means no CORS headers for any origin.
- AC11 (configurable) - The limits and window lengths come from configuration. Tests use low limits and short real windows (1-2 s), because the built-in limiters have no `TimeProvider` (P8).
