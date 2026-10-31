# Journeys

| # | Journey | Role | Steps (each checked by the recorder) | Result visible |
|---|---|---|---|---|
| J1 | Create an account and confirm it | Anonymous | Open sign-in, Create account, short password error, valid password, check-your-email, sign in before confirming (alert + resend), open confirm link, sign in | Products page |
| J2 | Find a product | Customer | Catalog, search "key", sort + max price + in stock, open detail | Detail page |
| J3 | Order and cancel | Customer | Add 2 products, cart quantity and remove, place order, My orders filter, open order, cancel with reason | "Order cancelled." |
| J4 | Manage the account | Customer | Edit phone, change password, sign out everywhere, forgot + reset password, /admin not allowed, delete account, sign-in fails | "Your account was erased." |
| J5 | Manage products | Admin | Sign in, dashboard, list search/sort, create (validation then success), edit stock, delete, include deleted, restore | "Restored ..." |

Run log: `demo/automation/web-record.mjs` (31 steps, all passed in the final recording).
