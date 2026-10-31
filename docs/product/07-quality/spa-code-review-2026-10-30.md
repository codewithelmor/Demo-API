# Code review: web/spa, Release 1 Customer portal (2026-10-30)

Reviewer: `react-code-reviewer` (fresh context, read-only). Saved by the main session. Condensed from the agent's report.

Scope: `web/spa` `src/`, `e2e/`, config (US-F020..F036 plus foundation). Checks run read-only: typecheck pass, lint `--max-warnings 0` pass, format:check pass, vitest 20 files / 189 tests pass, `npm audit --omit=dev` 0. Not run: build (writes files), Playwright (needs compose). Counts: Blocker 0, Major 6, Minor 15, Nit 5.

## Major

1. Cached user data survives a session change (`query-client.ts:8-12`, `sign-in-route.tsx:57-58`, `use-end-session.ts:17`). Same as security finding M-1. Clear the cache on any session identity change; test 401 then sign-in as another user.
2. An involuntary session end never shows "Your session ended" (`require-role.tsx:18-21`; tests at `catalog-route.test.tsx:107-113`, `sign-in-route.test.tsx:129`). Navigate with `reason=session-ended` when the 401 handler clears the session; assert the message.
3. Route loaders call the API before `RequireRole` runs, and failures are swallowed (`router.tsx`; `orders-route.tsx:26`, `account-route.tsx:14`, `edit-profile-route.tsx:26`; `profile.test.tsx:87-97` passes only because the MSW unhandled-request error is swallowed). Add `requireRoleLoader(queryClient, roles)` and a test asserting no request for the wrong role.
4. Place order clears the cart only after awaited refetches (`place-order-panel.tsx:48-59`): duplicate-order risk if the tab closes or the network stalls (no Idempotency-Key, gap G6). `setQueryData`, then `cart.clear()` and `navigate` immediately; do not await the invalidations.
5. Non-`ApiError` failures are dropped or rethrown (`sign-in-route.tsx:64`, `unconfirmed-alert.tsx:24-26`, `src/api/bff.ts:38`). Use `networkError(error)` as the fallback; wrap `bffJson` parse failures into an `ApiError` (slug `invalid-response`).
6. E2E suites fragile and can pass vacuously: file-level `test.skip` without `E2E_CUSTOMER_*` (`orders.spec.ts:10`, `profile.spec.ts:14`, `catalog-cart.spec.ts:10`); `fullyParallel: true` on one shared account (`playwright.config.ts:8`), Pending orders never cleaned up (5-open-order limit), concurrent profile writes, lockout counter. Fail in CI when env vars are missing; serial mode or one account per worker; cancel created orders in `afterEach`.

## Minor

1. Shared layers import a feature (`hooks/use-cart.ts:2`, `hooks/use-end-session.ts:3`, `app/query-client.ts:2` import `@/features/auth/session`); ESLint (`eslint.config.js:102-121`) misses it. Move `session.ts` to `src/lib` or `src/app`; restrict `hooks`, `components`, `lib`.
2. Focus lost after destructive or replacing actions (WCAG 2.4.3): cancel success (`order-detail-route.tsx:222-228`, `cancel-order-dialog.tsx:41-45`), cart line removal (`cart-route.tsx:107-111`), pending-to-detail swap (`order-detail-route.tsx:32-41`), `UnconfirmedAlert`.
3. Repeated identical live text not re-announced (`catalog-route.tsx:70-79,146-148`). Include the quantity or a counter key.
4. Announcement noise and gaps: errors announced three times (`register-route.tsx:102-123`, `form-field.tsx:35`); conditionally mounted `role="status"` (`order-detail-route.tsx:158-162`); filter/sort/page changes announce nothing (`catalog-route.tsx:95-99`, `orders-route.tsx:69-74`).
5. Sign-in email not trimmed (`sign-in-schema.ts:5`); use `.trim().pipe(z.email(...))`.
6. One bad cart line empties the whole cart (`cart-storage.ts:20-22,40-48`); salvage valid lines.
7. Stale `lines` snapshot in cart actions (`use-cart.ts:51-77`); read `readCart(key)` inside each action.
8. Mutation in a loader (`confirm-email-route.tsx:23-44`): revalidate re-sends a consumed token. Acceptable per AC3; note it.
9. Cancel order does not invalidate products (`cancel-order-dialog.tsx:37`); a 429 in the dialog shows no visible countdown.
10. Unmapped slug `concurrency-conflict` (409) in `problem-messages.ts:4-32` (PUT `/customers/me`, cancel). DoD item 3.
11. MSW handlers are not typed from the schema (`test/handlers.ts:7-22`, `session.ts:12`, `problem.ts`).
12. Bundle budget unenforced and unverified; the only figure on record predates the profile screens. Add a script to `npm run check` that gzips the entry plus modulepreloads and fails above 200 KB.
13. Playwright tagging inconsistent (`sign-in.spec.ts:5` uses `@US-F002`); no sign-in happy/error e2e; no register server-error path; `ft-01-token-scan.spec.ts` absent; Chromium only; `waitForTimeout(500)` (`catalog-cart.spec.ts:53`) and a real 1.2 s sleep (`place-order.test.tsx:182`).
14. `useCountdown` has no unit test (re-enable at 0, restart on new 429); use fake timers.
15. Possible navigation race on session end (`use-end-session.ts:17-19`): clear then navigate while `RequireRole` renders `<Navigate>` in the same tick. Navigate first, then clear; confirm `clear()` triggers no refetches.

## Nit

Stale comment `session.ts:7`; `safeHttpUrl` only used by tests, `REASON_MAX` exported but local; `key={index}` at `order-detail-route.tsx:212`; `README.md` still says "walking skeleton" and omits the e2e variables; `report-error.ts:3` logs the whole error object (log slug, status, traceId only).

## Good

Lazy route chunks, `/admin` gated with no loader, no barrels, XSS lint selectors; central ProblemDetails handling with `useServerFailure` (first-field focus, 429 countdown) and `referenceFor`; edit profile with If-Match, 412 flow and "Use my change"; place order single POST never retried; hashed, Zod-validated cart with cross-tab sync; accessibility basics (`aria-describedby`, status regions, AlertDialog safe-button focus, skip link, per-route titles); behaviour-based tests; no `.only`, `any`, `@ts-ignore`, `eslint-disable`; generated test data.
