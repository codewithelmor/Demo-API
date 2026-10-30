# Azure DevOps preview: Phase 2 (web front end)

Status: approved plan 2026-10-30 (DL-D7..DL-D11); board write pending `azdo-boards-manager` preview and product-owner approval. Source: [delivery-plan.md §10](delivery-plan.md). Project renamed `Demo-API` → `Demo-App` on 2026-10-30.

## Preview review (azdo-boards-manager, 2026-10-30, docs only; live checks R1-R10 pending)
- Input verified: 165 items, 335 SP, 158 parent links, 161 predecessor links, 2 Related links, 3 updates; no missing parents, no predecessor cycles. Priority 4 count is 10, not 11.
- Setup before apply (manual, product owner): create areas `Demo-App\Web`, `Web\Bff`, `Web\Customer`, `Web\Admin`, `Web\Design`; confirm "Include sub areas". After apply: `Design` swimlane, In progress WIP 2, 4 shared queries.
- Live pre-flight R1-R10 done 2026-10-30: project `Demo-App` (ID d77f3f29-…, private), team `Demo-App Team`, root area/iteration `Demo-App`, 9 Phase 1 areas exist, **5 `Web` areas missing**, 167 existing tags with no clashes (`template-strip` reused), no duplicates. #164/#165/#167 unchanged (parent #33, rev 1); re-parent is one atomic JSON Patch update each. Final apply ≈ 190 writes, ≈ 25 reads.
- Apply order: Epics → Features → Stories → all predecessor links → Related links → re-parent #164/#165/#167. About 188 writes, 37 reads. Predecessor links are created only after all items exist (forward references).

### Decisions (product owner, 2026-10-30)
| # | Decision |
|---|---|
| AZ-Q1 | ACs as in the approved plan: AC field filled verbatim for the 26 stories with numbered ACs; `needs-ac` only on the 26 listed items (R2 + API v1.1); the 84 enabler/FT/CI/spike items carry their source checks in the description as "Checks (from source)"; R2 "Key ACs" go in the description as "Draft key ACs (numbered by P2-089)" |
| AZ-Q2 | P2-094 (US-F036): AC field filled with its AC1; keeps `needs-ac` until P2-089 |
| AZ-Q3 | P2-125 tagged `template-strip` instead of `template` |
| AZ-Q4 | P2-051: the "(Assumption: …)" text moves from the title to the description |

## 2. Work item preview (azdo-preview.md format)

**Legend:**
- USF = 04-frontend.md; PRDF = prd-frontend.md; FA = frontend-architecture.md; TM10 = threat model §10; TSP2 = test strategy Phase 2; DP = design-profile.yaml; WF = wireframes.md.
- Area paths are relative to `<root>` (expected `Demo-App`).
- Pri: 2 = Must, 3 = should, 4 = could/optional/unscheduled.
- VA: B = Business, A = Architectural.
- Iteration = `<root>` on every item. Action = create, unless marked otherwise.

### E9 FE-S0a Walking skeleton (27 items, 61 SP)
| Plan ID | Type | Title | Parent | Area | Tags | Pri | SP | VA | AC source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E9 | Epic | FE-S0a Walking skeleton (web) | – | `<root>` | FE-S0a | 2 | – | B | §10.4 exit (M-F1) | – |
| F9.1 | Feature | Baseline, scaffold and pipeline | E9 | Web | FE-S0a; ci | 2 | – | B | FA §2-5 | – |
| F9.2 | Feature | Spikes and SPA stack ADR | E9 | Web | FE-S0a; spike | 2 | – | B | FA §6 | – |
| F9.3 | Feature | BFF and SPA thin slice | E9 | Web\Bff | FE-S0a | 2 | – | B | ADR-0015 | – |
| F9.4 | Feature | Test harness core | E9 | Web | FE-S0a; test-infra | 2 | – | B | TSP2 P2.2-P2.4 | – |
| P2-001 | User Story | Gate: Phase 1 baseline for the API freeze (merge to main via branch policy, Azure pipeline green, Bug #169 fixed and v1.json re-exported without the rate-limiter workaround, #170-#172 closed), then tag api-v1.0 | F9.1 | Delivery | FE-S0a; enabler; FT-20 | 2 | 2 | A | FA §5.1; review-2026-10-27 "Left open"; azdo-map #169-#172 | – (Related: #169, #170) |
| P2-002 | User Story | Enabler: web/ scaffold (nested Directory.*.props, DemoApi.Bff + Bff.Tests in DemoApi.slnx, Vite + React 19 + TS 6.0.3, ESLint strictTypeChecked + jsx-a11y + react-hooks + NFR-F35 bans, Prettier, .nvmrc/.npmrc); verify pins incl. Node LTS move | F9.1 | Web | FE-S0a; enabler | 2 | 4 | A | FA §1-3 | P2-007 |
| P2-003 | User Story | US-F009 Compose, configuration and pipeline | F9.1 | Web | FE-S0a; US-F009 | 2 | 2 | B | USF US-F009 AC1-3; FA §4 | P2-002 |
| P2-004 | User Story | CI: ApiFreeze stage (tag api-v1.0, allowApiChanges = false) | F9.1 | Web | FE-S0a; ci; security-test; FT-20 | 2 | 1 | A | TM10 FT-20; FA §5.1 | P2-001 |
| P2-005 | User Story | CI: Spa job (typecheck, lint 0 warnings, format, Vitest JUnit, schema.d.ts drift, build, bundle budget, no admin in entry) and BFF tests in FastTests | F9.1 | Web | FE-S0a; ci | 2 | 4 | A | FA §5.2-5.3; TSP2 P2.5 | P2-002 |
| P2-006 | User Story | CI: E2E stage (compose up --wait, Chromium full + Firefox @cross-browser + request project, JUnit + traces, retries 0, ≤ 12 min) | F9.1 | Web | FE-S0a; ci; test-infra | 2 | 4 | A | FA §5.5; TSP2 P2.5 | P2-003; P2-019 |
| P2-007 | User Story | Spike SP-F4: npm ci with overrides for openapi-typescript and eslint-plugin-jsx-a11y on TypeScript 6.0.3 (time-box 0.5 day) | F9.2 | Web | FE-S0a; spike | 2 | 2 | A | FA §1, §6 | – |
| P2-008 | User Story | Spike SP-F5: YARP 2.3.0 on .NET 10: transforms, typed route constraints, ActivityPropagation (time-box 0.5 day) | F9.2 | Web\Bff | FE-S0a; spike | 2 | 2 | A | FA §6; ADR-0015 | – |
| P2-009 | User Story | Spike SP-F1: __Host- cookie on http://localhost in Chromium, Firefox and Safari/WebKit (time-box 0.5 day; result to ADR-0017) | F9.2 | Web\Bff | FE-S0a; spike | 2 | 2 | A | FA §6; ADR-0015 | – |
| P2-010 | User Story | Spike SP-F2: API Location headers absolute or relative; response transform if absolute (time-box 0.25 day) | F9.2 | Web\Bff | FE-S0a; spike | 2 | 1 | A | FA §6 | – |
| P2-011 | User Story | Docs: ADR-0016 SPA stack and conventions | F9.2 | Web | FE-S0a; docs | 2 | 1 | A | ADR-0015 follow-ups; FA §1-2 | P2-007 |
| P2-012 | User Story | Enabler: BFF host core (ValkeyTicketStore with Data Protection, bff:session:{id} TTL, __Host-demoapi cookie, bff-dp-keys volume, ProblemDetails with traceId, OTel) | F9.3 | Web\Bff | FE-S0a; enabler | 2 | 4 | A | ADR-0015 Session and cookie; NFR-F10 | P2-002; P2-009 |
| P2-013 | User Story | US-F001 BFF proxies the API | F9.3 | Web\Bff | FE-S0a; US-F001 | 2 | 4 | B | USF AC1-3 | P2-012; P2-008; P2-010 |
| P2-014 | User Story | US-F002 Sign in through the BFF | F9.3 | Web\Bff | FE-S0a; US-F002 | 2 | 4 | B | USF AC1-3 | P2-012 |
| P2-015 | User Story | US-F004 Sign out | F9.3 | Web\Bff | FE-S0a; US-F004 | 2 | 2 | B | USF AC1-2 | P2-014 |
| P2-016 | User Story | Enabler: SPA API client (schema.d.ts from v1.json, openapi-fetch middleware X-CSRF / 401 / 15 s timeout, React Query: no write retry, GET once if Retry-After ≤ 10 s) | F9.3 | Web | FE-S0a; enabler | 2 | 4 | A | FA §1; NFR-F07, F23 | P2-001; P2-007 |
| P2-017 | User Story | US-F006 App shell, session bootstrap and role routing (incl. returnUrl rules) | F9.3 | Web | FE-S0a; US-F006; FT-11 | 2 | 4 | B | USF AC1-3; NFR-F34; TM10 FT-11 | P2-016; P2-014 |
| P2-018 | User Story | Test infra: BFF test project (Mvc.Testing, fake API via IForwarderHttpClientFactory, FakeTimeProvider, Valkey Testcontainer, traits, "no DemoApi.* reference" rule) | F9.4 | Web\Bff | FE-S0a; test-infra | 2 | 4 | A | TSP2 P2.2 | P2-002 |
| P2-019 | User Story | Test infra: Playwright projects (chromium, firefox, request, 360 px), story() tag helper, globalSetup seed Admin storageState, per-worker customers, readEmailLink, E2E compose override for the BFF auth limit (+ default-values test) | F9.4 | Web | FE-S0a; test-infra | 2 | 4 | A | TSP2 P2.3-P2.4 | P2-003 |
| P2-020 | User Story | Test infra: Vitest + Testing Library + MSW handlers typed from schema.d.ts, data factories, strict tag list | F9.4 | Web | FE-S0a; test-infra | 2 | 2 | A | TSP2 P2.2-P2.3 | P2-016 |
| P2-021 | User Story | Test infra: FT-01 token-scan fixture (responses, cookies, storage, console) and CSP-violation listener | F9.4 | Web | FE-S0a; test-infra; security-test; FT-01 | 2 | 2 | A | TM10 FT-01; NFR-F09 | P2-019 |
| P2-022 | User Story | Test infra: axe fixture (wcag2a, wcag2aa, wcag21aa, wcag22aa; fail on serious/critical) after each navigation, dialog and error | F9.4 | Web | FE-S0a; test-infra; a11y | 2 | 2 | A | TSP2 P2.2; NFR-F01 | P2-019 |

### E10 FE-S0b BFF hardening and test harness (29 items, 58 SP)
| Plan ID | Type | Title | Parent | Area | Tags | Pri | SP | VA | AC source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E10 | Epic | FE-S0b BFF hardening and test harness | – | `<root>` | FE-S0b | 2 | – | B | §10.4 exit (M-F2) | – |
| F10.1 | Feature | BFF security hardening | E10 | Web\Bff | FE-S0b; security-followup | 2 | – | B | ADR-0015 Amendment 1 | – |
| F10.2 | Feature | SPA foundations | E10 | Web | FE-S0b | 2 | – | B | FA §1; DP | – |
| F10.3 | Feature | Pipeline gates, harness completion and ADRs | E10 | Web | FE-S0b; ci | 2 | – | B | FA §5; TSP2 P2.3 | – |
| F10.4 | Feature | Security tests, perf baseline and review gate | E10 | Web\Bff | FE-S0b; security-test | 2 | – | B | TM10 §10.7 | – |
| P2-023 | User Story | Enabler: BFF hardening (session binding, headers on every response, no developer exception page and own login ProblemDetails, log hygiene, no-store + Clear-Site-Data, header stripping) | F10.1 | Web\Bff | FE-S0b; enabler; security-followup | 2 | 4 | A | NFR-F15, F30, F32, F33, F36, F39 | P2-013 |
| P2-024 | User Story | US-F003 Silent, single-flight refresh (conditional save, no retry on EP-05) | F10.1 | Web\Bff | FE-S0b; US-F003 | 2 | 4 | B | USF AC1-3; NFR-F31 | P2-014 |
| P2-025 | User Story | US-F005 CSRF protection (origin allow-list, Sec-Fetch-Site) | F10.1 | Web\Bff | FE-S0b; US-F005 | 2 | 2 | B | USF AC1-2; NFR-F29 | P2-012 |
| P2-026 | User Story | US-F008 BFF rate limits and API configuration (incl. anonymous 60/min) | F10.1 | Web\Bff | FE-S0b; US-F008 | 2 | 2 | B | USF AC1-2; NFR-F37 | P2-012 |
| P2-027 | User Story | Enabler: BFF health endpoints /bff/health/live and /bff/health/ready (EP-57) | F10.1 | Web\Bff | FE-S0b; enabler; should | 3 | 1 | A | NFR-F18; ADR-0015 BFF endpoints | P2-013 |
| P2-028 | User Story | Enabler: https compose profile on 127.0.0.1:8443 and Initialize-DevCert.ps1 | F10.1 | Web | FE-S0b; enabler; security-followup; FT-17 | 2 | 2 | A | NFR-F42; FA §4 | P2-009 |
| P2-029 | User Story | Spike SP-F3: Radix/shadcn under the strict CSP without 'unsafe-inline' styles; Trusted Types report-only (time-box 0.5 day) | F10.2 | Web | FE-S0b; spike | 2 | 2 | A | FA §6; S-D13 | P2-023 |
| P2-030 | User Story | Enabler: Design tokens and shadcn base from design-profile (neutral + blue primary, success/warning/info, contrast fixes, self-hosted Geist, Light/Dark); reconcile with Figma batch 1 | F10.2 | Web | FE-S0b; enabler; design | 2 | 2 | A | DP | P2-029 |
| P2-031 | User Story | US-F007 Error handling with ProblemDetails (incl. SY-1..SY-3) | F10.2 | Web | FE-S0b; US-F007 | 2 | 4 | B | USF AC1-4; WF SY-1..3 | P2-030; P2-016 |
| P2-032 | User Story | CI: Front-end supply-chain gates (npm ci, ignore-scripts, audit prod high / dev critical, audit signatures, CycloneDX SBOM, licence allow-list with MPL-2.0 exception, Trivy on demoapi/web, Node digest pin, gitleaks *.pfx/*.pem/*.key) | F10.3 | Web | FE-S0b; ci; security-test; FT-15 | 2 | 4 | A | NFR-F40; TM10 FT-15 | P2-005 |
| P2-033 | User Story | Test infra: Meta-tests (allow-list vs v1.json, ProblemDetails slug coverage, no features/admin import outside lazy()) | F10.3 | Web | FE-S0b; test-infra; FT-04 | 2 | 2 | A | TSP2 P2.2 | P2-013; P2-031 |
| P2-034 | User Story | Test infra: Traceability script (traceability.mjs → traceability-frontend.md; PR warning, release failure) and 07-quality/manual-checks.md | F10.3 | Web | FE-S0b; test-infra | 2 | 4 | A | TSP2 P2.3 | P2-018; P2-019; P2-020 |
| P2-035 | User Story | Docs: ADR-0017 Local HTTPS and cookie policy for development | F10.3 | Web | FE-S0b; docs | 2 | 1 | A | ADR-0015 follow-ups | P2-028 |
| P2-036 | User Story | Docs: ADR-0008, 0010, 0012, 0013 amendments and solution-structure §13 (template strip deletes web/) | F10.3 | Delivery | FE-S0b; docs | 2 | 2 | A | ADR-0015 follow-ups; FA §3 | – |
| P2-037 | User Story | FT-02 Cookie flags, new ID per sign-in, planted cookie, old session ended | F10.4 | Web\Bff | FE-S0b; security-test; FT-02 | 2 | 2 | A | TM10 FT-02 | P2-014 |
| P2-038 | User Story | FT-03 CSRF matrix | F10.4 | Web\Bff | FE-S0b; security-test; FT-03 | 2 | 2 | A | TM10 FT-03 | P2-025 |
| P2-039 | User Story | FT-04 Allow-list fuzz and header stripping | F10.4 | Web\Bff | FE-S0b; security-test; FT-04 | 2 | 2 | A | TM10 FT-04 | P2-023 |
| P2-040 | User Story | FT-05 Single-flight refresh tests (20 parallel → 1 EP-05, abort, logout during refresh, replay once; never quarantine) | F10.4 | Web\Bff | FE-S0b; security-test; FT-05 | 2 | 4 | A | TM10 FT-05 | P2-024 |
| P2-041 | User Story | FT-06 Session store tests (swapped/tampered blob, Valkey down 503, ACL both ways, DP keys) | F10.4 | Web\Bff | FE-S0b; security-test; FT-06 | 2 | 2 | A | TM10 FT-06 | P2-023 |
| P2-042 | User Story | FT-07 BFF rate-limit tests | F10.4 | Web\Bff | FE-S0b; security-test; FT-07 | 2 | 2 | A | TM10 FT-07 | P2-026 |
| P2-043 | User Story | FT-09 and FT-18 Header snapshot and BFF exception ProblemDetails | F10.4 | Web\Bff | FE-S0b; security-test; FT-09; FT-18 | 2 | 2 | A | TM10 FT-09, FT-18 | P2-023 |
| P2-044 | User Story | FT-13 and FT-19 Session-end tests (logout, logout-all, API down, EP-09/EP-37 transforms, Clear-Site-Data, no-store) | F10.4 | Web\Bff | FE-S0b; security-test; FT-13; FT-19 | 2 | 2 | A | TM10 FT-13, FT-19 | P2-023; P2-015 |
| P2-045 | User Story | Perf: Foundation baseline (Web Vitals lab p75 on sign-in and catalog skeleton; k6 NFR-F06 :8080 vs :8081, 50 VUs; bundle size) | F10.4 | Web | FE-S0b; perf | 2 | 2 | A | TSP2 P2.2 Performance; Q-D11 | P2-006 |
| P2-046 | User Story | Review gate: fresh-context dotnet-security-reviewer of BFF Session, RefreshCoordinator, CSRF and Proxy (no open High) | F10.4 | Web\Bff | FE-S0b; review | 2 | 2 | A | ADR-0015 Consequences | P2-040; P2-038; P2-041 |

### E11 UI design in Figma (14 items, 34 SP)
| Plan ID | Type | Title | Parent | Area | Tags | Pri | SP | VA | AC source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E11 | Epic | UI design in Figma (design lane) | – | Web\Design | FE-Design; design | 2 | – | B | DP figma | – |
| F11.1 | Feature | R1 design in Figma | E11 | Web\Design | FE-Design; FE-R1; design | 2 | – | B | DP apply_batches | – |
| F11.2 | Feature | R2 Admin design in Figma | E11 | Web\Design | FE-Design; FE-R2; design | 2 | – | B | WF R2 Admin screens | – |
| P2-047 | User Story | Figma: connect the Figma MCP; product owner duplicates the Obra shadcn/ui Community kit (MIT) and creates "Orders Shop – Design System" and "Orders Shop – Web" | F11.1 | Web\Design | FE-Design; FE-R1; design | 2 | 1 | A | DP figma.kit, files | – |
| P2-048 | User Story | Figma batch 0: inspect the kit against design-profile.yaml | F11.1 | Web\Design | FE-Design; FE-R1; design | 2 | 1 | A | DP | P2-047 |
| P2-049 | User Story | Figma batch 1: foundations (Theme Light/Dark, radius, spacing, typography, layout grids, success/warning/info, contrast fixes) | F11.1 | Web\Design | FE-Design; FE-R1; design | 2 | 4 | A | DP collections | P2-048 |
| P2-050 | User Story | Figma batch 2: components, icons and patterns (forms, 412 conflict, 429 countdown, loading/empty/error, dialogs) | F11.1 | Web\Design | FE-Design; FE-R1; design | 2 | 4 | A | DP; WF §0 | P2-049 |
| P2-051 | User Story | Figma batch 3: R1 screens 1-6 (Assumption: shell, AN-1..AN-8, CU-1, CU-2) | F11.1 | Web\Design | FE-Design; FE-R1; design | 2 | 4 | A | WF | P2-050 |
| P2-052 | User Story | Figma batch 4: R1 screens 7-12 and prototype (CU-3..CU-10, SY-1..SY-5) | F11.1 | Web\Design | FE-Design; FE-R1; design | 2 | 4 | A | WF | P2-051 |
| P2-053 | User Story | Design review R1: product owner approval, frame links on US-F020..F035, token diff vs code (P2-030) | F11.1 | Web\Design | FE-Design; FE-R1; design; review | 2 | 2 | A | USF DoR | P2-052 |
| P2-054 | User Story | Figma batch 5: Admin shell, dashboard, products, customers (AD-0..AD-9) | F11.2 | Web\Design | FE-Design; FE-R2; design | 2 | 4 | A | WF R2 | P2-053 |
| P2-055 | User Story | Figma batch 6: orders and the idle-timeout dialog (AD-10..AD-13) | F11.2 | Web\Design | FE-Design; FE-R2; design | 2 | 4 | A | WF R2 | P2-054 |
| P2-056 | User Story | Figma batch 7: reports, users, audit log and the step-up dialog (AD-14..AD-20) | F11.2 | Web\Design | FE-Design; FE-R2; design | 2 | 4 | A | WF R2; S-D11 | P2-055 |
| P2-057 | User Story | Design review R2: approval and frame links on R2 stories | F11.2 | Web\Design | FE-Design; FE-R2; design; review | 2 | 2 | A | USF DoR | P2-056 |

### E12 FE-R1 Customer portal (38 items, 67 SP)
| Plan ID | Type | Title | Parent | Area | Tags | Pri | SP | VA | AC source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E12 | Epic | FE-R1 Customer portal | – | `<root>` | FE-R1 | 2 | – | B | TSP2 P2.6 R1 | – |
| F12.1 | Feature | Access pages | E12 | Web\Customer | FE-R1 | 2 | – | B | USF | – |
| F12.2 | Feature | Catalog, cart and checkout | E12 | Web\Customer | FE-R1 | 2 | – | B | USF | – |
| F12.3 | Feature | My orders | E12 | Web\Customer | FE-R1 | 2 | – | B | USF | – |
| F12.4 | Feature | Account | E12 | Web\Customer | FE-R1 | 2 | – | B | USF | – |
| F12.5 | Feature | R1 enablers and security tests | E12 | Web | FE-R1; security-test | 2 | – | B | TM10 | – |
| F12.6 | Feature | R1 release gates | E12 | Delivery | FE-R1; review | 2 | – | B | TSP2 P2.6 | – |
| P2-058 | User Story | US-F020 Register | F12.1 | Web\Customer | FE-R1; US-F020 | 2 | 2 | B | USF AC1-4 | P2-051; P2-031 |
| P2-059 | User Story | US-F021 Confirm email | F12.1 | Web\Customer | FE-R1; US-F021 | 2 | 2 | B | USF AC1-3; NFR-F43 | P2-051 |
| P2-060 | User Story | US-F022 Resend confirmation | F12.1 | Web\Customer | FE-R1; US-F022 | 2 | 1 | B | USF AC1-2 | P2-061 |
| P2-061 | User Story | US-F023 Sign-in page | F12.1 | Web\Customer | FE-R1; US-F023 | 2 | 2 | B | USF AC1-3 | P2-051 |
| P2-062 | User Story | US-F024 Forgot password | F12.1 | Web\Customer | FE-R1; US-F024 | 2 | 1 | B | USF AC1-2 | P2-051 |
| P2-063 | User Story | US-F025 Reset password | F12.1 | Web\Customer | FE-R1; US-F025 | 2 | 2 | B | USF AC1-3 | P2-062 |
| P2-064 | User Story | US-F026 Browse and search the catalog | F12.2 | Web\Customer | FE-R1; US-F026 | 2 | 4 | B | USF AC1-4 | P2-051 |
| P2-065 | User Story | US-F027 Product detail | F12.2 | Web\Customer | FE-R1; US-F027 | 2 | 2 | B | USF AC1-2 | P2-064 |
| P2-066 | User Story | US-F028 Cart | F12.2 | Web\Customer | FE-R1; US-F028 | 2 | 4 | B | USF AC1-5; NFR-F41 | P2-052; P2-065 |
| P2-067 | User Story | US-F029 Place order | F12.2 | Web\Customer | FE-R1; US-F029 | 2 | 4 | B | USF AC1-5 | P2-066 |
| P2-068 | User Story | US-F030 My orders list | F12.3 | Web\Customer | FE-R1; US-F030 | 2 | 2 | B | USF AC1-3 | P2-067 |
| P2-069 | User Story | US-F031 Order detail (product-name lookup, G3 workaround) | F12.3 | Web\Customer | FE-R1; US-F031 | 2 | 2 | B | USF AC1-3 | P2-068 |
| P2-070 | User Story | US-F032 Cancel own order | F12.3 | Web\Customer | FE-R1; US-F032 | 2 | 2 | B | USF AC1-3 | P2-069 |
| P2-071 | User Story | US-F033 View and edit profile | F12.4 | Web\Customer | FE-R1; US-F033 | 2 | 2 | B | USF AC1-3 | P2-052 |
| P2-072 | User Story | US-F034 Change password | F12.4 | Web\Customer | FE-R1; US-F034 | 2 | 2 | B | USF AC1-2 | P2-071 |
| P2-073 | User Story | US-F035 Delete my account | F12.4 | Web\Customer | FE-R1; US-F035 | 2 | 2 | B | USF AC1-3; NFR-F17 | P2-071 |
| P2-074 | User Story | Enabler: Admin session limits in the BFF (30-min idle, 12 h absolute, LastActivityAt rules, idle seconds in /bff/user) | F12.5 | Web\Bff | FE-R1; enabler; security-followup; FT-12 | 2 | 4 | A | NFR-F12, F38; S-D10; TM10 FT-12 | P2-024 |
| P2-075 | User Story | Enabler: Light/Dark theme switch (system preference and manual toggle) | F12.5 | Web | FE-R1; enabler; design | 2 | 1 | A | DP theme | P2-030 |
| P2-076 | User Story | Enabler: SY-4 service-unavailable banner and @release resilience spec | F12.5 | Web | FE-R1; enabler; should | 3 | 1 | A | NFR-F18; WF SY-4 | P2-027 |
| P2-077 | User Story | Security follow-up: enforce Trusted Types | F12.5 | Web | FE-R1; security-followup | 2 | 2 | A | NFR-F35; S-D13 | P2-029 |
| P2-078 | User Story | FT-10 Stored-XSS payloads render as text (R1 data) | F12.5 | Web | FE-R1; security-test; FT-10 | 2 | 2 | A | TM10 FT-10 | P2-070 |
| P2-079 | User Story | FT-14 Cart storage tests | F12.5 | Web\Customer | FE-R1; security-test; FT-14 | 2 | 2 | A | TM10 FT-14 | P2-066 |
| P2-080 | User Story | FT-16 Confirm and reset links leave no token in the URL, Referer or logs | F12.5 | Web | FE-R1; security-test; FT-16 | 2 | 1 | A | TM10 FT-16 | P2-059; P2-063 |
| P2-081 | User Story | FT-08 Log and trace scan after a full E2E run | F12.5 | Web\Bff | FE-R1; security-test; FT-08 | 2 | 2 | A | TM10 FT-08 | P2-073 |
| P2-082 | User Story | Perf: pre-R1 Web Vitals p75 (sign-in, catalog, cart, order detail) and k6 NFR-F06 re-run | F12.6 | Delivery | FE-R1; perf | 2 | 2 | A | NFR-F04, F06; Q-D11 | P2-073 |
| P2-083 | User Story | Usability: G1 timed test, register to first order, 3 testers who did not build it (3 of 3 ≤ 5 min) | F12.6 | Delivery | FE-R1; usability | 2 | 2 | A | PRDF G1; TSP2 P2.2 | P2-070 |
| P2-084 | User Story | Accessibility: R1 manual pass (keyboard on every screen; NVDA + Firefox and VoiceOver + Safari on main tasks) | F12.6 | Delivery | FE-R1; a11y | 2 | 4 | A | NFR-F01..F03; TSP2 P2.2 | P2-073 |
| P2-085 | User Story | Browsers: WebKit on the https profile (@smoke, FT-01, FT-02, FT-14, FT-17), Firefox full, 360 px full, one real-device smoke | F12.6 | Delivery | FE-R1; test-infra; FT-17 | 2 | 2 | A | NFR-F08; TSP2 P2.5; TM10 FT-17 | P2-028; P2-073 |
| P2-086 | User Story | Design QA: R1 full pass (all screens, both themes, all states; no High) | F12.6 | Delivery | FE-R1; design-qa | 2 | 2 | A | TSP2 P2.6 | P2-053; P2-073 |
| P2-087 | User Story | Review gate: react-security-reviewer (SPA) and dotnet-security-reviewer (BFF changes since P2-046), no open High | F12.6 | Delivery | FE-R1; review | 2 | 2 | A | TSP2 P2.6 Security | P2-081 |
| P2-088 | User Story | Release: R1 sign-off (0 uncovered Must ACs, FT-01..FT-20 green, ApiFreeze green, Phase 1 API suite + ApiFiles green on the release commit, 0 quarantined front-end tests) and demo | F12.6 | Delivery | FE-R1; review | 2 | 2 | A | TSP2 P2.6 R1 | P2-082; P2-083; P2-084; P2-085; P2-086; P2-087 |

### E13 FE-R2 Admin back office (42 items, 83 SP)
| Plan ID | Type | Title | Parent | Area | Tags | Pri | SP | VA | AC source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E13 | Epic | FE-R2 Admin back office | – | `<root>` | FE-R2 | 2 | – | B | TSP2 P2.6 R2 | – |
| F13.1 | Feature | R2 readiness | E13 | Delivery | FE-R2; docs | 2 | – | B | TSP2 P2.3 | – |
| F13.2 | Feature | Admin shell, session and account | E13 | Web\Admin | FE-R2 | 2 | – | B | USF E-F3 | – |
| F13.3 | Feature | Products (Admin) | E13 | Web\Admin | FE-R2 | 2 | – | B | USF E-F3 | – |
| F13.4 | Feature | Customers (Admin) | E13 | Web\Admin | FE-R2 | 2 | – | B | USF E-F3 | – |
| F13.5 | Feature | Orders (Admin) | E13 | Web\Admin | FE-R2 | 2 | – | B | USF E-F3 | – |
| F13.6 | Feature | Reports and audit log | E13 | Web\Admin | FE-R2 | 2 | – | B | USF E-F3 | – |
| F13.7 | Feature | Users (Admin) | E13 | Web\Admin | FE-R2 | 2 | – | B | USF E-F3 | – |
| F13.8 | Feature | R2 security, quality and release | E13 | Delivery | FE-R2; review | 2 | – | B | TSP2 P2.6 R2 | – |
| P2-089 | User Story | Docs: number the R2 acceptance criteria (US-F036, US-F050..F071 as Given/When/Then AC1..n; remove needs-ac) | F13.1 | Delivery | FE-R2; docs | 2 | 4 | A | TSP2 P2.3 | – |
| P2-090 | User Story | Spike: visual regression for at most 5 stable screens, yes or no (time-box 0.25 day) | F13.1 | Web | FE-R2; spike; optional | 4 | 1 | A | Q-D10 | P2-088 |
| P2-091 | User Story | US-F050 Admin shell and landing page | F13.2 | Web\Admin | FE-R2; US-F050; needs-ac | 2 | 2 | B | USF table row → P2-089 | P2-089; P2-054; P2-126 |
| P2-092 | User Story | Enabler: Admin idle warning dialog at 28 minutes (UX-D4, WCAG 2.2.1) | F13.2 | Web\Admin | FE-R2; enabler; a11y | 2 | 2 | A | NFR-F12; WF idle text | P2-074; P2-055 |
| P2-093 | User Story | Enabler: step-up re-authentication for EP-16 and EP-19 (BFF 401 reauth-required, SPA sign-in dialog) | F13.2 | Web\Bff | FE-R2; enabler; security-followup; FT-12 | 2 | 4 | A | NFR-F38; S-D11; TM10 FT-12 | P2-074; P2-056 |
| P2-094 | User Story | US-F036 Sign out everywhere | F13.2 | Web\Customer | FE-R2; US-F036; should; needs-ac | 3 | 1 | B | USF AC1 → P2-089 | P2-089 |
| P2-095 | User Story | US-F051 Product list incl. deleted | F13.3 | Web\Admin | FE-R2; US-F051; needs-ac | 2 | 4 | B | USF table row → P2-089 | P2-091 |
| P2-096 | User Story | US-F052 Create product | F13.3 | Web\Admin | FE-R2; US-F052; needs-ac | 2 | 2 | B | as above | P2-095 |
| P2-097 | User Story | US-F053 Edit product (incl. stock) | F13.3 | Web\Admin | FE-R2; US-F053; needs-ac | 2 | 2 | B | as above | P2-096 |
| P2-098 | User Story | US-F054 Delete and restore product | F13.3 | Web\Admin | FE-R2; US-F054; needs-ac | 2 | 2 | B | as above | P2-095 |
| P2-099 | User Story | US-F055 Customer list | F13.4 | Web\Admin | FE-R2; US-F055; needs-ac | 2 | 2 | B | as above | P2-091 |
| P2-100 | User Story | US-F056 Customer detail and order history | F13.4 | Web\Admin | FE-R2; US-F056; needs-ac | 2 | 2 | B | as above | P2-099 |
| P2-101 | User Story | US-F057 Create customer | F13.4 | Web\Admin | FE-R2; US-F057; should; needs-ac | 3 | 2 | B | as above | P2-099 |
| P2-102 | User Story | US-F058 Edit customer | F13.4 | Web\Admin | FE-R2; US-F058; needs-ac | 2 | 2 | B | as above | P2-100 |
| P2-103 | User Story | US-F059 Delete customer | F13.4 | Web\Admin | FE-R2; US-F059; should; needs-ac | 3 | 1 | B | as above | P2-100 |
| P2-104 | User Story | US-F060 Orders list | F13.5 | Web\Admin | FE-R2; US-F060; needs-ac | 2 | 4 | B | as above | P2-091; P2-055 |
| P2-105 | User Story | US-F061 Order detail, status and cancel | F13.5 | Web\Admin | FE-R2; US-F061; needs-ac | 2 | 4 | B | as above | P2-104 |
| P2-106 | User Story | US-F062 Replace order items | F13.5 | Web\Admin | FE-R2; US-F062; should; needs-ac | 3 | 4 | B | as above | P2-105 |
| P2-107 | User Story | US-F063 Create order for a customer | F13.5 | Web\Admin | FE-R2; US-F063; should; needs-ac | 3 | 4 | B | as above | P2-104 |
| P2-108 | User Story | US-F064 Sales report | F13.6 | Web\Admin | FE-R2; US-F064; needs-ac | 2 | 4 | B | as above | P2-091; P2-056 |
| P2-109 | User Story | US-F065 Low-stock report | F13.6 | Web\Admin | FE-R2; US-F065; should; needs-ac | 3 | 2 | B | as above | P2-108 |
| P2-110 | User Story | US-F066 Top products | F13.6 | Web\Admin | FE-R2; US-F066; could; needs-ac | 4 | 2 | B | as above | P2-108 |
| P2-111 | User Story | US-F071 Audit-log viewer | F13.6 | Web\Admin | FE-R2; US-F071; should; needs-ac | 3 | 4 | B | as above | P2-091; P2-056 |
| P2-112 | User Story | US-F067 Users list and detail | F13.7 | Web\Admin | FE-R2; US-F067; needs-ac | 2 | 2 | B | as above | P2-091; P2-056 |
| P2-113 | User Story | US-F068 Create Admin user | F13.7 | Web\Admin | FE-R2; US-F068; should; needs-ac | 3 | 2 | B | as above | P2-112; P2-093 |
| P2-114 | User Story | US-F069 Lock and unlock user | F13.7 | Web\Admin | FE-R2; US-F069; needs-ac | 2 | 2 | B | as above | P2-112 |
| P2-115 | User Story | US-F070 Assign and remove Admin role | F13.7 | Web\Admin | FE-R2; US-F070; should; needs-ac | 3 | 2 | B | as above | P2-112; P2-093 |
| P2-116 | User Story | FT-10 extension: Admin data (names, reasons, audit values) render as text | F13.8 | Web\Admin | FE-R2; security-test; FT-10 | 2 | 2 | A | TM10 FT-10 | P2-111 |
| P2-117 | User Story | Perf: R2 Web Vitals (admin landing, products list), admin chunk lazy, NFR-F27 proven | F13.8 | Delivery | FE-R2; perf | 2 | 2 | A | NFR-F04, F05, F27 | P2-108 |
| P2-118 | User Story | Accessibility: R2 manual pass (1024 px, 768 px smoke; keyboard; screen reader on edit product, change status, lock user) | F13.8 | Delivery | FE-R2; a11y | 2 | 4 | A | TSP2 P2.2 | P2-115 |
| P2-119 | User Story | Design QA: R2 full pass | F13.8 | Delivery | FE-R2; design-qa | 2 | 2 | A | TSP2 P2.6 | P2-057; P2-115 |
| P2-120 | User Story | Review gate: security review of Admin areas and step-up (no open High) | F13.8 | Delivery | FE-R2; review | 2 | 2 | A | TSP2 P2.6 R2 | P2-093; P2-116 |
| P2-121 | User Story | Release: R2 sign-off (R1 gates + Admin at 1024 px, FT-12, WebKit smoke) and demo | F13.8 | Delivery | FE-R2; review | 2 | 2 | A | TSP2 P2.6 R2 | P2-117; P2-118; P2-119; P2-120 |

### E14 Front-end release and operations, light (7 items, 9 SP)
| Plan ID | Type | Title | Parent | Area | Tags | Pri | SP | VA | AC source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E14 | Epic | Front-end release and operations (light) | – | Delivery | FE-RelOps | 2 | – | B | §10.4 FE-RelOps | – |
| F14.1 | Feature | Front-end release and operations notes | E14 | Delivery | FE-RelOps | 2 | – | B | – | – |
| P2-122 | User Story | Docs: README and CLAUDE.md for the web front end (compose, https profile, npm scripts, E2E, email links, Demo-App project name) | F14.1 | Delivery | FE-RelOps; docs | 2 | 2 | A | FA §4; PRDF §11 | P2-088 |
| P2-123 | User Story | Ops: runbook additions (BFF sessions, demoapi_bff ACL, Valkey maxmemory headroom, unexpected-session-end metric, DP key volume, dev-cert regeneration) | F14.1 | Delivery | FE-RelOps; docs | 2 | 2 | A | S-D12; ADR-0015 | P2-088 |
| P2-124 | User Story | Docs: before-production register update (T-31 High as is, forwarded headers, DAST, MFA, DP keys in Blob + Key Vault, refresh lock for more than one BFF, real certificate + HSTS, DPoP, PII export question) | F14.1 | Delivery | FE-RelOps; docs; security-followup | 2 | 2 | A | TM10 §10.10-10.11 | P2-088 |
| P2-125 | User Story | CI: TemplateStrip removes web/ and its two .slnx entries | F14.1 | Template | FE-RelOps; ci; template | 2 | 1 | A | FA §3 | P2-036 |
| P2-126 | User Story | Release: timed fresh clone with the web service (≤ 15 min, 0 manual steps) | F14.1 | Delivery | FE-RelOps | 2 | 2 | A | PRD NFR-15; US-F009 AC1 | P2-122 |

### E15 API v1.1, deferred and unscheduled (8 new items, 23 SP indicative, plus 3 re-parents)
| Plan ID | Type | Title | Parent | Area | Tags | Pri | SP | VA | AC source | Pred |
|---|---|---|---|---|---|---|---|---|---|---|
| E15 | Epic | API v1.1 (deferred, unscheduled; lifts ApiFreeze) | – | `<root>` | API-v1.1 | 4 | – | B | PRDF §12; FE-D5 | – |
| F15.1 | Feature | API v1.1 changes | E15 | Platform | API-v1.1 | 4 | – | B | PRDF §12 | – |
| P2-127 | User Story | Enabler: API v1.1 release mechanics (allowApiChanges run, tag api-v1.1, re-export v1.json, regenerate schema.d.ts and Postman, BFF allow-list update, change notes) | F15.1 | Platform | API-v1.1; enabler | 4 | 2 | A | FA §5.1 | – |
| P2-128 | User Story | Forwarded headers with KnownProxies = BFF; restore API auth-strict 10/min and refresh 30/min; ADR-0010 update | F15.1 | Platform | API-v1.1; security-followup; gap-G1; ST-09; FT-07 | 4 | 4 | A | TM10 T-31; S-D9; ADR-0015 Revisit | P2-127 |
| P2-129 | User Story | G2 Anonymous catalog (AllowAnonymous on EP-22/23, cache and rate-limit review, authz matrix update, SPA public catalog route) | F15.1 | Products | API-v1.1; gap-G2; needs-ac | 4 | 4 | B | PRDF G2; FE-D1 | P2-127 |
| P2-130 | User Story | G3 Product name on order lines (incl. deleted products; SPA drops per-line lookups) | F15.1 | Orders | API-v1.1; gap-G3; needs-ac | 4 | 4 | B | PRDF G3 | P2-127 |
| P2-131 | User Story | G11 Customer changes own email (confirm new address, security stamp, PII-free audit; SPA profile) | F15.1 | Auth | API-v1.1; gap-G11; needs-ac | 4 | 8 | B | PRDF G11; D14 | P2-127 |
| P2-132 | User Story | Mask the email in the FakeEmailSender reset-link log line | F15.1 | Auth | API-v1.1; security-followup; ST-14 | 4 | 1 | A | TM10 §10.10 | P2-127 |
| #164 (P-131) | existing | US-106 Adjust product stock (G8): **re-parent to F15.1**, add `API-v1.1; gap-G8`, remove `Stretch` | F15.1 | Products | update | 4 | 4 | – | US2 | – |
| #165 (P-132) | existing | US-112 Restore a customer (G10): re-parent, add `API-v1.1; gap-G10`, remove `Stretch` | F15.1 | Customers | update | 4 | 8 | – | US2 | – |
| #167 (P-134) | existing | US-211 Customer edits items of an own Pending order (G9): re-parent, add `API-v1.1; gap-G9`, remove `Stretch` | F15.1 | Orders | update | 4 | 8 | – | US3 | – |

Each re-parented API item needs a follow-up SPA story when it is scheduled. Those stories are not created now.

---

## 3. Totals and board changes

| Type | New items |
|---|---|
| Epic | 7 (E9-E15) |
| Feature | 26 |
| User Story | 132 (48 US-F stories + 84 others) |
| Task / Bug | 0 |
| **Total new** | **165** (+ 3 updates: #164, #165, #167) |

| Epic | Items | SP | Time-box (days) |
|---|---|---|---|
| E9 FE-S0a | 27 | 61 | 15-17 |
| E10 FE-S0b | 29 | 58 | 14-17 |
| E11 FE-Design | 14 | 34 (R1 20, R2 14) | 4-5 + 3-4, parallel lane |
| E12 FE-R1 | 38 | 67 | 17-19 |
| E13 FE-R2 | 42 | 83 | 21-24 |
| E14 FE-RelOps | 7 | 9 | 2-3 |
| E15 API-v1.1 | 8 | 23 (unscheduled) | – |
| **Total** | **165** | **335** (312 scheduled) | **69-80** critical path; R1 + RelOps 48-56 |

Breakdown of the 84 non-US-F stories:

| Kind | Count |
|---|---|
| Enablers (incl. the P2-001 gate) | 14 |
| Figma design | 11 |
| Security tests (FT) | 13 |
| Test harness | 7 |
| Docs and ADRs | 6 |
| Spikes | 6 |
| CI | 5 |
| API v1.1 changes | 5 |
| Perf | 3 |
| Review gates | 3 |
| Release | 3 |
| Accessibility passes | 2 |
| Design QA | 2 |
| Security follow-up | 1 |
| Usability | 1 |
| Browsers | 1 |
| Ops | 1 |

Other counts:
- Value Area: B on 51 stories, all Epics and all Features; A on 81 stories.
- Priority 3 (should): 11 items. Priority 4: 10 items (8 in E15, plus P2-090 and P2-110).
- Links: 158 parent links and 161 predecessor links (`Dependency-Forward` on the predecessor), plus 2 Related links (P2-001 → #169, #170).

### Board changes for `azdo-boards-manager` (preview first, apply after approval)

1. **Project rename (note only; do not resolve here).** The product owner renamed the project `Demo-API` → `Demo-App` on 2026-10-30. `azdo-map.md` still shows `Project: Demo-API`, the team `Demo-API Team`, the board URL, all 172 item URLs (`/Demo-API/_workitems/...`) and `Demo-API\<leaf>` area paths. `CLAUDE.md` also says `Demo-API`. Use the read tools to verify:
   - the project name;
   - the root area and iteration node names (Microsoft says the root area matches the project name; confirm it changed);
   - whether the team name changed;
   - that old URLs still resolve.

   The main session updates the map header, URLs and CLAUDE.md after that check. `AB#<id>` links use IDs, so they are not affected.
2. **New area nodes** (manual UI step; the MCP has no area tool): `<root>\Web`, `Web\Bff`, `Web\Customer`, `Web\Admin`, `Web\Design`. "Include sub areas" is already on.
3. **New tags (86, created on first use):**
   - 7 slice tags: `FE-S0a`, `FE-S0b`, `FE-Design`, `FE-R1`, `FE-R2`, `FE-RelOps`, `API-v1.1`
   - 4 kind tags: `design`, `design-qa`, `a11y`, `usability`
   - 48 `US-F0xx`
   - 20 `FT-xx`
   - 7 `gap-Gx`

   Tags are case-insensitive, so check them against the existing 166 tags.
4. **Board settings (if DL-D9 is approved):** add a `Design` swimlane. Raise In progress WIP from 1 to 2, with the "one card per lane" rule written in the column's Definition of Done. Optional card styles: grey for `needs-ac`, purple for `design`.
5. **Updates:** re-parent #164, #165 and #167 under F15.1 and change their tags.
6. **Shared queries:** "FE current slice (tag)", "needs-ac (R2)", "Design lane", "API v1.1 (unscheduled)".
7. **Iteration:** `<root>` on every item, with the root name after the rename.
8. **Phase 1 reconciliation:** 47 items are Active and none is Closed. This work is inside P2-001; it is not new items.

