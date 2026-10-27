# ADR-0002: Use FastEndpoints 8.3 with FastEndpoints.OpenApi and Scalar, kept thin

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Product owner, architect |
| Consulted | stack-versions-2026-10-24.md, pre-mortem F7 |

Revised 2026-10-27 (architecture review R-4): no build-time OpenAPI export; drift is checked by an integration test against the committed file; Postman collection generated from it. `FastEndpoints.Messaging.Core` 8.3.0 verified; 8.3.x pinned through S1-S4.

## Context
The product owner fixed FastEndpoints as the endpoint framework. FastEndpoints 8.3.0 (MIT) is stable; 9.0 is beta. The maintainer warned the project may go "bugfix only" if sponsorship stays low (R6). `FastEndpoints.Swagger` (NSwag) is feature-frozen and deprecated after 2026-11-10. `FastEndpoints.OpenApi` builds on `Microsoft.AspNetCore.OpenApi`. FR-133 needs a full OpenAPI document; Scalar UI only in Development. This ADR records the choice, the risk and the exit path.

## Decision drivers
- Fixed stack (product owner).
- Long-term support risk (R6, F7).
- OpenAPI 3.1 from the Microsoft pipeline, committed to `api-tests/` and used to generate the Postman collection.
- Every endpoint must carry an auth rule, a validator and a summary that a test can read (P5).

## Options considered

### Option 1: FastEndpoints 8.3 + FastEndpoints.OpenApi + Scalar
- Good: one class per endpoint fits slices; built-in FluentValidation, groups, `Summary<T>`, command bus; endpoint metadata is easy to check in tests; OpenAPI on the Microsoft stack.
- Bad: funding risk; a third-party abstraction over ASP.NET Core; FastEndpoints-specific send/bind APIs.
- Cost / effort: low now; exit cost bounded by the thin-endpoint rules.

### Option 2: ASP.NET Core Minimal APIs + Microsoft.AspNetCore.OpenApi + Scalar
- Good: no third-party framework risk; Microsoft-supported; built-in validation in .NET 10.
- Bad: not the fixed stack; endpoint classes, validators and grouping need own conventions.
- Cost / effort: medium (own conventions).

### Option 3: MVC controllers
- Good: well known; mature.
- Bad: controller classes group many actions, which fights slices; more ceremony; not the fixed stack.
- Cost / effort: medium.

### Option 4: FastEndpoints.Swagger (NSwag) for OpenAPI
- Bad: deprecated after 2026-11-10. Rejected.

## Decision
We will use FastEndpoints 8.3.x with FastEndpoints.OpenApi and Scalar.AspNetCore 2.18.3 (Scalar in Development only), because it is the fixed stack and fits slices well. We will not use FastEndpoints.Swagger or the 9.0 beta. To limit R6 we keep FastEndpoints at the edge:
- Endpoints only bind, map, dispatch and map `Result` to HTTP (≤ 30 lines).
- Handlers depend only on `FastEndpoints.Messaging.Core` interfaces or our own `IQueryHandler<,>`; never on the main package or ASP.NET Core (architecture test A4).
- Validators inherit `Validator<T>` (a FluentValidation `AbstractValidator<T>`).
- `Result` → HTTP mapping lives in one BuildingBlocks class.
- The OpenAPI document is committed as `api-tests/openapi/v1.json`. No build-time export: an integration test fetches `/openapi/v1.json` from the test host and compares it with the committed file (drift fails the test; a documented command refreshes the file). The Postman collection is generated from this file; `.http` files stay hand-written.

**Exit path:** endpoints become Minimal API handlers grouped per feature; `Validator<T>` base changes to `AbstractValidator<T>` with an endpoint filter; `Summary<T>` becomes `.WithSummary/.WithDescription/.Produces`; commands dispatch through a small hand-written dispatcher or `Mediator` (ADR-0003). Domain, Application, Infrastructure and most tests do not change. The exported OpenAPI document is the regression check for the move.

## Consequences
- Good: fast endpoint authoring; DoD metadata testable (A9).
- Bad / trade-offs: FastEndpoints-specific code in Api; a move costs about 1-2 days per 50 endpoints (Assumption).
- Follow-up actions: S1a: OpenAPI drift integration test. `FastEndpoints.Messaging.Core` 8.3.0 is verified to exist; pin FastEndpoints packages to 8.3.x through S1-S4. Watch releases; no release for 3 months, or a "bugfix only" announcement before S3 = write the exit ADR (pre-mortem kill criterion).

## Revisit when
- FastEndpoints goes bugfix-only or 9.0 becomes stable with breaking changes.
- .NET 11 changes Minimal API features enough to remove the gap.

## Links
- [solution-structure.md](../solution-structure.md) section 7
- ADR-0001, ADR-0003
- PRD R6, FR-133; pre-mortem F7
