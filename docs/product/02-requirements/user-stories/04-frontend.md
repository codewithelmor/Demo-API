# User stories: Web front end (Phase 2)

Last updated: 2026-10-30. Requirements: prd-frontend.md (FR-F, NFR-F). Screen DoD: prd-frontend.md section 10.

## Story map

| Activity → | Get access | Find products | Buy | Track orders | Manage account | Run the shop (Admin) |
|---|---|---|---|---|---|---|
| **Steps** | register, confirm, sign in, recover | browse, search, view | cart, checkout | list, detail, cancel | profile, password, erase, sign out | products, customers, orders, reports, users, audit |
| **MVP (R1)** | F001-F009, F020-F025 | F026, F027 | F028, F029 | F030, F031, F032 | F033, F034, F035 | F006 (stub page) |
| **Release 2** | | | | | F036 | F050-F056, F058, F060, F061, F064, F067, F069 then F057, F059, F062, F063, F065, F068, F070, F071 |
| **Later** | anonymous catalog (G2) | | edit own order (G9) | | email change (G11) | F066, stock adjust (G8), restore customer (G10) |

## Epic E-F1: Foundation and BFF (MVP)

### US-F001: BFF proxies the API (Size M)
- **As a** developer, **I want** a BFF that forwards allow-listed `/api/v1/*` calls with the user's access token, **so that** the browser never needs a token.
- **Priority:** Must | **Release:** MVP | **Links:** FR-F001, NFR-F09, NFR-F06
1. **Given** a signed-in session, **when** the SPA calls `GET /api/v1/products`, **then** the BFF forwards it with `Authorization: Bearer` and returns the API status, body, `ETag`, `Location` and `Retry-After` unchanged.
2. **Given** any request, **when** the path is `/scalar`, `/openapi/*`, `/health/*` or not on the allow-list, **then** the BFF returns 404 and does not call the API.
3. **Given** any BFF response, **when** FT-01 scans it, **then** no body, header or cookie contains `accessToken` or `refreshToken`.

### US-F002: Sign in through the BFF (M)
- **As a** customer, **I want** to sign in with email and password, **so that** I get a secure session.
- **Links:** FR-F002, NFR-F10
1. **Given** valid credentials, **when** I sign in, **then** the BFF sets one `__Host-` HttpOnly, Secure, SameSite=Strict cookie and returns my email, roles and customerId (EP-08) without tokens.
2. **Given** wrong credentials, **when** I sign in, **then** I get 401 `invalid-credentials` and no cookie is set.
3. **Given** an unconfirmed email and the right password, **when** I sign in, **then** I get 403 `email-not-confirmed`.

### US-F003: Silent, single-flight refresh (M)
- **As a** signed-in user, **I want** my session to stay valid while I work, **so that** I am not signed out at random.
- **Links:** FR-F003; ADR-0008 SEC-2
1. **Given** an access token with ≤ 60 s left, **when** 5 requests arrive at once, **then** EP-05 is called exactly once and all 5 succeed.
2. **Given** EP-05 returns 401, **when** any request is made, **then** the BFF deletes the session and returns 401 `session-ended`; the SPA goes to sign-in with "Your session ended".
3. **Given** an Admin locked my account (security stamp changed), **when** I make my next request, **then** I am signed out within that request.

### US-F004: Sign out (S)
- **As a** user, **I want** to sign out, **so that** no one else can use my session.
- **Links:** FR-F004
1. **Given** a session, **when** I sign out, **then** EP-06 is called with my refresh token, the cookie is expired, and the cart is cleared.
2. **Given** the API is unreachable, **when** I sign out, **then** the BFF session and cookie are still removed.

### US-F005: CSRF protection (S)
- **Links:** NFR-F11
1. **Given** a POST, PUT or DELETE without the CSRF header or from another origin, **when** it reaches the BFF, **then** the BFF returns 403 and does not call the API.
2. **Given** a valid same-origin request with the header, **when** it is sent, **then** it is forwarded.

### US-F006: App shell, session bootstrap and role routing (M)
- **Links:** FR-F005, FR-F006
1. **Given** a valid session, **when** the app loads, **then** it shows the area for my role (Customer → catalog, Admin → `/admin` "coming soon" in R1).
2. **Given** a Customer, **when** I open `/admin/...` directly, **then** I see "Not allowed" and no Admin API call is made.
3. **Given** no session, **when** I open a protected route, **then** I go to sign-in, and I return to that route after signing in.

### US-F007: Error handling with ProblemDetails (M)
- **Links:** FR-F007, NFR-F19..F24
1. **Given** a 400 with `errors`, **when** the form shows it, **then** each message appears under its field and focus moves to the first one.
2. **Given** a 429 with `Retry-After: 30`, **when** it is shown, **then** the message shows a 30-second countdown and the submit button stays disabled until it ends.
3. **Given** a 500 or 503, **when** it is shown, **then** the message includes a copyable reference (`traceId`).
4. **Given** a slug the app does not know, **when** it is shown, **then** the app shows `title` and the reference.

### US-F008: BFF rate limits and API configuration (S)
- **Links:** FR-F008, FR-F009, gap G1
1. **Given** 10 auth calls from one browser IP in 60 s, **when** the 11th arrives, **then** the BFF returns 429 with `Retry-After`.
2. **Given** two different browser IPs, **when** each makes 10 auth calls, **then** neither gets 429.

### US-F009: Compose, configuration and pipeline (S)
- **Links:** FR-F009, G4 goal
1. **Given** `docker compose up -d`, **when** it finishes, **then** the SPA and BFF are reachable on localhost with no manual steps.
2. **Given** a register in the SPA, **when** I read the API log, **then** the confirm link points at the SPA origin.
3. **Given** a PR, **when** the pipeline runs, **then** it builds and tests the SPA and BFF, and fails if files under the API `src/` changed.

## Epic E-F2: Customer portal (MVP)

### US-F020: Register (S)
- **As a** visitor, **I want** to create an account, **so that** I can shop.
- **Links:** FR-F020, EP-01
1. **Given** valid fields, **when** I submit, **then** I see "Check your email to confirm your account" (also for an existing email).
2. **Given** a password under 15 characters, **when** I leave the field, **then** I see the rule before submit and the form is not sent.
3. **Given** the API returns 400 for a blocklisted password, **when** it returns, **then** the message shows under the password field.
4. **Given** a phone without `+country`, **when** I submit, **then** I see the E.164 hint.

### US-F021: Confirm email (S)
- **Links:** FR-F021, EP-02
1. **Given** a valid link, **when** I open it, **then** I see "Email confirmed" and a sign-in button.
2. **Given** an expired or used link, **when** I open it, **then** I see "This link is invalid or expired" and a resend form.
3. **Given** an already confirmed account, **when** I open the link again, **then** I see "Email confirmed".

### US-F022: Resend confirmation (XS)
- **Links:** FR-F022, EP-03
1. **Given** sign-in returns 403 `email-not-confirmed`, **when** I click "Resend email", **then** I see the same neutral message for any email.
2. **Given** I resend 11 times in a minute, **when** I click again, **then** I see the 429 countdown.

### US-F023: Sign-in page (S)
- **Links:** FR-F023
1. **Given** valid credentials, **when** I submit, **then** I land on the catalog.
2. **Given** any 401, **when** it returns, **then** I see "Email or password is incorrect" (no hint about locks or unknown emails).
3. **Given** a password manager, **when** it fills or I paste, **then** the field accepts it (WCAG 3.3.8).

### US-F024: Forgot password (XS)
- **Links:** FR-F024, EP-10
1. **Given** any email, **when** I submit, **then** I see "If an account exists, we sent a link".
2. **Given** an invalid email format, **when** I submit, **then** I see a field error and no call is made.

### US-F025: Reset password (S)
- **Links:** FR-F025, EP-11
1. **Given** a valid link and a valid new password, **when** I submit, **then** I see "Password changed" and go to sign-in.
2. **Given** an expired link (older than 1 h), **when** I submit, **then** I see "Link invalid or expired" and a link to request a new one.
3. **Given** the page loaded, **when** I look at the address bar, **then** the token is no longer shown.

### US-F026: Browse and search the catalog (M)
- **As a** customer, **I want** to search, filter and page through products, **so that** I find what I want fast.
- **Links:** FR-F026, EP-22
1. **Given** the catalog, **when** I type 2 or more characters, **then** results show matches by name or SKU, with the total count.
2. **Given** 1 character, **when** I type, **then** no search is sent and a hint says "Type at least 2 characters".
3. **Given** min price > max price, **when** I apply the filter, **then** I see a field error and no call is made.
4. **Given** a page past the end (for example, after products were removed), **when** it loads, **then** I see "No products" and a link to page 1.

### US-F027: Product detail (S)
- **Links:** FR-F027, EP-23
1. **Given** a product, **when** I open it, **then** I see name, SKU, description, price and "In stock" or "Out of stock".
2. **Given** the product was deleted, **when** I open its link, **then** I see "Product not found" (404).

### US-F028: Cart (M)
- **Links:** FR-F028, NFR-F14
1. **Given** a product in stock, **when** I add it, **then** the cart count updates and the change is announced.
2. **Given** 50 distinct products in the cart, **when** I add another, **then** I see "Max 50 products per order".
3. **Given** a quantity above 1000 or below 1, **when** I enter it, **then** the field shows an error.
4. **Given** I sign out, **when** I sign in again, **then** the cart is empty.
5. **Given** my session ended (refresh failed), **when** I sign in again, **then** my cart is still there (UX-D2).

### US-F029: Place order (M)
- **Links:** FR-F029, EP-23, EP-45, NFR-F24
1. **Given** a valid cart, **when** I place the order, **then** I see the created order with status Pending and the total from the response, and the cart is cleared.
2. **Given** a price changed since I added the item, **when** I open checkout, **then** I see the current price, marked "Price updated".
3. **Given** 409 `insufficient-stock`, **when** it returns, **then** the products in `productIds` are marked, and I can change the quantity or remove them.
4. **Given** 409 `too-many-pending-orders`, **when** it returns, **then** I see "You have 5 open orders. Wait for one to be confirmed or cancel one" with a link to My orders.
5. **Given** I click "Place order" twice, **when** the first call is still running, **then** only one order is created.

### US-F030: My orders list (S)
- **Links:** FR-F030, EP-46
1. **Given** orders, **when** I open My orders, **then** I see newest first, with status, total and date in my time zone.
2. **Given** a "To" date before the "From" date, **when** I filter, **then** I see a field error.
3. **Given** no orders, **when** I open the page, **then** I see an empty state with a link to the catalog.

### US-F031: Order detail (S)
- **Links:** FR-F031, EP-47, EP-23
1. **Given** my order, **when** I open it, **then** I see the items with product names, unit price, line totals, the total and the status history.
2. **Given** an item whose product was deleted, **when** the name lookup returns 404, **then** the line shows "Product no longer available" with its prices (gap G3).
3. **Given** another customer's order ID, **when** I open it, **then** I see "Order not found".

### US-F032: Cancel own order (S)
- **Links:** FR-F032, EP-48
1. **Given** a Pending order, **when** I cancel and confirm, **then** the status shows Cancelled and the history has a new row.
2. **Given** the order became Confirmed meanwhile, **when** I cancel, **then** I see "This order can no longer be cancelled (status: Confirmed)" and the page reloads.
3. **Given** a non-Pending order, **when** I view it, **then** no Cancel button is shown.

### US-F033: View and edit profile (S)
- **Links:** FR-F033, EP-35, EP-36, NFR-F22
1. **Given** my profile, **when** I change my phone and save, **then** I see the saved values, and the new ETag is kept.
2. **Given** the profile changed in another tab, **when** I save, **then** I see "Your profile changed since you opened it", the latest data loads, and my typed values stay visible.
3. **Given** an empty phone, **when** I save, **then** the phone is cleared.

### US-F034: Change password (S)
- **Links:** FR-F034, EP-09
1. **Given** a correct current password and a valid new one, **when** I submit, **then** I am signed out and see "Password changed. Sign in again".
2. **Given** a wrong current password, **when** I submit, **then** the error shows on the current-password field, with a warning that repeated failures lock the account.

### US-F035: Delete my account (S)
- **Links:** FR-F035, EP-37, NFR-F17
1. **Given** no open orders, **when** I enter my password and confirm, **then** my session ends and I see "Your account was erased".
2. **Given** an open order, **when** I confirm, **then** I see "Cancel or finish your open orders first" with a link to My orders.
3. **Given** a wrong password, **when** I confirm, **then** the error shows on the password field.

### US-F036: Sign out everywhere (XS, Should, R2)
- **Links:** FR-F036, EP-07
1. **Given** two sessions, **when** I choose "Sign out everywhere" in one, **then** the other session ends on its next refresh.

## Epic E-F3: Admin back office (Release 2; short ACs)

| ID | Story | Size | Pri | Key ACs (Given/When/Then, short) | EP |
|---|---|---|---|---|---|
| US-F050 | Admin shell and landing page | S | Must | Admin sees nav + low-stock count + 30-day sales; at most 2 report calls at once; a report 429 shows a retry on that tile only | 52, 53 |
| US-F051 | Product list incl. deleted | M | Must | Filters and sort as EP-22; "Include deleted" marks deleted rows; 1-char search blocked | 22 |
| US-F052 | Create product | S | Must | Valid → detail page; 409 `duplicate-sku` on SKU field; price with 3 decimals blocked client-side | 24 |
| US-F053 | Edit product (incl. stock) | S | Must | Sends `If-Match`; 412 → reload + keep values; stock 0-1,000,000 | 23, 25 |
| US-F054 | Delete and restore product | S | Must | Delete confirm → row marked deleted; restore → 409 `duplicate-sku` explained | 26, 27 |
| US-F055 | Customer list | S | Must | Search, date range, include deleted, paging | 30 |
| US-F056 | Customer detail + order history | S | Must | Shows lock state, link to user; history paged; deleted customer shows anonymized data | 31, 51 |
| US-F057 | Create customer | S | Should | 201 → detail; 409 `duplicate-email` on email field; note "reset link sent" | 29 |
| US-F058 | Edit customer | S | Must | `If-Match`; email read-only; 412 handled | 32 |
| US-F059 | Delete customer | XS | Should | Confirm shows anonymization effect; 409 `customer-has-open-orders` links to orders | 33 |
| US-F060 | Orders list | M | Must | Uses EP-50 for customer names; status multi-filter; customer picker filter; 429 on report limit handled | 50, 39 |
| US-F061 | Order detail, status, cancel | M | Must | Buttons only for `allowedNextStatuses`; 409 `invalid-status-transition` reloads; cancel only Pending or Confirmed | 40, 42, 43 |
| US-F062 | Replace order items | M | Should | Pending only; `If-Match`; 409 `insufficient-stock` marks products; 409 `invalid-order-state` | 41 |
| US-F063 | Create order for a customer | M | Should | Customer + product pickers; same 409 handling as US-F029; double submit blocked | 30, 22, 38 |
| US-F064 | Sales report | M | Must | Range > 366 days blocked; group day, week or month; empty periods show 0 | 52 |
| US-F065 | Low-stock report | S | Should | Threshold 0-1,000,000; sorted by stock ascending | 53 |
| US-F066 | Top products | S | Could | Top 1-50; range ≤ 366 days | 54 |
| US-F067 | Users list and detail | S | Must | Filters role, locked, confirmed, include deleted; detail shows lock type | 12, 13, 18 |
| US-F068 | Create Admin user | S | Should | 201 → detail; 409 `duplicate-email` | 19 |
| US-F069 | Lock and unlock user | S | Must | Optional "until" date (future) and reason; 409 `admin-safeguard` "You cannot lock yourself or the last Admin"; `user-deleted` | 14, 15 |
| US-F070 | Assign and remove Admin role | S | Should | 409 `user-is-customer`, `admin-safeguard` explained; user signed out by API | 16, 17 |
| US-F071 | Audit-log viewer | M | Should | Filters; field changes table; personal-data fields show "changed"; newest first | 55 |

## Definition of Ready
- [ ] User, value and Given/When/Then ACs including one error case
- [ ] EP-xx and the ProblemDetails slugs to handle are listed
- [ ] Figma frame linked (not needed for US-F001..F009)
- [ ] Fits in ≤ 5 days (size ≤ L)

## Definition of Done
Screen DoD in prd-frontend.md section 10, plus: ACs covered by tests tagged `US-F0xx`/`ACn`; PR with `AB#<id>`; green build; reviews done.

## Totals
MVP: 25 stories (9 Foundation US-F001..F009, 16 Customer US-F020..F035; F036 moves to R2). Screen work only; the full Phase 2 plan with enablers, harness and FT tests is in delivery-plan.md §10 (R1 + RelOps 48-56 days).
Release 2: 23 stories (F036 + 22 Admin), about 30-40 days.
