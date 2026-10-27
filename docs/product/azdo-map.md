# Azure DevOps map

Organization: codewithelmor | Project: Demo-API | Team: Demo-API Team | Last synced: 2026-10-27
Board: https://dev.azure.com/codewithelmor/Demo-API/_boards/board/t/Demo-API%20Team/Stories
Process: Agile

Status: **168 work items created 2026-10-27** (8 Epics, 25 Features, 135 User Stories; 449 Story Points; 160 parent links, 31 dependency links). Plan: [06-delivery/azdo-preview.md](06-delivery/azdo-preview.md).

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
| Area (team default, include sub areas on) | Demo-API | – | no |
| Area | Demo-API\Platform | – | no (product owner, UI) |
| Area | Demo-API\Auth | – | no (product owner, UI) |
| Area | Demo-API\AdminUsers | – | no (product owner, UI) |
| Area | Demo-API\Products | – | no (product owner, UI) |
| Area | Demo-API\Customers | – | no (product owner, UI) |
| Area | Demo-API\Orders | – | no (product owner, UI) |
| Area | Demo-API\Reports | – | no (product owner, UI) |
| Area | Demo-API\Template | – | no (product owner, UI) |
| Area | Demo-API\Delivery | – | no (product owner, UI) |
| Iteration (backlog + default) | Demo-API | – | no |
| Iteration (unused, Kanban) | Demo-API\Iteration 1, 2, 3 | none | no |

Team settings verified 2026-10-27: Epics backlog visible, bugs managed with requirements, default iteration = root, include sub areas = on.

## Tags
| Tag | Meaning | Created by plan |
|---|---|---|
| 166 tags in use | Approved scheme: slice `S1a`, `S1b`, `S2`, `S3`, `S4`, `template`, `RelOps`, `stretch`; kind tags; `template-strip` (P-086); `needs-ac` (75 stories without ACs); trace `US-xxx`, `EP-xx`, `ST-xx`, `FR-xxx` | yes (created on first use) |

## Work items
| Planning item (doc#anchor) | Type | ID | URL | Parent ID | Created by plan | Last synced |
|---|---|---|---|---|---|---|
| E1 — S1a Walking skeleton | Epic | 1 | [#1](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/1) | – | yes (permission test, then updated) | 2026-10-27 |
| E2 — S1b Auth and product CRUD | Epic | 2 | [#2](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/2) | – | yes | 2026-10-27 |
| E3 — S2 Customers, own-data rules and user admin | Epic | 3 | [#3](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/3) | – | yes | 2026-10-27 |
| E4 — S3 Orders, stock and statuses | Epic | 4 | [#4](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/4) | – | yes | 2026-10-27 |
| E5 — S4 Reports and Dapper reads | Epic | 5 | [#5](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/5) | – | yes | 2026-10-27 |
| E6 — Template extraction (P7) | Epic | 6 | [#6](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/6) | – | yes | 2026-10-27 |
| E7 — Release and operations (light) | Epic | 7 | [#7](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/7) | – | yes | 2026-10-27 |
| E8 — Stretch (cut first) | Epic | 8 | [#8](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/8) | – | yes | 2026-10-27 |
| F1.1 — Repo, solution and local run | Feature | 9 | [#9](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/9) | 1 | yes | 2026-10-27 |
| F1.2 — CI pipeline and supply-chain gates | Feature | 10 | [#10](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/10) | 1 | yes | 2026-10-27 |
| F1.3 — Test and architecture foundations | Feature | 11 | [#11](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/11) | 1 | yes | 2026-10-27 |
| F1.4 — Product create/get and health end to end | Feature | 12 | [#12](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/12) | 1 | yes | 2026-10-27 |
| F2.1 — Token and identity foundations | Feature | 13 | [#13](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/13) | 2 | yes | 2026-10-27 |
| F2.2 — Auth endpoints | Feature | 14 | [#14](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/14) | 2 | yes | 2026-10-27 |
| F2.3 — Product CRUD completion | Feature | 15 | [#15](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/15) | 2 | yes | 2026-10-27 |
| F2.4 — Platform conventions and observability | Feature | 16 | [#16](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/16) | 2 | yes | 2026-10-27 |
| F2.5 — DoD meta-tests, API files and performance run 1 | Feature | 17 | [#17](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/17) | 2 | yes | 2026-10-27 |
| F2.6 — Security tests S1b | Feature | 18 | [#18](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/18) | 2 | yes | 2026-10-27 |
| F3.1 — Password recovery | Feature | 19 | [#19](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/19) | 3 | yes | 2026-10-27 |
| F3.2 — Admin user and role management | Feature | 20 | [#20](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/20) | 3 | yes | 2026-10-27 |
| F3.3 — Customers (Admin) | Feature | 21 | [#21](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/21) | 3 | yes | 2026-10-27 |
| F3.4 — Customer self-service (/me) | Feature | 22 | [#22](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/22) | 3 | yes | 2026-10-27 |
| F3.5 — Template strip check and pipeline upkeep | Feature | 23 | [#23](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/23) | 3 | yes | 2026-10-27 |
| F3.6 — Security tests S2 | Feature | 24 | [#24](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/24) | 3 | yes | 2026-10-27 |
| F4.1 — Order domain and stock locking | Feature | 25 | [#25](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/25) | 4 | yes | 2026-10-27 |
| F4.2 — Orders (Admin) | Feature | 26 | [#26](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/26) | 4 | yes | 2026-10-27 |
| F4.3 — Orders self-service (/me) | Feature | 27 | [#27](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/27) | 4 | yes | 2026-10-27 |
| F4.4 — Order concurrency, load data and security tests | Feature | 28 | [#28](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/28) | 4 | yes | 2026-10-27 |
| F5.1 — Reports and audit-log query | Feature | 29 | [#29](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/29) | 5 | yes | 2026-10-27 |
| F5.2 — Final quality gates | Feature | 30 | [#30](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/30) | 5 | yes | 2026-10-27 |
| F6.1 — dotnet new template and start guide | Feature | 31 | [#31](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/31) | 6 | yes | 2026-10-27 |
| F7.1 — Release and operations notes | Feature | 32 | [#32](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/32) | 7 | yes | 2026-10-27 |
| F8.1 — Stretch stories (cut first) | Feature | 33 | [#33](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/33) | 8 | yes | 2026-10-27 |
| P-001 — Enabler: Solution skeleton and root files (slnx, global.json, Directory.*.props, nuget.config source mapping, BannedSymbols, .gitattributes); verify flagged package versions | User Story | 34 | [#34](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/34) | 9 | yes | 2026-10-27 |
| P-002 — Enabler: Compose chain secrets-init → postgres → migrator → api, Dockerfile api and migrator targets | User Story | 35 | [#35](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/35) | 9 | yes | 2026-10-27 |
| P-003 — Security follow-up: Compose hardening (localhost-only ports, Valkey ACL password, per-service secret mounts 0440, generated POSTGRES_PASSWORD, log rotation, dp-keys volume) | User Story | 36 | [#36](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/36) | 9 | yes | 2026-10-27 |
| P-004 — Security follow-up: DB roles, grants and timeouts (demoapi_migrator/demoapi_app, DELETE only on asp_net_user_roles, append-only audit_logs, statement/lock/idle timeouts, audit action CHECK incl. SessionsRevoked) | User Story | 37 | [#37](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/37) | 9 | yes | 2026-10-27 |
| P-005 — Docs: README quick start and CLAUDE.md (first version) | User Story | 38 | [#38](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/38) | 9 | yes | 2026-10-27 |
| P-006 — Docs: Requirements cleanup before build (C7, C9) | User Story | 39 | [#39](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/39) | 9 | yes | 2026-10-27 |
| P-007 — Spike SP-1: Compose chain on a fresh Windows clone (time-box 1 day) | User Story | 40 | [#40](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/40) | 9 | yes | 2026-10-27 |
| P-008 — Spike SP-4: EFCore.NamingConventions on EF Core 10, fallback own convention (time-box 0.5 day) | User Story | 41 | [#41](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/41) | 9 | yes | 2026-10-27 |
| P-009 — CI: Azure Pipelines Build and Test stages (locked restore, format check, warnings as errors, TRX, coverage and time-budget warnings) | User Story | 42 | [#42](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/42) | 10 | yes | 2026-10-27 |
| P-010 — CI: Supply-chain gates (gitleaks full history, nuget-license, vulnerable packages, CycloneDX SBOM, Trivy image scan, digest pins, pinned ubuntu-24.04) | User Story | 43 | [#43](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/43) | 10 | yes | 2026-10-27 |
| P-011 — CI: Branch policy on main (PR + green build, no direct push) and work-item linking | User Story | 44 | [#44](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/44) | 10 | yes | 2026-10-27 |
| P-012 — Test infra: Test projects and shared fixture (Testcontainers PostgreSQL 18.6 + Valkey 9.1.2, role script, migrator, Respawn, app role, ApiFactory, TestUsers, log capture, trait attributes) | User Story | 45 | [#45](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/45) | 11 | yes | 2026-10-27 |
| P-013 — Test infra: Architecture rules A1-A5, A10, A11 and BannedApiAnalyzers (each seen failing once) | User Story | 46 | [#46](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/46) | 11 | yes | 2026-10-27 |
| P-014 — Test infra: OpenAPI setup (Scalar in Development only), committed openapi/v1.json and drift test | User Story | 47 | [#47](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/47) | 11 | yes | 2026-10-27 |
| P-015 — ST-01 Authz matrix skeleton from EndpointDataSource (EP-23, EP-24, EP-56, EP-57; fails on unlisted endpoints) | User Story | 48 | [#48](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/48) | 11 | yes | 2026-10-27 |
| P-016 — ST-12 DB grant snapshot and role attributes | User Story | 49 | [#49](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/49) | 11 | yes | 2026-10-27 |
| P-017 — Spike SP-5: Test-time checks (Testcontainers pause/unpause; global FakeTimeProvider vs resilience delays) (time-box 0.5 day) | User Story | 50 | [#50](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/50) | 11 | yes | 2026-10-27 |
| P-018 — US-291 Enabler: Soft delete, audit interceptor and no hard delete (AC10 completed in S1b) | User Story | 51 | [#51](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/51) | 12 | yes | 2026-10-27 |
| P-019 — Enabler: Resilience (single EF retry layer, ITransactionRunner with ChangeTracker.Clear, connection-level DB breaker, Valkey 250 ms; tests 503 < 5 s, 10 × 409 keep breaker closed, forced transient retry) | User Story | 52 | [#52](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/52) | 12 | yes | 2026-10-27 |
| P-020 — US-102 Create a product | User Story | 53 | [#53](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/53) | 12 | yes | 2026-10-27 |
| P-021 — US-101 Get a product (HybridCache + Valkey L2) | User Story | 54 | [#54](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/54) | 12 | yes | 2026-10-27 |
| P-022 — US-218 Liveness check | User Story | 55 | [#55](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/55) | 12 | yes | 2026-10-27 |
| P-023 — US-219 Readiness check | User Story | 56 | [#56](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/56) | 12 | yes | 2026-10-27 |
| P-024 — Spike SP-2: Identity + audit interceptor in one transaction (forced rollback, ChangeTracker.Clear retry) (time-box 1 day) | User Story | 57 | [#57](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/57) | 12 | yes | 2026-10-27 |
| P-025 — Spike SP-3: HybridCache with Valkey (L2) down (time-box 0.5 day) | User Story | 58 | [#58](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/58) | 12 | yes | 2026-10-27 |
| P-026 — Enabler: JWT issuer, key ring with kid and bearer validation (HS256 allow-list, typ at+jwt, iss/aud/exp, 30 s skew) | User Story | 59 | [#59](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/59) | 13 | yes | 2026-10-27 |
| P-027 — Enabler: Refresh-token store (hash, rotation, family, 30-day cap, reuse revocation committed before 401) and revoke_refresh_tokens procedure (P9) | User Story | 60 | [#60](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/60) | 13 | yes | 2026-10-27 |
| P-028 — Enabler: Per-request security-stamp check (sst claim, 30 s in-process cache, fails closed) | User Story | 61 | [#61](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/61) | 13 | yes | 2026-10-27 |
| P-029 — Enabler: Password policy (15-128, local blocklist, PBKDF2 220,000 iterations, rehash on login) | User Story | 62 | [#62](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/62) | 13 | yes | 2026-10-27 |
| P-030 — Enabler: Enumeration defenses (equal work, response floor, password checked before confirmed/locked) | User Story | 63 | [#63](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/63) | 13 | yes | 2026-10-27 |
| P-031 — Enabler: Seed Admin at startup from configuration (no user story exists) | User Story | 64 | [#64](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/64) | 13 | yes | 2026-10-27 |
| P-032 — Enabler: Fake email sender with configured link base URL | User Story | 65 | [#65](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/65) | 13 | yes | 2026-10-27 |
| P-033 — US-001 Register as a customer | User Story | 66 | [#66](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/66) | 14 | yes | 2026-10-27 |
| P-034 — US-002 Confirm email | User Story | 67 | [#67](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/67) | 14 | yes | 2026-10-27 |
| P-035 — US-003 Resend confirmation link | User Story | 68 | [#68](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/68) | 14 | yes | 2026-10-27 |
| P-036 — US-004 Log in | User Story | 69 | [#69](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/69) | 14 | yes | 2026-10-27 |
| P-037 — US-005 Refresh tokens with rotation and reuse detection | User Story | 70 | [#70](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/70) | 14 | yes | 2026-10-27 |
| P-038 — US-006 Log out one session | User Story | 71 | [#71](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/71) | 14 | yes | 2026-10-27 |
| P-039 — US-007 Log out all sessions | User Story | 72 | [#72](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/72) | 14 | yes | 2026-10-27 |
| P-040 — US-008 Get current user | User Story | 73 | [#73](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/73) | 14 | yes | 2026-10-27 |
| P-041 — US-009 Change own password | User Story | 74 | [#74](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/74) | 14 | yes | 2026-10-27 |
| P-042 — US-100 List products | User Story | 75 | [#75](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/75) | 15 | yes | 2026-10-27 |
| P-043 — US-103 Update a product | User Story | 76 | [#76](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/76) | 15 | yes | 2026-10-27 |
| P-044 — US-104 Soft delete a product | User Story | 77 | [#77](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/77) | 15 | yes | 2026-10-27 |
| P-045 — US-105 Restore a product | User Story | 78 | [#78](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/78) | 15 | yes | 2026-10-27 |
| P-046 — US-290 Enabler: Paging, sorting, filtering and ProblemDetails convention | User Story | 79 | [#79](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/79) | 16 | yes | 2026-10-27 |
| P-047 — US-292 Enabler: Rate limiting, security headers and CORS (reports concurrency checked in S4) | User Story | 80 | [#80](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/80) | 16 | yes | 2026-10-27 |
| P-048 — Enabler: Observability (OTel traces and metrics, logs with traceId, email masking, query strings out of logs, security-event IDs, Aspire dashboard profile) | User Story | 81 | [#81](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/81) | 16 | yes | 2026-10-27 |
| P-049 — Test infra: Architecture rules A6-A9 incl. EndpointDoDTests | User Story | 82 | [#82](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/82) | 17 | yes | 2026-10-27 |
| P-050 — Test infra: AuditCoverageTests, EndpointPolicyTests and ApiFilesCoverageTests | User Story | 83 | [#83](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/83) | 17 | yes | 2026-10-27 |
| P-051 — Test infra: Traceability report (AC IDs vs test tags, CI artifact, 0 uncovered Must ACs per slice) | User Story | 84 | [#84](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/84) | 17 | yes | 2026-10-27 |
| P-052 — API files: .http structure per feature and Postman collection generated from OpenAPI (environments without secrets) | User Story | 85 | [#85](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/85) | 17 | yes | 2026-10-27 |
| P-053 — Perf: k6 run 1 (NFR-01 cached, uncached, Valkey down) recorded in 07-quality/perf-results.md | User Story | 86 | [#86](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/86) | 17 | yes | 2026-10-27 |
| P-054 — Review gate: Fresh-context security review of JwtTokenIssuer and RefreshTokenStore (no open High) | User Story | 87 | [#87](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/87) | 17 | yes | 2026-10-27 |
| P-055 — ST-04 JWT negative tests | User Story | 88 | [#88](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/88) | 18 | yes | 2026-10-27 |
| P-056 — ST-05 Refresh tests (rotation, reuse commits before 401, parallel race, 30-day cap) | User Story | 89 | [#89](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/89) | 18 | yes | 2026-10-27 |
| P-057 — ST-06 Security-stamp tests (password change, logout-all) | User Story | 90 | [#90](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/90) | 18 | yes | 2026-10-27 |
| P-058 — ST-07 Lockout tests (5 failures, EP-09 counts, automatic lockout keeps sessions) | User Story | 91 | [#91](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/91) | 18 | yes | 2026-10-27 |
| P-059 — ST-08 Enumeration tests (same status, body, headers; timing floor; 10 calls per path) | User Story | 92 | [#92](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/92) | 18 | yes | 2026-10-27 |
| P-060 — ST-09 Rate-limit tests (each policy, shared auth-strict bucket, 429 before 401, X-Forwarded-For ignored; verify TestServer client IP) | User Story | 93 | [#93](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/93) | 18 | yes | 2026-10-27 |
| P-061 — ST-10 Production-environment tests (headers, HSTS, CORS, /scalar and /openapi 404, no stack trace) | User Story | 94 | [#94](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/94) | 18 | yes | 2026-10-27 |
| P-062 — ST-11 Injection tests for products sort and search | User Story | 95 | [#95](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/95) | 18 | yes | 2026-10-27 |
| P-063 — ST-14 Log hygiene tests (no password, JWT, refresh token, unmasked email) | User Story | 96 | [#96](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/96) | 18 | yes | 2026-10-27 |
| P-064 — ST-15 Cache tests (scope by role, includeDeleted bypass, search lists not cached) | User Story | 97 | [#97](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/97) | 18 | yes | 2026-10-27 |
| P-065 — ST-16 Limits tests (413/415, pageSize, 1024-char password) | User Story | 98 | [#98](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/98) | 18 | yes | 2026-10-27 |
| P-066 — ST-17 Confirm-token lifetime (24 h) and key-ring restart tests | User Story | 99 | [#99](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/99) | 18 | yes | 2026-10-27 |
| P-067 — US-010 Request a password reset | User Story | 100 | [#100](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/100) | 19 | yes | 2026-10-27 |
| P-068 — US-011 Reset password with a token | User Story | 101 | [#101](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/101) | 19 | yes | 2026-10-27 |
| P-069 — US-012 List users | User Story | 102 | [#102](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/102) | 20 | yes | 2026-10-27 |
| P-070 — US-013 Get a user | User Story | 103 | [#103](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/103) | 20 | yes | 2026-10-27 |
| P-071 — US-014 Lock a user | User Story | 104 | [#104](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/104) | 20 | yes | 2026-10-27 |
| P-072 — US-015 Unlock a user | User Story | 105 | [#105](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/105) | 20 | yes | 2026-10-27 |
| P-073 — US-016 Assign the Admin role | User Story | 106 | [#106](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/106) | 20 | yes | 2026-10-27 |
| P-074 — US-017 Remove the Admin role | User Story | 107 | [#107](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/107) | 20 | yes | 2026-10-27 |
| P-075 — US-018 List roles | User Story | 108 | [#108](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/108) | 20 | yes | 2026-10-27 |
| P-076 — US-019 Create an Admin user | User Story | 109 | [#109](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/109) | 20 | yes | 2026-10-27 |
| P-077 — US-107 Admin creates a customer | User Story | 110 | [#110](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/110) | 21 | yes | 2026-10-27 |
| P-078 — US-108 List customers | User Story | 111 | [#111](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/111) | 21 | yes | 2026-10-27 |
| P-079 — US-109 Get a customer | User Story | 112 | [#112](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/112) | 21 | yes | 2026-10-27 |
| P-080 — US-110 Update a customer | User Story | 113 | [#113](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/113) | 21 | yes | 2026-10-27 |
| P-081 — US-111 Delete and anonymize a customer | User Story | 114 | [#114](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/114) | 21 | yes | 2026-10-27 |
| P-082 — Enabler: OwnedBy(ICurrentUser) helper and /me pattern | User Story | 115 | [#115](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/115) | 22 | yes | 2026-10-27 |
| P-083 — US-113 Read own profile | User Story | 116 | [#116](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/116) | 22 | yes | 2026-10-27 |
| P-084 — US-114 Update own profile | User Story | 117 | [#117](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/117) | 22 | yes | 2026-10-27 |
| P-085 — US-115 Erase own account | User Story | 118 | [#118](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/118) | 22 | yes | 2026-10-27 |
| P-086 — CI: TemplateStrip stage (delete sample-domain folders, apply Customer-leak edits, build and test the copy) | User Story | 119 | [#119](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/119) | 23 | yes | 2026-10-27 |
| P-087 — Test infra: Architecture rules A12, A13 and C10 test (Admin get of a deleted customer incl. email) | User Story | 120 | [#120](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/120) | 23 | yes | 2026-10-27 |
| P-088 — CI: Weekly scheduled pipeline (ST-08 at 30 calls per path) and Renovate updates | User Story | 121 | [#121](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/121) | 23 | yes | 2026-10-27 |
| P-089 — ST-02 BOLA tests for /me routes (EP-35 to EP-37; fails on an uncovered /me/{id} route) | User Story | 122 | [#122](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/122) | 24 | yes | 2026-10-27 |
| P-090 — ST-03 Mass-assignment tests and per-role DTO snapshots | User Story | 123 | [#123](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/123) | 24 | yes | 2026-10-27 |
| P-091 — ST-13 Erasure test (every PII field, all tables, captured logs, Valkey keys → 0 matches) | User Story | 124 | [#124](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/124) | 24 | yes | 2026-10-27 |
| P-092 — ST-19 Security-event tests (role assign, Admin create, lockout, refresh reuse) | User Story | 125 | [#125](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/125) | 24 | yes | 2026-10-27 |
| P-093 — Security tests S2 extensions: ST-06 (lock, role remove, erase), ST-07 (EP-37, reset), ST-08 (forgot), ST-14 (PII), ST-17 (reset 1 h, single use) | User Story | 126 | [#126](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/126) | 24 | yes | 2026-10-27 |
| P-094 — Enabler: Order aggregate, line items, status machine, OrderStatusHistory and Touch() for ETags | User Story | 127 | [#127](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/127) | 25 | yes | 2026-10-27 |
| P-095 — Enabler: Stock locking with SELECT … FOR UPDATE in product ID order inside ITransactionRunner, and the Pending-order cap | User Story | 128 | [#128](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/128) | 25 | yes | 2026-10-27 |
| P-096 — US-200 Admin creates an order for a customer | User Story | 129 | [#129](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/129) | 26 | yes | 2026-10-27 |
| P-097 — US-201 Admin lists orders | User Story | 130 | [#130](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/130) | 26 | yes | 2026-10-27 |
| P-098 — US-202 Admin gets an order with items and history | User Story | 131 | [#131](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/131) | 26 | yes | 2026-10-27 |
| P-099 — US-203 Admin replaces items of a Pending order (first cut candidate in S3) | User Story | 132 | [#132](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/132) | 26 | yes | 2026-10-27 |
| P-100 — US-204 Admin moves an order to the next status | User Story | 133 | [#133](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/133) | 26 | yes | 2026-10-27 |
| P-101 — US-205 Admin cancels an order | User Story | 134 | [#134](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/134) | 26 | yes | 2026-10-27 |
| P-102 — US-207 Customer places an own order | User Story | 135 | [#135](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/135) | 27 | yes | 2026-10-27 |
| P-103 — US-208 Customer lists own orders | User Story | 136 | [#136](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/136) | 27 | yes | 2026-10-27 |
| P-104 — US-209 Customer gets an own order | User Story | 137 | [#137](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/137) | 27 | yes | 2026-10-27 |
| P-105 — US-210 Customer cancels an own Pending order | User Story | 138 | [#138](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/138) | 27 | yes | 2026-10-27 |
| P-106 — Test: Concurrency suite (about 9 scenarios; 20 local loop runs before merge) | User Story | 139 | [#139](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/139) | 28 | yes | 2026-10-27 |
| P-107 — Test data: Sample-data seeder (1,000 products, 1,000 customers, 10,000 orders) for k6 | User Story | 140 | [#140](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/140) | 28 | yes | 2026-10-27 |
| P-108 — Optional: Stryker.NET one-off mutation run on Domain (skip if xUnit v3 is not supported) | User Story | 141 | [#141](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/141) | 28 | yes | 2026-10-27 |
| P-109 — ST-18 Timeout tests (lock wait ends within budget) | User Story | 142 | [#142](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/142) | 28 | yes | 2026-10-27 |
| P-110 — Security tests S3 extensions: ST-02 (EP-47, EP-48), ST-03 (customerId, status, unitPrice), ST-15 (poisoned Valkey price ignored), ST-16 (lines, quantity, Pending cap) | User Story | 143 | [#143](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/143) | 28 | yes | 2026-10-27 |
| P-111 — US-212 Order summary report (view) | User Story | 144 | [#144](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/144) | 29 | yes | 2026-10-27 |
| P-112 — US-213 Customer order history (function) | User Story | 145 | [#145](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/145) | 29 | yes | 2026-10-27 |
| P-113 — US-214 Sales report by date range | User Story | 146 | [#146](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/146) | 29 | yes | 2026-10-27 |
| P-114 — US-215 Low-stock report | User Story | 147 | [#147](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/147) | 29 | yes | 2026-10-27 |
| P-115 — US-216 Top products report | User Story | 148 | [#148](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/148) | 29 | yes | 2026-10-27 |
| P-116 — US-217 Query the audit log | User Story | 149 | [#149](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/149) | 29 | yes | 2026-10-27 |
| P-117 — Perf: k6 run 2 (NFR-01 + NFR-02) and DB timeout and report-concurrency tuning | User Story | 150 | [#150](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/150) | 30 | yes | 2026-10-27 |
| P-118 — CI: ApiFiles stage (httpyac .http run and generated Postman collection against a running API) | User Story | 151 | [#151](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/151) | 30 | yes | 2026-10-27 |
| P-119 — Security tests S4 extensions: ST-09 (reports concurrency 429), ST-11 (Dapper SQL architecture rule), ST-16 (report range), ST-18 (slow query), ST-19 (audit-log query) | User Story | 152 | [#152](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/152) | 30 | yes | 2026-10-27 |
| P-120 — Template: dotnet new package (template.json, sourceName DemoApi, kept parts per strip path, single Initial migration) | User Story | 153 | [#153](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/153) | 31 | yes | 2026-10-27 |
| P-121 — Template: "Start a new API" guide (strip steps, Customer leaks, option to merge Domain + Application) | User Story | 154 | [#154](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/154) | 31 | yes | 2026-10-27 |
| P-122 — Template: Template suites green from dotnet new; TemplateStrip job switched to dotnet new install | User Story | 155 | [#155](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/155) | 31 | yes | 2026-10-27 |
| P-123 — Template: Bootstrap trial and "hours to bootstrap" baseline | User Story | 156 | [#156](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/156) | 31 | yes | 2026-10-27 |
| P-124 — Spike SP-6: .NET 11 compatibility check (build and tests on the .NET 11 SDK, no retarget) | User Story | 157 | [#157](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/157) | 31 | yes | 2026-10-27 |
| P-125 — Release: Fresh-clone rebuild timed (≤ 15 min, 0 manual steps) and final README | User Story | 158 | [#158](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/158) | 32 | yes | 2026-10-27 |
| P-126 — Release: Release checklist and POC sign-off (North Star 100%, test-strategy §6 minimums) | User Story | 159 | [#159](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/159) | 32 | yes | 2026-10-27 |
| P-127 — Ops: Light runbook (health, logs, regenerating dev secrets, HS256 key rotation) | User Story | 160 | [#160](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/160) | 32 | yes | 2026-10-27 |
| P-128 — Docs: Before-production register (threat model §7.2 items 1-12, retention plan §4.3) for the client project | User Story | 161 | [#161](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/161) | 32 | yes | 2026-10-27 |
| P-129 — US-020 Revoke a user's sessions | User Story | 162 | [#162](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/162) | 33 | yes | 2026-10-27 |
| P-130 — US-021 Send a password-reset link for a user | User Story | 163 | [#163](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/163) | 33 | yes | 2026-10-27 |
| P-131 — US-106 Adjust product stock | User Story | 164 | [#164](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/164) | 33 | yes | 2026-10-27 |
| P-132 — US-112 Restore a customer (incl. restored-customer lock type and email change, D14) | User Story | 165 | [#165](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/165) | 33 | yes | 2026-10-27 |
| P-133 — US-206 Admin soft deletes a Cancelled order | User Story | 166 | [#166](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/166) | 33 | yes | 2026-10-27 |
| P-134 — US-211 Customer edits items of an own Pending order | User Story | 167 | [#167](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/167) | 33 | yes | 2026-10-27 |
| P-135 — Idempotency-Key on order create (no user story exists) | User Story | 168 | [#168](https://dev.azure.com/codewithelmor/Demo-API/_workitems/edit/168) | 33 | yes | 2026-10-27 |
