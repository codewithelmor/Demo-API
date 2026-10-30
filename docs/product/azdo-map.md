# Azure DevOps map

Organization: codewithelmor | Project: Demo-App | Team: Demo-App Team | Project ID: d77f3f29-8ff8-4271-ab4d-f6f1e8ca7d49 (private) | Last synced: 2026-10-30
Board: https://dev.azure.com/codewithelmor/Demo-App/_boards/board/t/Demo-App%20Team/Stories
Process: Agile
Renamed from `Demo-API` (project and team) on 2026-10-30; old-name URLs no longer resolve. GUID URLs (`https://dev.azure.com/codewithelmor/d77f3f29-8ff8-4271-ab4d-f6f1e8ca7d49/_workitems/edit/<id>`) survive future renames.

Status: **168 work items created 2026-10-27** (8 Epics, 25 Features, 135 User Stories; 449 Story Points; 160 parent links, 31 dependency links). Plan: [06-delivery/azdo-preview.md](06-delivery/azdo-preview.md).
Phase 2 status: **Phase 2 board apply complete 2026-10-30** (plan: [06-delivery/azdo-preview-phase2.md](06-delivery/azdo-preview-phase2.md)). Items created: 165 (7 Epics #173-#179, 26 Features #180-#205, 132 Stories P2-001..P2-132 = #206-#337; 335 Story Points on the stories), 158 parent links, 161 predecessor links (Dependency-Forward on the predecessor), 2 Related links (#206 with #169 and #170). Tag count: 253 expected (167 + 86 new); not read back, because no MCP tool lists tags.
Re-parented 2026-10-30 (existing items, rev 2): #164 (P-131, G8), #165 (P-132, G10) and #167 (P-134, G9) moved from #33 to F15.1 (#205); `Stretch` removed, tags `API-v1.1` and `gap-G8` / `gap-G10` / `gap-G9` added.

## Work item types used
| Planning item | Work item type | Estimate field | Acceptance criteria field | Done states |
|---|---|---|---|---|
| Slice (E1-E8) | Epic | – (Effort not set) | – (in Description) | Closed |
| Feature (F-x.y) | Feature | – (Effort not set) | – (in Description) | Closed |
| P-xxx (stories, enablers, spikes, tests) | User Story | Story Points (`Microsoft.VSTS.Scheduling.StoryPoints`), XS=1 S=2 M=4 L=8 | `Microsoft.VSTS.Common.AcceptanceCriteria` | Closed |
| Flaky test / defect | Bug (managed with requirements) | – | Repro Steps | Closed |

Required fields found: Title, State (default New), Value Area (default Business); Priority is required on Epic (default 2).

## Area paths and iterations
| Kind | Path | Dates | Created by plan |
|---|---|---|---|
| Area (team default, include sub areas on) | Demo-App | – | no |
| Area | Demo-App\Platform | – | no (product owner, UI) |
| Area | Demo-App\Auth | – | no (product owner, UI) |
| Area | Demo-App\AdminUsers | – | no (product owner, UI) |
| Area | Demo-App\Products | – | no (product owner, UI) |
| Area | Demo-App\Customers | – | no (product owner, UI) |
| Area | Demo-App\Orders | – | no (product owner, UI) |
| Area | Demo-App\Reports | – | no (product owner, UI) |
| Area | Demo-App\Template | – | no (product owner, UI) |
| Area | Demo-App\Delivery | – | no (product owner, UI) |
| Area | Demo-App\Web | – | no (product owner, UI) |
| Area | Demo-App\Web\Bff | – | no (product owner, UI) |
| Area | Demo-App\Web\Customer | – | no (product owner, UI) |
| Area | Demo-App\Web\Admin | – | no (product owner, UI) |
| Area | Demo-App\Web\Design | – | no (product owner, UI) |
| Iteration (backlog + default) | Demo-App | – | no |
| Iteration (unused, Kanban) | Demo-App\Iteration 1, 2, 3 | none | no |

Team settings verified 2026-10-27 and re-verified 2026-10-30 (unchanged): Epics backlog visible, bugs managed with requirements, default iteration = root, include sub areas = on.

## Tags
| Tag | Meaning | Created by plan |
|---|---|---|
| 167 tags in use (2026-10-30; `review-finding` added with #169/#170) | Approved scheme: slice `S1a`, `S1b`, `S2`, `S3`, `S4`, `Template`, `RelOps`, `Stretch` (live case); kind tags; `template-strip` (P-086); `needs-ac` (75 stories without ACs); trace `US-xxx`, `EP-xx`, `ST-xx`, `FR-xxx` | yes (created on first use) |

## Work items
| Planning item (doc#anchor) | Type | ID | URL | Parent ID | Created by plan | Last synced |
|---|---|---|---|---|---|---|
| E1 — S1a Walking skeleton | Epic | 1 | [#1](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/1) | – | yes (permission test, then updated) | 2026-10-27 |
| E2 — S1b Auth and product CRUD | Epic | 2 | [#2](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/2) | – | yes | 2026-10-27 |
| E3 — S2 Customers, own-data rules and user admin | Epic | 3 | [#3](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/3) | – | yes | 2026-10-27 |
| E4 — S3 Orders, stock and statuses | Epic | 4 | [#4](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/4) | – | yes | 2026-10-27 |
| E5 — S4 Reports and Dapper reads | Epic | 5 | [#5](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/5) | – | yes | 2026-10-27 |
| E6 — Template extraction (P7) | Epic | 6 | [#6](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/6) | – | yes | 2026-10-27 |
| E7 — Release and operations (light) | Epic | 7 | [#7](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/7) | – | yes | 2026-10-27 |
| E8 — Stretch (cut first) | Epic | 8 | [#8](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/8) | – | yes | 2026-10-27 |
| F1.1 — Repo, solution and local run | Feature | 9 | [#9](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/9) | 1 | yes | 2026-10-27 |
| F1.2 — CI pipeline and supply-chain gates | Feature | 10 | [#10](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/10) | 1 | yes | 2026-10-27 |
| F1.3 — Test and architecture foundations | Feature | 11 | [#11](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/11) | 1 | yes | 2026-10-27 |
| F1.4 — Product create/get and health end to end | Feature | 12 | [#12](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/12) | 1 | yes | 2026-10-27 |
| F2.1 — Token and identity foundations | Feature | 13 | [#13](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/13) | 2 | yes | 2026-10-27 |
| F2.2 — Auth endpoints | Feature | 14 | [#14](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/14) | 2 | yes | 2026-10-27 |
| F2.3 — Product CRUD completion | Feature | 15 | [#15](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/15) | 2 | yes | 2026-10-27 |
| F2.4 — Platform conventions and observability | Feature | 16 | [#16](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/16) | 2 | yes | 2026-10-27 |
| F2.5 — DoD meta-tests, API files and performance run 1 | Feature | 17 | [#17](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/17) | 2 | yes | 2026-10-27 |
| F2.6 — Security tests S1b | Feature | 18 | [#18](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/18) | 2 | yes | 2026-10-27 |
| F3.1 — Password recovery | Feature | 19 | [#19](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/19) | 3 | yes | 2026-10-27 |
| F3.2 — Admin user and role management | Feature | 20 | [#20](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/20) | 3 | yes | 2026-10-27 |
| F3.3 — Customers (Admin) | Feature | 21 | [#21](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/21) | 3 | yes | 2026-10-27 |
| F3.4 — Customer self-service (/me) | Feature | 22 | [#22](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/22) | 3 | yes | 2026-10-27 |
| F3.5 — Template strip check and pipeline upkeep | Feature | 23 | [#23](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/23) | 3 | yes | 2026-10-27 |
| F3.6 — Security tests S2 | Feature | 24 | [#24](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/24) | 3 | yes | 2026-10-27 |
| F4.1 — Order domain and stock locking | Feature | 25 | [#25](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/25) | 4 | yes | 2026-10-27 |
| F4.2 — Orders (Admin) | Feature | 26 | [#26](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/26) | 4 | yes | 2026-10-27 |
| F4.3 — Orders self-service (/me) | Feature | 27 | [#27](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/27) | 4 | yes | 2026-10-27 |
| F4.4 — Order concurrency, load data and security tests | Feature | 28 | [#28](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/28) | 4 | yes | 2026-10-27 |
| F5.1 — Reports and audit-log query | Feature | 29 | [#29](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/29) | 5 | yes | 2026-10-27 |
| F5.2 — Final quality gates | Feature | 30 | [#30](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/30) | 5 | yes | 2026-10-27 |
| F6.1 — dotnet new template and start guide | Feature | 31 | [#31](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/31) | 6 | yes | 2026-10-27 |
| F7.1 — Release and operations notes | Feature | 32 | [#32](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/32) | 7 | yes | 2026-10-27 |
| F8.1 — Stretch stories (cut first) | Feature | 33 | [#33](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/33) | 8 | yes | 2026-10-27 |
| P-001 — Enabler: Solution skeleton and root files (slnx, global.json, Directory.*.props, nuget.config source mapping, BannedSymbols, .gitattributes); verify flagged package versions | User Story | 34 | [#34](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/34) | 9 | yes | 2026-10-27 |
| P-002 — Enabler: Compose chain secrets-init → postgres → migrator → api, Dockerfile api and migrator targets | User Story | 35 | [#35](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/35) | 9 | yes | 2026-10-27 |
| P-003 — Security follow-up: Compose hardening (localhost-only ports, Valkey ACL password, per-service secret mounts 0440, generated POSTGRES_PASSWORD, log rotation, dp-keys volume) | User Story | 36 | [#36](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/36) | 9 | yes | 2026-10-27 |
| P-004 — Security follow-up: DB roles, grants and timeouts (demoapi_migrator/demoapi_app, DELETE only on asp_net_user_roles, append-only audit_logs, statement/lock/idle timeouts, audit action CHECK incl. SessionsRevoked) | User Story | 37 | [#37](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/37) | 9 | yes | 2026-10-27 |
| P-005 — Docs: README quick start and CLAUDE.md (first version) | User Story | 38 | [#38](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/38) | 9 | yes | 2026-10-27 |
| P-006 — Docs: Requirements cleanup before build (C7, C9) | User Story | 39 | [#39](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/39) | 9 | yes | 2026-10-27 |
| P-007 — Spike SP-1: Compose chain on a fresh Windows clone (time-box 1 day) | User Story | 40 | [#40](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/40) | 9 | yes | 2026-10-27 |
| P-008 — Spike SP-4: EFCore.NamingConventions on EF Core 10, fallback own convention (time-box 0.5 day) | User Story | 41 | [#41](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/41) | 9 | yes | 2026-10-27 |
| P-009 — CI: Azure Pipelines Build and Test stages (locked restore, format check, warnings as errors, TRX, coverage and time-budget warnings) | User Story | 42 | [#42](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/42) | 10 | yes | 2026-10-27 |
| P-010 — CI: Supply-chain gates (gitleaks full history, nuget-license, vulnerable packages, CycloneDX SBOM, Trivy image scan, digest pins, pinned ubuntu-24.04) | User Story | 43 | [#43](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/43) | 10 | yes | 2026-10-27 |
| P-011 — CI: Branch policy on main (PR + green build, no direct push) and work-item linking | User Story | 44 | [#44](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/44) | 10 | yes | 2026-10-27 |
| P-012 — Test infra: Test projects and shared fixture (Testcontainers PostgreSQL 18.6 + Valkey 9.1.2, role script, migrator, Respawn, app role, ApiFactory, TestUsers, log capture, trait attributes) | User Story | 45 | [#45](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/45) | 11 | yes | 2026-10-27 |
| P-013 — Test infra: Architecture rules A1-A5, A10, A11 and BannedApiAnalyzers (each seen failing once) | User Story | 46 | [#46](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/46) | 11 | yes | 2026-10-27 |
| P-014 — Test infra: OpenAPI setup (Scalar in Development only), committed openapi/v1.json and drift test | User Story | 47 | [#47](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/47) | 11 | yes | 2026-10-27 |
| P-015 — ST-01 Authz matrix skeleton from EndpointDataSource (EP-23, EP-24, EP-56, EP-57; fails on unlisted endpoints) | User Story | 48 | [#48](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/48) | 11 | yes | 2026-10-27 |
| P-016 — ST-12 DB grant snapshot and role attributes | User Story | 49 | [#49](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/49) | 11 | yes | 2026-10-27 |
| P-017 — Spike SP-5: Test-time checks (Testcontainers pause/unpause; global FakeTimeProvider vs resilience delays) (time-box 0.5 day) | User Story | 50 | [#50](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/50) | 11 | yes | 2026-10-27 |
| P-018 — US-291 Enabler: Soft delete, audit interceptor and no hard delete (AC10 completed in S1b) | User Story | 51 | [#51](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/51) | 12 | yes | 2026-10-27 |
| P-019 — Enabler: Resilience (single EF retry layer, ITransactionRunner with ChangeTracker.Clear, connection-level DB breaker, Valkey 250 ms; tests 503 < 5 s, 10 × 409 keep breaker closed, forced transient retry) | User Story | 52 | [#52](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/52) | 12 | yes | 2026-10-27 |
| P-020 — US-102 Create a product | User Story | 53 | [#53](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/53) | 12 | yes | 2026-10-27 |
| P-021 — US-101 Get a product (HybridCache + Valkey L2) | User Story | 54 | [#54](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/54) | 12 | yes | 2026-10-27 |
| P-022 — US-218 Liveness check | User Story | 55 | [#55](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/55) | 12 | yes | 2026-10-27 |
| P-023 — US-219 Readiness check | User Story | 56 | [#56](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/56) | 12 | yes | 2026-10-27 |
| P-024 — Spike SP-2: Identity + audit interceptor in one transaction (forced rollback, ChangeTracker.Clear retry) (time-box 1 day) | User Story | 57 | [#57](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/57) | 12 | yes | 2026-10-27 |
| P-025 — Spike SP-3: HybridCache with Valkey (L2) down (time-box 0.5 day) | User Story | 58 | [#58](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/58) | 12 | yes | 2026-10-27 |
| P-026 — Enabler: JWT issuer, key ring with kid and bearer validation (HS256 allow-list, typ at+jwt, iss/aud/exp, 30 s skew) | User Story | 59 | [#59](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/59) | 13 | yes | 2026-10-27 |
| P-027 — Enabler: Refresh-token store (hash, rotation, family, 30-day cap, reuse revocation committed before 401) and revoke_refresh_tokens procedure (P9) | User Story | 60 | [#60](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/60) | 13 | yes | 2026-10-27 |
| P-028 — Enabler: Per-request security-stamp check (sst claim, 30 s in-process cache, fails closed) | User Story | 61 | [#61](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/61) | 13 | yes | 2026-10-27 |
| P-029 — Enabler: Password policy (15-128, local blocklist, PBKDF2 220,000 iterations, rehash on login) | User Story | 62 | [#62](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/62) | 13 | yes | 2026-10-27 |
| P-030 — Enabler: Enumeration defenses (equal work, response floor, password checked before confirmed/locked) | User Story | 63 | [#63](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/63) | 13 | yes | 2026-10-27 |
| P-031 — Enabler: Seed Admin at startup from configuration (no user story exists) | User Story | 64 | [#64](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/64) | 13 | yes | 2026-10-27 |
| P-032 — Enabler: Fake email sender with configured link base URL | User Story | 65 | [#65](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/65) | 13 | yes | 2026-10-27 |
| P-033 — US-001 Register as a customer | User Story | 66 | [#66](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/66) | 14 | yes | 2026-10-27 |
| P-034 — US-002 Confirm email | User Story | 67 | [#67](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/67) | 14 | yes | 2026-10-27 |
| P-035 — US-003 Resend confirmation link | User Story | 68 | [#68](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/68) | 14 | yes | 2026-10-27 |
| P-036 — US-004 Log in | User Story | 69 | [#69](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/69) | 14 | yes | 2026-10-27 |
| P-037 — US-005 Refresh tokens with rotation and reuse detection | User Story | 70 | [#70](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/70) | 14 | yes | 2026-10-27 |
| P-038 — US-006 Log out one session | User Story | 71 | [#71](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/71) | 14 | yes | 2026-10-27 |
| P-039 — US-007 Log out all sessions | User Story | 72 | [#72](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/72) | 14 | yes | 2026-10-27 |
| P-040 — US-008 Get current user | User Story | 73 | [#73](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/73) | 14 | yes | 2026-10-27 |
| P-041 — US-009 Change own password | User Story | 74 | [#74](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/74) | 14 | yes | 2026-10-27 |
| P-042 — US-100 List products | User Story | 75 | [#75](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/75) | 15 | yes | 2026-10-27 |
| P-043 — US-103 Update a product | User Story | 76 | [#76](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/76) | 15 | yes | 2026-10-27 |
| P-044 — US-104 Soft delete a product | User Story | 77 | [#77](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/77) | 15 | yes | 2026-10-27 |
| P-045 — US-105 Restore a product | User Story | 78 | [#78](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/78) | 15 | yes | 2026-10-27 |
| P-046 — US-290 Enabler: Paging, sorting, filtering and ProblemDetails convention | User Story | 79 | [#79](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/79) | 16 | yes | 2026-10-27 |
| P-047 — US-292 Enabler: Rate limiting, security headers and CORS (reports concurrency checked in S4) | User Story | 80 | [#80](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/80) | 16 | yes | 2026-10-27 |
| P-048 — Enabler: Observability (OTel traces and metrics, logs with traceId, email masking, query strings out of logs, security-event IDs, Aspire dashboard profile) | User Story | 81 | [#81](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/81) | 16 | yes | 2026-10-27 |
| P-049 — Test infra: Architecture rules A6-A9 incl. EndpointDoDTests | User Story | 82 | [#82](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/82) | 17 | yes | 2026-10-27 |
| P-050 — Test infra: AuditCoverageTests, EndpointPolicyTests and ApiFilesCoverageTests | User Story | 83 | [#83](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/83) | 17 | yes | 2026-10-27 |
| P-051 — Test infra: Traceability report (AC IDs vs test tags, CI artifact, 0 uncovered Must ACs per slice) | User Story | 84 | [#84](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/84) | 17 | yes | 2026-10-27 |
| P-052 — API files: .http structure per feature and Postman collection generated from OpenAPI (environments without secrets) | User Story | 85 | [#85](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/85) | 17 | yes | 2026-10-27 |
| P-053 — Perf: k6 run 1 (NFR-01 cached, uncached, Valkey down) recorded in 07-quality/perf-results.md | User Story | 86 | [#86](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/86) | 17 | yes | 2026-10-27 |
| P-054 — Review gate: Fresh-context security review of JwtTokenIssuer and RefreshTokenStore (no open High) | User Story | 87 | [#87](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/87) | 17 | yes | 2026-10-27 |
| P-055 — ST-04 JWT negative tests | User Story | 88 | [#88](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/88) | 18 | yes | 2026-10-27 |
| P-056 — ST-05 Refresh tests (rotation, reuse commits before 401, parallel race, 30-day cap) | User Story | 89 | [#89](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/89) | 18 | yes | 2026-10-27 |
| P-057 — ST-06 Security-stamp tests (password change, logout-all) | User Story | 90 | [#90](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/90) | 18 | yes | 2026-10-27 |
| P-058 — ST-07 Lockout tests (5 failures, EP-09 counts, automatic lockout keeps sessions) | User Story | 91 | [#91](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/91) | 18 | yes | 2026-10-27 |
| P-059 — ST-08 Enumeration tests (same status, body, headers; timing floor; 10 calls per path) | User Story | 92 | [#92](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/92) | 18 | yes | 2026-10-27 |
| P-060 — ST-09 Rate-limit tests (each policy, shared auth-strict bucket, 429 before 401, X-Forwarded-For ignored; verify TestServer client IP) | User Story | 93 | [#93](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/93) | 18 | yes | 2026-10-27 |
| P-061 — ST-10 Production-environment tests (headers, HSTS, CORS, /scalar and /openapi 404, no stack trace) | User Story | 94 | [#94](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/94) | 18 | yes | 2026-10-27 |
| P-062 — ST-11 Injection tests for products sort and search | User Story | 95 | [#95](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/95) | 18 | yes | 2026-10-27 |
| P-063 — ST-14 Log hygiene tests (no password, JWT, refresh token, unmasked email) | User Story | 96 | [#96](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/96) | 18 | yes | 2026-10-27 |
| P-064 — ST-15 Cache tests (scope by role, includeDeleted bypass, search lists not cached) | User Story | 97 | [#97](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/97) | 18 | yes | 2026-10-27 |
| P-065 — ST-16 Limits tests (413/415, pageSize, 1024-char password) | User Story | 98 | [#98](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/98) | 18 | yes | 2026-10-27 |
| P-066 — ST-17 Confirm-token lifetime (24 h) and key-ring restart tests | User Story | 99 | [#99](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/99) | 18 | yes | 2026-10-27 |
| P-067 — US-010 Request a password reset | User Story | 100 | [#100](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/100) | 19 | yes | 2026-10-27 |
| P-068 — US-011 Reset password with a token | User Story | 101 | [#101](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/101) | 19 | yes | 2026-10-27 |
| P-069 — US-012 List users | User Story | 102 | [#102](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/102) | 20 | yes | 2026-10-27 |
| P-070 — US-013 Get a user | User Story | 103 | [#103](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/103) | 20 | yes | 2026-10-27 |
| P-071 — US-014 Lock a user | User Story | 104 | [#104](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/104) | 20 | yes | 2026-10-27 |
| P-072 — US-015 Unlock a user | User Story | 105 | [#105](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/105) | 20 | yes | 2026-10-27 |
| P-073 — US-016 Assign the Admin role | User Story | 106 | [#106](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/106) | 20 | yes | 2026-10-27 |
| P-074 — US-017 Remove the Admin role | User Story | 107 | [#107](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/107) | 20 | yes | 2026-10-27 |
| P-075 — US-018 List roles | User Story | 108 | [#108](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/108) | 20 | yes | 2026-10-27 |
| P-076 — US-019 Create an Admin user | User Story | 109 | [#109](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/109) | 20 | yes | 2026-10-27 |
| P-077 — US-107 Admin creates a customer | User Story | 110 | [#110](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/110) | 21 | yes | 2026-10-27 |
| P-078 — US-108 List customers | User Story | 111 | [#111](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/111) | 21 | yes | 2026-10-27 |
| P-079 — US-109 Get a customer | User Story | 112 | [#112](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/112) | 21 | yes | 2026-10-27 |
| P-080 — US-110 Update a customer | User Story | 113 | [#113](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/113) | 21 | yes | 2026-10-27 |
| P-081 — US-111 Delete and anonymize a customer | User Story | 114 | [#114](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/114) | 21 | yes | 2026-10-27 |
| P-082 — Enabler: OwnedBy(ICurrentUser) helper and /me pattern | User Story | 115 | [#115](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/115) | 22 | yes | 2026-10-27 |
| P-083 — US-113 Read own profile | User Story | 116 | [#116](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/116) | 22 | yes | 2026-10-27 |
| P-084 — US-114 Update own profile | User Story | 117 | [#117](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/117) | 22 | yes | 2026-10-27 |
| P-085 — US-115 Erase own account | User Story | 118 | [#118](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/118) | 22 | yes | 2026-10-27 |
| P-086 — CI: TemplateStrip stage (delete sample-domain folders, apply Customer-leak edits, build and test the copy) | User Story | 119 | [#119](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/119) | 23 | yes | 2026-10-27 |
| P-087 — Test infra: Architecture rules A12, A13 and C10 test (Admin get of a deleted customer incl. email) | User Story | 120 | [#120](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/120) | 23 | yes | 2026-10-27 |
| P-088 — CI: Weekly scheduled pipeline (ST-08 at 30 calls per path) and Renovate updates | User Story | 121 | [#121](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/121) | 23 | yes | 2026-10-27 |
| P-089 — ST-02 BOLA tests for /me routes (EP-35 to EP-37; fails on an uncovered /me/{id} route) | User Story | 122 | [#122](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/122) | 24 | yes | 2026-10-27 |
| P-090 — ST-03 Mass-assignment tests and per-role DTO snapshots | User Story | 123 | [#123](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/123) | 24 | yes | 2026-10-27 |
| P-091 — ST-13 Erasure test (every PII field, all tables, captured logs, Valkey keys → 0 matches) | User Story | 124 | [#124](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/124) | 24 | yes | 2026-10-27 |
| P-092 — ST-19 Security-event tests (role assign, Admin create, lockout, refresh reuse) | User Story | 125 | [#125](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/125) | 24 | yes | 2026-10-27 |
| P-093 — Security tests S2 extensions: ST-06 (lock, role remove, erase), ST-07 (EP-37, reset), ST-08 (forgot), ST-14 (PII), ST-17 (reset 1 h, single use) | User Story | 126 | [#126](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/126) | 24 | yes | 2026-10-27 |
| P-094 — Enabler: Order aggregate, line items, status machine, OrderStatusHistory and Touch() for ETags | User Story | 127 | [#127](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/127) | 25 | yes | 2026-10-27 |
| P-095 — Enabler: Stock locking with SELECT … FOR UPDATE in product ID order inside ITransactionRunner, and the Pending-order cap | User Story | 128 | [#128](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/128) | 25 | yes | 2026-10-27 |
| P-096 — US-200 Admin creates an order for a customer | User Story | 129 | [#129](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/129) | 26 | yes | 2026-10-27 |
| P-097 — US-201 Admin lists orders | User Story | 130 | [#130](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/130) | 26 | yes | 2026-10-27 |
| P-098 — US-202 Admin gets an order with items and history | User Story | 131 | [#131](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/131) | 26 | yes | 2026-10-27 |
| P-099 — US-203 Admin replaces items of a Pending order (first cut candidate in S3) | User Story | 132 | [#132](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/132) | 26 | yes | 2026-10-27 |
| P-100 — US-204 Admin moves an order to the next status | User Story | 133 | [#133](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/133) | 26 | yes | 2026-10-27 |
| P-101 — US-205 Admin cancels an order | User Story | 134 | [#134](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/134) | 26 | yes | 2026-10-27 |
| P-102 — US-207 Customer places an own order | User Story | 135 | [#135](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/135) | 27 | yes | 2026-10-27 |
| P-103 — US-208 Customer lists own orders | User Story | 136 | [#136](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/136) | 27 | yes | 2026-10-27 |
| P-104 — US-209 Customer gets an own order | User Story | 137 | [#137](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/137) | 27 | yes | 2026-10-27 |
| P-105 — US-210 Customer cancels an own Pending order | User Story | 138 | [#138](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/138) | 27 | yes | 2026-10-27 |
| P-106 — Test: Concurrency suite (about 9 scenarios; 20 local loop runs before merge) | User Story | 139 | [#139](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/139) | 28 | yes | 2026-10-27 |
| P-107 — Test data: Sample-data seeder (1,000 products, 1,000 customers, 10,000 orders) for k6 | User Story | 140 | [#140](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/140) | 28 | yes | 2026-10-27 |
| P-108 — Optional: Stryker.NET one-off mutation run on Domain (skip if xUnit v3 is not supported) | User Story | 141 | [#141](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/141) | 28 | yes | 2026-10-27 |
| P-109 — ST-18 Timeout tests (lock wait ends within budget) | User Story | 142 | [#142](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/142) | 28 | yes | 2026-10-27 |
| P-110 — Security tests S3 extensions: ST-02 (EP-47, EP-48), ST-03 (customerId, status, unitPrice), ST-15 (poisoned Valkey price ignored), ST-16 (lines, quantity, Pending cap) | User Story | 143 | [#143](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/143) | 28 | yes | 2026-10-27 |
| P-111 — US-212 Order summary report (view) | User Story | 144 | [#144](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/144) | 29 | yes | 2026-10-27 |
| P-112 — US-213 Customer order history (function) | User Story | 145 | [#145](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/145) | 29 | yes | 2026-10-27 |
| P-113 — US-214 Sales report by date range | User Story | 146 | [#146](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/146) | 29 | yes | 2026-10-27 |
| P-114 — US-215 Low-stock report | User Story | 147 | [#147](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/147) | 29 | yes | 2026-10-27 |
| P-115 — US-216 Top products report | User Story | 148 | [#148](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/148) | 29 | yes | 2026-10-27 |
| P-116 — US-217 Query the audit log | User Story | 149 | [#149](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/149) | 29 | yes | 2026-10-27 |
| P-117 — Perf: k6 run 2 (NFR-01 + NFR-02) and DB timeout and report-concurrency tuning | User Story | 150 | [#150](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/150) | 30 | yes | 2026-10-27 |
| P-118 — CI: ApiFiles stage (httpyac .http run and generated Postman collection against a running API) | User Story | 151 | [#151](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/151) | 30 | yes | 2026-10-27 |
| P-119 — Security tests S4 extensions: ST-09 (reports concurrency 429), ST-11 (Dapper SQL architecture rule), ST-16 (report range), ST-18 (slow query), ST-19 (audit-log query) | User Story | 152 | [#152](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/152) | 30 | yes | 2026-10-27 |
| P-120 — Template: dotnet new package (template.json, sourceName DemoApi, kept parts per strip path, single Initial migration) | User Story | 153 | [#153](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/153) | 31 | yes | 2026-10-27 |
| P-121 — Template: "Start a new API" guide (strip steps, Customer leaks, option to merge Domain + Application) | User Story | 154 | [#154](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/154) | 31 | yes | 2026-10-27 |
| P-122 — Template: Template suites green from dotnet new; TemplateStrip job switched to dotnet new install | User Story | 155 | [#155](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/155) | 31 | yes | 2026-10-27 |
| P-123 — Template: Bootstrap trial and "hours to bootstrap" baseline | User Story | 156 | [#156](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/156) | 31 | yes | 2026-10-27 |
| P-124 — Spike SP-6: .NET 11 compatibility check (build and tests on the .NET 11 SDK, no retarget) | User Story | 157 | [#157](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/157) | 31 | yes | 2026-10-27 |
| P-125 — Release: Fresh-clone rebuild timed (≤ 15 min, 0 manual steps) and final README | User Story | 158 | [#158](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/158) | 32 | yes | 2026-10-27 |
| P-126 — Release: Release checklist and POC sign-off (North Star 100%, test-strategy §6 minimums) | User Story | 159 | [#159](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/159) | 32 | yes | 2026-10-27 |
| P-127 — Ops: Light runbook (health, logs, regenerating dev secrets, HS256 key rotation) | User Story | 160 | [#160](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/160) | 32 | yes | 2026-10-27 |
| P-128 — Docs: Before-production register (threat model §7.2 items 1-12, retention plan §4.3) for the client project | User Story | 161 | [#161](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/161) | 32 | yes | 2026-10-27 |
| P-129 — US-020 Revoke a user's sessions | User Story | 162 | [#162](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/162) | 33 | yes | 2026-10-27 |
| P-130 — US-021 Send a password-reset link for a user | User Story | 163 | [#163](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/163) | 33 | yes | 2026-10-27 |
| P-131 — US-106 Adjust product stock | User Story | 164 | [#164](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/164) | 205 (was 33) | yes | 2026-10-27 |
| P-132 — US-112 Restore a customer (incl. restored-customer lock type and email change, D14) | User Story | 165 | [#165](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/165) | 205 (was 33) | yes | 2026-10-27 |
| P-133 — US-206 Admin soft deletes a Cancelled order | User Story | 166 | [#166](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/166) | 33 | yes | 2026-10-27 |
| P-134 — US-211 Customer edits items of an own Pending order | User Story | 167 | [#167](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/167) | 205 (was 33) | yes | 2026-10-27 |
| P-135 — Idempotency-Key on order create (no user story exists) | User Story | 168 | [#168](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/168) | 33 | yes | 2026-10-27 |
| Bug: OpenAPI missing error response codes and 201/202 (post-build gap) | Bug | 169 | [#169](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/169) | 11 | yes | 2026-10-27 |
| Review: triage code-review and security-review findings (post-build gap) | User Story | 170 | [#170](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/170) | 17 | yes | 2026-10-27 |
| Task: generate Postman collection from corrected OpenAPI (predecessor: #169) | Task | 171 | [#171](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/171) | 85 | yes | 2026-10-27 |
| Task: run destructive .http file (customer-c-lifecycle.http) | Task | 172 | [#172](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/172) | 85 | yes | 2026-10-27 |
| E9 — FE-S0a Walking skeleton (web) | Epic | 173 | [#173](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/173) | – | yes | 2026-10-30 |
| E10 — FE-S0b BFF hardening and test harness | Epic | 174 | [#174](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/174) | – | yes | 2026-10-30 |
| E11 — UI design in Figma (design lane) | Epic | 175 | [#175](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/175) | – | yes | 2026-10-30 |
| E12 — FE-R1 Customer portal | Epic | 176 | [#176](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/176) | – | yes | 2026-10-30 |
| E13 — FE-R2 Admin back office | Epic | 177 | [#177](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/177) | – | yes | 2026-10-30 |
| E14 — Front-end release and operations (light) | Epic | 178 | [#178](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/178) | – | yes | 2026-10-30 |
| E15 — API v1.1 (deferred, unscheduled; lifts ApiFreeze) | Epic | 179 | [#179](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/179) | – | yes | 2026-10-30 |
| F9.1 — Baseline, scaffold and pipeline | Feature | 180 | [#180](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/180) | 173 | yes | 2026-10-30 |
| F9.2 — Spikes and SPA stack ADR | Feature | 181 | [#181](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/181) | 173 | yes | 2026-10-30 |
| F9.3 — BFF and SPA thin slice | Feature | 182 | [#182](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/182) | 173 | yes | 2026-10-30 |
| F9.4 — Test harness core | Feature | 183 | [#183](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/183) | 173 | yes | 2026-10-30 |
| F10.1 — BFF security hardening | Feature | 184 | [#184](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/184) | 174 | yes | 2026-10-30 |
| F10.2 — SPA foundations | Feature | 185 | [#185](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/185) | 174 | yes | 2026-10-30 |
| F10.3 — Pipeline gates, harness completion and ADRs | Feature | 186 | [#186](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/186) | 174 | yes | 2026-10-30 |
| F10.4 — Security tests, perf baseline and review gate | Feature | 187 | [#187](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/187) | 174 | yes | 2026-10-30 |
| F11.1 — R1 design in Figma | Feature | 188 | [#188](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/188) | 175 | yes | 2026-10-30 |
| F11.2 — R2 Admin design in Figma | Feature | 189 | [#189](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/189) | 175 | yes | 2026-10-30 |
| F12.1 — Access pages | Feature | 190 | [#190](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/190) | 176 | yes | 2026-10-30 |
| F12.2 — Catalog, cart and checkout | Feature | 191 | [#191](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/191) | 176 | yes | 2026-10-30 |
| F12.3 — My orders | Feature | 192 | [#192](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/192) | 176 | yes | 2026-10-30 |
| F12.4 — Account | Feature | 193 | [#193](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/193) | 176 | yes | 2026-10-30 |
| F12.5 — R1 enablers and security tests | Feature | 194 | [#194](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/194) | 176 | yes | 2026-10-30 |
| F12.6 — R1 release gates | Feature | 195 | [#195](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/195) | 176 | yes | 2026-10-30 |
| F13.1 — R2 readiness | Feature | 196 | [#196](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/196) | 177 | yes | 2026-10-30 |
| F13.2 — Admin shell, session and account | Feature | 197 | [#197](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/197) | 177 | yes | 2026-10-30 |
| F13.3 — Products (Admin) | Feature | 198 | [#198](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/198) | 177 | yes | 2026-10-30 |
| F13.4 — Customers (Admin) | Feature | 199 | [#199](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/199) | 177 | yes | 2026-10-30 |
| F13.5 — Orders (Admin) | Feature | 200 | [#200](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/200) | 177 | yes | 2026-10-30 |
| F13.6 — Reports and audit log | Feature | 201 | [#201](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/201) | 177 | yes | 2026-10-30 |
| F13.7 — Users (Admin) | Feature | 202 | [#202](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/202) | 177 | yes | 2026-10-30 |
| F13.8 — R2 security, quality and release | Feature | 203 | [#203](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/203) | 177 | yes | 2026-10-30 |
| F14.1 — Front-end release and operations notes | Feature | 204 | [#204](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/204) | 178 | yes | 2026-10-30 |
| F15.1 — API v1.1 changes | Feature | 205 | [#205](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/205) | 179 | yes | 2026-10-30 |
| P2-001 — Gate: Phase 1 baseline for the API freeze | User Story | 206 | [#206](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/206) | 180 | yes | 2026-10-30 |
| P2-002 — Enabler: web/ scaffold | User Story | 207 | [#207](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/207) | 180 | yes | 2026-10-30 |
| P2-003 — US-F009 Compose, configuration and pipeline | User Story | 208 | [#208](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/208) | 180 | yes | 2026-10-30 |
| P2-004 — CI: ApiFreeze stage | User Story | 209 | [#209](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/209) | 180 | yes | 2026-10-30 |
| P2-005 — CI: Spa job and BFF tests in FastTests | User Story | 210 | [#210](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/210) | 180 | yes | 2026-10-30 |
| P2-006 — CI: E2E stage | User Story | 211 | [#211](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/211) | 180 | yes | 2026-10-30 |
| P2-007 — Spike SP-F4: npm overrides on TypeScript 6.0.3 | User Story | 212 | [#212](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/212) | 181 | yes | 2026-10-30 |
| P2-008 — Spike SP-F5: YARP 2.3.0 on .NET 10 | User Story | 213 | [#213](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/213) | 181 | yes | 2026-10-30 |
| P2-009 — Spike SP-F1: __Host- cookie on http://localhost | User Story | 214 | [#214](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/214) | 181 | yes | 2026-10-30 |
| P2-010 — Spike SP-F2: API Location headers | User Story | 215 | [#215](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/215) | 181 | yes | 2026-10-30 |
| P2-011 — Docs: ADR-0016 SPA stack and conventions | User Story | 216 | [#216](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/216) | 181 | yes | 2026-10-30 |
| P2-012 — Enabler: BFF host core | User Story | 217 | [#217](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/217) | 182 | yes | 2026-10-30 |
| P2-013 — US-F001 BFF proxies the API | User Story | 218 | [#218](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/218) | 182 | yes | 2026-10-30 |
| P2-014 — US-F002 Sign in through the BFF | User Story | 219 | [#219](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/219) | 182 | yes | 2026-10-30 |
| P2-015 — US-F004 Sign out | User Story | 220 | [#220](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/220) | 182 | yes | 2026-10-30 |
| P2-016 — Enabler: SPA API client | User Story | 221 | [#221](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/221) | 182 | yes | 2026-10-30 |
| P2-017 — US-F006 App shell, session bootstrap and role routing | User Story | 222 | [#222](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/222) | 182 | yes | 2026-10-30 |
| P2-018 — Test infra: BFF test project | User Story | 223 | [#223](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/223) | 183 | yes | 2026-10-30 |
| P2-019 — Test infra: Playwright projects and fixtures | User Story | 224 | [#224](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/224) | 183 | yes | 2026-10-30 |
| P2-020 — Test infra: Vitest + Testing Library + MSW | User Story | 225 | [#225](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/225) | 183 | yes | 2026-10-30 |
| P2-021 — Test infra: FT-01 token-scan fixture and CSP listener | User Story | 226 | [#226](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/226) | 183 | yes | 2026-10-30 |
| P2-022 — Test infra: axe fixture | User Story | 227 | [#227](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/227) | 183 | yes | 2026-10-30 |
| P2-023 — Enabler: BFF hardening | User Story | 228 | [#228](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/228) | 184 | yes | 2026-10-30 |
| P2-024 — US-F003 Silent, single-flight refresh | User Story | 229 | [#229](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/229) | 184 | yes | 2026-10-30 |
| P2-025 — US-F005 CSRF protection | User Story | 230 | [#230](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/230) | 184 | yes | 2026-10-30 |
| P2-026 — US-F008 BFF rate limits and API configuration | User Story | 231 | [#231](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/231) | 184 | yes | 2026-10-30 |
| P2-027 — Enabler: BFF health endpoints | User Story | 232 | [#232](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/232) | 184 | yes | 2026-10-30 |
| P2-028 — Enabler: https compose profile and Initialize-DevCert.ps1 | User Story | 233 | [#233](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/233) | 184 | yes | 2026-10-30 |
| P2-029 — Spike SP-F3: Radix/shadcn under the strict CSP | User Story | 234 | [#234](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/234) | 185 | yes | 2026-10-30 |
| P2-030 — Enabler: Design tokens and shadcn base | User Story | 235 | [#235](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/235) | 185 | yes | 2026-10-30 |
| P2-031 — US-F007 Error handling with ProblemDetails | User Story | 236 | [#236](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/236) | 185 | yes | 2026-10-30 |
| P2-032 — CI: Front-end supply-chain gates | User Story | 237 | [#237](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/237) | 186 | yes | 2026-10-30 |
| P2-033 — Test infra: Meta-tests | User Story | 238 | [#238](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/238) | 186 | yes | 2026-10-30 |
| P2-034 — Test infra: Traceability script and manual-checks.md | User Story | 239 | [#239](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/239) | 186 | yes | 2026-10-30 |
| P2-035 — Docs: ADR-0017 Local HTTPS and cookie policy | User Story | 240 | [#240](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/240) | 186 | yes | 2026-10-30 |
| P2-036 — Docs: ADR-0008/0010/0012/0013 amendments and template strip | User Story | 241 | [#241](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/241) | 186 | yes | 2026-10-30 |
| P2-037 — FT-02 Cookie flags and session renewal | User Story | 242 | [#242](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/242) | 187 | yes | 2026-10-30 |
| P2-038 — FT-03 CSRF matrix | User Story | 243 | [#243](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/243) | 187 | yes | 2026-10-30 |
| P2-039 — FT-04 Allow-list fuzz and header stripping | User Story | 244 | [#244](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/244) | 187 | yes | 2026-10-30 |
| P2-040 — FT-05 Single-flight refresh tests | User Story | 245 | [#245](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/245) | 187 | yes | 2026-10-30 |
| P2-041 — FT-06 Session store tests | User Story | 246 | [#246](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/246) | 187 | yes | 2026-10-30 |
| P2-042 — FT-07 BFF rate-limit tests | User Story | 247 | [#247](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/247) | 187 | yes | 2026-10-30 |
| P2-043 — FT-09 and FT-18 Header snapshot and exception ProblemDetails | User Story | 248 | [#248](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/248) | 187 | yes | 2026-10-30 |
| P2-044 — FT-13 and FT-19 Session-end tests | User Story | 249 | [#249](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/249) | 187 | yes | 2026-10-30 |
| P2-045 — Perf: Foundation baseline | User Story | 250 | [#250](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/250) | 187 | yes | 2026-10-30 |
| P2-046 — Review gate: BFF security review | User Story | 251 | [#251](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/251) | 187 | yes | 2026-10-30 |
| P2-047 — Figma: connect MCP, duplicate Obra kit, create files | User Story | 252 | [#252](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/252) | 188 | yes | 2026-10-30 |
| P2-048 — Figma batch 0: inspect the kit | User Story | 253 | [#253](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/253) | 188 | yes | 2026-10-30 |
| P2-049 — Figma batch 1: foundations | User Story | 254 | [#254](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/254) | 188 | yes | 2026-10-30 |
| P2-050 — Figma batch 2: components, icons and patterns | User Story | 255 | [#255](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/255) | 188 | yes | 2026-10-30 |
| P2-051 — Figma batch 3: R1 screens 1-6 | User Story | 256 | [#256](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/256) | 188 | yes | 2026-10-30 |
| P2-052 — Figma batch 4: R1 screens 7-12 and prototype | User Story | 257 | [#257](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/257) | 188 | yes | 2026-10-30 |
| P2-053 — Design review R1 | User Story | 258 | [#258](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/258) | 188 | yes | 2026-10-30 |
| P2-054 — Figma batch 5: Admin shell, dashboard, products, customers | User Story | 259 | [#259](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/259) | 189 | yes | 2026-10-30 |
| P2-055 — Figma batch 6: orders and idle-timeout dialog | User Story | 260 | [#260](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/260) | 189 | yes | 2026-10-30 |
| P2-056 — Figma batch 7: reports, users, audit log, step-up dialog | User Story | 261 | [#261](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/261) | 189 | yes | 2026-10-30 |
| P2-057 — Design review R2 | User Story | 262 | [#262](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/262) | 189 | yes | 2026-10-30 |
| P2-058 — US-F020 Register | User Story | 263 | [#263](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/263) | 190 | yes | 2026-10-30 |
| P2-059 — US-F021 Confirm email | User Story | 264 | [#264](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/264) | 190 | yes | 2026-10-30 |
| P2-060 — US-F022 Resend confirmation | User Story | 265 | [#265](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/265) | 190 | yes | 2026-10-30 |
| P2-061 — US-F023 Sign-in page | User Story | 266 | [#266](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/266) | 190 | yes | 2026-10-30 |
| P2-062 — US-F024 Forgot password | User Story | 267 | [#267](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/267) | 190 | yes | 2026-10-30 |
| P2-063 — US-F025 Reset password | User Story | 268 | [#268](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/268) | 190 | yes | 2026-10-30 |
| P2-064 — US-F026 Browse and search the catalog | User Story | 269 | [#269](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/269) | 191 | yes | 2026-10-30 |
| P2-065 — US-F027 Product detail | User Story | 270 | [#270](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/270) | 191 | yes | 2026-10-30 |
| P2-066 — US-F028 Cart | User Story | 271 | [#271](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/271) | 191 | yes | 2026-10-30 |
| P2-067 — US-F029 Place order | User Story | 272 | [#272](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/272) | 191 | yes | 2026-10-30 |
| P2-068 — US-F030 My orders list | User Story | 273 | [#273](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/273) | 192 | yes | 2026-10-30 |
| P2-069 — US-F031 Order detail | User Story | 274 | [#274](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/274) | 192 | yes | 2026-10-30 |
| P2-070 — US-F032 Cancel own order | User Story | 275 | [#275](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/275) | 192 | yes | 2026-10-30 |
| P2-071 — US-F033 View and edit profile | User Story | 276 | [#276](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/276) | 193 | yes | 2026-10-30 |
| P2-072 — US-F034 Change password | User Story | 277 | [#277](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/277) | 193 | yes | 2026-10-30 |
| P2-073 — US-F035 Delete my account | User Story | 278 | [#278](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/278) | 193 | yes | 2026-10-30 |
| P2-074 — Enabler: Admin session limits in the BFF | User Story | 279 | [#279](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/279) | 194 | yes | 2026-10-30 |
| P2-075 — Enabler: Light/Dark theme switch | User Story | 280 | [#280](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/280) | 194 | yes | 2026-10-30 |
| P2-076 — Enabler: SY-4 service-unavailable banner and @release resilience spec | User Story | 281 | [#281](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/281) | 194 | yes | 2026-10-30 |
| P2-077 — Security follow-up: enforce Trusted Types | User Story | 282 | [#282](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/282) | 194 | yes | 2026-10-30 |
| P2-078 — FT-10 Stored-XSS payloads render as text (R1 data) | User Story | 283 | [#283](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/283) | 194 | yes | 2026-10-30 |
| P2-079 — FT-14 Cart storage tests | User Story | 284 | [#284](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/284) | 194 | yes | 2026-10-30 |
| P2-080 — FT-16 Confirm and reset links leave no token | User Story | 285 | [#285](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/285) | 194 | yes | 2026-10-30 |
| P2-081 — FT-08 Log and trace scan after a full E2E run | User Story | 286 | [#286](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/286) | 194 | yes | 2026-10-30 |
| P2-082 — Perf: pre-R1 Web Vitals p75 and k6 NFR-F06 re-run | User Story | 287 | [#287](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/287) | 195 | yes | 2026-10-30 |
| P2-083 — Usability: G1 timed test | User Story | 288 | [#288](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/288) | 195 | yes | 2026-10-30 |
| P2-084 — Accessibility: R1 manual pass | User Story | 289 | [#289](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/289) | 195 | yes | 2026-10-30 |
| P2-085 — Browsers: WebKit https, Firefox full, 360 px, device smoke | User Story | 290 | [#290](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/290) | 195 | yes | 2026-10-30 |
| P2-086 — Design QA: R1 full pass | User Story | 291 | [#291](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/291) | 195 | yes | 2026-10-30 |
| P2-087 — Review gate: R1 security review (SPA + BFF delta) | User Story | 292 | [#292](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/292) | 195 | yes | 2026-10-30 |
| P2-088 — Release: R1 sign-off and demo | User Story | 293 | [#293](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/293) | 195 | yes | 2026-10-30 |
| P2-089 — Docs: number the R2 acceptance criteria | User Story | 294 | [#294](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/294) | 196 | yes | 2026-10-30 |
| P2-090 — Spike: visual regression yes or no | User Story | 295 | [#295](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/295) | 196 | yes | 2026-10-30 |
| P2-091 — US-F050 Admin shell and landing page | User Story | 296 | [#296](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/296) | 197 | yes | 2026-10-30 |
| P2-092 — Enabler: Admin idle warning dialog at 28 minutes | User Story | 297 | [#297](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/297) | 197 | yes | 2026-10-30 |
| P2-093 — Enabler: step-up re-authentication for EP-16 and EP-19 | User Story | 298 | [#298](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/298) | 197 | yes | 2026-10-30 |
| P2-094 — US-F036 Sign out everywhere | User Story | 299 | [#299](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/299) | 197 | yes | 2026-10-30 |
| P2-095 — US-F051 Product list incl. deleted | User Story | 300 | [#300](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/300) | 198 | yes | 2026-10-30 |
| P2-096 — US-F052 Create product | User Story | 301 | [#301](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/301) | 198 | yes | 2026-10-30 |
| P2-097 — US-F053 Edit product (incl. stock) | User Story | 302 | [#302](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/302) | 198 | yes | 2026-10-30 |
| P2-098 — US-F054 Delete and restore product | User Story | 303 | [#303](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/303) | 198 | yes | 2026-10-30 |
| P2-099 — US-F055 Customer list | User Story | 304 | [#304](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/304) | 199 | yes | 2026-10-30 |
| P2-100 — US-F056 Customer detail and order history | User Story | 305 | [#305](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/305) | 199 | yes | 2026-10-30 |
| P2-101 — US-F057 Create customer | User Story | 306 | [#306](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/306) | 199 | yes | 2026-10-30 |
| P2-102 — US-F058 Edit customer | User Story | 307 | [#307](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/307) | 199 | yes | 2026-10-30 |
| P2-103 — US-F059 Delete customer | User Story | 308 | [#308](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/308) | 199 | yes | 2026-10-30 |
| P2-104 — US-F060 Orders list | User Story | 309 | [#309](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/309) | 200 | yes | 2026-10-30 |
| P2-105 — US-F061 Order detail, status and cancel | User Story | 310 | [#310](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/310) | 200 | yes | 2026-10-30 |
| P2-106 — US-F062 Replace order items | User Story | 311 | [#311](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/311) | 200 | yes | 2026-10-30 |
| P2-107 — US-F063 Create order for a customer | User Story | 312 | [#312](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/312) | 200 | yes | 2026-10-30 |
| P2-108 — US-F064 Sales report | User Story | 313 | [#313](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/313) | 201 | yes | 2026-10-30 |
| P2-109 — US-F065 Low-stock report | User Story | 314 | [#314](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/314) | 201 | yes | 2026-10-30 |
| P2-110 — US-F066 Top products | User Story | 315 | [#315](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/315) | 201 | yes | 2026-10-30 |
| P2-111 — US-F071 Audit-log viewer | User Story | 316 | [#316](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/316) | 201 | yes | 2026-10-30 |
| P2-112 — US-F067 Users list and detail | User Story | 317 | [#317](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/317) | 202 | yes | 2026-10-30 |
| P2-113 — US-F068 Create Admin user | User Story | 318 | [#318](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/318) | 202 | yes | 2026-10-30 |
| P2-114 — US-F069 Lock and unlock user | User Story | 319 | [#319](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/319) | 202 | yes | 2026-10-30 |
| P2-115 — US-F070 Assign and remove Admin role | User Story | 320 | [#320](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/320) | 202 | yes | 2026-10-30 |
| P2-116 — FT-10 extension: Admin data render as text | User Story | 321 | [#321](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/321) | 203 | yes | 2026-10-30 |
| P2-117 — Perf: R2 Web Vitals, admin chunk lazy, NFR-F27 | User Story | 322 | [#322](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/322) | 203 | yes | 2026-10-30 |
| P2-118 — Accessibility: R2 manual pass | User Story | 323 | [#323](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/323) | 203 | yes | 2026-10-30 |
| P2-119 — Design QA: R2 full pass | User Story | 324 | [#324](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/324) | 203 | yes | 2026-10-30 |
| P2-120 — Review gate: security review of Admin areas and step-up | User Story | 325 | [#325](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/325) | 203 | yes | 2026-10-30 |
| P2-121 — Release: R2 sign-off and demo | User Story | 326 | [#326](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/326) | 203 | yes | 2026-10-30 |
| P2-122 — Docs: README and CLAUDE.md for the web front end | User Story | 327 | [#327](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/327) | 204 | yes | 2026-10-30 |
| P2-123 — Ops: runbook additions | User Story | 328 | [#328](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/328) | 204 | yes | 2026-10-30 |
| P2-124 — Docs: before-production register update | User Story | 329 | [#329](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/329) | 204 | yes | 2026-10-30 |
| P2-125 — CI: TemplateStrip removes web/ and its two .slnx entries | User Story | 330 | [#330](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/330) | 204 | yes | 2026-10-30 |
| P2-126 — Release: timed fresh clone with the web service | User Story | 331 | [#331](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/331) | 204 | yes | 2026-10-30 |
| P2-127 — Enabler: API v1.1 release mechanics | User Story | 332 | [#332](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/332) | 205 | yes | 2026-10-30 |
| P2-128 — Forwarded headers with KnownProxies = BFF; restore API auth limits | User Story | 333 | [#333](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/333) | 205 | yes | 2026-10-30 |
| P2-129 — G2 Anonymous catalog | User Story | 334 | [#334](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/334) | 205 | yes | 2026-10-30 |
| P2-130 — G3 Product name on order lines | User Story | 335 | [#335](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/335) | 205 | yes | 2026-10-30 |
| P2-131 — G11 Customer changes own email | User Story | 336 | [#336](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/336) | 205 | yes | 2026-10-30 |
| P2-132 — Mask the email in the FakeEmailSender reset-link log line | User Story | 337 | [#337](https://dev.azure.com/codewithelmor/Demo-App/_workitems/edit/337) | 205 | yes | 2026-10-30 |

## Board state after build sync (2026-10-27)

Active (47): Epics #1-#5; enablers #34, #35, #45, #46, #85; Tier A stories #53-#56, #66, #67, #69-#72, #74-#78, #101, #110, #111, #113, #114, #117, #118, #129, #130, #132-#138, #144-#149. Evidence: endpoint built, tagged integration test, `.http` run passed. Not Closed: no merge, CI on `main` or review sign-off yet. Tier B (built, no tagged integration test found, left New): #68, #73, #100, #102-#109, #112, #116, #131. Comments added to #1-#5, #34, #35, #45, #46, #47, #85, #87.
