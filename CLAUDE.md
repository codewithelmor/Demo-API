# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state

Phase 1 (the API, slices S1a-S4) is built on branch `feature/build-orders-api` and not yet merged. Phase 2 (a React + TypeScript web front end behind an ASP.NET Core BFF, no API source changes) is planned: see `docs/product/02-requirements/prd-frontend.md`, ADR-0015, `docs/product/04-architecture/frontend-architecture.md` and `docs/product/06-delivery/delivery-plan.md` §10. Start with `docs/product/00-summary.md`; it holds the snapshot, phase status, every key decision, and the next step.

The product is the **Orders API**: a .NET 10 REST API (no UI) for products, customers and orders, with Admin and Customer roles. It is a proof of concept with production-grade practices. Its long-term goal is a reusable `dotnet new` template (only SharedKernel, BuildingBlocks, Auth, AdminUsers and Products are kept; see solution-structure §13).

## Where the plan lives

| Need | Document |
|---|---|
| Decisions log, open questions, next step | `docs/product/00-summary.md` |
| Requirements, Definition of Done (§15), decisions D1-D16 (§16), P1-P10 (§17) | `docs/product/02-requirements/prd.md` |
| Who may call what (Admin / Customer / anonymous) | `docs/product/02-requirements/permission-matrix.md` |
| Projects, folders, dependency rules, architecture tests, compose, pipeline | `docs/product/04-architecture/solution-structure.md` |
| Per-endpoint route, codes, auth, rate limit, cache rule | `docs/product/04-architecture/endpoint-catalog.md` |
| Tables, indexes, views, functions, DB roles | `docs/product/04-architecture/data-model.md` |
| Why each choice was made | `docs/product/04-architecture/adr/0001-0014` |
| Test layers, tags, flaky-test policy, slice exit gates | `docs/product/07-quality/test-strategy.md` |
| Slices, time-boxes, board rules | `docs/product/06-delivery/delivery-plan.md` |
| Work item IDs per story | `docs/product/azdo-map.md` |
| Pinned versions and licences | `docs/product/stack-versions-2026-10-24.md` |

When code and these documents disagree, raise it; do not silently change a decision. Spike results in S1a are written back into the ADRs.

## Stack (fixed; do not swap without approval)

.NET 10 (SDK 10.0.402), FastEndpoints 8.3.x (command bus for CQRS, no MediatR), FluentValidation, EF Core 10 + Npgsql (writes, migrations), Dapper (views, functions, one stored procedure), PostgreSQL 18, Valkey 9.1 via HybridCache (not Redis), ASP.NET Core Identity with our own JWT issuer and rotated refresh tokens (no `MapIdentityApi`), Microsoft.Extensions.Resilience (no direct Polly), ASP.NET Core rate limiting, FastEndpoints.OpenApi + Scalar (not FastEndpoints.Swagger). Tests: xUnit v3, Shouldly (not FluentAssertions), NSubstitute, Bogus, Testcontainers, Respawn, ArchUnitNET. CI: Azure Pipelines; repo and board in Azure DevOps (org `codewithelmor`, project `Demo-App`, renamed from `Demo-API` on 2026-10-30).

## Planned commands

These come from the plan and will work once S1a creates the solution.

```bash
docker compose up -d                       # secrets-init -> postgres -> migrator -> api, no manual steps; API on http://localhost:8080 (Scalar at /scalar)
docker compose exec api cat /run/secrets/dev/SeedAdmin__Password   # dev Admin password
docker compose --profile observability up  # adds the Aspire dashboard (127.0.0.1:18888)
docker compose down -v                     # reset data and regenerate dev secrets

dotnet restore --locked-mode
dotnet build -c Release                    # warnings are errors
dotnet format --verify-no-changes
pwsh deploy/scripts/Initialize-DotnetRun.ps1   # once: publishes Postgres 5432 and Valkey 6379 on 127.0.0.1, sets user secrets (stop any local PostgreSQL first)
dotnet run --project src/DemoApi.Api       # outside Docker; Development environment, uses those user secrets

dotnet test tests/DemoApi.UnitTests
dotnet test tests/DemoApi.ArchitectureTests
dotnet test tests/DemoApi.IntegrationTests # needs Docker (Testcontainers)
dotnet test --filter "FullyQualifiedName~CreateProductTests"   # single test class
dotnet test --filter "Category!=Quarantine"
```

`.http` files in `api-tests/http/` run with the httpyac CLI; the Postman collection in `api-tests/postman/` is generated from the committed `api-tests/openapi/v1.json`.

## Architecture (planned)

**Clean Architecture decides the projects; vertical slices decide the folders inside them.** Root namespace `DemoApi`, solution `DemoApi.slnx`.

- `src/BuildingBlocks/DemoApi.SharedKernel`: BCL only. `Result`/`Error`, `IAuditable`, `IDeletable`, `IDateTimeProvider`, `ICurrentUser`, `IAppCache`, `IQueryHandler<,>`, `ISqlRunner`, `ITransactionRunner`.
- `src/BuildingBlocks/DemoApi.BuildingBlocks`: reusable plumbing with **no domain references** (interceptors, cache, auth setup, ProblemDetails, rate limiting, health, OTel, OpenAPI).
- `src/DemoApi.Domain` → `DemoApi.Application` → `DemoApi.Infrastructure`; `src/DemoApi.Api` is the only composition root.
- First folder level in each project is the business feature (`Products`, `Customers`, `Orders`, `Reports`, `Auth`, `AdminUsers`). No technical folders like `Commands/` or `Handlers/`. Feature folders must not reference each other; shared code goes to Domain or `Application/Common`.

Request flow: FastEndpoints endpoint (bind + `Validator<T>` for input shape) → maps request to a command → `command.ExecuteAsync(ct)` → handler in Application calls domain methods and `SaveChangesAsync` → `Result` mapped to HTTP by one extension class. Queries skip the bus: endpoints inject an `IQueryHandler<,>` that uses an EF `AsNoTracking` projection or Dapper SQL through `ISqlRunner`.

Rules enforced by architecture tests or banned APIs (build errors):
- Endpoints hold no business logic (target ≤ 30 lines). Handlers depend only on `FastEndpoints.Messaging.Core`, never on the main FastEndpoints package or ASP.NET Core. Application must not reference Infrastructure, BuildingBlocks or Npgsql.
- Never return entities from endpoints; request DTOs live in Api, response DTOs in Application.
- Every endpoint declares an explicit auth rule, a validator (if it has properties) and an OpenAPI summary/description.
- Banned: `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow` (use `IDateTimeProvider`), `ExecuteDelete*`, `ExecuteUpdate*`, `Thread.Sleep`, Newtonsoft.Json.
- Expected errors return `Result`/`Error`; exceptions are for bugs and infrastructure failures only.

## Data rules that are easy to break

- **Never delete rows.** Soft delete via `IDeletable` + interceptor + global query filters. The app DB role (`demoapi_app`) has no DELETE privilege, except on `asp_net_user_roles`. Unique indexes are partial (`WHERE is_deleted = false`).
- **Dapper bypasses EF query filters**: every view/function/SQL must filter `is_deleted = false` explicitly (reports intentionally keep deleted customers/products; see PRD §16). Dapper SQL is constant or built from allow-listed fragments.
- All writes go through EF Core so the audit interceptor writes `AuditLogs` in the same transaction. Fields marked `[PersonalData]` are never stored as values in the audit log.
- All dates are `DateTimeOffset`, stored as `timestamptz`, UTC only. Money is `numeric(12,2)`.
- Multi-step writes (register, create order, erase customer) use `ITransactionRunner`. EF `EnableRetryOnFailure` is the only DB retry layer; do not add another.
- Concurrency: ETag from `xmin`; `PUT` needs `If-Match` (428 if missing, 412 if stale). Order stock is locked with `SELECT … FOR UPDATE` in product ID order.
- Customers use `/customers/me/...` routes; another customer's data returns 404, not 403. ID routes are Admin only.
- Only Products uses the cache (`IAppCache`).

## Definition of Done for each endpoint (PRD §15)

DTOs + validator + explicit auth rule; authz tests for Anonymous/Customer/Admin plus BOLA on `/me` routes; ProblemDetails with `traceId`; audit row on writes; OpenAPI description, `.http` entry and Postman request; unit + integration tests; rate-limit and cache rule from the endpoint catalog; fresh-context review by `dotnet-code-reviewer` (and `dotnet-security-reviewer` when auth, personal data or tokens are touched).

## Workflow

- Kanban, WIP 1, one story per PR. Commits and PRs reference the work item as `AB#<id>` (IDs in `docs/product/azdo-map.md`). `main` has a branch policy (PR + green build, no direct push).
- Only `azdo-boards-manager` writes to Azure DevOps Boards, always preview first, apply after approval.
- No automatic test retries. A flaky test is fixed or tagged `[Trait("Category","Quarantine")]` with a Bug logged (max 3). Never quarantine authz/BOLA, token, concurrency or audit tests. Test tags: `[Story("US-xxx","ACn")]`, `[Endpoint("EP-xx")]`, `[Security("ST-xx")]`, `[Audit("EP-xx")]`, `Category` = `Serial`/`Slow`/`Quarantine`.
- Full test suite must stay ≤ 5 minutes.
- After any prompt that uses an agent or skill, add a row to `docs/product/notes.md` (section 1 for user commands, section 2 for agent calls).
