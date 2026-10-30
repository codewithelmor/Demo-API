# Front-end architecture (Phase 2)

Last updated: 2026-10-30 | Status: Accepted | Decision record: [ADR-0015](adr/0015-web-front-end-bff.md) | Requirements: [prd-frontend.md](../02-requirements/prd-frontend.md)

## 1. Stack (versions checked 2026-10-30; newest stable at least 7 days old)

Approved by the product owner on 2026-10-30; approving this table approves these packages. Sizes and weekly downloads are shown at the setup step.

### SPA
| Package | Version | Licence | Reason |
|---|---|---|---|
| Node.js (build only) | 26.12.1 (`.nvmrc` 26). 26.13.0 became LTS on 10-28; move after 11-04 | MIT | Build time only; no Node in production |
| react / react-dom | 19.3.1 | MIT | Current stable |
| vite | 8.3.5 | MIT | Fast dev server, Rolldown build, plain SPA output for the BFF `wwwroot` |
| @vitejs/plugin-react + babel-plugin-react-compiler | 6.1.2 + 1.0.0 | MIT | React Compiler on; no manual memoization |
| typescript | **6.0.3** (7.x not used) | Apache-2.0 | `typescript-eslint` 8.72 supports TypeScript < 6.1 only |
| react-router | 8.5.0 | MIT | Data mode with lazy route modules; `/admin` is a separate chunk |
| @tanstack/react-query | 5.104.5 | MIT | Server-state cache, invalidation, retry rules per NFR-F23 |
| react-hook-form + @hookform/resolvers | 7.90.0 + 6.0.0 | MIT | Fast forms, Zod resolver |
| zod | 4.6.7 | MIT | One schema per form mirroring catalog §1.5 |
| openapi-typescript (dev) + openapi-fetch | 7.13.0 + 0.17.0 | MIT | Types from committed `api-tests/openapi/v1.json`; typed `fetch` with middleware for `X-CSRF`, 401 and 15 s timeout. Peer range TypeScript `^5.x` → npm `overrides` (spike) |
| tailwindcss + @tailwindcss/vite | 4.3.3 | MIT | CSS-first tokens from Figma |
| shadcn (CLI, dev) + radix-ui + lucide-react | 4.22.1 + 1.8.1 + 1.61.0 | MIT / MIT / ISC | Components copied into `components/ui`; accessible Radix primitives |
| vitest + jsdom | 5.0.5 + 30.1.3 | MIT | Unit and component tests |
| @testing-library/react, user-event, jest-dom | 16.3.3, 14.6.8, 7.0.1 | MIT | Tests through user behaviour |
| msw | 3.0.3 | MIT | API mocks typed from the generated schema |
| @playwright/test | 1.64.1 | Apache-2.0 | E2E against compose; Chromium + Firefox in CI, WebKit in the HTTPS profile |
| @axe-core/playwright | 4.14.0 | **MPL-2.0** | Accessibility scan (NFR-F01). Dev-only, not-distributed licence exception approved 2026-10-30 |
| eslint + typescript-eslint | 10.13.0 + 8.72.0 | MIT | `strictTypeChecked`, zero warnings |
| eslint-plugin-react-hooks | 7.1.1 | MIT | Hooks and React Compiler rules |
| eslint-plugin-jsx-a11y | 6.10.2 | MIT | Strict accessibility lint; peer range stops at ESLint 9 → `overrides` |
| prettier + prettier-plugin-tailwindcss | 3.9.10 + 0.8.1 | MIT | Formatting and class order |

Runner-ups: TanStack Router (typed search params, second routing model); Orval (generated React Query hooks, more generated code).

### BFF (.NET)
| Package | Version | Licence | Reason |
|---|---|---|---|
| Yarp.ReverseProxy | 2.3.0 | MIT | Allow-list proxy, transforms, `traceparent` propagation |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.13 (root pin) | MIT | Valkey session store |
| OpenTelemetry.* | 1.20.1 (root pins) | Apache-2.0 | Same traces and logs as the API |
| xunit.v3, Shouldly, NSubstitute, Mvc.Testing | root pins | Apache-2.0 / BSD / MIT | BFF tests; YARP `IForwarderHttpClientFactory` swapped for an in-process fake API |

## 2. Repository layout

```text
web/                                  # outside src/ so the API stays untouched (G4)
  Directory.Build.props               # imports the root file, then adds BannedApiAnalyzers explicitly
  Directory.Packages.props            # imports the root file, adds Yarp.ReverseProxy
  DemoApi.Bff/                        # ASP.NET Core 10 + YARP; references no DemoApi.* project
    Session/  Proxy/  Security/  Health/  Program.cs  wwwroot/ (build output, git-ignored)
  DemoApi.Bff.Tests/                  # unit + in-process integration (fake API)
  spa/                                # package "demoapi-web"
    src/
      main.tsx
      app/          router.tsx, layouts, providers, query-client.ts, guards (RequireRole), error pages
      features/
        auth/       sign-in, register, confirm-email, resend, forgot-password, reset-password
        catalog/    catalog-route.tsx, catalog-api.ts, product-detail-route.tsx, ...
        cart/       cart-store.ts (localStorage keyed by userId; ids, qty, price seen)
        orders/     place-order, my-orders, order-detail, cancel-order
        profile/    profile, change-password, delete-account
        admin/      products/ customers/ orders/ users/ reports/ audit-logs/   (lazy, own chunk)
      components/ui/  (shadcn)   components/   hooks/
      lib/          problem-details.ts, problem-messages.ts (slug → text), etag.ts, money.ts, dates.ts
      api/          schema.d.ts (generated, committed, lint-excluded), client.ts
      test/         msw handlers, data factories (generated data only)
    e2e/            Playwright specs tagged US-F0xx / ACn; ft-01-token-scan.spec.ts
    .nvmrc  .npmrc (save-exact, engine-strict, min-release-age=7, allow-git=none)  package-lock.json
deploy/docker/Dockerfile.web          # new file; the API Dockerfile is not changed
```

Import rules (ESLint `no-restricted-imports`): no `features/*` folder imports another feature's internals; nothing outside `features/admin` imports from it; the router imports admin routes only through `lazy()`. No barrel files.

## 3. Solution membership and isolation
- `web/DemoApi.Bff` and `web/DemoApi.Bff.Tests` join `DemoApi.slnx` under a `/web/` solution folder, so existing build, format and vulnerable-package steps cover the BFF. Editing `DemoApi.slnx` is a root-file change, not an API source change.
- API architecture tests load explicit assemblies (`ArchitectureModel.Production`), so the BFF is invisible to them; they stay unchanged.
- One BFF rule in `DemoApi.Bff.Tests`: the BFF references no `DemoApi.*` assembly (HTTP only).
- **Do not edit** the root `Directory.Build.props` or `Directory.Packages.props` (freeze guard). `web/Directory.Build.props` and `web/Directory.Packages.props` import the parent with `$([MSBuild]::GetPathOfFileAbove(...))` and add BFF-only items (`Yarp.ReverseProxy`; the banned-API analyzer, which the root adds only for paths containing `src`).
- Template strip (solution-structure §13): delete `web/` and its two `.slnx` entries.

## 4. Compose (configuration only)
| Service | Change |
|---|---|
| `web` (new) | Build `deploy/docker/Dockerfile.web`; image `demoapi/web:dev`; ports `127.0.0.1:${WEB_HTTP_PORT:-8081}:8080`; same hardening as `api` (`read_only`, `cap_drop: ALL`, non-root chiseled). `depends_on`: `api` (started), `valkey` (healthy). Env: `Bff__ApiBaseUrl=http://api:8080`, `Bff__PublicOrigin=http://localhost:8081`, `DataProtection__KeysPath`, `OTEL_SERVICE_NAME=demoapi-bff`, `OTEL_EXPORTER_OTLP_ENDPOINT`. Valkey credentials from `dev-secrets` subpath `bff` (read-only). Volume `bff-dp-keys` |
| `web` + profile `https` | Adds `127.0.0.1:8443:8443` with a dev certificate mounted read-only (`deploy/scripts/Initialize-DevCert.ps1` exports `dotnet dev-certs https`). For Safari/WebKit only |
| `api` | Add `Email__LinkBaseUrl: ${WEB_ORIGIN:-http://localhost:8081}`, `RateLimiting__AuthStrict__PermitLimit: "1000"`, `RateLimiting__Refresh__PermitLimit: "1000"`. `Cors__AllowedOrigins` stays empty |
| `secrets-init` / `init-dev-secrets.sh` | Generate the `demoapi_bff` Valkey ACL user (`~bff:*`, `-@dangerous`) and its password file; never overwrites |
| Outside Docker | `dotnet run` the BFF on `http://localhost:5080`; `npm run dev` (Vite 5173) proxies `/api` and `/bff` to it. In Development `Bff:PublicOrigin` and `Email:LinkBaseUrl` are `http://localhost:5173`. No CSP on the Vite dev server |

`Dockerfile.web` stages: (1) `node:26.12.1-alpine` pinned by digest: `npm ci`, `npm run build`; (2) SDK `10.0.402-noble`: locked restore and publish of `DemoApi.Bff`; (3) `aspnet:10.0.13-noble-chiseled`, non-root: publish output + `dist/` into `wwwroot`.

## 5. Azure Pipelines additions
1. **Stage `ApiFreeze`** (`dependsOn: []`): checkout with `fetchDepth: 0`, `fetchTags: true`; fail if anything changed since tag `api-v1.0` (created on the Phase 2 start commit):
   ```bash
   changed=$(git diff --name-only api-v1.0 HEAD -- src/ Directory.Build.props Directory.Packages.props BannedSymbols.txt global.json nuget.config .editorconfig deploy/docker/Dockerfile)
   [ -z "$changed" ] || { echo "##vso[task.logissue type=error]API is frozen (G4): $changed"; exit 1; }
   ```
   Parameter `allowApiChanges` (default `false`) lifts the freeze for the API v1.1 epic only.
2. **Job `Spa`** (Build stage): `NodeTool@0` from `.nvmrc`; `Cache@2` on npm cache keyed by `package-lock.json`; `npm ci`; `typecheck`, `lint --max-warnings 0`, `format:check`, `test` (JUnit → `PublishTestResults@2`); API-type drift `npm run api:types && git diff --exit-code src/api/schema.d.ts`; `npm run build` + bundle budget (Customer initial JS ≤ 200 KB gzip, no admin module in the entry chunk); `npm audit --omit=dev --audit-level=high`; licence check against the allow-list (MPL-2.0 dev-only exception).
3. **Existing .NET jobs:** BFF built, formatted and scanned through `DemoApi.slnx`; add `web/DemoApi.Bff.Tests` to the FastTests loop.
4. **`DockerImages`:** `docker build -f deploy/docker/Dockerfile.web -t demoapi/web:ci .`
5. **Stage `E2E`** (after Build): `docker compose up -d --wait`; `npx playwright install --with-deps chromium firefox`; Playwright + axe against `http://localhost:8081` including FT-01 (every response and storage scanned for tokens); JUnit results and traces on failure; `retries: 0`.

## 6. Spikes (US-F001, US-F002)
- Safari and Firefox behaviour with `__Host-` cookies on `http://localhost`.
- Whether API `Location` headers are absolute or relative.
- Radix/shadcn under the strict CSP.
- `npm ci` with the `overrides` entries for `openapi-typescript` and `eslint-plugin-jsx-a11y`.
- YARP 2.3.0 on .NET 10: transforms and `ActivityPropagation`.

## 7. Decisions (product owner, 2026-10-30)
| # | Decision |
|---|---|
| FA-D1 | BFF sessions in Valkey (own ACL user, Data Protection encryption, DP keys on a file volume) |
| FA-D2 | HTTP on :8081 for Chromium/Firefox; opt-in `https` compose profile on :8443 for Safari/WebKit; cookie flags never weakened |
| FA-D3 | Top-level `web/` with nested build files; BFF in `DemoApi.slnx`; root build files untouched |
| FA-D4 | Tag `api-v1.0` and `ApiFreeze` pipeline stage; lifted only via `allowApiChanges` in the API v1.1 epic |
| FA-D5 | MPL-2.0 dev-only exception for axe-core; stack in §1 approved |
