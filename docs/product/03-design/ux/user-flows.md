# User flows: Orders web front end

> Decisions (product owner, 2026-10-30): UX-D1 cart stores product ID, quantity and the price seen; UX-D2 cart survives session end, cleared on Sign out and Delete account; UX-D3 UI says "Delete account"; UX-D4 Admin idle warning dialog 2 minutes before the 30-minute sign-out; UX-D5 placeholder shop name "Orders Shop".

Last updated: 2026-10-30. Shapes: rectangle = screen, diamond = decision, rounded box = system action, stadium = start or end.
Shared behaviour that is not drawn in every flow:
- Any 401 `session-ended` follows flow F7.
- Any 429 shows the countdown pattern.
- A 500 or 503 shows the error with its reference (traceId).

## F1. Register, confirm, sign in, first order (US-F020, F021, F022, F023, F026, F028, F029)
```mermaid
flowchart TD
  s(["Visitor opens the shop"]) --> land["Landing /"]
  land -->|"Create account"| reg["Create account /register"]
  reg --> v1{"Fields valid in browser?"}
  v1 -->|"No"| e1["Errors under fields, focus first invalid field"] --> reg
  v1 -->|"Yes"| p1("POST /auth/register via BFF")
  p1 --> r1{"Response"}
  r1 -->|"202"| chk["Check your email /register/check-email"]
  r1 -->|"400 validation-failed"| e1
  r1 -->|"429"| rl1["Countdown, button disabled"] --> reg
  chk --> mail(["User opens link from email"])
  mail --> conf("Confirm page posts userId and token once")
  conf --> r2{"Response"}
  r2 -->|"204"| ok["Email confirmed, Sign in button"]
  r2 -->|"400 invalid-token"| bad["Link invalid or expired, resend form"]
  bad --> rs("POST resend-confirmation") --> chk
  ok --> si["Sign in /sign-in"]
  si --> r3{"Response"}
  r3 -->|"401"| e3["Email or password is incorrect"] --> si
  r3 -->|"403 email-not-confirmed"| nc["Confirm your email first, Resend email button uses typed email"] --> rs
  r3 -->|"200 Admin"| adm["/admin"]
  r3 -->|"200 Customer"| cat["Products /products"]
  cat --> add("Add to cart on a product card")
  add --> cart["Cart /cart"]
  cart --> f3(["Continue in flow F3 checkout"])
```
Steps for a new customer: 9 user actions (register, open email, sign in, search, add, open cart, place order).
How steps were cut:
- Add to cart works from the list, so the detail page is optional.
- The cart is the checkout (D4).
- "Resend" reuses the email already typed.
- After confirming, the Sign in button goes straight to `/sign-in`.

Prefilling the email after confirmation is not possible: the link has only `userId`, and NFR-F14 forbids storing personal data.

## F2. Forgot and reset password (US-F024, F025)
```mermaid
flowchart TD
  s(["Sign in page"]) -->|"Forgot password?"| fp["Forgot password /forgot-password"]
  fp --> v{"Email format valid?"}
  v -->|"No"| fe["Field error, no call"] --> fp
  v -->|"Yes"| p("POST /auth/forgot-password")
  p --> r{"Response"}
  r -->|"202"| sent["If an account exists, we sent a link. It works for 1 hour."]
  r -->|"429"| rl["Countdown"] --> fp
  sent --> mail(["User opens reset link"])
  mail --> rp("Reset page reads email and token, then removes them from the address bar")
  rp --> has{"Token in memory?"}
  has -->|"No, page was refreshed"| lost["Open the link from your email again, or request a new link"] --> fp
  has -->|"Yes"| form["Set a new password form"]
  form --> pv{"15 to 128 characters?"}
  pv -->|"No"| pe["Field error"] --> form
  pv -->|"Yes"| post("POST /auth/reset-password")
  post --> rr{"Response"}
  rr -->|"204"| done(["Sign in with message: Password changed. Sign in with your new password."])
  rr -->|"400 invalid-token"| exp["This link is invalid or expired. Request a new link."] --> fp
  rr -->|"400 validation-failed"| pe
  rr -->|"429"| rl2["Countdown"] --> form
```

## F3. Checkout (US-F028, F029; NFR-F24)
```mermaid
flowchart TD
  s(["Open Cart"]) --> empty{"Cart empty?"}
  empty -->|"Yes"| es["Empty state: Browse products"]
  empty -->|"No"| reread("Re-read each product, GET /products/:id")
  reread --> chk{"Any line not available, out of stock, low stock or price changed?"}
  chk -->|"404"| m1["Line marked: No longer available, Remove"]
  chk -->|"Stock below quantity"| m2["Line marked: Only N left, Set to N or Remove"]
  chk -->|"Price changed"| m3["Line marked: Price updated, was X"]
  chk -->|"No"| ready["Cart ready, estimated total"]
  m1 --> fix("User removes or adjusts the line") --> chk
  m2 --> fix
  m3 --> ready
  ready -->|"Place order"| post("POST /customers/me/orders, button disabled")
  post --> r{"Response"}
  r -->|"201"| ok("Clear cart, go to /orders/:id, announce Order placed")
  ok --> done(["Order detail, status Pending"])
  r -->|"409 insufficient-stock"| is("Re-read the products in productIds")
  is --> m2
  r -->|"409 too-many-pending-orders"| tm["Alert: You have maxPending open orders. Cancel one or wait. Link to My orders. Cart kept."]
  r -->|"400 items[i].productId"| m1
  r -->|"429"| rl["Countdown, button disabled"] --> ready
  r -->|"Network error or 15 s timeout"| ne["We could not confirm your order. Check My orders before trying again. Cart kept."]
  r -->|"401 session-ended"| f7(["Flow F7"])
```

## F4. Cancel own order (US-F032)
```mermaid
flowchart TD
  s(["Order detail /orders/:id"]) --> st{"Status Pending?"}
  st -->|"No"| nob["No Cancel button; text: Only pending orders can be cancelled"]
  st -->|"Yes"| btn["Cancel order button"]
  btn --> dlg["AlertDialog: Cancel this order? Optional reason"]
  dlg -->|"Keep order"| back["Dialog closes, focus back on Cancel order button"]
  dlg -->|"Cancel order"| post("POST /customers/me/orders/:id/cancel")
  post --> r{"Response"}
  r -->|"204"| ok("Reload order, announce Order cancelled, focus status")
  ok --> done(["Status Cancelled, new history row"])
  r -->|"409 invalid-status-transition"| late["Alert: This order can no longer be cancelled (status: Confirmed). Page reloads."]
  r -->|"404"| nf["Order not found, link to My orders"]
  r -->|"400 reason too long"| fe["Field error in dialog"] --> dlg
  r -->|"429"| rl["Countdown in dialog"] --> dlg
```

## F5. Edit profile with a 412 conflict (US-F033; NFR-F22)
```mermaid
flowchart TD
  s(["My account /account"]) -->|"Edit profile"| load("GET /customers/me, keep ETag")
  load --> form["Edit profile form"]
  form -->|"Cancel"| s
  form -->|"Save changes"| v{"Valid in browser?"}
  v -->|"No"| fe["Field errors"] --> form
  v -->|"Yes"| put("PUT /customers/me with If-Match")
  put --> r{"Response"}
  r -->|"200"| ok("Keep new ETag, toast Profile saved")
  ok --> done(["My account read view"])
  r -->|"400"| fe
  r -->|"412"| re("GET latest data and new ETag")
  re --> conflict["Warning Alert: Your profile changed since you opened it. Form shows latest saved values. Under each changed field: Your change X, Use my change."]
  conflict -->|"Review, then Save changes"| put
  r -->|"404"| gone(["Account no longer exists, flow F7"])
  r -->|"428"| bug["Generic error with reference, logged as client bug"]
```

## F6. Delete account (US-F035; NFR-F17)
```mermaid
flowchart TD
  s(["My account"]) -->|"Delete account"| pg["Delete account page: what is deleted, what is kept"]
  pg --> fill["Enter current password, tick I understand this cannot be undone"]
  fill -->|"Delete my account"| v{"Password entered and box ticked?"}
  v -->|"No"| fe["Field errors"] --> fill
  v -->|"Yes"| dlg["AlertDialog: Delete your account permanently?"]
  dlg -->|"Keep my account"| pg
  dlg -->|"Delete account"| post("POST /customers/me/erase")
  post --> r{"Response"}
  r -->|"204"| ok("BFF ends session, cart cleared")
  ok --> done(["Landing / with Alert: Your account was deleted"])
  r -->|"400 currentPassword"| pe["Password is incorrect. Too many failed attempts locks your account for 15 minutes."] --> fill
  r -->|"409 customer-has-open-orders"| oo["Alert: Cancel or finish your open orders first. Link: View open orders"]
  oo --> mo(["My orders filtered to Pending, Confirmed, Shipped"])
  r -->|"429"| rl["Countdown"] --> fill
```
Note: the API route for EP-37 is taken from the catalog name. Use the exact path from the endpoint catalog index when building.

## F7. Session ended (US-F003; FR-F003; NFR-F12)
```mermaid
flowchart TD
  s(["Any API call from any screen"]) --> bff("BFF refresh fails, or the account was locked or deleted")
  bff --> r401("BFF returns 401 session-ended")
  r401 --> h("Global handler: cancel other requests, save current path as returnUrl")
  h --> si["Sign in /sign-in?reason=session-ended&returnUrl=..."]
  si --> msg["Info Alert: Your session ended. Sign in again to continue."]
  msg --> sign("User signs in")
  sign --> role{"Role may open returnUrl?"}
  role -->|"Yes"| back(["Back to the same page"])
  role -->|"No"| home(["Role home"])
  idle(["Admin idle 28 minutes, R2"]) --> warn["Dialog: You will be signed out in 2:00. Stay signed in / Sign out"]
  warn -->|"Stay signed in"| ping("Any BFF call resets idle timer") --> cont(["Continue"])
  warn -->|"No answer"| r401
```
Unsaved form data is lost when the session ends. The cart is kept (Q2). For Admins, the idle warning reduces this loss.

## F8. Admin: edit product with a 412 conflict (US-F053, R2)
```mermaid
flowchart TD
  s(["Products list /admin/products"]) --> d["Product detail"]
  d -->|"Edit"| load("GET /products/:id?includeDeleted=true, keep ETag")
  load --> form["Edit product form"]
  form -->|"Save product"| v{"Valid in browser? price has max 2 decimals, stock 0 to 1,000,000"}
  v -->|"No"| fe["Field errors, error summary"] --> form
  v -->|"Yes"| put("PUT /products/:id with If-Match")
  put --> r{"Response"}
  r -->|"200"| ok(["Detail page, toast Product saved"])
  r -->|"409 duplicate-sku"| sku["SKU field: Another active product uses this SKU"] --> form
  r -->|"412"| re("GET latest and new ETag") --> cf["Conflict pattern: latest values, Your change X, Use my change"] --> form
  r -->|"404"| nf["Product not found. It may have been deleted. Link: Products with deleted shown"]
  r -->|"400"| fe
```

## F9. Admin: change order status (US-F061, R2)
```mermaid
flowchart TD
  s(["Order detail /admin/orders/:id"]) --> btns{"allowedNextStatuses empty?"}
  btns -->|"Yes"| none["No status buttons; text: This order is Completed or Cancelled"]
  btns -->|"No"| b["Buttons, for example Mark as confirmed"]
  b --> dlg["Dialog: Mark order as Shipped? Optional reason. Mark as shipped / Keep status"]
  dlg -->|"Keep status"| s
  dlg -->|"Confirm"| post("POST /orders/:id/status")
  post --> r{"Response"}
  r -->|"204"| ok(["Reload, toast Order marked as Shipped, new history row"])
  r -->|"409 invalid-status-transition"| it["Alert: This order is now currentStatus. Reloaded with the actions that are still possible."] --> s
  r -->|"409 concurrency-conflict"| cc["Alert: Someone changed this order at the same time. We reloaded it. Try again."] --> s
  r -->|"404"| nf["Order not found"]
  s -->|"Cancel order, Pending or Confirmed only"| cd["AlertDialog with reason"] --> cpost("POST /orders/:id/cancel") --> ok
```
