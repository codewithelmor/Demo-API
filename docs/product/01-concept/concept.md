# Concept: Orders API (working name)

Date: 2026-10-24 | Version: 1 | Status: Approved (product owner delegated choices, 2026-10-24)
Stage: internal proof of concept, production-grade practices | Mode: Standard

## 1. Problem statement
Staff (Admins) need one secure back-end API to manage products, customers and orders, because there is no shared, tested service for this today. Customers need safe self-service access to their own data, including placing and cancelling their own orders.
For the organization, the real problem is different: there is no proven, reusable reference for building a secure .NET 10 API. Each client API today starts from scratch. That costs setup time and repeats security mistakes such as broken object-level authorization (BOLA), weak token handling and untestable data access.

Assumption: the main value of this POC is the reusable reference, not the order domain itself.

## 2. Users
| Role | Goal | Main pain | Access |
|---|---|---|---|
| Admin (staff) | Keep the catalog, customers and orders correct | Mistakes are hard to undo. No history of who changed what | All data. Restore of soft-deleted products and customers |
| Customer | Buy products and track own orders without calling staff | No self-service. Worry about privacy | Own profile and own orders only |
| API consumers (secondary) | Future front ends, partner integrations, and the developer reusing the template | Unclear contracts and auth flows | Through OpenAPI, `.http` files and Postman |

Assumption: customers who ask for another customer's resource get 404 (not 403). This hides that the resource exists.

## 3. Jobs to be done
**Admin**
- When a product is added or its price changes, I want to update the catalog once, so customers see correct data right away.
- When an order arrives, I want to move it through its statuses, so I know what to do next.
- When something is deleted by mistake, I want to restore it and see who changed it, so I can recover without a database admin.

**Customer**
- When I want to buy, I want to register and place an order myself, so I don't have to contact staff.
- When I change my mind, I want to cancel my order while it is still Pending, so I'm not charged for something I don't want.
- When I leave, I want my personal data removed, so I can trust the service.

**API consumer / developer**
- When I start a new client API, I want a working, tested reference I can copy, so I can skip weeks of setup and known security mistakes.

## 4. Goals for the POC
1. Every endpoint enforces role-based and own-data rules, and tests prove it.
2. The whole system can be rebuilt from an empty machine with `docker compose up` plus migrations (schema, views, functions, seed Admin).
3. Product reads are fast with the cache and still work, more slowly, when Valkey is down.
4. Every write is audited and nothing is hard deleted.
5. The code is clean enough to copy as a template for the next client API.

## 5. Success metrics
**North Star: % of endpoints that meet the Definition of Done** (authorization tests for Admin, Customer and anonymous; validation; ProblemDetails; OpenAPI description; `.http` entry; audit on writes).
Baseline: 0% (new build). Target: 100% at the end of each slice.

| Supporting metric | Baseline | Target |
|---|---|---|
| Cross-customer access tests (BOLA) passing | unknown (none written) | 100%. Every customer-owned endpoint has a "different customer → 404" test |
| Clean rebuild: fresh clone to working API with seed Admin | unknown | One command plus migrations, under 15 min, no manual steps |
| p95 latency for GET product by id and product list (local, warm) | unknown | Cached ≤ 20 ms. Uncached ≤ 100 ms. Valkey down: no 5xx, ≤ 300 ms |
| Writes that produce an audit row | unknown | 100%, checked by integration tests |
| Full test suite run time (with Testcontainers) | unknown | ≤ 5 min locally |

Assumption: the latency targets are indicative local numbers from a short load test, not an SLA.

## 6. Non-goals (this phase)
- Front-end UI.
- Payments, shipping, invoices, tax.
- Real email sending (fake sender that logs only).
- Cloud deployment, high availability, multi-region.
- Multi-tenancy and multi-currency (pending confirmation).
- Making the stack generic (for example supporting other databases).
- Partner onboarding, API keys or client-credentials flows.

## 7. Riskiest assumptions
| Assumption | Type | Importance / evidence | Cheap test |
|---|---|---|---|
| The finished POC will be reused as a template for client APIs | Viability | High / Low | Before slice 3, list 1-2 upcoming client projects that would use it. If none, cut scope |
| One developer with Claude can deliver all cross-cutting concerns without stalling | Feasibility | High / Low | Time-box slice 1 (auth + products + cache). If it takes far longer than planned, trim the stretch items |
| Own JWT issuing with rotating refresh tokens can be made safe | Feasibility | High / Medium | Write reuse-detection, revocation-on-logout and revocation-on-password-change tests first, in slice 1 |
| Soft delete + anonymization satisfies erasure while the audit log keeps old values | Viability (privacy) | High / Low | Integration test: after a customer is deleted, search all tables (including `AuditLogs`) for their email. Expect 0 hits |
| Stock decrement with optimistic concurrency stops overselling under parallel orders | Feasibility | Medium / Low | Concurrency integration test: 20 parallel orders for stock 5. Exactly 5 succeed |
| Caching product reads gives a measurable gain locally | Desirability (of the feature) | Low / Low | Measure p95 with and without cache in slice 1 and record the result |

Confirmed rules (2026-10-24): anonymous registration creates the Identity user and the Customer in one transaction, and Admins can also create customers. Products have stock, decremented on order and restored on cancel; line items keep a price snapshot. Order statuses are Pending → Confirmed → Shipped → Completed, plus Cancelled.

## 8. Lean Canvas (adapted for an internal POC)
For an internal POC, "revenue" means value to the organization and learning value. "Channels" means who consumes the API. "Unfair advantage" means the reusable reference. No market data is used.

| Box | Content |
|---|---|
| **1. Problem** | No proven, reusable secure .NET API reference. Every client API is rebuilt from scratch. Common risks: BOLA, weak tokens, untestable data access |
| **Existing alternatives** | Do nothing (start each project fresh). Copy pieces from old client projects. Public sample repos. Off-the-shelf commerce back ends (good for selling, wrong for owning the architecture) |
| **2. Customer segments** | Main: the developer and future client projects. Inside the domain: Admins and Customers |
| **Early adopters** | The developer's next internal client API project |
| **3. Unique value proposition** | A secure, fully tested .NET 10 API you can rebuild with one command and copy for the next client |
| **High-level concept** | "A production-grade starter kit, proven on a real order domain" |
| **4. Solution** | Role- and own-data authorization with tests on every endpoint. Audited soft-delete data model with a clean rebuild. Cached reads that degrade gracefully, plus rate limiting and observability |
| **5. Channels** | Consumers: future web front end, partner integrations, the developer reusing the code. Reached through OpenAPI + Scalar, `.http` files, Postman collection, README |
| **6. Revenue streams (value)** | Setup time saved on each new client API (unknown; measure on the next project). Fewer security defects. Proven patterns to show clients |
| **7. Cost structure** | Developer time (main cost). Claude usage. Local Docker only, no hosting. All licenses permissive (MediatR, FluentAssertions, direct Polly and Redis 8 avoided) |
| **8. Key metrics** | North Star: % of endpoints meeting the Definition of Done. Supporting: BOLA test pass rate, clean-rebuild time, p95 product reads with and without cache |
| **9. Unfair advantage** | A reference architecture tested and documented against the developer's own client patterns. ADRs explain every choice |

## 9. Decisions made in this phase (2026-10-24)
- **Purpose:** the POC is a reusable reference template for future client APIs.
- **Sign-off and stop point:** the product owner signs off against the written Definition of Done. The POC is done after 4 slices: (1) auth + products + cache, (2) customers + own-data rules, (3) orders + line items + statuses + stock, (4) Dapper views and reports. Email confirmation (confirm + resend) is in slice 1 because login requires a confirmed email. Forgot/reset password is in slice 2 because Admin-created customers set their password through it. Stretch: extra Admin user management beyond list, get, lock/unlock, and role assignment.
- **Audit log and erasure:** audit rows never store personal-data values (they record "changed", P1), so erasure needs no audit change. Integration test: search every table for the deleted customer's email and expect 0 hits.
- **Future consumers:** expect one web front end and one machine-to-machine client later. Routes use `/api/v1`; CORS allow-list from configuration. No client-credentials flow this phase.
- **Build vs. buy:** build. Off-the-shelf commerce back ends solve selling, not owning and proving the architecture.

## 10. Open questions
- None blocking. Domain rules confirmed in `00-summary.md` (section 9 answers).
