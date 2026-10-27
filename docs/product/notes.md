# Notes: agents and skills used



Last updated: 2026-10-27 | Related: [summary](00-summary.md)



This log lists which Claude Code agents and skills produced each planning document, from the first prompt onward. Add a row for every future prompt that uses an agent or skill.



- **Preloaded skills:** listed in the agent's definition file (`skills:` in its frontmatter). They load whenever that agent runs.

- **Tools:** WebSearch and WebFetch were used to check versions, licences and standards.

- **Main session:** talks to you, merges the agents' drafts, checks them against each other, and saves files to `docs/product/`. Advisor agents never write files.



## 1. Commands typed by the user



| Date | Command | Purpose |

|---|---|---|

| 2026-10-24 | `/product-brainstorm` (skill) | Started the guided session from the brief in `product-brainstorm-template-dotnet10-webapi.md`. The skill sets the phase order, approval gates, the 5-questions-per-message rule and the tracker rules |

| 2026-10-24 to 2026-10-27 | `/rate-limit-options` | Run by the user during the session (not an agent call) |



## 2. Agent calls so far



| # | Date | Phase | Agent | Preloaded skills | Tools used | Output |

|---|---|---|---|---|---|---|

| 1 | 2026-10-24 | Setup | `general-purpose` | – | WebSearch, WebFetch | [stack-versions-2026-10-24.md](stack-versions-2026-10-24.md) (versions and licences) |

| 2 | 2026-10-24 | 1 Concept | `product-strategist` | `lean-canvas` | – | [01-concept/concept.md](01-concept/concept.md) |

| 3 | 2026-10-24 | 2 Requirements | `requirements-analyst` | `prd-writer`, `user-story-writer` | WebSearch | [prd.md](02-requirements/prd.md), [endpoint-list.md](02-requirements/endpoint-list.md), [permission-matrix.md](02-requirements/permission-matrix.md) |

| 4-6 | 2026-10-24 | 2 Requirements | `requirements-analyst` (3 in parallel) | `prd-writer`, `user-story-writer` | WebSearch | User stories: [01 auth & admin](02-requirements/user-stories/01-auth-and-admin-users.md), [02 products & customers](02-requirements/user-stories/02-products-and-customers.md), [03 orders, reports & platform](02-requirements/user-stories/03-orders-reports-and-platform.md) |

| 7 | 2026-10-24 | 2 Requirements | `devils-advocate` | `pre-mortem` | WebSearch, WebFetch | [pre-mortem.md](02-requirements/pre-mortem.md) → decisions P1-P10 |

| 8 | 2026-10-24 | 6 Architecture | `solution-architect` | `c4-diagram`, `adr-writer` | – | [architecture.md](04-architecture/architecture.md) (C4, flows, ADR index) |

| 9 | 2026-10-24 | 6 Architecture | `dotnet-architect` | `dotnet-solution-setup`, `dotnet-clean-architecture`, `dotnet-vertical-slice`, `dotnet-architecture-tests` | – | [solution-structure.md](04-architecture/solution-structure.md), ADR 0001-0003, 0011-0013 |

| 10 | 2026-10-24 | 6 Architecture | `dotnet-data-specialist` | `dotnet-data-access-decision`, `dotnet-ef-core`, `dotnet-dapper`, `db-sqlserver` | – | [data-model.md](04-architecture/data-model.md), ADR 0004-0007 |

| 11 | 2026-10-24 | 6 Architecture | `dotnet-api-designer` | `dotnet-api-design`, `dotnet-result-and-errors`, `dotnet-api-security-hardening`, `dotnet-authz` | – | [endpoint-catalog.md](04-architecture/endpoint-catalog.md), ADR 0008-0010 |

| 12 | 2026-10-27 | 6 Architecture | `devils-advocate` | `pre-mortem` | WebSearch, WebFetch | [architecture-review.md](04-architecture/architecture-review.md) → changes R-1..R-5 |

| 13 | 2026-10-27 | 6 Architecture | `dotnet-architect` (resumed) | as #9 | – | Applied R-1..R-5 to the architecture docs and ADRs |

| 14 | 2026-10-27 | 7 Security | `security-privacy-reviewer` | `threat-model` | WebSearch, WebFetch | [05-security/threat-model.md](05-security/threat-model.md) → decisions S-D1..S-D8 |

| 15 | 2026-10-27 | 7 Security | `dotnet-architect` (resumed) | as #9 | – | Applied S-D1..S-D8: ADR 0007-0010, 0012, 0013, new [ADR 0014](04-architecture/adr/0014-authorization-model.md), data model, architecture, endpoint catalog, PRD (new FR-096, section 18), permission matrix, user stories 01 and 03 |



| 16 | 2026-10-27 | 9 Quality | `qa-strategist` | `test-strategy` | WebSearch, WebFetch | `07-quality/test-strategy.md` approved, decisions Q-D1..Q-D6 |
| 17 | 2026-10-27 | 8 Delivery | `delivery-planner` | – | WebSearch, WebFetch | `06-delivery/delivery-plan.md` (approved, DL-D1..DL-D6): roadmap, time-boxes, risk register, 168-item Azure DevOps preview |
| 18 | 2026-10-27 | 8 Delivery (tracker) | `azdo-boards-manager` (preview only) | `work-item-standards`; `azdo-setup`, `azdo-plan-to-work-items` | none (Azure DevOps read tools not connected) | Draft setup preview + 168-item work-item preview from the docs; blocked on org/project and `ado-read` sign-in; nothing created |

Notes:

- Usage limits stopped agents #8-#11 twice (a session limit, then the weekly limit). They were resumed, not restarted.

- Agents that ran at the same time named some things differently. The main session fixed the clashes (database role names, Valkey timeouts, the rate-limit order in the request pipeline).

- One hook blocked a `dotnet-architect` command containing the word "Production"; the agent ran the edit from a script file instead (docs only). The hook is `guard-api-commands`. Later agents are told to stop and report hook blocks; none was blocked in #15.



## 3. Phases skipped



| Phase | Reason |

|---|---|

| 3 Research (`ux-researcher`) | API only; users and problem are clear |

| 4 UX design (`ux-designer`) | API only |

| 5 UI design (`product-designer`, Figma) | API only, no Figma |



## 4. Planned for the coming prompts



| Phase | Agent | Preloaded / related skills | Output |

|---|---|---|---|

| 9 Quality (API tests) | `api-test-architect` (optional) | `api-test-structure`, `api-http-files`, `api-postman-collections` | `.http` + Postman plan |



| 10 Release (light) | `devops-platform-engineer` / `release-readiness` skill | `release-readiness` | Release checklist (local Docker only) |

| 11 Operations (light) | `sre-operations` | `runbook-writer` | Health, logging and runbook notes |

| Template extraction (P7) | `dotnet-architect` | `dotnet-solution-setup` | `dotnet new` template / strip guide plan |

| Build (after plan approval only) | `build-app-builder`, `dotnet-test-engineer`, `api-collection-engineer`, `dotnet-code-reviewer`, `dotnet-security-reviewer` | dotnet-/db-/api- skills | Code, tests, `.http`, Postman, reviews |



## 5. How to keep this file current

- After each prompt that calls an agent or skill, add a row to section 2 with the date, phase, agent, preloaded skills, tools and output link.

- Move a row from section 4 to section 2 once it has run.

