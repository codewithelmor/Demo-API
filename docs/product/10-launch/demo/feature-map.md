# Feature map: Orders Shop web front end v0.1.0 (commit c85f560)

Built from the docs (prd-frontend, frontend-architecture), the router (`web/spa/src/app/router.tsx`) and a live run of every page on 2026-10-31 (web image rebuilt from the branch).

| Area | Feature | Built | Shown in video |
|---|---|---|---|
| Access | Register (validation, same answer for any email), check-email page | Yes | Yes |
| Access | Sign in, generic error, "email not confirmed" alert with resend | Yes | Yes |
| Access | Confirm email from link (token removed from address bar) | Yes | Yes |
| Access | Forgot password and reset password from link | Yes | Yes |
| Access | Sign out, sign out everywhere | Yes | Yes (sign out everywhere); sign out button visible |
| Customer | Catalog: list, search (2+ chars), sort, price filters, in stock only, paging | Yes | Yes (paging not needed, 8 products) |
| Customer | Product detail with quantity and add to cart | Yes | Yes |
| Customer | Cart: quantity, remove/undo, estimated total, persisted per user | Yes | Yes |
| Customer | Place order, order detail, status history | Yes | Yes |
| Customer | My orders: status/date filters, sort | Yes | Status filter shown |
| Customer | Cancel pending order with reason | Yes | Yes |
| Customer | Profile view/edit, change password, delete account | Yes | Yes |
| Role guard | Customer opening /admin gets "not allowed" | Yes | Yes |
| Admin | Shell with Dashboard and Products navigation | Yes | Yes |
| Admin | Dashboard: low-stock count, 30-day sales | Yes | Yes |
| Admin | Products: list, search, sort, include deleted, create, edit (If-Match), delete, restore | Yes | Yes |
| Admin | Customers, orders, reports, users, audit logs screens | No (planned, API only) | No, said so in the outro |
| Cross-cutting | 412 conflict notice, 429 retry countdown, Admin idle timeout and warning | Built or specified, hard to trigger honestly | No |
