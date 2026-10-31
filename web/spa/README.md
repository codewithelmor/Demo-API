# demoapi-web

React + TypeScript single-page app for the Orders API (Phase 2, Release 1 Customer portal). It is served
by the ASP.NET Core BFF (`web/DemoApi.Bff`) on one origin. The browser never holds a token: it only has the
BFF's HttpOnly session cookie. Design: `docs/product/04-architecture/frontend-architecture.md` and ADR-0015.

What is built: anonymous access (register, confirm, resend, forgot and reset password), sign in and out,
catalog and product detail, cart, place order, My orders, order detail and cancel, My account (profile,
change password, delete account, sign out everywhere). The Admin back office is a placeholder.

## Commands

| Command                | Purpose                                                                 |
| ---------------------- | ----------------------------------------------------------------------- |
| `npm ci`               | Install from the lockfile (`.npmrc`: exact versions, scripts off)       |
| `npm run dev`          | Vite on :5173; `/api` and `/bff` are proxied to the BFF on :5080        |
| `npm run typecheck`    | `tsc -b`                                                                |
| `npm run lint`         | ESLint, zero warnings                                                   |
| `npm run format:check` | Prettier check (`npm run format` writes)                                |
| `npm test`             | Vitest + Testing Library + MSW                                          |
| `npm run build`        | Type check, then production build into `dist/`                          |
| `npm run check:bundle` | Fails when the initial JS (entry + modulepreloads, gzip) is over 200 KB |
| `npm run api:types`    | Regenerate `src/api/schema.d.ts` from `api-tests/openapi/v1.json`       |
| `npm run test:e2e`     | Playwright against the compose stack (`E2E_BASE_URL`, default :8081)    |
| `npm run check`        | typecheck, lint, format:check, test, build, check:bundle                |

Node 26 (`.nvmrc`). After `npm ci`, install Playwright browsers once with `npx playwright install chromium`
(install scripts are disabled, so browsers are not downloaded automatically).

## End-to-end tests

Playwright runs against the full compose stack (`docker compose up -d --wait`); the BFF serves the SPA on
`E2E_BASE_URL` (default `http://localhost:8081`). Files and tests run one at a time (`workers: 1`, serial
mode), because they share one account and one database. The tests cancel the orders they create.

| Variable                                                      | Needed by                            | Notes                                                                      |
| ------------------------------------------------------------- | ------------------------------------ | -------------------------------------------------------------------------- |
| `E2E_BASE_URL`                                                | all                                  | Default `http://localhost:8081`                                            |
| `E2E_CUSTOMER_EMAIL`, `E2E_CUSTOMER_PASSWORD`                 | catalog, cart, orders, profile specs | A confirmed Customer with at least one product in stock                    |
| `E2E_THROWAWAY_EMAIL`, `E2E_THROWAWAY_PASSWORD`               | change password (success)            | A **new, confirmed Customer for every run**: the test changes its password |
| `E2E_THROWAWAY_DELETE_EMAIL`, `E2E_THROWAWAY_DELETE_PASSWORD` | delete account (success)             | A second **new, confirmed Customer for every run**: the test erases it     |

Projects: `chromium` runs every test; `firefox` runs only tests tagged `@cross-browser` (sign-in, register
validation, catalog list, cart add, order list; test strategy Q-D7). Install the browsers once with
`npx playwright install chromium firefox`; run one with `npx playwright test --project=firefox`.

Without the variables the tests skip locally. When `CI` is set they **fail** instead, so a pipeline never
goes green without running them. `node scripts/create-e2e-throwaway.mjs` creates both throw-away accounts
before a run on the local compose stack: it registers through the API (`E2E_API_URL`, default
`http://localhost:8080`), takes the confirmation link from the API log (the dev stack uses a fake e-mail sender)
and writes the four variables with random passwords to `$TEMP/e2e-throwaway-env.sh`, outside the repo and never
printed. Then `source "$TEMP/e2e-throwaway-env.sh"`. Never reuse the shared account for them. Use generated data only.

`node scripts/provision-e2e-ci.mjs [file]` does everything on a **fresh** stack: it creates one product with stock
(as the seed Admin, password read from the api container), the shared Customer and both throw-away Customers, and
writes all six variables to `$TEMP/e2e-env.sh` (`source` it). With `--vso` it prints
`##vso[task.setvariable ...;issecret=true]` lines instead, which is what the pipeline uses (the agent masks the
values; nothing is written to disk or published). Run it again before every run that executes the throw-away
tests. In CI traces are off, and the pipeline publishes only the JUnit file.

**CI (Azure Pipelines).** The `E2E` stage in `azure-pipelines.yml` is written but has not yet run on Azure, so
`runE2E` is `false` by default. To try it: Pipelines > Run pipeline > tick `runE2E`. To make it required on
every PR, change the parameter default to `true` after one green run.
The BFF limits per IP (anonymous 60/min, auth 10/min), so the compose file raises `Bff__AnonymousPermitLimit`,
`Bff__AuthStrictPermitLimit` and `Bff__RefreshPermitLimit` (dev only); a full browser run needs it. Playwright traces record typed passwords and the session cookie: keep
`test-results/` inside the build and never upload it elsewhere.

## Rules

- Feature folders never import each other; shared code (`hooks`, `components`, `lib`, `api`) never imports a
  feature; only `features/admin` imports admin code, and the router loads it with `lazy()`. No barrel files.
  ESLint enforces this (`no-restricted-imports`).
- No `dangerouslySetInnerHTML`, `eval`, `new Function`, `innerHTML`, `setAttribute` on URL attributes,
  `location` assignment or `window.open` (NFR-F34, NFR-F35); `href` and `src` from data go through
  `safeHttpUrl()`, and redirects after sign-in through `safeReturnUrl()`. Server text is rendered as text.
  `src/lint-fixtures/sinks.tsx` holds every banned sink and `sinks.test.ts` checks that lint reports each one.
- `<Link to={...}>` and `navigate()` take in-app paths built in the code; a `to` that comes from data must go
  through `safeReturnUrl()` (lint only catches an absolute literal).
- Customer data in the query cache is dropped when a session ends or a new one starts (`endSessionLocally`,
  `startSession` in `src/lib/session.ts`). Route loaders are wrapped in `guarded()` so they make no API call
  for an anonymous visitor or the wrong role.
- `src/api/schema.d.ts` is generated, committed and never edited by hand.
- `/bff/*` endpoints are not in the API's OpenAPI document; `src/api/bff.ts` calls them with plain
  `fetch` and checks responses with Zod (a body it does not recognise becomes an `invalid-response` error).
- Test data is generated; no real customer data.

## Accepted risks

- **`shadcn` CLI as a dev dependency** (`package.json`, `devDependencies`) pulls in a large dev-only tree
  (`npm audit` shows 3 low findings, dev only; `npm audit --omit=dev` shows 0). It never reaches the bundle. We keep it
  for scaffolding components and review the lockfile on every bump; if it becomes a problem, drop it and use
  `npx shadcn@<pinned> add ...` instead.
- **CSP and Radix dialogs (finding L-3, closed):** Radix modals inject a scroll-lock `<style>`; the BFF sends a
  per-response nonce in `style-src` and `src/app/csp-nonce.ts` gives it to Radix. Zod's `new Function` probe also
  tripped `script-src`; `src/lib/zod.ts` turns the JIT off and is the only runtime import of `zod` (a unit test
  enforces it; a `main.tsx` config call was too late because a module-level schema shares Zod's chunk). Two e2e
  tests tagged `[L-3]` assert zero `securitypolicyviolation` events and CSP console errors. Never use `unsafe-inline`
  or `unsafe-eval`.
