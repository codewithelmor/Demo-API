# Low-fidelity wireframes: Orders web front end

> Decisions (product owner, 2026-10-30): UX-D1 cart stores product ID, quantity and the price seen; UX-D2 cart survives session end, cleared on Sign out and Delete account; UX-D3 UI says "Delete account"; UX-D4 Admin idle warning dialog 2 minutes before the 30-minute sign-out; UX-D5 placeholder shop name "Orders Shop".

Last updated: 2026-10-30. Components are shadcn/ui names. Visual style is product-designer's job.
Breakpoints: Customer from 360 px, Admin from 1024 px (usable at 768 px).

## 0. Patterns used on every screen

### 0.1 Forms
- One column. The label sits above the field. Hints go under the label.
- Mark the optional fields "(optional)", because most fields are required. Explain this once at the top: "All fields are required unless marked optional."
- Build with `Field` and `FieldGroup` (the shadcn form layout added in Oct 2025) or `Form`, with `Input`, `Textarea`, `Select`, `Checkbox` and `Label`.
- **Validation timing:**
  - Check a field when the user leaves it.
  - Once a field shows an error, re-check it on every keystroke so the error clears as soon as it is fixed.
  - On submit, check everything.
  - Never show errors while the user is typing in a field for the first time.
- **On submit with errors:**
  - Focus moves to the first invalid field (NFR-F03).
  - Forms with 5 or more fields also show an error-summary `Alert` at the top: "Fix 2 problems to continue", with links to the fields.
  - Each error is linked to its field with `aria-describedby`, and the field gets `aria-invalid`.
- **Buttons:**
  - Primary action at the bottom left, on the reading path. Secondary action ("Cancel") is a ghost button next to it.
  - Button labels say what happens ("Save changes", not "OK").
  - While a request runs: the button is disabled and shows a `Spinner` plus a verb in -ing form ("Saving…"). It also has `aria-busy`. No double submit.
- **Password fields:**
  - A show/hide toggle (`InputGroup` with a button labelled "Show password").
  - Paste and password managers are always allowed (WCAG 3.3.8).
  - Correct `autocomplete` values: `current-password` or `new-password`.
  - No "confirm password" field. The toggle replaces it.

### 0.2 ProblemDetails mapping (by `type` slug)
| Slug | Where shown | Message |
|---|---|---|
| validation-failed | Under each field from `errors`. Keys with no matching field go to a form-level `Alert` | Client message for known rules; otherwise the server text |
| invalid-token | Page state | "This link is invalid or expired." plus the next action |
| invalid-credentials | `Alert` above the form | "Email or password is incorrect." |
| email-not-confirmed | `Alert` with a button | "Confirm your email first. We sent you a link when you created your account." [Resend email] |
| session-ended (BFF), unauthorized | Global, flow F7 | "Your session ended. Sign in again to continue." |
| forbidden | On page load: SY-2. On an action: `Alert` | "You do not have access to this." |
| not-found | Page state (SY-1 variant) | "Order not found", "Product not found" |
| insufficient-stock | Cart lines | "Only N left." or "Out of stock." |
| too-many-pending-orders | `Alert` in the cart | "You have {maxPending} open orders, the most allowed. Cancel one or wait until one is confirmed." [View my orders] |
| invalid-status-transition | `Alert`, then reload | "This order can no longer be cancelled (status: {currentStatus})." |
| invalid-order-state | `Alert`, then reload | "This order's items can no longer be changed (status: {currentStatus})." |
| customer-has-open-orders | `Alert` with a link | Customer: "Cancel or finish your open orders first." Admin: "This customer has open orders. Cancel or complete them first." |
| duplicate-sku | SKU field | "Another active product uses this SKU. Enter a different SKU." |
| duplicate-email | Email field | "An account with this email already exists." |
| not-deleted | `Alert`, then reload | "This product is already active." |
| admin-safeguard | `Alert` in the dialog | "You cannot do this to your own account or to the last active Admin." |
| user-is-customer | `Alert` | "This user is a customer. Create a separate Admin user instead." |
| user-deleted | `Alert` | "This user was deleted and cannot be unlocked." |
| concurrency-conflict | `Alert`, then reload | "Someone changed this at the same time. We reloaded it. Try again." |
| precondition-failed (412) | Conflict pattern 0.3 | "This record changed since you opened it." |
| precondition-required (428), payload-too-large, unsupported-media-type, method-not-allowed, email-change-not-allowed | Generic `Alert`, logged as a client bug | "Something went wrong on our side." plus the reference |
| rate-limited (429) | Pattern 0.4 | "Too many attempts. Try again in N seconds." |
| internal-error (500) | `Alert` or SY-3 | "Something went wrong. Try again. If it keeps happening, give us this reference: {traceId} [Copy]" |
| service-unavailable (503) | `Alert` with countdown | "The service is busy. Try again in N seconds." |
| Unknown slug | `Alert` | The `title`, plus the reference |
| Network error or 15 s timeout | `Alert` with [Try again] | "We could not reach the service. Check your connection and try again." For a create request: "...Check My orders before trying again." |

### 0.3 412 conflict pattern
1. Keep the user's typed values in memory.
2. GET the latest data and store the new ETag.
3. Show a warning `Alert` at the top of the form, and move focus to it: "This record changed since you opened it. The form now shows the latest saved values. Your changes are shown under each field."
4. Under each field where the user's value differs from the latest value, show: "Your change: {value}" and a [Use my change] button (a ghost button at least 24×24 px).
5. The user clicks Save again. Nothing is saved automatically.

### 0.4 429 countdown
- Show an inline `Alert` next to the submit button: "Too many attempts. Try again in 30 seconds." The number counts down visibly, and the submit button is disabled until it ends.
- Screen readers hear the message once when it appears and once when it ends ("You can try again now"). They do not hear every second.
- Writes are never retried automatically. A GET is retried once automatically only if `Retry-After` is 10 seconds or less.

### 0.5 Destructive actions
- Use `AlertDialog`. The title is a question ("Cancel this order?"). The body states the effect. Buttons: the destructive verb ("Cancel order"), and a safe verb that is not "Cancel" ("Keep order").
- Focus starts on the safe button. Esc closes the dialog. Focus returns to the button that opened it.
- Where the API can reverse the action, use Undo instead of a dialog (D9).

### 0.6 Toasts vs inline messages
| Use a `Sonner` toast | Use an inline `Alert` or field error |
|---|---|
| Success of an action where the user stays on the page or the result is obvious: Added to cart, Profile saved, Product saved, Removed (with Undo) | Every error. Anything the user must act on. Success that changes the page context (Order placed is shown as an Alert on the order page) |

- Toasts stay at least 6 s, and pause on hover or focus. Toasts with an Undo action stay 10 s.
- Toasts are never the only place information appears. The page state also changes.
- A toast must not cover the sticky header or the primary button (WCAG 2.4.11).

### 0.7 Loading and timeouts
- If there is no response after 400 ms, show a `Skeleton` in the shape of the content. Buttons show a `Spinner`.
- After 15 s, show the timeout `Alert` with [Try again].
- When data loads in the background, keep the old content visible with reduced emphasis instead of a blank page.

### 0.8 Route change and focus
- On every route change: update `document.title` ("My orders – Orders Shop") and move focus to the page `<h1>` (`tabindex="-1"`).
- A "Skip to main content" link is the first focusable element.
- When filters, sort or page change on the same page, focus does NOT move. A live region announces "24 products found" or "Page 2 of 7".
- When a dialog closes, focus returns to the element that opened it.

### 0.9 Lists, tables and the URL
- Search, filters, sort and page all live in the URL with the API's own parameter names. Back, refresh and shared links keep them. Changing a filter resets `page` to 1.
- Search: debounced (waits 400 ms after the last keystroke), or runs on Enter. Fewer than 2 characters: the hint "Type at least 2 characters" and no request.
- Ranges (price, dates): check that min ≤ max before sending.
- User dates are inclusive. "To: 31 Oct" is sent as `createdTo` = 1 Nov 00:00 in local time with its offset, because the API's `To` is exclusive.
- Sorting:
  - Admin tables: sortable column headers are buttons with `aria-sort`.
  - Customer lists: a sort `Select`.
- `Pagination` shows "Page 2 of 7 · 134 results". A page past the end shows an empty state with [Go to page 1].
- Admin `Table`s. Customer lists use `Card`s or `Item`s below 768 px.
- Status always appears as a `Badge` with text, never colour alone (WCAG 1.4.1).

### 0.10 Formats
- Money: `Intl.NumberFormat`, en-US, with the response `currency`. Products use the configured currency.
- Dates: "Oct 30, 2026, 2:05 PM" in the browser's time zone. Show the full timestamp in a `Tooltip` only where it matters (Admin audit log).

---

## SH-1. Customer app shell
- **Purpose:** frame every signed-in Customer page.
- **Layout, top to bottom:**
  - Skip link.
  - Header (see the IA navigation model).
  - Global banner slot (SY-4).
  - `<main>` with breadcrumb-free content and an `<h1>`.
  - A small footer (shop name, "Signed in as {email}").
  - `Toaster`.
  - A polite live region for announcements.
- **States:**
  - Bootstrapping the session: full-page `Skeleton` of the header and content. No flash of the public header.
  - No session: redirect to `/sign-in?returnUrl`.
- **Sign out:** account menu > Sign out. No confirmation, because signing out is reversible. The cart is cleared and the user goes to `/sign-in` with "You signed out."
- **Writing:** Cart button `aria-label` "Cart, 3 products". Count updates are announced: "Added to cart. 3 products in cart."

## AN-1. Landing `/`
- **Purpose:** explain the shop and get the visitor to sign in or create an account (FE-D1: no anonymous catalog).
- **Layout:**
  - Public header.
  - Hero: `<h1>` "Orders Shop", one sentence ("Order products online and track every order in one place."), primary [Create account], secondary [Sign in].
  - Three short `Card`s: "Browse and search products", "Place an order in minutes", "Track or cancel your orders".
  - A note: "You need an account to see products and prices."
- **States:** `?reason=account-erased` shows the success `Alert` "Your account was deleted. Your personal data has been erased."

## AN-2. Sign in `/sign-in`
- **Layout:** centred `Card`.
  - `<h1>` "Sign in".
  - Reason `Alert` if a reason is present: "Your session ended. Sign in again to continue." / "Password changed. Sign in with your new password." / "You signed out."
  - Fields: Email, Password (with show/hide).
  - Link "Forgot password?" under the password field.
  - [Sign in].
  - Footer: "New here? Create account". "Didn't get the confirmation email? Resend it".
- **States:**
  - 401: `Alert` "Email or password is incorrect." The password is cleared and focus moves to it.
  - 403 `email-not-confirmed`: `Alert` with [Resend email]. This uses the email already typed (WCAG 3.3.7). The Alert then changes to "If an account exists for this email, we sent a new link."
  - 429: countdown.
  - Loading: "Signing in…".
- **Success:** Customer goes to `returnUrl` or `/products`. Admin goes to `/admin`.

## AN-3. Create account `/register`
- **Layout:** `Card`.
  - `<h1>` "Create your account".
  - Fields: First name, Last name, Email, Password (hint: "At least 15 characters. A short sentence works well. Spaces are allowed."), Phone (optional) (hint: "Include the country code, for example +14155550123").
  - [Create account].
  - Link "Already have an account? Sign in".
- **Validation:**
  - Names: 1-100 characters.
  - Email: 256 characters or fewer.
  - Password: 15-128 code points, with a live character counter after the first time the user leaves the field.
  - Phone: E.164 → "Enter the phone number with + and the country code, for example +14155550123."
  - A blocklist 400 lands on the password field: "This password is too common. Choose another."
- **States:** error summary (5 fields), 429, loading "Creating account…".
- **Success:** go to AN-4.

## AN-4. Check your email `/register/check-email`
- **Content:**
  - `<h1>` "Check your email".
  - "If this email can be used, we sent a link to confirm your account. The link works for 24 hours."
  - [Resend email] goes to AN-6.
  - [Back to sign in].
  - Dev-only note (shown only in the Development build): "Local setup: find the link with `docker compose logs api`."
- The email is not shown and not stored.

## AN-5. Confirm email `/confirm-email`
- **Behaviour:** posts once on load. A guard stops a second post (for example from React StrictMode).
- **States:**
  - Loading: "Confirming your email…" with a `Spinner`.
  - 204: "Email confirmed. You can now sign in." [Sign in].
  - 400 `invalid-token` or missing parameters: "This link is invalid or expired." The resend form (Email, [Send new link]) is inline.
  - 429: countdown with [Try again].

## AN-6. Resend confirmation `/resend-confirmation`
- **Content:** Email, then [Send confirmation email].
- **Result, always the same:** "If an account needs confirming, we sent a new link."
- **States:** field error, 429.

## AN-7. Forgot password `/forgot-password`
- **Content:**
  - `<h1>` "Reset your password".
  - Email field.
  - [Send reset link].
  - Result: "If an account exists for this email, we sent a link. It works for 1 hour."
  - [Back to sign in].
- **States:** field error (no request is sent), 429.

## AN-8. Reset password `/reset-password`
- **Behaviour:** read `email` and `token`, then call `history.replaceState` to remove them from the address bar.
- **Content:**
  - `<h1>` "Set a new password".
  - Email (read-only, `autocomplete="username"`, so password managers save the right account).
  - New password with show/hide and hint.
  - [Set new password].
- **States:**
  - Token missing (page refreshed): "Open the link from your email again, or request a new link." [Request new link].
  - 400 `invalid-token`: "This link is invalid or expired." [Request new link].
  - Validation, 429.
  - Success: go to sign-in with "Password changed".

## CU-1. Products `/products`
```text
[Header: Shop  Products  My orders           Cart(3)  account v]
<h1> Products                                  134 products
[Search name or SKU______] [Filters (2)] [Sort: Name A-Z v]
[Card][Card][Card][Card]   (1 col @360, 2 @640, 3-4 @1024)
 name / SKU / price / Badge In stock / [Add to cart]
[Pagination  < 1 2 3 ... 7 >   Page 1 of 7]
```
- **Filters:**
  - At 1024 px and wider: an inline toolbar with Min price, Max price, Checkbox "In stock only", [Clear filters].
  - Below 1024 px: a [Filters] button with a count `Badge` opens a `Sheet`.
- **Card:** the product name links to the detail page. Price. `Badge` "In stock" / "Out of stock". [Add to cart] adds a quantity of 1 and is disabled with "Out of stock" when stock is 0. If the product is already in the cart, the button reads "Add one more" and a line says "2 in cart".
- **States:**
  - Loading: 8 `Skeleton` cards.
  - Empty with filters: `Empty` component, "No products match your search." [Clear filters].
  - Empty catalog: "No products yet. Check back later."
  - Page past the end: "No products on this page." [Go to page 1].
  - Search with 1 character: hint only, no request.
  - min > max: "Min price must be less than or equal to max price."
  - Error: `Alert` with [Try again].
- **Cart full (50):** toast plus inline text: "Your cart has 50 products, the most for one order. Place this order or remove a product."

## CU-2. Product detail `/products/:productId`
- **Layout:**
  - Back link "Back to products" (keeps the list query).
  - `<h1>` name.
  - SKU.
  - Price.
  - Stock `Badge`.
  - Description (or "No description").
  - Quantity (`Input` type number with − and + in a `ButtonGroup`, 1 to min(1000, stock)).
  - [Add to cart].
- **States:**
  - Loading skeleton.
  - 404: "Product not found. It may have been removed." [Back to products].
  - Out of stock: quantity and button disabled, with the text "Out of stock".
  - Quantity error: "Enter a quantity from 1 to {max}."

## CU-3. Cart `/cart` (also the checkout, D4)
```text
<h1> Cart (3 products)
[Alert slot: too-many-pending / network / 429]
Product        Price            Qty        Line total
Widget A       $12.00 Badge:Price updated (was $10.00)
                                [-][2][+]   $24.00   [Remove]
Gadget B  Alert: Only 1 left [Set to 1] [Remove]
Thing C   Alert: No longer available     [Remove]
-----------------------------------------------
Card: Estimated total $..  "Final total is confirmed when you place the order."
[Place order]    [Continue shopping]
```
- **Components:**
  - Lines: `Table` at 768 px and wider; `Item` list below.
  - The remove control is an icon button with `aria-label` "Remove Widget A".
  - Totals sit in a `Card`.
- **Behaviour:**
  - On open, re-read every product.
  - Lines with problems sort to the top.
  - [Place order] is disabled while any line is unavailable, with the reason shown next to it: "Remove unavailable products to continue."
  - Removing a line shows the toast "Removed Widget A" [Undo].
- **States:**
  - Loading: skeleton lines.
  - Empty: "Your cart is empty. Browse products to add some." [Browse products].
  - Placing: "Placing order…" with the button disabled.
  - Errors: as in flow F3.
- **Success:** go to CU-5.

## CU-4. My orders `/orders`
- **Layout:**
  - `<h1>` "My orders".
  - Toolbar:
    - Status: a multi-select of checkboxes in a `Popover` (Pending, Confirmed, Shipped, Completed, Cancelled).
    - From and To dates.
    - Sort `Select` (Newest first, Oldest first, Total high to low, Total low to high).
    - [Clear filters].
  - List:
    - `Table` at 768 px and wider: Order (short ID link), Date, Status `Badge`, Total.
    - `Card`s below.
  - `Pagination`.
- **States:**
  - Loading skeleton.
  - No orders: "You have not placed any orders yet." [Browse products].
  - No match: "No orders match these filters." [Clear filters].
  - To before From: "The To date must be on or after the From date."
  - Error with [Try again].

## CU-5. Order detail `/orders/:orderId`
- **Layout:**
  - Breadcrumb-like back link "My orders".
  - `<h1>` "Order #{short ID}".
  - Status `Badge`, placed date, last updated.
  - Success `Alert` when arriving from checkout: "Order placed. We will confirm it soon."
  - Items `Table`: Product, Unit price, Qty, Line total. Names load per line with a small skeleton. A 404 shows "Product no longer available".
  - Total with currency.
  - Status history: a list, newest first (status, date, reason if any).
  - [Cancel order] (only when Pending, as a destructive outline button).
- **States:**
  - Loading.
  - 404: "Order not found." [Go to My orders].
  - Error.
  - Not Pending: no Cancel button, and the text "Only pending orders can be cancelled."

## CU-6. Cancel order dialog
- **Layout:**
  - `AlertDialog` title "Cancel this order?"
  - Body: "The items go back into stock. You cannot undo this."
  - `Textarea` "Reason (optional)", 500 characters at most, with a counter.
  - [Cancel order] (destructive) and [Keep order].
- **States:** submitting, 409 (an Alert inside the dialog, then the order reloads and the dialog closes), 429.
- **Success:** the dialog closes. Status is Cancelled. Toast "Order cancelled".

## CU-7. My account `/account`
- **Layout:**
  - `<h1>` "My account".
  - Side links (or a stack on mobile): Profile, Change password, Delete account.
  - Profile `Card` as a read view: Name, Email ("To change your email, contact support"), Phone ("Not added"), Member since. [Edit profile].
  - R2: a `Card` "Sign out everywhere" with explanation and button.
- **States:** loading, error, 404 (flow F7).

## CU-8. Edit profile `/account/edit`
- **Fields:** First name, Last name, Phone (optional; clearing it removes the phone). Email is shown as read-only text, not a disabled field.
- **Buttons:** [Save changes], [Cancel] (returns to `/account`).
- **States:** validation, 412 (pattern 0.3), saving.
- **Success:** go back to `/account` with the toast "Profile saved".

## CU-9. Change password `/account/password`
- **Layout:**
  - `<h1>` "Change password".
  - Info text: "After you change it, you will be signed out on all devices."
  - Fields: Current password, New password (with hint).
  - [Change password].
- **States:**
  - Wrong current password: field error "Current password is incorrect. After 5 failed attempts your account is locked for 15 minutes."
  - 429.
- **Success:** go to sign-in with "Password changed. Sign in with your new password."

## CU-10. Delete account `/account/delete`
- **Layout:**
  - `<h1>` "Delete your account".
  - "What happens" list:
    - "Your name, email and phone are erased."
    - "Your order records are kept without your personal data, for accounting."
    - "You are signed out and cannot sign in again."
    - "Open orders must be cancelled or finished first."
  - Current password field.
  - Checkbox: "I understand this cannot be undone."
  - [Delete my account] (destructive), then the `AlertDialog` "Delete your account permanently?" with [Delete account] and [Keep my account].
- **States:** as in flow F6.

## SY-1 / SY-2 / SY-3 / SY-4 / SY-5
- **SY-1 Not found:** `<h1>` "Page not found". "Check the address, or go back to the start." [Go to Products] (or role home). Item variants: "Order not found", "Product not found".
- **SY-2 Not allowed:** `<h1>` "You do not have access to this page." "This area is for administrators." [Go to Products]. It is shown in place, so the URL stays. No API call is made.
- **SY-3 Something went wrong:** `<h1>` "Something went wrong". "Try again. If it keeps happening, contact support with this reference." A reference code with a [Copy] button. [Try again] [Go to home].
- **SY-4 Service unavailable banner:** a full-width `Alert` under the header, `role="status"`: "The service is not available right now. We will keep trying." It hides itself when the service is ready again.
- **SY-5 Back office coming soon (R1):** `<h1>` "Back office coming soon". "Admin tools arrive in the next release. Use the API documentation for now." [Sign out].

---

## R2 Admin screens (short)
Shared shell (AD-0):
- `Sidebar`, a header with `Breadcrumb` and the account menu, `<main>`.
- Lists: a toolbar (search, filters in a `Popover`, "Include deleted" `Switch`), a `Table` with `aria-sort` headers, `Pagination` with a page-size `Select` (20, 50, 100).
- Deleted rows show a "Deleted" `Badge` and reduced emphasis (not colour alone).
- Detail pages: a read view with [Edit]. Edit pages follow 0.1 and 0.3.

| ID | Key content | Primary action | Key states and writing |
|---|---|---|---|
| AD-1 Dashboard | Two `Card` tiles: "Low stock" (count, link to the report) and "Sales, last 30 days" (orders, units, revenue). They load one after the other, at most 2 at once | - | Each tile has its own skeleton and error. A 429 on a tile shows "Busy. Try again" [Retry] on that tile only |
| AD-2 Products | Columns: Name, SKU, Price, Stock, Created, status Badge. Row actions in a `DropdownMenu`: Edit, Delete / Restore | [New product] | Delete: toast "Product deleted" [Undo] (calls restore), plus a Restore row action. Restore 409 `duplicate-sku`: "Another active product now uses SKU {sku}. Change that product's SKU first." |
| AD-3 / AD-5 Product form | Name, SKU (hint "Letters, numbers and hyphens. Saved in upper case."), Description (optional), Price, Stock quantity (hint "No reason is recorded; the audit log shows old and new values") | [Create product] / [Save product] | duplicate-sku on the SKU field. 3 decimals: "Enter a price with up to 2 decimals." 412 pattern |
| AD-4 Product detail | All fields, created date, deleted state, link "View changes in audit log" (pre-filtered) | [Edit] | 404 "Product not found" with a link to the list with deleted shown |
| AD-6 Customers | Search (name, email), created date range, include deleted. Columns: Name, Email, Phone, Created | [New customer] | Empty: "No customers match these filters." |
| AD-7 New customer | First name, Last name, Email, Phone (optional) | [Create customer] | Success: "Customer created. We sent them a link to set a password." duplicate-email on the field |
| AD-8 Customer detail | Profile, lock state `Badge`, link "Sign-in account" to AD-19, order history `Table` (paged) | [Edit] | Delete: `AlertDialog` that lists what is anonymized and what is kept (NFR-F17). "This cannot be undone." 409 links to the customer's orders. Deleted customer: anonymized data plus a Deleted Badge, no actions |
| AD-9 Edit customer | First name, Last name, Phone; Email read-only | [Save changes] | 412 pattern |
| AD-10 Orders | Status (multi), customer picker (`Command` in a `Popover`, searches EP-30), date range, total range. Columns: Order, Customer, Date, Status, Items, Total | [New order] | Shares the 4-report limit: 429 shows [Retry] |
| AD-11 New order | Step 1: customer picker. Step 2: product picker plus a lines table (as in CU-3). One page, no wizard | [Create order] | Same 409 and 400 handling as F3. Double submit blocked |
| AD-12 Order detail | Customer link, status Badge, items, total, history (with "by" user), buttons from `allowedNextStatuses`, [Change items] (Pending only), [Cancel order] (Pending or Confirmed) | Next-status button | Flow F9 |
| AD-13 Change items | Lines editor as in CU-3, with `If-Match` | [Save items] | invalid-order-state reloads. insufficient-stock marks lines. 412 pattern |
| AD-14 Sales report | From, To (366 days at most: "Choose a range of 366 days or less."), Group by `ToggleGroup` (Day, Week, Month). Totals `Card`s, then a periods `Table`. A chart is optional and only alongside the table | [Show report] | Empty periods show 0. Times are UTC: say so in a caption, "Periods use UTC." |
| AD-15 Low stock | Threshold field (default 10), `Table` sorted by stock ascending, with a link to edit each product | [Apply] | Empty: "No products at or below {n} in stock." |
| AD-16 Top products (Later) | From, To, Top N (1-50), `Table` | [Show report] | - |
| AD-17 Users | Email search, Role, Locked, Confirmed, include deleted. Columns: Email, Name, Roles, Locked, Confirmed | [New Admin user] | - |
| AD-18 New Admin user | Email, First name, Last name | [Create Admin user] | Success note: "We sent a link to set a password." duplicate-email |
| AD-19 User detail | Roles, lock state and type ("Locked by an Admin until..." / "Locked after failed sign-ins"), failed sign-ins, customer link | [Lock user] / [Unlock user] | Lock `Dialog`: "Until" (date and time, optional, must be in the future) and Reason (optional). Text: "The user is signed out at once." admin-safeguard, user-deleted. Assign or remove Admin: `AlertDialog` "The user will be signed out." user-is-customer |
| AD-20 Audit log | Filters: entity type (`Select` with a fixed list, G13), entity ID, user, action, date range. `Table`: Time, Entity, Action, User, then an expandable row (`Collapsible`) with a field / old / new table. Personal-data fields show "changed". Reference (traceId) with Copy | [Apply filters] | Empty: "No changes match these filters." |

Admin idle timeout (FE-D4): at 28 minutes idle, show a `Dialog`: "You will be signed out in 2:00 because you have been inactive." [Stay signed in] [Sign out]. This meets WCAG 2.2.1 Timing Adjustable.

---

## Heuristics and WCAG 2.2 AA check
- **Visibility of status:** cart count, button spinners, live-region announcements, status Badges.
- **Error prevention:**
  - Quantity is capped at the stock level.
  - Min/max and date checks run before the call.
  - Place order is disabled when a line is invalid.
  - Double submit is blocked.
- **User control:** Undo on cart remove and on Admin product delete. Safe default buttons in dialogs. Cancel on every edit page.
- **Recognition over recall:** filters are visible as chips or a count, and live in the URL. The resend action reuses the typed email.
- **Error recovery:** every error says what happened and what to do next. A reference is shown for unexpected errors.
- **WCAG 2.2 specifics:**
  - 2.4.11: a sticky header gets `scroll-padding-top`, so focused fields are not hidden behind it.
  - 2.5.8: icon buttons are at least 24×24 px (44 px recommended on mobile).
  - 3.3.7: no confirm-password field, and resend reuses the typed email.
  - 3.3.8: paste is allowed, no CAPTCHA.
  - 3.3.1: errors are identified in text and linked to fields.
  - 2.2.1: the Admin idle warning.

## Usability risks
| Risk | Mitigation |
|---|---|
| Users cannot find the confirmation email (local demo) | AN-4 dev note. Resend reachable from 3 places |
| Long password rule (15+) surprises users | Hint before typing, character counter, "a short sentence works well" |
| Stock or price changes at checkout confuse users | Problem lines sorted to the top, one-click "Set to N" |
| A network error after Place order leads to a duplicate order | "Check My orders before trying again", cart kept |
| The 412 conflict pattern is unfamiliar | Per-field "Your change" plus "Use my change". Covered in the test |
| "Cancel" vs "Cancel order" wording in the dialog | The safe button reads "Keep order" |

Quick test (`usability-test` skill): 5 people, 3 tasks.
1. Create an account and place your first order. Time it against G1.
2. A product in your cart is now low in stock. Complete the order anyway.
3. Cancel the order you just placed.

Riskiest flow: F1 plus F3.
