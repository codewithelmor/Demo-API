# Pre-mortem: Orders API (requirements, before Architecture)

Date: 2026-10-24 | Reviewer: devils-advocate (files only) | Status: Reviewed; decisions P1-P10 recorded in prd.md section 17
Scenario: It is 2027-04-24. The POC was abandoned, or it was finished but no one used it as a template.
Inputs: `00-summary.md`, `01-concept/concept.md`, `02-requirements/prd.md` (incl. D1-D16), `endpoint-list.md`, `permission-matrix.md`, the 3 user-story files, `stack-versions-2026-10-24.md`.

**Verdict: go with changes.** The requirements are thorough and consistent, but sized for a small team, not one developer. Several rules add special cases to the patterns the template should show (no-DELETE, append-only audit, ETag).

## 1. Failure reasons

| ID | Category | Why it failed | L | I | Early warning sign | Mitigation (slice) |
|---|---|---|---|---|---|---|
| F1 | Scope / time | S2 never finished. 52 endpoints × 9-item DoD is ~500 test cases and 150+ doc artifacts for one person. Motivation faded before orders. | H | H | S1 takes over 2× its time-box, or more than 3 endpoints are "mostly done" | Time-box each slice with a hard cut rule (stretch first). All slices |
| F2 | Value | Finished but never reused. Patterns were tangled with Orders code; no `dotnet new` template or strip guide. | H | H | No named next client project by end of S2 | Template-extraction deliverable (BuildingBlocks with no domain references + `dotnet new` template or strip guide); metric "hours to bootstrap the next API". S1 structure, S4 extraction |
| F3 | Technical | `xmin` retries inside a multi-row order transaction clashed with EF's retrying execution strategy and the resilience pipeline (retries on retries). The 20-parallel test was flaky. | M | H | Concurrency test passes alone, fails in the suite | Pessimistic `SELECT … FOR UPDATE` on products in ID order; one retry layer only; `xmin` for ETags. ADR. S3 |
| F4 | Privacy | Erasure check failed: free-text fields and logs held PII; audit redaction needed UPDATE on `AuditLogs`, so "append-only audit" was false. | M | H | Redaction needs wider grants than planned | Never store PII values in audit (mark PII properties; write `"changed"`). Audit becomes INSERT/SELECT only. Scope erasure test to structured PII columns. S1, S2 |
| F5 | Technical | Audit/soft-delete interceptor fought ASP.NET Core Identity (UserManager saves on its own, lockout inside SignInManager, register transaction across contexts). | M | M | More than 2 workarounds in S1 auth | One `IdentityDbContext`, explicit transactions, week-1 spike (register + lockout + audit). S1 |
| F6 | Process | "Code review" and "security review" were self-graded with one developer. | H | M | PRs merged with no reviewer | Review = fresh-context agent (`dotnet-code-reviewer`, `dotnet-security-reviewer`) + checklist; automated DoD architecture test. S1 |
| F7 | Stack | FastEndpoints went bugfix-only or 9.0 broke APIs; .NET 11 made the template look dated. | M | M | No FastEndpoints release for 3+ months | Plain handler classes behind endpoints; ADR per stack choice with exit path |
| F8 | Ops | "Fresh clone, 0 manual steps" failed: secrets only from user-secrets/env, HTTPS-only outside Development. | H | M | First fresh-clone test needs a README step | Compose runs as Development; `.env.example`; init step generates dev-only secrets; HTTPS/HSTS tested in a production-like profile. S1 |
| F9 | Quality | Suite over 5 min and flaky; rate-limit tests depended on real time. | M | M | Suite over 3 min at end of S1 | Table-driven authz tests from the permission matrix; shared containers + Respawn; 1-2 s limiter windows in tests (built-in limiters have no `TimeProvider`). S1 |
| F10 | Technical | Product cache gave little gain; list invalidation became the most complex S1 code. | M | L | Hit ratio under 50% | Generation counter for list keys; per-key get-by-ID; spike HybridCache failure handling. S1 |
| F11 | Scope | Special-case rules spread: restored-customer lock type, D14 email-change exception, Identity DELETE exception, D12 audit redaction. | H | M | More than two "exception to…" ADRs | Cut customer restore; PII-free audit |
| F12 | Tooling | Secret scan and licence check never enforced because no CI exists. | M | L | No pipeline file by end of S1 | Choose CI (Azure Pipelines / GitHub Actions / pre-commit). S1 |

### Kill or pivot criteria
- S1 not at 100% DoD after 2× its time-box → cut stretch and reduce S4 to one view + one function.
- No client project named by end of S2 → extract the template from S1-S2; S3-S4 optional.
- FastEndpoints announces bugfix-only before S3 → write the exit ADR; no new FastEndpoints-specific features.
- Test suite over 5 min at the end of a slice → no new endpoints until it is back under.

## 2. Over-engineering check (reviewer's view)

| Item | Call | Reason |
|---|---|---|
| Customer restore + D14 + restored-customer lock type | Cut / stretch | Restores an anonymized, locked shell; many special rules; product restore already shows the pattern |
| Audit redaction on erase (D12) | Simplify | PII never stored in audit; same privacy, append-only audit |
| Assign/remove Admin role + last-Admin + DELETE grant | Simplify | Demote = lock; removes the only no-DELETE exception |
| List roles | Cut | Two fixed roles |
| Admin replace order items (EP-41) | Move to stretch | Hardest S3 logic; cancel and re-create covers it |
| ETag + If-Match on every PUT | Keep | Cheap with `xmin`; good template pattern |
| Postman in DoD | Simplify | OpenAPI + `.http` already describe the contract |
| Reports (6) | Simplify | Template needs 1 view + 1 paged function |
| Sales `groupBy=week` + edge cutting | Simplify | Keep day/month |
| Circuit breaker on PostgreSQL | Cut | Single local instance; keep for Valkey |
| `OrderStatusHistory` + audit rows for it | Simplify | History is already an audit trail |
| Rate limiting, headers, CORS, ProblemDetails, paging, health | Keep | Core of a template |
| Load tests per slice | Simplify | Once in S1, once at the end |

## 3. Points the architect must decide (ADR topics)
1. Compose environment and dev-secret generation (rebuild vs secrets vs HTTPS).
2. Order aggregate concurrency token: always touch the order row so the ETag changes on item changes.
3. Stored procedures: the stack promises Dapper for procedures, but the PRD has none.
4. Clean Architecture vs Vertical Slice layout: which one decides project structure ("easy to strip" as main criterion).
5. Stock concurrency vs D11 (`ExecuteUpdate` ban) vs NFR-03 retries: one retry layer only.
6. Erasure scope: free-text fields and logs.
7. Rate-limit tests: built-in limiters have no `TimeProvider`; US-292 AC1 must include `erase` (D16).
8. Reviewer for "code review" / "security review"; CI platform for NFR-07 and NFR-18.

## 4. Reviewer's simplest scope (~30 endpoints)
S1 (19): auth core + products CRUD/restore + health. S2 (12): forgot/reset, admin users list/get/lock/unlock/create-Admin, customers CRUD + erase, `/me` get/put/erase. S3 (8-9): order create/list/get/status/cancel for Admin and `/me`. S4 (2-3): order summary view, sales function, optional order-history function. Plus a template-extraction step.

## 5. Advice (stack is fixed)
- FastEndpoints: business logic in plain handlers, command bus as a thin dispatch layer.
- Clean Architecture + VSA + CQRS: prefer feature folders with a thin shared kernel; CQRS = separate read/write handlers on one database.
- Microsoft.Extensions.Resilience on DB duplicates EF Core's execution strategy.
- Own JWT issuer is the highest-risk code: token tests first, fresh-context security review.
- .NET 10 LTS is right; plan a .NET 11 compatibility check.

Sources: [FixedWindowRateLimiterOptions (no TimeProvider)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting.fixedwindowratelimiteroptions?view=net-10.0)
