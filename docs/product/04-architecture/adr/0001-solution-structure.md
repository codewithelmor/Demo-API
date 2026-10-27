# ADR-0001: Use Clean Architecture projects with vertical-slice folders and CQRS on one database

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Product owner, architect |
| Consulted | Pre-mortem (devils-advocate), PRD section 17, architecture review |

Revised 2026-10-27 (architecture review R-3, R-4, R-5): `ITransactionRunner` moves to SharedKernel; S1 has only the core architecture rules; the strip guide lists every Customer leak and the strip CI job runs at the end of S2.

## Context
The stack fixes "Clean Architecture + Vertical Slice + CQRS". These overlap: Clean Architecture splits by layer, vertical slices split by feature. The pre-mortem (section 3, item 4) asks which one decides the layout. The main criterion is P7/F2: after S4 the shared parts become a `dotnet new` template, so stripping the Orders domain must be easy. One developer builds 51 endpoints, so ceremony per endpoint must stay small.

## Decision drivers
- Easy to strip the sample domain (P7, F2).
- Layer rules enforced by the compiler and by architecture tests (NFR-17).
- Few files and few places to touch per endpoint (F1, one developer).
- Handlers independent of FastEndpoints (R6, F7).

## Options considered

### Option 1: Single web project with feature folders
- Good: fewest files; fastest per endpoint; matches the right-sizing ladder for a small app.
- Bad: no compile-time boundary; building blocks mix with domain code, so template extraction becomes manual; handlers sit next to FastEndpoints code.
- Cost / effort: lowest now, highest at extraction.

### Option 2: Clean Architecture projects with technical folders (`Commands/`, `Handlers/`, `Validators/`)
- Good: familiar; strong layer rules.
- Bad: one feature is spread over many folders in four projects; stripping a feature means hunting by name.
- Cost / effort: medium; high ongoing friction.

### Option 3: Clean Architecture projects decide the boundaries; vertical-slice feature folders inside each project; separate BuildingBlocks with no domain references
- Good: compiler enforces layers; each feature is one folder name across projects, so stripping = delete folders; BuildingBlocks are the template core and are tested to have no domain references.
- Bad: six source projects; one use case spans 2 projects (Api endpoint + Application handler).
- Cost / effort: medium; one-time setup in S1.

## Decision
We will use Option 3. Clean Architecture decides the projects: `SharedKernel`, `BuildingBlocks`, `Domain`, `Application`, `Infrastructure`, `Api`. Vertical slices decide the folders inside them (`Features/<Area>/<UseCase>/`). CQRS means separate command and query handlers on one PostgreSQL database: commands through the FastEndpoints command bus (ADR-0003), queries through EF Core projections or Dapper SQL, called directly. This best serves P7 and keeps handlers free of the web framework.

## Consequences
- Good: template extraction is a folder delete plus a short list of known Customer edits (`ICurrentUser.CustomerId`, `customer_id` claim, `CustomerOnly` policy, `Roles`, register; solution-structure section 13). Architecture tests guard the rules: core layer rules and banned APIs from S1, the rest when first needed.
- Good: Application may use EF Core LINQ and Dapper directly; no repository layer to maintain.
- Bad / trade-offs: more projects than a typical small client app needs. The strip guide will offer merging Domain + Application for small apps.
- Bad / trade-offs: Application depends on EF Core; swapping the ORM is not a goal.
- Follow-up actions: create the structure in S1a; add the core architecture tests and prove each fails once; add a CI job at the end of S2 that strips the sample domain on a copy, then builds and tests it.

## Revisit when
- The template is used for an app with no real domain rules (consider the single-project variant).
- A second front end or a worker needs the same use cases (structure already supports it).

## Links
- [solution-structure.md](../solution-structure.md)
- ADR-0002, ADR-0003
- PRD P5, P7; pre-mortem F2, F7
