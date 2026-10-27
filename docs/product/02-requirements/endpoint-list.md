# Endpoint list (requirements level)

Date: 2026-10-24 | Status: Approved 2026-10-24
All business routes are prefixed with `/api/v1`. Health routes are not. This list seeds the full endpoint catalog in Phase 6 (Architecture).

| ID | Method | Route | Purpose | Slice |
|---|---|---|---|---|
| EP-01 | POST | /auth/register | Register a customer (user + role + Customer record) | S1 |
| EP-02 | POST | /auth/confirm-email | Confirm email with a token | S1 |
| EP-03 | POST | /auth/resend-confirmation | Resend the confirmation link | S1 |
| EP-04 | POST | /auth/login | Get access and refresh tokens | S1 |
| EP-05 | POST | /auth/refresh | Rotate the refresh token | S1 |
| EP-06 | POST | /auth/logout | Revoke one refresh token | S1 |
| EP-07 | POST | /auth/logout-all | Revoke all of the caller's refresh tokens | S1 |
| EP-08 | GET | /auth/me | Current user info | S1 |
| EP-09 | POST | /auth/change-password | Change own password | S1 |
| EP-10 | POST | /auth/forgot-password | Log a reset link | S2 |
| EP-11 | POST | /auth/reset-password | Set a new password with a token | S2 |
| EP-12 | GET | /admin/users | List users | S2 |
| EP-13 | GET | /admin/users/{userId} | Get a user | S2 |
| EP-14 | POST | /admin/users/{userId}/lock | Lock a user | S2 |
| EP-15 | POST | /admin/users/{userId}/unlock | Unlock a user | S2 |
| EP-16 | PUT | /admin/users/{userId}/roles/{role} | Assign the Admin role | S2 |
| EP-17 | DELETE | /admin/users/{userId}/roles/{role} | Remove the Admin role | S2 |
| EP-18 | GET | /admin/roles | List roles | S2 |
| EP-19 | POST | /admin/users | Create an Admin user | S2 |
| EP-20 | POST | /admin/users/{userId}/revoke-sessions | Revoke all of a user's tokens | Stretch |
| EP-21 | POST | /admin/users/{userId}/password-reset-link | Send a reset link for a user | Stretch |
| EP-22 | GET | /products | List products | S1 |
| EP-23 | GET | /products/{productId} | Get a product | S1 |
| EP-24 | POST | /products | Create a product | S1 |
| EP-25 | PUT | /products/{productId} | Update a product | S1 |
| EP-26 | DELETE | /products/{productId} | Soft delete a product | S1 |
| EP-27 | POST | /products/{productId}/restore | Restore a product | S1 |
| EP-28 | POST | /products/{productId}/stock-adjustments | Adjust stock by a delta | Stretch |
| EP-29 | POST | /customers | Admin creates a customer | S2 |
| EP-30 | GET | /customers | List customers | S2 |
| EP-31 | GET | /customers/{customerId} | Get a customer | S2 |
| EP-32 | PUT | /customers/{customerId} | Update a customer | S2 |
| EP-33 | DELETE | /customers/{customerId} | Soft delete and anonymize | S2 |
| EP-34 | POST | /customers/{customerId}/restore | Restore a customer | Stretch |
| EP-35 | GET | /customers/me | Own profile | S2 |
| EP-36 | PUT | /customers/me | Update own profile | S2 |
| EP-37 | POST | /customers/me/erase | Erase own account (password required) | S2 |
| EP-38 | POST | /orders | Admin creates an order for a customer | S3 |
| EP-39 | GET | /orders | List orders | S3 |
| EP-40 | GET | /orders/{orderId} | Get an order with items and history | S3 |
| EP-41 | PUT | /orders/{orderId}/items | Replace items while Pending | S3 |
| EP-42 | POST | /orders/{orderId}/status | Confirm, ship or complete | S3 |
| EP-43 | POST | /orders/{orderId}/cancel | Admin cancel (Pending or Confirmed) | S3 |
| EP-44 | DELETE | /orders/{orderId} | Soft delete a Cancelled order | Stretch |
| EP-45 | POST | /customers/me/orders | Customer places an order | S3 |
| EP-46 | GET | /customers/me/orders | List own orders | S3 |
| EP-47 | GET | /customers/me/orders/{orderId} | Get an own order | S3 |
| EP-48 | POST | /customers/me/orders/{orderId}/cancel | Cancel an own Pending order | S3 |
| EP-49 | PUT | /customers/me/orders/{orderId}/items | Edit items of an own Pending order | Stretch |
| EP-50 | GET | /reports/order-summaries | Order summary (view) | S4 |
| EP-51 | GET | /customers/{customerId}/order-history | Order history (function, paged) | S4 |
| EP-52 | GET | /reports/sales | Sales by date range | S4 |
| EP-53 | GET | /reports/low-stock | Low-stock products | S4 |
| EP-54 | GET | /reports/top-products | Top products | S4 (Could) |
| EP-55 | GET | /audit-logs | Query the audit log | S4 (Should) |
| EP-56 | GET | /health/live | Liveness | S1 |
| EP-57 | GET | /health/ready | Readiness | S1 |

Totals: 57 endpoints. 51 in slices S1-S4 (including 2 health checks, EP-54 Could and EP-55 Should) and 6 stretch (EP-20, 21, 28, 34, 44, 49).
