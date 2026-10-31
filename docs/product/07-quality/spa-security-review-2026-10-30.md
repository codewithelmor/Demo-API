# Security review: web/spa (2026-10-30)

Reviewer: `react-security-reviewer` (fresh context, read-only). Saved by the main session (agent cannot write files). Condensed from the agent's report; IDs and evidence unchanged.

Scope: React 19.3.1 + Vite 8.3.5 SPA (all of `src/`, `e2e/`, config, `package.json`, `.npmrc`, lockfile) behind the BFF. Checked against NFR-F09..F16 and F29..F43, threat model §10, OWASP ASVS 5.0 and Top 10:2025. Commands: `npm audit --omit=dev` (0), `npm audit` (3 low, dev only), `npm audit signatures` (857/857 verified), `npm ls`, `npm run lint`. Not run dynamically; the BFF and API are out of scope.

## Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 1 |
| Low | 5 (one needs confirmation) |
| Info | 4 |

Tokens never reach the SPA. The only stored data is the Zod-validated, hashed-key cart. No XSS sinks. Email-link tokens are stripped from the URL before the POST. `X-CSRF` is on every unsafe request.

## Findings

### M-1 (Medium): React Query cache survives a 401 session end and the next sign-in (NFR-F33, T-50)
- Where: `src/app/query-client.ts:8-12`, `src/features/auth/sign-in-route.tsx:57-58`, `src/hooks/use-end-session.ts:17-18`.
- Evidence: the 401 handler only runs `setQueryData(sessionKey, null)`; sign-in `onSuccess` only sets the session. `queryClient.clear()` exists only in `useEndSession` (explicit sign out, sign out everywhere, delete account, change password). Query keys carry no user identity (`['profile']`, `['orders','list',params]`, `['orders','detail',id]`); `staleTime` 30 s, `gcTime` 5 min.
- Impact: customer A's session ends (idle, refresh failure, lock); customer B signs in in the same tab without reload and sees A's cached profile and orders. Memory only, needs a shared tab.
- Fix: one `endSessionLocally(client)` helper in `query-client.ts` that calls `client.clear()` then `setQueryData(sessionKey, null)`, used by the 401 handler (deferred with `queueMicrotask`) and `useEndSession`; call `queryClient.clear()` before setting the session in sign-in `onSuccess`; defence in depth: prefix customer query keys with the customer ID.
- Test: MSW test loads `/account` and `/orders` for user A, 401 on the next call, assert only the session query remains; second test signs in as B and asserts no A data renders; Playwright FT-14/FT-19.
- Mapping: A01/A02:2025; ASVS V7, V14.3.

### L-1 (Low): `safeReturnUrl` does not meet NFR-F34 / FT-11
- Where: `src/lib/safe-url.ts:15-22`, used at `sign-in-route.tsx:34,60`.
- Evidence: checks only leading `/`, no `//` or `/\`, chars below 0x20. Does not reject `/api` or `/bff`, DEL, backslashes elsewhere, or targets the role may not open.
- Impact: no off-site redirect (client-side `navigate`); a spec gap and weak foundation.
- Fix: parse with `new URL(value, location.origin)`, require same origin and single-slash path, reject `/api` and `/bff` (also percent-decoded), control chars, DEL and backslashes; resolve against the user's roles with fallback to `homeFor(user)`.
- Test: extend `safe-url.test.ts` with `/api/v1/x`, `/bff/logout`, `/%2f%2fevil`, `/\u007f`, `/a\\b`, `/admin` as Customer, encoded variants.

### L-2 (Low): `reason` is looked up on a plain object
- Where: `src/features/auth/sign-in-route.tsx:18-23,88`.
- Evidence: `reasonMessages[reason]`; `?reason=constructor` returns a function and React renders an empty status paragraph with a warning. No reflection, no XSS.
- Fix: `Object.hasOwn(reasonMessages, reason)` or a `Map`/`z.enum` of the fixed codes.
- Test: render `/sign-in?reason=constructor` and `?reason=__proto__`, assert no status paragraph.

### L-3 (Low, needs confirmation): CSP `style-src 'self'` vs Radix dialog inline `<style>`; Trusted Types not wired
- Where: BFF CSP `web/DemoApi.Bff/Security/Middleware.cs:49`; `src/components/ui/alert-dialog.tsx`, `cancel-order-dialog.tsx`, `delete-account-route.tsx`.
- Evidence: Radix modals pull in `react-remove-scroll` -> `react-style-singleton`, which injects a `<style>` element (`singleton.js:5`). Under `style-src 'self'` without a nonce the browser blocks it. No `require-trusted-types-for 'script'` report-only header (S-D13).
- Impact: a CSP violation report on every dialog open and a dropped scroll-lock style, contradicting "0 CSP violations in E2E" (FT-09/FT-10). Risk: someone "fixes" it with `unsafe-inline`.
- Fix: do not add `'unsafe-inline'`. Preferred: BFF per-response nonce in `index.html` and `style-src 'nonce-...'`, set the nonce the library reads before the first dialog; or replace the scroll lock with a CSS class. Add Trusted Types in a separate Report-Only header.
- Test: Playwright on :8081 listens for `securitypolicyviolation`, opens the cancel and delete dialogs, asserts 0 violations.

### L-4 (Low): XSS lint rules cover only `href`/`src` and a few sinks
- Where: `eslint.config.js:24-51`.
- Evidence: not covered: `to=` with an absolute URL from data, `action`, `formAction`, `srcDoc`, `setAttribute('href'|'src')`, assignment to `location`/`location.href`, `window.open`. No violations today.
- Fix: add selectors for those, plus a fixture in `lint-fixtures/` that must fail lint.

### L-5 (Low): dev-only `shadcn` CLI widens the supply chain
- Where: `package.json:63`, lockfile (`@modelcontextprotocol/sdk`, `express`, `hono`, ... all `"dev": true`).
- Evidence: `npm audit` (incl. dev) shows 3 low findings, `deepmerge` GHSA-gcqx-w48m-w2h4 via `@shadcn/registry`. `--omit=dev`: 0.
- Fix: drop from `devDependencies` and run `npx shadcn@4.22.1 add ...` only when scaffolding, or record the accepted risk and review the lockfile on each bump.

## Info

- CI supply-chain gates (`npm ci`, `npm audit` prod high / dev critical, `npm audit signatures`, CycloneDX SBOM, licence allow-list) are not in the repo yet: P2-032 / FT-15 / NFR-F40.
- Licences: 26 MPL-2.0 lockfile entries (`axe-core`, `@axe-core/playwright`, `lightningcss` and platform binaries), all dev-only; `caniuse-lite` CC-BY-4.0 data. No GPL, AGPL, SSPL, BUSL or unlicensed packages. Keep `lightningcss` and axe out of the production bundle.
- Cart is compliant (Zod on read, hashed key, cleared on explicit sign-out, sign-out everywhere, delete account). Kept after a session just ends (UX-D2, accepted in T-50). Hand-written `sha256.ts` verified against FIPS 180-4; optional 55/64-byte tests or `crypto.subtle`.
- Other: a 401 `reauth-required` (S-D11) is treated as a session end by `query-client.ts:9`; confirm intended. `report-error.ts:4` logs the raw `ApiError`/`ZodError`: log only `status`, `slug`, `traceId` when real monitoring is wired. Playwright traces record typed passwords and the cookie: never upload `test-results/` outside the build. `Clear-Site-Data: "storage"` on logout wipes other customers' carts on that browser.

## Checked and fine

No `localStorage`/`sessionStorage`/IndexedDB/`document.cookie` except the cart; no `VITE_*` secrets; `userSchema` has no token fields. `history.replaceState` before the POST on confirm; reset token only in loader memory. No `dangerouslySetInnerHTML`, `innerHTML`, `eval`; server text rendered as JSX text; ProblemDetails `detail` never displayed. `X-CSRF: 1` on every unsafe method. Role guards are UX only. Error pages show a generic message plus `traceId`. Password paste allowed, correct `autocomplete` values. `sourcemap: false`, no CDN or remote fonts, no inline script. `.npmrc` hardened; all 903 resolved URLs on registry.npmjs.org; React 19.3.1; Next.js advisories do not apply.

## Not checked

The BFF itself and the API (only the CSP string read); runtime behaviour (CSP, Trusted Types and cache leak from code reading only); generated `schema.d.ts`; git history for secrets; Admin features (placeholder).
