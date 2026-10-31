# Orders API (working name) - Product Summary

Last updated: 2026-10-30
Mode: Standard (confirmed 2026-10-24)

## Snapshot
- **Problem:** Staff need one secure back-end API to manage products, customers, and orders; customers need safe self-service access to their own data, including placing their own orders.
- **Users:** Admins (staff) and Customers. Phase 1 was API only. Phase 2 (from 2026-10-30) adds a React + TypeScript web front end: Customer portal first, then an Admin back office. API source code stays unchanged; only configuration (CORS, compose) may change.
- **Client / sponsor:** internal proof of concept
- **Team, budget, deadline:** one developer working with Claude; no fixed deadline; delivered in slices.
- **Stage:** Proof of concept built with production-grade practices.
- **Constraints (stack):** .NET 10 Web API, FastEndpoints, Clean Architecture + Vertical Slice + CQRS, FluentValidation, PostgreSQL 18 in Docker, Valkey 9.1 (Redis-compatible cache), EF Core (writes, CRUD, migrations), Dapper (views, functions, procedures), ASP.NET Core Identity, JWT bearer with role- and resource-based authorization, resilience (retry, timeout, circuit breaker), ASP.NET Core rate limiting. Local Docker only. Versions in [stack-versions-2026-10-24.md](stack-versions-2026-10-24.md).

## Links
- **Tracker:** Azure DevOps Boards (chosen 2026-10-24); run `azdo-setup` before first push
- **Linear project:** not used yet
- **Azure DevOps:** org `codewithelmor`, project `Demo-App` (renamed from `Demo-API` on 2026-10-30 by the product owner; Agile process). MCP servers `ado-read` / `ado-write` added to this repo's local scope 2026-10-27. Board created 2026-10-27: 168 work items (+165 Phase 2 items on 2026-10-30) ([azdo-map.md](azdo-map.md)), https://dev.azure.com/codewithelmor/Demo-App/_boards/board/t/Demo-App%20Team/Stories (team renamed to `Demo-App Team`)
- **Figma files:** used for the front end (phase 2); file not created yet

## Phase status
| Phase | Status | Documents |
|---|---|---|
| 1. Concept | Done (2026-10-24) | [01-concept/concept.md](01-concept/concept.md) |
| 2. Requirements | Done (2026-10-24), pre-mortem done | [prd](02-requirements/prd.md), [endpoint list](02-requirements/endpoint-list.md), [permission matrix](02-requirements/permission-matrix.md), user stories: [auth & admin](02-requirements/user-stories/01-auth-and-admin-users.md), [products & customers](02-requirements/user-stories/02-products-and-customers.md), [orders, reports & platform](02-requirements/user-stories/03-orders-reports-and-platform.md), [pre-mortem](02-requirements/pre-mortem.md) |
| 3. Research | Skipped (API only, users and problem are clear) | |
| 4. UX design | Skipped (API only) | |
| 5. UI design | Skipped (API only, no Figma) | |
| 6. Architecture | Done (2026-10-27) | [architecture](04-architecture/architecture.md), [data model](04-architecture/data-model.md), [solution structure](04-architecture/solution-structure.md), [endpoint catalog](04-architecture/endpoint-catalog.md), [ADRs 0001-0013](04-architecture/adr/), [architecture review](04-architecture/architecture-review.md) |
| 7. Security | Done (2026-10-27) | [threat model](05-security/threat-model.md) |
| 8. Delivery | Done (2026-10-27) | [delivery plan](06-delivery/delivery-plan.md) |
| 9. Quality | Done (2026-10-27) | [test strategy](07-quality/test-strategy.md) |
| 10. Release | Not started (local Docker only; light) | |
| 11. Operations | Not started (light) | |
| **Phase 2: web front end** | | |
| 1. Concept addendum | Done (2026-10-30) | [concept.md, Phase 2 addendum](01-concept/concept.md) |
| 2. Requirements (FE) | Done (2026-10-30) | [prd-frontend](02-requirements/prd-frontend.md), [front-end stories](02-requirements/user-stories/04-frontend.md) (MVP 25 stories, R2 23 Admin stories, API gaps G1-G14) |
| 3. Research (FE) | Skipped (users and tasks known from Phase 1) | |
| 4. UX design (FE) | Done (2026-10-30) | [information architecture](03-design/ux/information-architecture.md), [user flows](03-design/ux/user-flows.md), [wireframes](03-design/ux/wireframes.md) |
| 5. UI design (FE) | Planned (2026-10-30); Figma work deferred to a board story (Figma not connected) | [design profile](03-design/design-profile.yaml) |
| 6. Architecture (FE) | Done (2026-10-30) | [ADR-0015](04-architecture/adr/0015-web-front-end-bff.md), [frontend-architecture](04-architecture/frontend-architecture.md) |
| 7. Security (FE) | Done (2026-10-30) | [threat model §10](05-security/threat-model.md) (T-37..T-55, NFR-F29..F43, FT-01..FT-20) |
| 9. Quality (FE) | Done (2026-10-30) | [test strategy, Phase 2](07-quality/test-strategy.md) |
| 8. Delivery (FE) | Done (2026-10-30) | [delivery plan §10](06-delivery/delivery-plan.md), [board preview Phase 2](06-delivery/azdo-preview-phase2.md) (165 items, 335 SP) |

| 10. Build (FE-S0a + FE-R1 Customer portal) | Built, not merged (2026-10-31) | `web/DemoApi.Bff` (97 tests), `web/spa` (232 tests, 40 e2e on the compose stack, initial JS 159.7 KB gzip of 200). Reviews: [BFF security](07-quality/bff-security-review-2026-10-30.md), [SPA security](07-quality/spa-security-review-2026-10-30.md), [SPA code](07-quality/spa-code-review-2026-10-30.md). Open: BFF L-5 (events/OTel), L-7 (step-up, before Admin UI), CI e2e accounts, Firefox/WebKit/360 px, G1 usability test, Web Vitals/k6, tag `api-v1.0` and Phase 1 merge (product owner) |

Planned order: Concept → Requirements → Architecture → Security → Quality → Delivery → Release (light) → Operations (light).

## Key decisions
| Date | Decision | Reason | Link |
|---|---|---|---|
| 2026-10-24 | Stack fixed as listed in Snapshot | Given by product owner | product-brainstorm-template-dotnet10-webapi.md |
| 2026-10-24 | Skip Research, UX design, UI design, Figma | API-only product | |
| 2026-10-24 | Standard mode | Production-grade practices, full threat model and authz tests | |
| 2026-10-24 | All business entities implement `IAuditable` (CreatedAt/By, LastModifiedAt/By) and `IDeletable` (IsDeleted, DeletedAt/By) | Product owner decision | |
| 2026-10-24 | Soft delete only; the app never deletes rows. EF Core interceptor turns Delete into Update; global query filters hide deleted rows | Product owner decision; keeps history | |
| 2026-10-24 | App database role has no DELETE privilege; architecture test bans `ExecuteDelete` | Enforce "never delete" in the database, not only in code | |
| 2026-10-24 | Unique indexes are partial (`WHERE is_deleted = false`) | Allow reuse of SKU/email after soft delete | |
| 2026-10-24 | Dapper views and functions filter `is_deleted = false` explicitly; covered by integration tests | Dapper bypasses EF query filters | |
| 2026-10-24 | Audit trail: EF Core SaveChanges interceptor writes `AuditLogs` (table, key, action, changed fields old/new as jsonb, user, time, trace id) in the same transaction | Knows the real user; testable; all writes go through EF Core | |
| 2026-10-24 | All dates `DateTimeOffset`, stored as `timestamptz`, always UTC | Npgsql writes only offset-0 values to `timestamptz` | |
| 2026-10-24 | `IDateTimeProvider` (our interface) backed by .NET `TimeProvider`; tests use `FakeTimeProvider`; returns UTC only | Testable time, built-in fake | |
| 2026-10-24 | Customer delete: soft delete customer, anonymize personal data (name, email, phone), soft delete + lock Identity user, revoke all refresh tokens; orders kept unchanged | Privacy (erasure) without losing order history | |
| 2026-10-24 | Admin-only restore endpoints for products and customers | Recover from mistakes | |
| 2026-10-24 | Customers can create their own orders (customer taken from token) and cancel own orders while Pending; Admins create/update any order | Customer role needs a real purpose | |
| 2026-10-24 | Orders have line items (`OrderItems`: product, quantity, unit price, line total) | Needed for totals, summary view, sales report | |
| 2026-10-24 | Valkey 9.1 (BSD-3) instead of Redis 8 | Redis 8 is RSALv2/SSPL/AGPLv3; Valkey is drop-in | stack-versions-2026-10-24.md |
| 2026-10-24 | CQRS with FastEndpoints command bus (no MediatR) | MIT, already in stack; MediatR is paid or reciprocal | |
| 2026-10-24 | Resilience via Microsoft.Extensions.Resilience (no direct Polly reference) | Avoid Polly fee on next major | |
| 2026-10-24 | OpenAPI via FastEndpoints.OpenApi + Scalar | FastEndpoints.Swagger deprecated after 2026-11-10 | |
| 2026-10-24 | Own JWT issuer (JwtBearer + Identity UserManager); refresh tokens stored in DB and rotated; no `MapIdentityApi` | `MapIdentityApi` tokens are not JWTs | |
| 2026-10-24 | Tests use Shouldly (or AwesomeAssertions), not FluentAssertions | FluentAssertions v8 non-commercial license | |
| 2026-10-24 | Exception to no-DELETE: app DB role may DELETE only on `asp_net_user_roles` (narrowed 2026-10-27; `asp_net_user_tokens` unused); every role removal is audited. ADR 0006 | Identity removes these rows; fighting it costs more | 02-requirements/prd.md |
| 2026-10-24 | Admin and Customer are separate accounts; role endpoints manage only `Admin`; create Admin user (EP-19) is in slice 2 | Clear separation; only way to get new Admins | 02-requirements/prd.md |
| 2026-10-24 | Customer delete blocked (409) while any order is Pending/Confirmed/Shipped; self-delete requires current password | Avoid orphaned open orders; stop token-theft account erasure | 02-requirements/prd.md |
| 2026-10-24 | ~~Customer restore in S2~~ → moved to stretch by pre-mortem (P2) | Special-case rules for a low-value feature | 02-requirements/prd.md section 17 |
| 2026-10-24 | Product catalog not public: anonymous read → 401 | Follow brief; revisit with web front end | 02-requirements/prd.md |
| 2026-10-24 | ID routes Admin only (Customer 403); Customers use `/customers/me/...` (others' data 404) | One place for ownership checks and BOLA tests | 02-requirements/permission-matrix.md |
| 2026-10-24 | Concurrency via ETag (from `xmin`) + `If-Match` on PUT (428 missing, 412 stale) | Standard HTTP, good for a template | 02-requirements/prd.md |
| 2026-10-24 | Register, forgot-password, resend-confirmation always 202 | Stop account enumeration | 02-requirements/prd.md |
| 2026-10-24 | Password 15-128 chars, no composition rules (NIST SP 800-63B-4); minimum length configurable | Current guidance for password-only accounts | 02-requirements/prd.md |
| 2026-10-24 | User-story review decisions D1-D16 (reset vs locks, reports keep deleted customers/products, self-delete is `POST /customers/me/erase`, identity audit scope, field limits, response codes, `ExecuteUpdate` banned, audit redaction exception) | Gaps found while writing 60 stories | 02-requirements/prd.md section 16 |
| 2026-10-24 | Pre-mortem decisions P1-P10: PII never stored in audit (append-only `AuditLogs`, replaces D12); customer restore to stretch; stock locked with `SELECT … FOR UPDATE`, one retry layer; compose runs as Development with generated dev secrets; reviews by fresh-context agents; CI on Azure Pipelines; slice time-boxes; `dotnet new` template extraction after S4; one real stored procedure; brief-required items kept | Pre-mortem by `devils-advocate` | [pre-mortem](02-requirements/pre-mortem.md), prd.md section 17 |
| 2026-10-27 | Architecture decisions A-D1..A-D17: Aspire dashboard opt-in; template keeps Auth, AdminUsers, Products; six projects; money limit → 400; email only on Identity user; currency on each order; retention plan before production; DB roles `demoapi_app`/`demoapi_migrator`; HS256 own JWT; HybridCache + Valkey with per-ID keys and list generation key; authn → rate limit → authz; one PG retry layer (EF execution strategy); Valkey 250 ms total timeout | Phase 6 with solution-architect, dotnet-architect, dotnet-data-specialist, dotnet-api-designer | [architecture.md section 10](04-architecture/architecture.md) |
| 2026-10-27 | Architecture review R-1..R-5: DB breaker is connection-level only; S1 split into S1a walking skeleton + S1b auth; `ITransactionRunner` in SharedKernel with `ChangeTracker.Clear()` per attempt and reuse revocation committed before 401; cuts (replay list, advisory lock, prodlike compose, extra S1 indexes, build-time OpenAPI export; Postman generated from OpenAPI); strip CI job at end of S2 | Devil's advocate review, verdict go with changes | [architecture-review.md](04-architecture/architecture-review.md) |
| 2026-10-27 | Security decisions S-D1..S-D8: per-request security-stamp check (R1); auto-lockout keeps sessions, change password on auth-strict; reset 1 h / confirm 24 h; 5 open Pending orders cap; all Admins read audit logs; strict refresh; equal-work + time floor against enumeration; localhost-only compose, Valkey password; supply-chain gates and Renovate; ADR 0014 | STRIDE threat model by security-privacy-reviewer | [threat model §9](05-security/threat-model.md) |
| 2026-10-27 | Quality decisions Q-D1..Q-D6: test pyramid (~200 unit, ~350 integration/API, ~40 serial, ~20 architecture rules); generated authz matrix from `EndpointDataSource`; tokens minted for non-auth tests; no CI retries, max 3 quarantined; coverage as warning; NFR-02 recorded, NFR-01 blocks; branch policy on `main`; k6 twice (end S1b, after S4) | Test strategy by qa-strategist | [test strategy §10](07-quality/test-strategy.md) |
| 2026-10-27 | Azure DevOps org `codewithelmor`, new project `Demo-API`; board first, then S1a build | Product owner choice ("Both, board first") | |
| 2026-10-27 | Delivery decisions DL-D1..DL-D6: Kanban, WIP 1; time-boxes S1a 16-18, S1b 24-28, S2 17-20, S3 13-15, S4 7-8, template + rel/ops 6-9 days (83-98 total); Agile process, Epic per slice, no iterations; Azure Repos; 168-item preview | delivery-planner | [delivery plan §9](06-delivery/delivery-plan.md) |
| 2026-10-30 | Phase 2: add a React + TypeScript web front end; no API source changes (configuration and compose changes allowed) | Product owner request | |
| 2026-10-30 | Sign-in through a small, separate ASP.NET Core BFF host (YARP); tokens stay on the server, the browser gets an HttpOnly cookie | The API returns refresh tokens in the JSON body; keeps tokens out of JavaScript without changing the API | |
| 2026-10-30 | Audience: Customer portal first, then Admin back office | Product owner choice | |
| 2026-10-30 | UI: Figma used; shadcn/ui + Tailwind | Product owner choice | |
| 2026-10-30 | Azure DevOps project renamed `Demo-API` → `Demo-App` | Product owner | azdo-map.md |
| 2026-10-30 | Front-end decisions FE-D1..FE-D5: sign in to browse (no anonymous catalog); API auth/refresh limits raised by config, real limits enforced per browser IP in the BFF; one SPA with role-gated `/admin`; sessions follow the API plus 30-min Admin idle timeout; API gaps go to an unscheduled "API v1.1 (deferred)" epic | requirements-analyst, product owner | [prd-frontend §14](02-requirements/prd-frontend.md) |
| 2026-10-30 | UX decisions UX-D1..D5: cart keeps price seen; cart survives session end; "Delete account" wording; Admin idle warning 2 min before; placeholder name "Orders Shop". Layout: top bar (Customer), sidebar (Admin), `/` switches by role, cart = checkout | ux-designer, product owner | [user flows](03-design/ux/user-flows.md) |
| 2026-10-30 | Architecture FA-D1..D5: Vite SPA served by ASP.NET Core BFF (YARP) on one origin; sessions in Valkey (encrypted); single-flight refresh, never retried or cancelled; `X-CSRF` + Origin check; HTTP :8081 + opt-in HTTPS profile for Safari; `web/` folder outside `src/`; `api-v1.0` tag + ApiFreeze stage; TypeScript 6.0.3; MPL-2.0 dev-only exception (axe) | react-architect, product owner | [ADR-0015](04-architecture/adr/0015-web-front-end-bff.md) |
| 2026-10-30 | Security S-D9..S-D13: direct API calls keep raised limits locally; Admin absolute session 12 h (amends FE-D4); R2 step-up (sign-in ≤ 15 min) for EP-16/EP-19; one Valkey for sessions and cache; Trusted Types report-only then enforced. ADR-0015 amendment 1 (origin list, session binding, conditional refresh save) | security-privacy-reviewer, product owner | [threat model §10](05-security/threat-model.md) |
| 2026-10-30 | Quality Q-D7..Q-D11: E2E on every PR (Chromium full + Firefox subset); BFF↔API via Playwright request project; E2E data through the API, email links from logs; no visual regression in R1; Web Vitals lab p75 at milestones | qa-strategist, product owner | [test strategy Phase 2](07-quality/test-strategy.md) |
| 2026-10-30 | UI design: Obra shadcn/ui Community kit (MIT), neutral base + blue placeholder primary, custom success/warning/info tokens, contrast fixes, Light/Dark only. Figma work runs later as a board story (Figma not connected) | product-designer (PLAN), product owner | [design profile](03-design/design-profile.yaml) |
| 2026-10-30 | Delivery DL-D7..DL-D11: finish Phase 1 first (merge, green pipeline, fix #169 OpenAPI metadata, tag `api-v1.0`); E9 skeleton + E10 hardening split; parallel Figma design lane (WIP 1 per lane); 0.25 day/pt, R2 go/no-go after R1; new `Web` area subtree, #164/#165/#167 moved under API v1.1. 7 Epics E9-E15, 165 items, 335 SP; R1 + RelOps 48-56 days | delivery-planner, product owner | [delivery plan §10](06-delivery/delivery-plan.md) |

## Open-question answers (section 9 of the brief)
| # | Question | Answer |
|---|---|---|
| 1 | Customers place / cancel own orders? | Yes: create own orders; cancel own order while Pending. Admins create/update any order |
| 2 | Order line items? | Yes, `OrderItems` entity |
| 3 | Customer create = anonymous registration? Admin can create? | Anonymous `POST /api/v1/auth/register` creates Identity user + Customer role + Customer record in one transaction. Admins can also create customers (`POST /api/v1/customers`); the customer sets a password through the reset-password link (logged by the fake email sender) |
| 4 | Customer delete | Soft delete + anonymize PII (audit rows never hold PII values); Identity user soft deleted, locked, tokens revoked; orders kept |
| 5 | Customers own record only; Admins all | Confirmed. Customers use `/me` routes; another customer's resource returns 404 (not 403). Admins read/change all |
| 6 | Product delete: soft? | Yes, soft delete; Admin restore |
| 7 | Stock levels; price saved at order time | Products have `StockQuantity`; decremented on order, restored on cancel; insufficient stock = 409. Unit price snapshot on each line item. Product rows locked with `SELECT … FOR UPDATE` during order changes; `xmin` used for ETags |
| 8 | Order statuses and who changes them | Pending → Confirmed → Shipped → Completed, plus Cancelled. Customer: cancel own order while Pending. Admin: all valid transitions, cancel up to Confirmed. No cancel after Shipped. Line items editable only while Pending. Transitions logged in `OrderStatusHistory` |
| 9 | Email confirmation before login | Required. Fake sender logs the confirmation link. Seeded Admin is pre-confirmed. Confirm + resend endpoints are in slice 1 (not stretch) |
| 10 | First Admin seed; Admins create Admins | Seeded at startup from configuration (user secrets / environment variables, never in source) only if no Admin exists. Admins can create Admins and assign/remove roles, but cannot remove their own Admin role or the last Admin |
| 11 | CQRS mediator choice | FastEndpoints command bus |
| 12 | Refresh token storage, rotation, lifetimes | Access token 15 min. Refresh token 7 days, rotated on every use, stored as SHA-256 hash. Reuse of a used token revokes the whole token family. All refresh tokens revoked on logout-all, password change/reset, Admin lock, role change, and account delete (automatic failed-login lockout keeps sessions, S-D2). Every request also checks the security stamp (S-D1) |
| 13 | Single tenant, one currency, UTC | Single tenant; one currency set in configuration (ISO 4217 code, default `USD`); money as `numeric(12,2)`; all dates UTC `timestamptz` |

## Risks noted
- FastEndpoints maintainer warned it may go "bugfix only" if sponsorship stays low.
- Xabaril health check packages are stale (9.0.0, 2024); prefer small custom health checks.
- "Never delete" means tables grow forever; retention plan agreed (A-D7), built before production.
- S1 spikes may change ADRs: FastEndpoints command-bus package, build-time OpenAPI export, HybridCache when Valkey fails, 503 within 5 s when PostgreSQL is down, Identity saves inside the audit transaction.
- Top pre-mortem risks: F1 scope too large for one developer (time-boxes, cut stretch first); F2 template never reused (extraction deliverable, named reuse target); F3 stock concurrency flakiness (row locks). Kill criteria in 02-requirements/pre-mortem.md.

## Assumptions
- Assumption: Email sending is faked (logged) in this phase.

## Concept outcome (Phase 1)
- **Purpose:** reusable reference template for future client APIs.
- **North Star:** % of endpoints meeting the Definition of Done (authz tests Admin/Customer/anonymous incl. BOLA, validation, ProblemDetails, OpenAPI, `.http` entry, audit on writes). Target 100% per slice.
- **Stop point:** 4 slices - (1) auth + products + cache, (2) customers + own-data rules, (3) orders + line items + statuses + stock, (4) Dapper views and reports. Confirm email in slice 1; forgot/reset password in slice 2 (Admin-created customers need it). Stretch: extra Admin user management beyond list, get, lock/unlock, and role assignment.
- **API shape:** routes under `/api/v1`; CORS allow-list from configuration; no client-credentials flow this phase.

## Open questions
- [ ] Client project's security owner and compliance needs (GDPR, ISO 27001) - with the named client project
- [ ] Name a client project that will reuse the template - product owner, before S3 (pre-mortem F2)
- Section 9 answers were chosen by Claude on the product owner's instruction ("use whichever is best"); the product owner may override any of them.

## Next step
Phase 2 (web front end) is planned and its board entries are applied (2026-10-30): 7 Epics #173-#179, 26 Features #180-#205, 132 Stories #206-#337 (335 SP), 161 predecessor links, and #164/#165/#167 moved under F15.1 (#205). Product owner, on the board: add a Design swimlane, set In progress to WIP 2, optional card styles, and save the 4 shared queries (preview file). Then start P2-001 (#206: finish Phase 1, merge via the branch policy, fix Bug #169, tag `api-v1.0`), then FE-S0a. Figma work is board story P2-047.

Earlier next-step text (Phase 1): Board done (168 items). Start S1a with `build-app-builder` (E1, work item #1); commits use `AB#<id>`.
