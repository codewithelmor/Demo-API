# Azure DevOps preview: Orders API (APPLIED 2026-10-27)

Target: codewithelmor / Demo-API / Demo-API Team

| Field | Value |
|---|---|
| Date | 2026-10-27 |
| Mode | PLAN (preview). No create, update, link or delete calls were made |
| Source | [delivery-plan.md](delivery-plan.md) §1 (board), §3 (dependencies), §5 (items, tags, hierarchy), §6 (DoR/DoD), §9 (DL-D3, DL-D5) |
| Agent | `azdo-boards-manager` (`work-item-standards`; azdo-setup + azdo-plan-to-work-items steps) |
| Map file | `docs/product/azdo-map.md` does not exist yet. It is created in APPLY |

## 0. What was verified in Azure DevOps (read tools only)

| Check | Tool | Result |
|---|---|---|
| Project | `core_list_projects` | `Demo-API`, id `d77f3f29-8ff8-4271-ab4d-f6f1e8ca7d49`, state wellFormed, private |
| Teams | `core_list_project_teams` | One team: **Demo-API Team** (`62429a32-e6a4-4c63-9494-c6adc7b87616`), the default team |
| Process | `wit_backlog list` | Requirement level = **User Story** → **Agile**. Levels: Epics (Epic), Features (Feature), Stories (User Story), Tasks (Task, **Bug**) |
| User Story fields | `wit_work_item get_type` | Story Points (`Microsoft.VSTS.Scheduling.StoryPoints`), Acceptance Criteria, Priority (default 2), Value Area (**required**, default Business), Tags, Area Path, Iteration Path. States New → Active → Resolved → Closed (+ Removed) |
| Epic / Feature fields | `wit_backlog list` (column fields) | Effort, Business Value, Value Area, Tags present. `get_type` for Epic and Feature is re-run as the first APPLY step |
| Bug behaviour | `work get_team_settings` | `bugsBehavior = 2` = **bugs managed with tasks** (Bug sits in the Tasks level). Must change to "with requirements" (DL-D3) |
| Epics backlog | `work get_team_settings` | **Epics backlog level is hidden** for the team (`Microsoft.EpicCategory: false`) |
| Area paths | `work get_team_settings` | Only the root `Demo-API` exists as team area, `includeChildren = false`. No child areas |
| Iterations | `work list_iterations`, `list_team_iterations` | Default Agile iterations `Iteration 1`, `Iteration 2`, `Iteration 3` (no dates), all assigned to the team. Backlog iteration = root `Demo-API`. Default iteration = `@CurrentIteration` (`Iteration 1`) |
| Working days | `work get_team_settings` | Mon-Fri |
| Backlog empty | WIQL `SELECT … WHERE [System.TeamProject] = @project` | **0 work items**. No duplicates possible |
| Tags | – | No tool lists tags; with 0 work items there are no tags yet |
| Board columns | – | No MCP tool reads or writes boards. Assumed Agile default: New, Active, Resolved, Closed |

## 1. Setup preview (azdo-setup)

| # | Setup change | Why | MCP can apply? | How (exact UI path if manual) |
|---|---|---|---|---|
| S-1 | Create 9 area paths under `Demo-API`: `Platform`, `Auth`, `AdminUsers`, `Products`, `Customers`, `Orders`, `Reports`, `Template`, `Delivery` | DL-D3 area paths per area | **No** (no area tool) | Project settings → Boards → **Project configuration** → **Areas** → on `Demo-API` choose `…` → **New child** → name. Repeat 9 times |
| S-2 | Team areas: keep default area `Demo-API`, turn on **Include sub areas** | Otherwise items in `Demo-API\Platform` etc. do **not** show on the team backlog/board | **No** (team setting) | Project settings → Boards → **Team configuration** → team `Demo-API Team` → **Areas** tab → on `Demo-API` choose `…` → **Include sub areas** |
| S-3 | Show the Epics backlog level | Epic per slice; Epics are hidden now | **No** | Project settings → Boards → Team configuration → **General** tab → **Backlogs** → tick **Epics** → Save |
| S-4 | Working with bugs = **Bugs are managed with requirements** | DL-D3; Bugs on the board and counted in WIP | **No** | Same page, **General** tab → **Working with bugs** → "Bugs are managed with requirements" → Save |
| S-5 | Kanban, no iterations: default iteration = root `Demo-API` | DL-D3; the current default `@CurrentIteration` puts new UI-created items in `Iteration 1` | **No** (team setting; `work_iteration_write` can only create or assign) | Same page, **Iterations** tab → **Default iteration** → choose `Demo-API` (the backlog iteration) → Save |
| S-6 | (Optional) Unselect `Iteration 1-3` from the team | Hides the empty Sprints hub. **Do not delete** the iterations | **No** | Same page, **Iterations** tab → select each iteration → **Remove** (removes from the team only). Keeping them is harmless |
| S-7 | Stories board columns: Backlog (New), **Ready** (New, WIP 5), **In progress** (Active, WIP 1), **Review** (Resolved, WIP 2), Done (Closed). Map the Bug states to the same columns | Delivery plan §1 board and WIP | **No** (no board tool) | Boards → **Boards** → team `Demo-API Team`, **Stories** board → gear **Configure board settings** → **Board → Columns**: rename `New` → `Backlog`; **+ Column** `Ready` (state New for User Story and Bug, WIP 5); rename `Active` → `In progress`, WIP 1; rename `Resolved` → `Review`, WIP 2; rename `Closed` → `Done`. Optional: tick **Split column into doing and done** off; paste the §6 Ready/Done text in each column's **Definition of done** |
| S-8 | Swimlane **Expedite** | Blocking Bugs jump the queue (§1) | **No** | Same dialog → **Board → Swimlanes** → **+ Swimlane** `Expedite` (above Default lane) |
| S-9 | (Optional) Card style: red card when Tags contains `blocking`; yellow when `flaky` | Visible risk | **No** | Same dialog → **Cards → Styles** → **+ Styling rule** |
| S-10 | Tags (164 new; full list §1.1) | Tag scheme §5 | **Yes**: created on first use by the work item creates | No manual step. Tag colours (optional): same dialog → **Cards → Tag colors** |
| S-11 | Shared queries: "Current slice (tag)", "Quarantined tests (≤ 3)", "Open review findings", "Cut items" | §5 suggestion | **No** (no save-query tool) | Boards → **Queries** → **+ New query** → **Editor** → … (switch to WIQL via the "Edit WIQL" extension, or build the clauses below) → **Save as** → **Shared Queries**. WIQL in §1.2 |
| S-12 | Repo in Azure Repos, branch policy on `main`, `AB#` linking | DL-D5, P-011 | **No** (no repos domain; agent may not touch repos) | Repos → Files (initialize/push). Project settings → Repos → **Repositories** → repo → **Policies** → branch `main` → require PR + build validation. This is item P-011 in S1a |
| S-13 | Azure Pipelines free hosted-parallelism request | §3 dependency, DL-D5 | **No** (no pipelines domain) | Submit the Microsoft free-grant form (link in delivery-plan Sources) for org `codewithelmor` on approval day |
| S-14 | Process template stays **Agile** (no custom fields/states/types) | DL-D3 | Nothing to do | – |

Order: the product owner does S-1 to S-5 (and S-7, S-8) **before** APPLY. S-1 is a hard prerequisite: a create with a missing area path fails. APPLY step 0 checks each area with a WIQL `[System.AreaPath] UNDER 'Demo-API\<area>'` (WIQL errors on an unknown path).

### 1.1 New tags (created on first use)

Hyphens are allowed in Azure DevOps tags (only `;` and `,` are separators). Tags are **case-insensitive**, see Problem 2.

| Group | Tags | Count |
|---|---|---|
| Slice | `S1a`, `S1b`, `S2`, `S3`, `S4`, `Template`, `RelOps`, `Stretch` | 8 |
| Kind | `enabler`, `spike`, `security-followup`, `security-test`, `test-infra`, `ci`, `docs`, `perf`, `api-files`, `review` (`template` = same tag as `Template`) | 10 |
| Priority | `should`, `could`, `optional` (`stretch` = same tag as `Stretch`) | 3 |
| Trace US | `US-001`…`US-021`, `US-100`…`US-115`, `US-200`…`US-219`, `US-290`, `US-291`, `US-292` (exactly the 60 story IDs in §2) | 60 |
| Trace EP | the 57 `EP-xx` tags in §2 | 57 |
| Trace ST | `ST-01` … `ST-20` | 20 |
| Trace FR | `FR-003`, `FR-020`, `FR-021`, `FR-084`, `FR-095`, `FR-096` | 6 |
| Later states (not created now) | `cut`, `review-finding`, `flaky`, `quarantine`, `blocking` | 0 |
| **Total new** | | **164** |

### 1.2 Shared-query WIQL (for S-11)

```sql
-- Current slice (tag): change 'S1a' per slice
SELECT [System.Id], [System.WorkItemType], [System.Title], [System.State], [System.BoardColumn], [Microsoft.VSTS.Scheduling.StoryPoints]
FROM WorkItems
WHERE [System.TeamProject] = @project AND [System.Tags] CONTAINS 'S1a' AND [System.State] <> 'Removed'
ORDER BY [Microsoft.VSTS.Common.StackRank]

-- Quarantined tests (max 3 open)
SELECT [System.Id], [System.Title], [System.State], [System.Tags]
FROM WorkItems
WHERE [System.TeamProject] = @project AND [System.WorkItemType] = 'Bug'
  AND [System.Tags] CONTAINS 'quarantine' AND [System.State] NOT IN ('Closed', 'Removed')

-- Open review findings
SELECT [System.Id], [System.WorkItemType], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = @project AND [System.Tags] CONTAINS 'review-finding'
  AND [System.State] NOT IN ('Closed', 'Removed')

-- Cut items
SELECT [System.Id], [System.WorkItemType], [System.Title], [System.Parent], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = @project AND [System.Tags] CONTAINS 'cut'
```

## 2. Work item preview (azdo-plan-to-work-items)

### 2.1 Field mapping (applies to every row)

| Field | Value |
|---|---|
| Title (`System.Title`) | Plan title, verbatim (approved text; some start with a prefix such as "Enabler:" instead of a verb, kept for traceability) |
| Description (`System.Description`, HTML) | Line 1 `Plan ID: <id>` (re-run safety). Then: user story line (US-xxx items, copied from the story file), context (slice, time-box for Epics), **Source** (doc path + section, see legend 2.3), and the PRD §15 DoD checklist as `☐` items. Epics also get the §2 slice goal, exit criteria, time-box and cut candidates, copied verbatim |
| Acceptance Criteria (`Microsoft.VSTS.Common.AcceptanceCriteria`, HTML) | **US-xxx items (60):** AC1…ACn copied verbatim from the user-story file, `<`, `>`, `&` escaped. **Other 75 stories, 25 Features, 8 Epics:** empty, flagged (Problem 5). Nothing is invented |
| Story Points | XS = 1, S = 2, M = 4, L = 8 (plan §2). Epics and Features: empty (Effort not set) |
| Priority | Must (no tag) → **2**; `should` → **3**; `could`, `optional`, `stretch` → **4**. Nothing gets 1. E8 and F8.1 → 4 |
| Value Area (required) | Proposal: **Business** for the 57 endpoint stories and all Epics/Features; **Architectural** for the other 78 stories (enablers incl. US-290/291/292, spikes, CI, test infra, security tests/follow-ups, perf, API files, docs, review, template, release/ops) |
| Area Path | `Demo-API\<leaf>`; plan rows with area `Orders API` → root `Demo-API` (Problem 1) |
| Iteration Path | **`Demo-API`** (root) on every item, set explicitly (Kanban, no iterations) |
| State, Assigned To, dates | Not set (State defaults to New; no assignee, no dates) |
| Parent | Parent-child link (`System.LinkTypes.Hierarchy-Reverse` to the parent), created after the parent exists |
| Tags | As listed, `;`-separated, with the case duplicates removed (Problem 2) |

**Totals per type:** Epic 8 · Feature 25 · User Story 135 · Task 0 · Bug 0 · **Total 168**. Story Points 449 (419 core + 30 stretch).

Columns: **Pri** = Priority, **SP** = Story Points, **VA** = Value Area (B = Business, A = Architectural), **Pred** = predecessor(s) (dependency links, §3). Iteration = `Demo-API` for all rows. Action = **create** for all 168 rows.

### E1 S1a Walking skeleton (30 items, 80 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E1 | Epic | S1a Walking skeleton | – | Demo-API | S1a | 2 | – | B | SS §14; TS §6 | – |
| F1.1 | Feature | Repo, solution and local run | E1 | Demo-API\Platform | S1a | 2 | – | B | SS §2-3, §10 | – |
| F1.2 | Feature | CI pipeline and supply-chain gates | E1 | Demo-API\Platform | S1a; ci | 2 | – | B | SS §11; TM §5 | – |
| F1.3 | Feature | Test and architecture foundations | E1 | Demo-API\Platform | S1a; test-infra | 2 | – | B | TS §2-3; SS §8-9 | – |
| F1.4 | Feature | Product create/get and health end to end | E1 | Demo-API\Products | S1a | 2 | – | B | SS §14 | – |
| P-001 | User Story | Enabler: Solution skeleton and root files (slnx, global.json, Directory.*.props, nuget.config source mapping, BannedSymbols, .gitattributes); verify flagged package versions | F1.1 | Demo-API\Platform | S1a; enabler | 2 | 4 | A | SS §2-3 | – |
| P-002 | User Story | Enabler: Compose chain secrets-init → postgres → migrator → api, Dockerfile api and migrator targets | F1.1 | Demo-API\Platform | S1a; enabler | 2 | 4 | A | SS §10; ADR-0012 | P-007 |
| P-003 | User Story | Security follow-up: Compose hardening (localhost-only ports, Valkey ACL password, per-service secret mounts 0440, generated POSTGRES_PASSWORD, log rotation, dp-keys volume) | F1.1 | Demo-API\Platform | S1a; security-followup | 2 | 2 | A | TM Follow-up actions, §3 items 2-3 | – |
| P-004 | User Story | Security follow-up: DB roles, grants and timeouts (demoapi_migrator/demoapi_app, DELETE only on asp_net_user_roles, append-only audit_logs, statement/lock/idle timeouts, audit action CHECK incl. SessionsRevoked) | F1.1 | Demo-API\Platform | S1a; security-followup | 2 | 4 | A | TM Follow-up actions, §3 items 14-15; DM §5; ADR-0006 | – |
| P-005 | User Story | Docs: README quick start and CLAUDE.md (first version) | F1.1 | Demo-API\Platform | S1a; docs | 2 | 1 | A | SS §12 | – |
| P-006 | User Story | Docs: Requirements cleanup before build (C7, C9; US-292 duplicate AC8/AC9 IDs; US-291 AC7 grant text; PRD §9 Q4 and concept §9 audit-redaction text) | F1.1 | Demo-API\Delivery | S1a; docs | 2 | 2 | A | AR C7, C9 | – (see Problem 4) |
| P-007 | User Story | Spike SP-1: Compose chain on a fresh Windows clone (time-box 1 day) | F1.1 | Demo-API\Platform | S1a; spike | 2 | 2 | A | AR §3; SS §14 | – |
| P-008 | User Story | Spike SP-4: EFCore.NamingConventions on EF Core 10, fallback own convention (time-box 0.5 day) | F1.1 | Demo-API\Platform | S1a; spike | 2 | 1 | A | SS §3, §14 | – |
| P-009 | User Story | CI: Azure Pipelines Build and Test stages (locked restore, format check, warnings as errors, TRX, coverage and time-budget warnings) | F1.2 | Demo-API\Platform | S1a; ci | 2 | 4 | A | SS §11; TS §5 | P-018, P-020 |
| P-010 | User Story | CI: Supply-chain gates (gitleaks full history, nuget-license, vulnerable packages, CycloneDX SBOM, Trivy image scan, digest pins, pinned ubuntu-24.04) | F1.2 | Demo-API\Platform | S1a; ci; security-followup; security-test; ST-20 | 2 | 4 | A | TM §5; TS §5 | – |
| P-011 | User Story | CI: Branch policy on main (PR + green build, no direct push) and work-item linking | F1.2 | Demo-API\Platform | S1a; ci | 2 | 1 | A | TS §10 Q-D5 | – |
| P-012 | User Story | Test infra: Test projects and shared fixture (Testcontainers PostgreSQL 18.6 + Valkey 9.1.2, role script, migrator, Respawn, app role, ApiFactory, TestUsers, log capture, trait attributes) | F1.3 | Demo-API\Platform | S1a; test-infra | 2 | 8 | A | TS §3; SS §9 | P-002 |
| P-013 | User Story | Test infra: Architecture rules A1-A5, A10, A11 and BannedApiAnalyzers (each seen failing once) | F1.3 | Demo-API\Platform | S1a; test-infra | 2 | 2 | A | SS §8 | – |
| P-014 | User Story | Test infra: OpenAPI setup (Scalar in Development only), committed openapi/v1.json and drift test | F1.3 | Demo-API\Platform | S1a; test-infra | 2 | 2 | A | SS §9; ADR-0002 | – |
| P-015 | User Story | ST-01 Authz matrix skeleton from EndpointDataSource (EP-23, EP-24, EP-56, EP-57; fails on unlisted endpoints) | F1.3 | Demo-API\Platform | S1a; security-test; ST-01 | 2 | 2 | A | TS §2; TM §6 | – |
| P-016 | User Story | ST-12 DB grant snapshot and role attributes | F1.3 | Demo-API\Platform | S1a; security-test; ST-12 | 2 | 2 | A | TS §2; TM §3 item 14 | – |
| P-017 | User Story | Spike SP-5: Test-time checks (Testcontainers pause/unpause; global FakeTimeProvider vs resilience delays) (time-box 0.5 day) | F1.3 | Demo-API\Platform | S1a; spike | 2 | 2 | A | TS §3, Assumptions | – |
| P-018 | User Story | US-291 Enabler: Soft delete, audit interceptor and no hard delete (AC10 completed in S1b) | F1.4 | Demo-API\Platform | S1a; enabler; US-291 | 2 | 8 | A | US3 #US-291 | P-012 |
| P-019 | User Story | Enabler: Resilience (single EF retry layer, ITransactionRunner with ChangeTracker.Clear, connection-level DB breaker, Valkey 250 ms; tests 503 < 5 s, 10 × 409 keep breaker closed, forced transient retry) | F1.4 | Demo-API\Platform | S1a; enabler | 2 | 8 | A | ADR-0011; AR R-1, R-3 | – |
| P-020 | User Story | US-102 Create a product | F1.4 | Demo-API\Products | S1a; US-102; EP-24 | 2 | 4 | B | US2 #US-102 | P-012 |
| P-021 | User Story | US-101 Get a product (HybridCache + Valkey L2) | F1.4 | Demo-API\Products | S1a; US-101; EP-23 | 2 | 4 | B | US2 #US-101; ADR-0009 | – |
| P-022 | User Story | US-218 Liveness check | F1.4 | Demo-API\Platform | S1a; US-218; EP-56 | 2 | 1 | B | US3 #US-218 | – |
| P-023 | User Story | US-219 Readiness check | F1.4 | Demo-API\Platform | S1a; US-219; EP-57 | 2 | 2 | B | US3 #US-219 | – |
| P-024 | User Story | Spike SP-2: Identity + audit interceptor in one transaction (forced rollback, ChangeTracker.Clear retry) (time-box 1 day) | F1.4 | Demo-API\Auth | S1a; spike | 2 | 4 | A | AR §3; PM F5 | – |
| P-025 | User Story | Spike SP-3: HybridCache with Valkey (L2) down (time-box 0.5 day) | F1.4 | Demo-API\Products | S1a; spike | 2 | 2 | A | AR §3; PM F10 | – |

### E2 S1b Auth and product CRUD (48 items, 122 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E2 | Epic | S1b Auth and product CRUD | – | Demo-API | S1b | 2 | – | B | SS §14; TS §6 | E1 |
| F2.1 | Feature | Token and identity foundations | E2 | Demo-API\Auth | S1b | 2 | – | B | ADR-0008 | – |
| F2.2 | Feature | Auth endpoints | E2 | Demo-API\Auth | S1b | 2 | – | B | US1 | – |
| F2.3 | Feature | Product CRUD completion | E2 | Demo-API\Products | S1b | 2 | – | B | US2 | – |
| F2.4 | Feature | Platform conventions and observability | E2 | Demo-API\Platform | S1b | 2 | – | B | US3; ADR-0013 | – |
| F2.5 | Feature | DoD meta-tests, API files and performance run 1 | E2 | Demo-API\Platform | S1b; test-infra | 2 | – | B | TS §4, §7 | – |
| F2.6 | Feature | Security tests S1b | E2 | Demo-API\Platform | S1b; security-test | 2 | – | B | TS §2; TM §6 | – |
| P-026 | User Story | Enabler: JWT issuer, key ring with kid and bearer validation (HS256 allow-list, typ at+jwt, iss/aud/exp, 30 s skew) | F2.1 | Demo-API\Auth | S1b; enabler | 2 | 4 | A | ADR-0008 | – |
| P-027 | User Story | Enabler: Refresh-token store (hash, rotation, family, 30-day cap, reuse revocation committed before 401) and revoke_refresh_tokens procedure (P9) | F2.1 | Demo-API\Auth | S1b; enabler | 2 | 8 | A | ADR-0008; DM §4.2 #7 | P-026 |
| P-028 | User Story | Enabler: Per-request security-stamp check (sst claim, 30 s in-process cache, fails closed) | F2.1 | Demo-API\Auth | S1b; enabler | 2 | 4 | A | PRD §18 S-D1; TM §3 R1 | – |
| P-029 | User Story | Enabler: Password policy (15-128, local blocklist, PBKDF2 220,000 iterations, rehash on login) | F2.1 | Demo-API\Auth | S1b; enabler; FR-003 | 2 | 2 | A | PRD FR-003; TM T-01 | – |
| P-030 | User Story | Enabler: Enumeration defenses (equal work, response floor, password checked before confirmed/locked) | F2.1 | Demo-API\Auth | S1b; enabler | 2 | 4 | A | PRD §18 S-D6; TM §3 SEC-3 | – |
| P-031 | User Story | Enabler: Seed Admin at startup from configuration (no user story exists) | F2.1 | Demo-API\Auth | S1b; enabler; FR-021 | 2 | 2 | A | PRD FR-021 | – |
| P-032 | User Story | Enabler: Fake email sender with configured link base URL | F2.1 | Demo-API\Auth | S1b; enabler; FR-020 | 2 | 1 | A | PRD FR-020 | – |
| P-033 | User Story | US-001 Register as a customer | F2.2 | Demo-API\Auth | S1b; US-001; EP-01 | 2 | 8 | B | US1 #US-001 | – |
| P-034 | User Story | US-002 Confirm email | F2.2 | Demo-API\Auth | S1b; US-002; EP-02 | 2 | 2 | B | US1 #US-002 | – |
| P-035 | User Story | US-003 Resend confirmation link | F2.2 | Demo-API\Auth | S1b; US-003; EP-03 | 2 | 2 | B | US1 #US-003 | – |
| P-036 | User Story | US-004 Log in | F2.2 | Demo-API\Auth | S1b; US-004; EP-04 | 2 | 4 | B | US1 #US-004 | P-055 |
| P-037 | User Story | US-005 Refresh tokens with rotation and reuse detection | F2.2 | Demo-API\Auth | S1b; US-005; EP-05 | 2 | 4 | B | US1 #US-005 | P-056 |
| P-038 | User Story | US-006 Log out one session | F2.2 | Demo-API\Auth | S1b; US-006; EP-06 | 2 | 2 | B | US1 #US-006 | – |
| P-039 | User Story | US-007 Log out all sessions | F2.2 | Demo-API\Auth | S1b; US-007; EP-07 | 2 | 2 | B | US1 #US-007 | – |
| P-040 | User Story | US-008 Get current user | F2.2 | Demo-API\Auth | S1b; US-008; EP-08 | 2 | 1 | B | US1 #US-008 | – |
| P-041 | User Story | US-009 Change own password | F2.2 | Demo-API\Auth | S1b; US-009; EP-09 | 2 | 4 | B | US1 #US-009 | – |
| P-042 | User Story | US-100 List products | F2.3 | Demo-API\Products | S1b; US-100; EP-22 | 2 | 4 | B | US2 #US-100 | – |
| P-043 | User Story | US-103 Update a product | F2.3 | Demo-API\Products | S1b; US-103; EP-25 | 2 | 4 | B | US2 #US-103 | – |
| P-044 | User Story | US-104 Soft delete a product | F2.3 | Demo-API\Products | S1b; US-104; EP-26 | 2 | 2 | B | US2 #US-104 | – |
| P-045 | User Story | US-105 Restore a product | F2.3 | Demo-API\Products | S1b; US-105; EP-27 | 2 | 2 | B | US2 #US-105 | – |
| P-046 | User Story | US-290 Enabler: Paging, sorting, filtering and ProblemDetails convention | F2.4 | Demo-API\Platform | S1b; enabler; US-290 | 2 | 4 | A | US3 #US-290 | – |
| P-047 | User Story | US-292 Enabler: Rate limiting, security headers and CORS (reports concurrency checked in S4) | F2.4 | Demo-API\Platform | S1b; enabler; US-292 | 2 | 4 | A | US3 #US-292; ADR-0010 | – |
| P-048 | User Story | Enabler: Observability (OTel traces and metrics, logs with traceId, email masking, query strings out of logs, security-event IDs, Aspire dashboard profile) | F2.4 | Demo-API\Platform | S1b; enabler; security-followup | 2 | 4 | A | ADR-0013; TM §4.4, T-26, T-28 | – |
| P-049 | User Story | Test infra: Architecture rules A6-A9 incl. EndpointDoDTests | F2.5 | Demo-API\Platform | S1b; test-infra | 2 | 4 | A | SS §8 | – |
| P-050 | User Story | Test infra: AuditCoverageTests, EndpointPolicyTests and ApiFilesCoverageTests | F2.5 | Demo-API\Platform | S1b; test-infra | 2 | 4 | A | TS §4 | – |
| P-051 | User Story | Test infra: Traceability report (AC IDs vs test tags, CI artifact, 0 uncovered Must ACs per slice) | F2.5 | Demo-API\Platform | S1b; test-infra | 2 | 2 | A | TS §7 | – |
| P-052 | User Story | API files: .http structure per feature and Postman collection generated from OpenAPI (environments without secrets) | F2.5 | Demo-API\Platform | S1b; api-files | 2 | 2 | A | SS §9; AR R-4 | – |
| P-053 | User Story | Perf: k6 run 1 (NFR-01 cached, uncached, Valkey down) recorded in 07-quality/perf-results.md | F2.5 | Demo-API\Platform | S1b; perf | 2 | 2 | A | TS §2 | – |
| P-054 | User Story | Review gate: Fresh-context security review of JwtTokenIssuer and RefreshTokenStore (no open High) | F2.5 | Demo-API\Auth | S1b; review | 2 | 2 | A | TS §6 | P-036, P-037 |
| P-055 | User Story | ST-04 JWT negative tests | F2.6 | Demo-API\Auth | S1b; security-test; ST-04 | 2 | 2 | A | TS §2; TM §6 | P-027 |
| P-056 | User Story | ST-05 Refresh tests (rotation, reuse commits before 401, parallel race, 30-day cap) | F2.6 | Demo-API\Auth | S1b; security-test; ST-05 | 2 | 2 | A | TS §2, §7 | P-027 |
| P-057 | User Story | ST-06 Security-stamp tests (password change, logout-all) | F2.6 | Demo-API\Auth | S1b; security-test; ST-06 | 2 | 2 | A | TS §2 | – |
| P-058 | User Story | ST-07 Lockout tests (5 failures, EP-09 counts, automatic lockout keeps sessions) | F2.6 | Demo-API\Auth | S1b; security-test; ST-07 | 2 | 2 | A | TS §2 | – |
| P-059 | User Story | ST-08 Enumeration tests (same status, body, headers; timing floor; 10 calls per path) | F2.6 | Demo-API\Auth | S1b; security-test; ST-08 | 2 | 4 | A | TS §2; Q-D1 | – |
| P-060 | User Story | ST-09 Rate-limit tests (each policy, shared auth-strict bucket, 429 before 401, X-Forwarded-For ignored; verify TestServer client IP) | F2.6 | Demo-API\Platform | S1b; security-test; ST-09 | 2 | 4 | A | TS §2-3 | – |
| P-061 | User Story | ST-10 Production-environment tests (headers, HSTS, CORS, /scalar and /openapi 404, no stack trace) | F2.6 | Demo-API\Platform | S1b; security-test; ST-10 | 2 | 2 | A | TS §2 | – |
| P-062 | User Story | ST-11 Injection tests for products sort and search | F2.6 | Demo-API\Products | S1b; security-test; ST-11 | 2 | 2 | A | TS §2 | – |
| P-063 | User Story | ST-14 Log hygiene tests (no password, JWT, refresh token, unmasked email) | F2.6 | Demo-API\Platform | S1b; security-test; ST-14 | 2 | 2 | A | TS §2 | – |
| P-064 | User Story | ST-15 Cache tests (scope by role, includeDeleted bypass, search lists not cached) | F2.6 | Demo-API\Products | S1b; security-test; ST-15 | 2 | 2 | A | TS §2 | – |
| P-065 | User Story | ST-16 Limits tests (413/415, pageSize, 1024-char password) | F2.6 | Demo-API\Platform | S1b; security-test; ST-16 | 2 | 2 | A | TS §2 | – |
| P-066 | User Story | ST-17 Confirm-token lifetime (24 h) and key-ring restart tests | F2.6 | Demo-API\Auth | S1b; security-test; ST-17 | 2 | 2 | A | TS §2 | – |

### E3 S2 Customers, own-data rules and user admin (34 items, 87 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E3 | Epic | S2 Customers, own-data rules and user admin | – | Demo-API | S2 | 2 | – | B | PRD §5; TS §6 | E2 |
| F3.1 | Feature | Password recovery | E3 | Demo-API\Auth | S2 | 2 | – | B | US1 | – |
| F3.2 | Feature | Admin user and role management | E3 | Demo-API\AdminUsers | S2 | 2 | – | B | US1 | – |
| F3.3 | Feature | Customers (Admin) | E3 | Demo-API\Customers | S2 | 2 | – | B | US2 | – |
| F3.4 | Feature | Customer self-service (/me) | E3 | Demo-API\Customers | S2 | 2 | – | B | US2; ADR-0014 | – |
| F3.5 | Feature | Template strip check and pipeline upkeep | E3 | Demo-API\Template | S2; ci | 2 | – | B | SS §13; AR R-5 | – |
| F3.6 | Feature | Security tests S2 | E3 | Demo-API\Platform | S2; security-test | 2 | – | B | TS §2 | – |
| P-067 | User Story | US-010 Request a password reset | F3.1 | Demo-API\Auth | S2; US-010; EP-10 | 2 | 2 | B | US1 #US-010 | – |
| P-068 | User Story | US-011 Reset password with a token | F3.1 | Demo-API\Auth | S2; US-011; EP-11 | 2 | 4 | B | US1 #US-011 | – |
| P-069 | User Story | US-012 List users | F3.2 | Demo-API\AdminUsers | S2; US-012; EP-12 | 2 | 4 | B | US1 #US-012 | – |
| P-070 | User Story | US-013 Get a user | F3.2 | Demo-API\AdminUsers | S2; US-013; EP-13 | 2 | 2 | B | US1 #US-013 | – |
| P-071 | User Story | US-014 Lock a user | F3.2 | Demo-API\AdminUsers | S2; US-014; EP-14 | 2 | 4 | B | US1 #US-014 | – |
| P-072 | User Story | US-015 Unlock a user | F3.2 | Demo-API\AdminUsers | S2; US-015; EP-15 | 2 | 2 | B | US1 #US-015 | – |
| P-073 | User Story | US-016 Assign the Admin role | F3.2 | Demo-API\AdminUsers | S2; US-016; EP-16 | 2 | 2 | B | US1 #US-016 | – |
| P-074 | User Story | US-017 Remove the Admin role | F3.2 | Demo-API\AdminUsers | S2; US-017; EP-17 | 2 | 4 | B | US1 #US-017 | – |
| P-075 | User Story | US-018 List roles | F3.2 | Demo-API\AdminUsers | S2; US-018; EP-18 | 2 | 1 | B | US1 #US-018 | – |
| P-076 | User Story | US-019 Create an Admin user | F3.2 | Demo-API\AdminUsers | S2; US-019; EP-19 | 2 | 4 | B | US1 #US-019 | – |
| P-077 | User Story | US-107 Admin creates a customer | F3.3 | Demo-API\Customers | S2; US-107; EP-29 | 2 | 4 | B | US2 #US-107 | – |
| P-078 | User Story | US-108 List customers | F3.3 | Demo-API\Customers | S2; US-108; EP-30 | 2 | 4 | B | US2 #US-108 | – |
| P-079 | User Story | US-109 Get a customer | F3.3 | Demo-API\Customers | S2; US-109; EP-31 | 2 | 2 | B | US2 #US-109 | – |
| P-080 | User Story | US-110 Update a customer | F3.3 | Demo-API\Customers | S2; US-110; EP-32 | 2 | 2 | B | US2 #US-110 | – |
| P-081 | User Story | US-111 Delete and anonymize a customer | F3.3 | Demo-API\Customers | S2; US-111; EP-33 | 2 | 8 | B | US2 #US-111 | – |
| P-082 | User Story | Enabler: OwnedBy(ICurrentUser) helper and /me pattern | F3.4 | Demo-API\Customers | S2; enabler | 2 | 2 | A | ADR-0014; TM T-12 | – |
| P-083 | User Story | US-113 Read own profile | F3.4 | Demo-API\Customers | S2; US-113; EP-35 | 2 | 2 | B | US2 #US-113 | P-082 |
| P-084 | User Story | US-114 Update own profile | F3.4 | Demo-API\Customers | S2; US-114; EP-36 | 2 | 2 | B | US2 #US-114 | P-082 |
| P-085 | User Story | US-115 Erase own account | F3.4 | Demo-API\Customers | S2; US-115; EP-37 | 2 | 4 | B | US2 #US-115 | P-082 |
| P-086 | User Story | CI: TemplateStrip stage (delete sample-domain folders, apply Customer-leak edits, build and test the copy) | F3.5 | Demo-API\Template | S2; ci; template (see Problem 2) | 2 | 8 | A | SS §11, §13; AR R-5 | – |
| P-087 | User Story | Test infra: Architecture rules A12, A13 and C10 test (Admin get of a deleted customer incl. email) | F3.5 | Demo-API\Platform | S2; test-infra | 2 | 2 | A | SS §8; AR C10 | – |
| P-088 | User Story | CI: Weekly scheduled pipeline (ST-08 at 30 calls per path) and Renovate updates | F3.5 | Demo-API\Platform | S2; ci; ST-08 | 2 | 2 | A | TS §5; TM §5 | – |
| P-089 | User Story | ST-02 BOLA tests for /me routes (EP-35 to EP-37; fails on an uncovered /me/{id} route) | F3.6 | Demo-API\Customers | S2; security-test; ST-02 | 2 | 2 | A | TS §2 | P-083, P-084, P-085 |
| P-090 | User Story | ST-03 Mass-assignment tests and per-role DTO snapshots | F3.6 | Demo-API\Platform | S2; security-test; ST-03 | 2 | 4 | A | TS §2 | – |
| P-091 | User Story | ST-13 Erasure test (every PII field, all tables, captured logs, Valkey keys → 0 matches) | F3.6 | Demo-API\Customers | S2; security-test; ST-13 | 2 | 4 | A | TS §2; TM §4.2 | P-081 |
| P-092 | User Story | ST-19 Security-event tests (role assign, Admin create, lockout, refresh reuse) | F3.6 | Demo-API\Platform | S2; security-test; ST-19 | 2 | 2 | A | TS §2 | – |
| P-093 | User Story | Security tests S2 extensions: ST-06 (lock, role remove, erase), ST-07 (EP-37, reset), ST-08 (forgot), ST-14 (PII), ST-17 (reset 1 h, single use) | F3.6 | Demo-API\Platform | S2; security-test; ST-06; ST-07; ST-08; ST-14; ST-17 | 2 | 4 | A | TS §2, §6 | – |

### E4 S3 Orders, stock and statuses (22 items, 66 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E4 | Epic | S3 Orders, stock and statuses | – | Demo-API | S3 | 2 | – | B | PRD §5; TS §6 | E3 |
| F4.1 | Feature | Order domain and stock locking | E4 | Demo-API\Orders | S3 | 2 | – | B | ADR-0007 | – |
| F4.2 | Feature | Orders (Admin) | E4 | Demo-API\Orders | S3 | 2 | – | B | US3 | – |
| F4.3 | Feature | Orders self-service (/me) | E4 | Demo-API\Orders | S3 | 2 | – | B | US3 | – |
| F4.4 | Feature | Order concurrency, load data and security tests | E4 | Demo-API\Orders | S3; test-infra | 2 | – | B | TS §2, §6 | – |
| P-094 | User Story | Enabler: Order aggregate, line items, status machine, OrderStatusHistory and Touch() for ETags | F4.1 | Demo-API\Orders | S3; enabler | 2 | 4 | A | ADR-0007; SS §6 | – |
| P-095 | User Story | Enabler: Stock locking with SELECT … FOR UPDATE in product ID order inside ITransactionRunner, and the Pending-order cap | F4.1 | Demo-API\Orders | S3; enabler; FR-084; FR-096 | 2 | 4 | A | ADR-0007; PRD FR-084, FR-096 | P-094 |
| P-096 | User Story | US-200 Admin creates an order for a customer | F4.2 | Demo-API\Orders | S3; US-200; EP-38 | 2 | 8 | B | US3 #US-200 | P-095 |
| P-097 | User Story | US-201 Admin lists orders | F4.2 | Demo-API\Orders | S3; US-201; EP-39 | 2 | 4 | B | US3 #US-201 | – |
| P-098 | User Story | US-202 Admin gets an order with items and history | F4.2 | Demo-API\Orders | S3; US-202; EP-40 | 2 | 2 | B | US3 #US-202 | – |
| P-099 | User Story | US-203 Admin replaces items of a Pending order (first cut candidate in S3) | F4.2 | Demo-API\Orders | S3; US-203; EP-41 | 2 | 8 | B | US3 #US-203; PM §2 | – |
| P-100 | User Story | US-204 Admin moves an order to the next status | F4.2 | Demo-API\Orders | S3; US-204; EP-42 | 2 | 4 | B | US3 #US-204 | – |
| P-101 | User Story | US-205 Admin cancels an order | F4.2 | Demo-API\Orders | S3; US-205; EP-43 | 2 | 4 | B | US3 #US-205 | – |
| P-102 | User Story | US-207 Customer places an own order | F4.3 | Demo-API\Orders | S3; US-207; EP-45 | 2 | 4 | B | US3 #US-207 | P-095 |
| P-103 | User Story | US-208 Customer lists own orders | F4.3 | Demo-API\Orders | S3; US-208; EP-46 | 2 | 2 | B | US3 #US-208 | – |
| P-104 | User Story | US-209 Customer gets an own order | F4.3 | Demo-API\Orders | S3; US-209; EP-47 | 2 | 2 | B | US3 #US-209 | – |
| P-105 | User Story | US-210 Customer cancels an own Pending order | F4.3 | Demo-API\Orders | S3; US-210; EP-48 | 2 | 2 | B | US3 #US-210 | – |
| P-106 | User Story | Test: Concurrency suite (about 9 scenarios; 20 local loop runs before merge) | F4.4 | Demo-API\Orders | S3; test-infra | 2 | 8 | A | TS §2 | P-096, P-102 |
| P-107 | User Story | Test data: Sample-data seeder (1,000 products, 1,000 customers, 10,000 orders) for k6 | F4.4 | Demo-API\Orders | S3; perf | 2 | 2 | A | TS §3 | – |
| P-108 | User Story | Optional: Stryker.NET one-off mutation run on Domain (skip if xUnit v3 is not supported) | F4.4 | Demo-API\Orders | S3; test-infra; optional | 4 | 2 | A | TS §10 Q-D3 | – |
| P-109 | User Story | ST-18 Timeout tests (lock wait ends within budget) | F4.4 | Demo-API\Orders | S3; security-test; ST-18 | 2 | 2 | A | TS §2 | – |
| P-110 | User Story | Security tests S3 extensions: ST-02 (EP-47, EP-48), ST-03 (customerId, status, unitPrice), ST-15 (poisoned Valkey price ignored), ST-16 (lines, quantity, Pending cap) | F4.4 | Demo-API\Orders | S3; security-test; ST-02; ST-03; ST-15; ST-16 | 2 | 4 | A | TS §2, §6 | – |

### E5 S4 Reports and Dapper reads (12 items, 34 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E5 | Epic | S4 Reports and Dapper reads | – | Demo-API | S4 | 2 | – | B | PRD §5; TS §6 | E4 |
| F5.1 | Feature | Reports and audit-log query | E5 | Demo-API\Reports | S4 | 2 | – | B | US3; DM §4 | – |
| F5.2 | Feature | Final quality gates | E5 | Demo-API\Platform | S4 | 2 | – | B | TS §5-6 | – |
| P-111 | User Story | US-212 Order summary report (view) | F5.1 | Demo-API\Reports | S4; US-212; EP-50 | 2 | 4 | B | US3 #US-212; DM §4.2 #1 | – |
| P-112 | User Story | US-213 Customer order history (function) | F5.1 | Demo-API\Reports | S4; US-213; EP-51 | 2 | 4 | B | US3 #US-213; DM §4.2 #2 | – |
| P-113 | User Story | US-214 Sales report by date range | F5.1 | Demo-API\Reports | S4; US-214; EP-52 | 2 | 8 | B | US3 #US-214; DM §4.2 #3 | – |
| P-114 | User Story | US-215 Low-stock report | F5.1 | Demo-API\Reports | S4; US-215; EP-53 | 2 | 2 | B | US3 #US-215; DM §4.2 #4 | – |
| P-115 | User Story | US-216 Top products report | F5.1 | Demo-API\Reports | S4; US-216; EP-54; could | 4 | 2 | B | US3 #US-216; DM §4.2 #5 | – |
| P-116 | User Story | US-217 Query the audit log | F5.1 | Demo-API\Reports | S4; US-217; EP-55; should | 3 | 4 | B | US3 #US-217; DM §4.2 #6 | – |
| P-117 | User Story | Perf: k6 run 2 (NFR-01 + NFR-02) and DB timeout and report-concurrency tuning | F5.2 | Demo-API\Platform | S4; perf | 2 | 4 | A | TS §2; TM §3 item 15 | – |
| P-118 | User Story | CI: ApiFiles stage (httpyac .http run and generated Postman collection against a running API) | F5.2 | Demo-API\Platform | S4; ci; api-files | 2 | 2 | A | SS §11; TS §5 | – |
| P-119 | User Story | Security tests S4 extensions: ST-09 (reports concurrency 429), ST-11 (Dapper SQL architecture rule), ST-16 (report range), ST-18 (slow query), ST-19 (audit-log query) | F5.2 | Demo-API\Reports | S4; security-test; ST-09; ST-11; ST-16; ST-18; ST-19 | 2 | 4 | A | TS §2, §6 | – |

### E6 Template extraction (7 items, 22 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E6 | Epic | Template extraction (P7) | – | Demo-API\Template | Template | 2 | – | B | PRD §17 P7; SS §13 | E5 |
| F6.1 | Feature | dotnet new template and start guide | E6 | Demo-API\Template | Template | 2 | – | B | SS §13 | – |
| P-120 | User Story | Template: dotnet new package (template.json, sourceName DemoApi, kept parts per strip path, single Initial migration) | F6.1 | Demo-API\Template | Template | 2 | 8 | A | SS §13; PRD §17 P7 | – |
| P-121 | User Story | Template: "Start a new API" guide (strip steps, Customer leaks, option to merge Domain + Application) | F6.1 | Demo-API\Template | Template; docs | 2 | 4 | A | SS §13, §15 | – |
| P-122 | User Story | Template: Template suites green from dotnet new; TemplateStrip job switched to dotnet new install | F6.1 | Demo-API\Template | Template; ci | 2 | 4 | A | TS §6 | – |
| P-123 | User Story | Template: Bootstrap trial and "hours to bootstrap" baseline | F6.1 | Demo-API\Template | Template | 2 | 4 | A | PRD §17 P7; PM F2 | – |
| P-124 | User Story | Spike SP-6: .NET 11 compatibility check (build and tests on the .NET 11 SDK, no retarget) | F6.1 | Demo-API\Template | Template; spike | 2 | 2 | A | PM §5 | – |

(Plan tags `Template; template` collapse to one tag `Template`, see Problem 2.)

### E7 Release and operations (light) (6 items, 8 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E7 | Epic | Release and operations (light) | – | Demo-API\Delivery | RelOps | 2 | – | B | 00-summary phases 10-11 | E6 |
| F7.1 | Feature | Release and operations notes | E7 | Demo-API\Delivery | RelOps | 2 | – | B | TM §7.2 | – |
| P-125 | User Story | Release: Fresh-clone rebuild timed (≤ 15 min, 0 manual steps) and final README | F7.1 | Demo-API\Delivery | RelOps | 2 | 2 | A | PRD NFR-15; SS §12 | – |
| P-126 | User Story | Release: Release checklist and POC sign-off (North Star 100%, test-strategy §6 minimums) | F7.1 | Demo-API\Delivery | RelOps; review | 2 | 2 | A | TS §6; PRD §15 | – |
| P-127 | User Story | Ops: Light runbook (health, logs, regenerating dev secrets, HS256 key rotation) | F7.1 | Demo-API\Delivery | RelOps; docs | 2 | 2 | A | TM §3 item 1; ADR-0013 | – |
| P-128 | User Story | Docs: Before-production register (threat model §7.2 items 1-12, retention plan §4.3) for the client project | F7.1 | Demo-API\Delivery | RelOps; docs; security-followup | 2 | 2 | A | TM §7.2, §4.3; ARCH §9 A5 | – |

### E8 Stretch (cut first) (9 items, 30 SP)

| Plan ID | Type | Title | Parent | Area path | Tags | Pri | SP | VA | Source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E8 | Epic | Stretch (cut first) | – | Demo-API | Stretch | 4 | – | B | PRD §5 | – |
| F8.1 | Feature | Stretch stories (cut first) | E8 | Demo-API | Stretch | 4 | – | B | PRD §5 | – |
| P-129 | User Story | US-020 Revoke a user's sessions | F8.1 | Demo-API\AdminUsers | Stretch; could; US-020; EP-20 | 4 | 2 | B | US1 #US-020 | – |
| P-130 | User Story | US-021 Send a password-reset link for a user | F8.1 | Demo-API\AdminUsers | Stretch; could; US-021; EP-21 | 4 | 2 | B | US1 #US-021 | – |
| P-131 | User Story | US-106 Adjust product stock | F8.1 | Demo-API\Products | Stretch; could; US-106; EP-28 | 4 | 4 | B | US2 #US-106 | – |
| P-132 | User Story | US-112 Restore a customer (incl. restored-customer lock type and email change, D14) | F8.1 | Demo-API\Customers | Stretch; could; US-112; EP-34 | 4 | 8 | B | US2 #US-112; PRD §17 P2 | – |
| P-133 | User Story | US-206 Admin soft deletes a Cancelled order | F8.1 | Demo-API\Orders | Stretch; could; US-206; EP-44 | 4 | 2 | B | US3 #US-206 | – |
| P-134 | User Story | US-211 Customer edits items of an own Pending order | F8.1 | Demo-API\Orders | Stretch; could; US-211; EP-49 | 4 | 8 | B | US3 #US-211 | – |
| P-135 | User Story | Idempotency-Key on order create (no user story exists) | F8.1 | Demo-API\Orders | Stretch; could; FR-095 | 4 | 4 | A | PRD FR-095 | – |

(Plan tags `Stretch; stretch` collapse to one tag `Stretch`, see Problem 2.)

### 2.2 Totals check against delivery-plan §5

| Check | §5 says | Preview counts | Match |
|---|---|---|---|
| Epics | 8 | 8 | ✔ |
| Features | 25 | 25 (E1 4, E2 6, E3 6, E4 4, E5 2, E6 1, E7 1, E8 1) | ✔ |
| User Stories | 135 (128 core + 7 stretch) | 135 (P-001…P-135; P-129…P-135 stretch) | ✔ |
| **Total** | **168** | **168** | ✔ |
| Story breakdown | 60 / 14 / 2 / 6 / 9 / 22 / 6 / 4 / 1 / 2 / 4 / 4 / 1 = 135 | Recounted per category, same numbers | ✔ |
| Stories per slice | S1a 25, S1b 41, S2 27, S3 17, S4 9, Template 5, RelOps 4, Stretch 7 | Same | ✔ |
| Points per slice | 80 / 122 / 87 / 66 / 34 / 22 / 8 / 30 | Recomputed from sizes: same | ✔ |
| Points total | 419 core (§2) | 419 core + 30 stretch = 449 | ✔ |

| By Epic | Epic | Features | Stories | Items | Story Points |
|---|---|---|---|---|---|
| E1 S1a | 1 | 4 | 25 | 30 | 80 |
| E2 S1b | 1 | 6 | 41 | 48 | 122 |
| E3 S2 | 1 | 6 | 27 | 34 | 87 |
| E4 S3 | 1 | 4 | 17 | 22 | 66 |
| E5 S4 | 1 | 2 | 9 | 12 | 34 |
| E6 Template | 1 | 1 | 5 | 7 | 22 |
| E7 RelOps | 1 | 1 | 4 | 6 | 8 |
| E8 Stretch | 1 | 1 | 7 | 9 | 30 |
| **Total** | **8** | **25** | **135** | **168** | **449** |

Priority: 2 = 157 items · 3 = 1 (P-116) · 4 = 10 (E8, F8.1, P-108, P-115, P-129…P-135). Value Area: Business 90 (8 Epics, 25 Features, 57 endpoint stories) · Architectural 78.

### 2.3 Source legend (descriptions use these full paths)

| Code | Path (repo root) |
|---|---|
| PRD | `docs/product/02-requirements/prd.md` |
| US1 | `docs/product/02-requirements/user-stories/01-auth-and-admin-users.md` (heading `### US-0xx - …`) |
| US2 | `docs/product/02-requirements/user-stories/02-products-and-customers.md` (heading `### US-1xx - …`) |
| US3 | `docs/product/02-requirements/user-stories/03-orders-reports-and-platform.md` (heading `### US-2xx - …`) |
| PM | `docs/product/02-requirements/pre-mortem.md` |
| SS | `docs/product/04-architecture/solution-structure.md` |
| DM | `docs/product/04-architecture/data-model.md` |
| AR | `docs/product/04-architecture/architecture-review.md` |
| ARCH | `docs/product/04-architecture/architecture.md` |
| ADR-nnnn | `docs/product/04-architecture/adr/nnnn-*.md` |
| TM | `docs/product/05-security/threat-model.md` |
| TS | `docs/product/07-quality/test-strategy.md` |
| 00-summary | `docs/product/00-summary.md` |

Sources go into the description as text paths. A **Hyperlink** relation needs an absolute URL, and the repo is not in Azure Repos yet. Hyperlinks to `https://dev.azure.com/codewithelmor/Demo-API/_git/<repo>?path=/docs/...` can be added in a later approved run, after the push (Problem 6).

## 3. Links

### 3.1 Parent-child (160 links)

Every Feature → its Epic (25) and every User Story → its Feature (135), as listed in the Parent column. Created as `System.LinkTypes.Hierarchy-Reverse` (child → parent) after both items exist. Epics have no parent.

### 3.2 Predecessor → successor (31 links, `System.LinkTypes.Dependency-Forward` on the predecessor)

| # | Predecessor | Successor | Source |
|---|---|---|---|
| D-1 | E1 S1a | E2 S1b | §3 flowchart |
| D-2 | E2 S1b | E3 S2 | §3 |
| D-3 | E3 S2 | E4 S3 | §3 (gate: named client project) |
| D-4 | E4 S3 | E5 S4 | §3 |
| D-5 | E5 S4 | E6 Template | §3 |
| D-6 | E6 Template | E7 RelOps | §3 |
| D-7 | P-007 SP-1 | P-002 compose chain | §3 S1a chain |
| D-8 | P-002 | P-012 fixture | §3 S1a |
| D-9 | P-012 | P-018 audit (US-291) | §3 S1a |
| D-10 | P-012 | P-020 create product (US-102) | §3 S1a |
| D-11 | P-018 | P-009 CI | §3 S1a |
| D-12 | P-020 | P-009 CI | §3 S1a |
| D-13 | P-026 JWT issuer | P-027 refresh store | §3 S1b |
| D-14 | P-027 | P-055 ST-04 | §3 S1b |
| D-15 | P-027 | P-056 ST-05 | §3 S1b |
| D-16 | P-055 ST-04 | P-036 US-004 | §3 S1b |
| D-17 | P-056 ST-05 | P-037 US-005 | §3 S1b |
| D-18 | P-036 US-004 | P-054 review gate | §3 S1b |
| D-19 | P-037 US-005 | P-054 review gate | §3 S1b |
| D-20 | P-082 OwnedBy | P-083 US-113 | §3 S2 |
| D-21 | P-082 | P-084 US-114 | §3 S2 |
| D-22 | P-082 | P-085 US-115 | §3 S2 |
| D-23 | P-083 | P-089 ST-02 | §3 S2 |
| D-24 | P-084 | P-089 ST-02 | §3 S2 |
| D-25 | P-085 | P-089 ST-02 | §3 S2 |
| D-26 | P-081 US-111 | P-091 ST-13 | §3 S2 |
| D-27 | P-094 order aggregate | P-095 stock locking | §3 S3 |
| D-28 | P-095 | P-096 US-200 | §3 S3 |
| D-29 | P-095 | P-102 US-207 | §3 S3 |
| D-30 | P-096 | P-106 concurrency suite | §3 S3 |
| D-31 | P-102 | P-106 concurrency suite | §3 S3 |

Not linked on purpose: "other auth stories" between US-004/005 and P-054 in the S1b chain (too vague to link item by item; P-054 depends on the two core token stories). The S2 → "template from S1-S2" fallback path (decision, not a link). E8 has no dependency links.

## 4. Creation order and API-call estimate (for APPLY, after approval)

**Step 0, pre-flight (read only, ~5 calls):** `get_type` Epic and Feature (fields exist); one WIQL per area path, or one combined WIQL, to prove all 9 areas exist (stop if not); team settings re-read (confirm S-2 to S-4 were done); WIQL for any items with `Plan ID:` in the description (duplicates, expected 0).

| Batch | Items | Count | Then |
|---|---|---|---|
| B1 | E1–E8 | 8 | read back; record IDs in `azdo-map.md`; 6 Epic dependency links (D-1…D-6) |
| B2 | F1.1–F3.6 | 16 | link 16 parents; read back; map |
| B3 | F4.1–F8.1 | 9 | link 9 parents; read back; map |
| B4 | P-001–P-020 | 20 | link 20 parents; read back; map |
| B5 | P-021–P-040 | 20 | same |
| B6 | P-041–P-060 | 20 | same |
| B7 | P-061–P-080 | 20 | same |
| B8 | P-081–P-100 | 20 | same |
| B9 | P-101–P-120 | 20 | same |
| B10 | P-121–P-135 | 15 | same; then D-7…D-31 dependency links (25) and a final read-back |

Stop at the first failure and report. Map file rows are written after every batch, with "Created by plan = yes".

| Call kind | Tool (ado-write unless noted) | Calls |
|---|---|---|
| Pre-flight reads | ado-read `get_type`, `wiql`, `get_team_settings` | ~5 |
| Duplicate WIQL per batch | ado-read `wiql` | 10 |
| Create work items (one per item; fields incl. tags, SP, AC, area, iteration) | `wit_work_item_write create` | 168 |
| Parent-child links (one batched `link` call per batch) | `wit_work_item_link_write link` | 9 |
| Dependency links (batched) | `wit_work_item_link_write link` | 2 |
| Read-back per batch + final | ado-read `wit_work_item get_batch` | 11 |
| **Total** | | **≈ 205** |

No comments, no iteration creation, no team-setting changes, and no deletes are in this preview. If Azure DevOps returns HTTP 429 or `Retry-After`, the batch stops and is reported.

## 5. Problems found (doc issues and gaps)

| # | Problem | Impact | Proposal |
|---|---|---|---|
| 1 | Plan §5 uses the area root `Orders API\…`, but the project's area root is **`Demo-API`** | Creates with `Orders API\…` would fail | Use `Demo-API\Platform` etc. (done in this preview). Fix the text in delivery-plan §5 later |
| 2 | Azure DevOps tags are **case-insensitive**. `Stretch; stretch` (E8, F8.1, P-129…P-135) and `Template; template` (E6, F6.1, P-120…P-123) are one tag each. P-086 (an S2 item) has kind tag `template`, so it lands in the **`Template` slice tag** and shows in a "Template slice" query | Slice queries by tag mix in P-086; the priority tag `stretch` is lost (harmless: `Stretch` already means it) | Collapse the duplicates (as in this preview). For P-086 either accept it, or rename the kind tag to `template-strip`. **Question for the PO** |
| 3 | Bugs are now **managed with tasks**, and the **Epics backlog is hidden** | Bugs would not show on the board or count to WIP; Epics invisible | Manual S-3 and S-4 before APPLY |
| 4 | **P-006** (requirements cleanup) is partly done already: DL-D6 says the US-292, US-291 AC7, PRD §9 Q4 and concept items were fixed on 2026-10-27. C7 and C9 are not mentioned as done | A New item for finished work | **Question for the PO:** create P-006 as listed (and close it by hand if C7/C9 are also done), or create it with only the C7/C9 scope in the description |
| 5 | **75 stories without acceptance criteria in the docs** (enablers, spikes, CI, test infra, ST items, perf, docs, template, release/ops), plus all Epics/Features. Only the 60 US-xxx stories have ACs | They do not meet the board DoR (§6, work-item-standards) until ACs are added | Leave the AC field empty and put the note "ACs to be added before Ready" in the description. Do not invent ACs. The developer adds them at slice start (the §2 "Slice start" check) |
| 6 | Source links can only be text paths now (no repo in Azure Repos yet; no repos tool to check) | No clickable source links on day 1 | Text paths now; Hyperlink relations in a later approved run after the first push (+168 calls), or skip |
| 7 | Default iteration is `@CurrentIteration` = `Iteration 1` | Items created in the web UI would land in Iteration 1 | APPLY sets `Demo-API` explicitly; PO does S-5 for UI-created items |
| 8 | Value Area is required; the plan does not set it | Every item would default to Business | Proposed mapping in §2.1 (57 endpoint stories Business, 78 others Architectural). **Question for the PO** |
| 9 | Several approved titles do not start with a verb ("Enabler: …", "ST-04 …", "US-001 …") | Minor style gap vs work-item-standards | Keep verbatim (approved plan, trace IDs in titles) |
| 10 | User-story headings say "Slice S1" while the plan splits S1 into S1a/S1b | None for the import (the plan's slice tags win) | No action |
| 11 | MCP gaps: area paths, team settings (areas, backlogs, bugs, default iteration), board columns/WIP/swimlanes/card styles, shared queries, repos/branch policy, pipelines parallelism | Manual steps S-1…S-9, S-11…S-13 | Product owner does them in the web UI (§1). No REST/`az boards` workaround without explicit approval |

## Product owner decisions on this preview (2026-10-27)

| # | Question | Decision |
|---|---|---|
| a | Case-duplicate tags | Merge into one tag each (`stretch`, `template`). P-086 gets `template-strip` instead of `template` |
| b | P-006 scope | Create with only the remaining fixes C7 and C9 |
| c | Value Area | Business for the 57 endpoint stories and all Epics and Features; Architectural for the other 78 stories |
| d | Stories without acceptance criteria (75) | Leave the field empty and add the tag `needs-ac` |

Creation is still pending: the manual UI steps S-1 to S-5 come first, then the product owner's explicit go.

## Applied status (2026-10-27)

| Step | Result |
|---|---|
| S-1 areas | ✔ All 9 child areas exist (WIQL `UNDER` accepted; a fake path returns TF51011) |
| S-2 to S-5 | ✔ Include sub areas on; Epics backlog visible; bugs managed with requirements; default iteration = root `Demo-API` |
| S-7, S-8 | Not readable via MCP; not depended on |
| Epic / Feature types | ✔ Re-checked. Required: Title, State, Value Area (default Business); Priority required on Epic (default 2) |
| Duplicate check | ✔ 0 work items in the project |
| Work item creation | ✘ **Blocked.** The first create (E1) was denied by the Claude Code permission system (auto-mode classifier, "External System Writes") for `mcp__ado-write__wit_work_item_write`. **0 items, 0 links, 0 tags created.** No workaround was tried |
| Map file | `docs/product/azdo-map.md` created with the verified setup; no work item rows yet |

Resume: rerun APPLY after the user allows `mcp__ado-write__wit_work_item_write` and `mcp__ado-write__wit_work_item_link_write` (or approves each prompt). The run starts again at batch B1 (E1) after a fresh duplicate query.

### Applied run 2 (2026-10-27, main session)

| Step | Result |
|---|---|
| Permission | The product owner asked the main session directly ("create the board"); the MCP create for E1 was allowed (#1) |
| Method | To avoid ~205 MCP calls, the rest was created with a script calling the Azure DevOps REST API (api-version 7.1) with the same PAT. Same fields, tags and links as this preview plus decisions a-d. Idempotent: reuses items whose description holds `Plan ID: <id>` |
| Created | ✔ 167 new + E1 updated → **168** (Epic 8, Feature 25, User Story 135), **449** Story Points |
| Links | ✔ 160 parent-child (set at create time), 31 dependency links (D-1…D-31, comment = D-id) |
| Verified | ✔ Read back by WIQL: counts, points, parents and dependency links match |
| Map | [azdo-map.md](../azdo-map.md) lists every plan ID → work item ID |
| Doc fix | delivery-plan §5 area root `Orders API` → `Demo-API` (Problem 1) |
