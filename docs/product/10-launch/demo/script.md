# Script: Orders Shop web front end demo

Voice: `en-US-AndrewNeural` (AI voice), rate -14%. Read at about 147 words per minute. App version shown: web 0.1.0, commit `c85f560`.

Each segment starts together with the action in the middle column; the recorder waits until the segment has been spoken.

| Segment | On screen | Voice-over | Seconds |
|---|---|---|---|
| intro | Title card. | Welcome to Orders Shop, the web front end for the Orders API. This tour uses the real app, running locally with demo data. We start as a customer, and then switch to the administrator. | 13.1 |
| c1_open | Sign-in page; click Create account. | This is the sign-in page. A new customer starts with Create account. | 4.8 |
| c1_short | Fill first name, last name, email; type a too-short password; Create account; password field shows an error. | Registration asks for a name, an email, and a password. If the password is too short, the form says so right away, before anything is sent. | 10.9 |
| c1_ok | Type a valid passphrase; Create account; lands on "check your email". | The password needs at least fifteen characters, with no composition rules, and spaces are allowed. So a short sentence works well. | 9.0 |
| c1_check | The same check-your-email page stays on screen. | The result is always the same check your email message, whether or not the address was already registered. That keeps account lookups private. | 9.4 |
| c1_unconf | Sign in before confirming; alert says the email is not confirmed; click Resend email. | If you try to sign in before confirming, the app explains why, and offers to resend the confirmation email. | 7.4 |
| c1_link | (Link read from the API log, not shown.) Open it: "Email confirmed. You can now sign in." | In this local setup, email is simulated, and the confirmation link is written to the server log. Opening it confirms the address, and the app removes the token from the address bar right away. | 13.4 |
| c1_signin | Sign in; land on Products. | Now the customer can sign in. | 2.1 |
| c2_catalog | Catalog with 8 products. | Signed-in customers land on the catalog: eight demo products, each with a price and a stock state. | 7.4 |
| c2_search | Type "k" (hint appears), then "ey": list narrows to Mechanical keyboard. | Search matches name or SKU, and starts after two characters. One letter only shows a hint. Typing key narrows the list, and the search is kept in the address, so the page can be bookmarked. | 13.9 |
| c2_sort | Clear search; sort Price high to low; Max price 100; In stock only; Apply filters. | Sorting and filters work too. Here the list runs from most to least expensive, limited to products under one hundred dollars, and in stock only. | 10.2 |
| c2_detail | Open Mechanical keyboard detail page. | Opening a product shows its details and stock, with a quantity picker and an add to cart button. | 6.6 |
| c2_add | Add to cart; back to Products; add Wireless mouse; header count goes 1, then 2. | Let's add the keyboard, and then a wireless mouse from the list. The cart count in the header updates each time. | 7.5 |
| c3_cart | Open cart; set keyboard quantity to 2; estimated total; remove the mouse. | In the cart, quantities can be changed and products removed, and the estimated total follows. The cart lives in this browser, and is cleared when you sign out. | 11.2 |
| c3_place | Place order; order page: Pending, items, total, status history. | Placing the order checks current prices and stock, and sends it just once. The new order opens right away as pending, with its items, total, and status history. | 11.5 |
| c3_list | My orders; tick Pending; Apply filters. | My orders lists every order, with filters for status and dates, and sorting. The filters stay in the address as well. | 8.7 |
| c3_cancel | Open the order; Cancel order; reason; confirm; "Order cancelled." | A pending order can be cancelled, with an optional reason. The status changes to cancelled, and the history records it. | 8.1 |
| c4_profile | My account; Edit profile; add phone; Save changes. | My account shows the profile. The email is read only. Edit profile changes the name and the phone number. | 8.1 |
| c4_pwd | Change password; signed out; sign in with the new password. | Changing the password signs you out on every device, so you sign in again with the new one. | 6.1 |
| c4_everywhere | My account; Sign out everywhere; signed out. | Sign out everywhere ends the sessions on all devices at once. | 4.6 |
| c4_forgot | Forgot password; enter email; same message; open reset link from the log; new password; sign in. | If you forget the password, enter the email and you always see the same message. The reset link, again from the server log, opens a page to choose a new password, and then returns you to sign-in. | 13.7 |
| c4_guard | Open /admin as a customer: "You do not have access to this page." | Customers cannot open the back office. The admin address shows a not-allowed page instead. | 6.4 |
| c4_delete | Delete account page; current password; tick; Delete my account; confirm dialog. | Delete account explains what happens. Personal details are erased, order records are kept without them, and you cannot sign in again. It asks for the current password and a confirmation. | 12.8 |
| c4_gone | Try to sign in again: error. | Afterwards, the same credentials no longer work. | 3.7 |
| e_signin | Admin signs in (password field masked); Back office dashboard. | Administrators sign in on the same page, and land in the back office. The dashboard shows the low-stock count, and the last thirty days of sales. | 10.4 |
| e_list | Products; search "lamp"; clear; sort Price high to low. | The Products section lists every product, with search, sorting, and paging. | 5.7 |
| e_new | New product; submit empty (errors); fill name, SKU, description, price, stock; Create product; find it. | Creating a product validates the form first. Then the name, SKU, description, price, and stock are saved. | 8.7 |
| e_edit | Edit; change stock to 60; Save changes; row shows 60. | Editing loads the latest version and saves with a concurrency check, so two admins cannot silently overwrite each other. Here I raise the stock. | 10.1 |
| e_delete | Delete; confirm dialog; "Deleted ...". | Deleting asks for confirmation. It is a soft delete: customers stop seeing the product, and existing orders keep their lines. | 9.0 |
| e_restore | Tick Include deleted; row shows Deleted; Restore. | With Include deleted ticked, the product shows as deleted, and one click restores it. | 6.0 |
| outro | End card with run instructions. | That is the customer portal, and the first part of the back office. Screens for customers, orders, reports, users, and audit logs are not in the interface yet. For now those are available through the API. To try it yourself, start the stack with docker compose up, and open localhost, port eight zero eight one. | 22.0 |

Total speech: 283 s, 691 words.
