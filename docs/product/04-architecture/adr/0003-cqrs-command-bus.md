# ADR-0003: Dispatch commands with the FastEndpoints command bus; call query handlers directly

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Product owner, architect |
| Consulted | stack-versions-2026-10-24.md |

Revised 2026-10-27 (architecture review R-3, section 3): `FastEndpoints.Messaging.Core` 8.3.0 verified, fallback spike removed, 8.3.x pinned through S1-S4; `ITransactionRunner` lives in SharedKernel.

## Context
CQRS needs a way for endpoints to reach command handlers. The brief rules out MediatR (14.x is RPL-1.5 or commercial). The product owner chose the FastEndpoints command bus (MIT, already in the stack). Handlers must stay framework-light so FastEndpoints can be replaced (ADR-0002). Queries are simple reads (EF projections, Dapper) that gain little from a bus.

## Decision drivers
- Permissive licence only (NFR-18).
- No extra dependency if the stack already has one.
- Handlers testable without a web host; cheap exit from FastEndpoints.
- Simple for one developer: no hidden pipeline behaviour.

## Options considered

### Option A: FastEndpoints command bus (`ICommand<T>`, `ICommandHandler<,>`, `ExecuteAsync`)
- Good: MIT; already in the stack; auto-registers handlers; supports command middleware; handlers only need the `FastEndpoints.Messaging.Core` interfaces.
- Bad: ties dispatch to FastEndpoints (R6); command middleware is less known than MediatR behaviours.
- Cost / effort: none extra.

### Option B: MediatR 14
- Good: widely known.
- Bad: RPL-1.5 or commercial licence; fails NFR-18. Rejected.

### Option C: Mediator (martinothamar) 3.0.2
- Good: MIT; source-generated, fast; near-identical shape to MediatR.
- Bad: one more dependency for what the stack already provides.
- Cost / effort: low; the natural exit target.

### Option D: Hand-written interfaces and dispatcher
- Good: no dependency; full control.
- Bad: we write and maintain registration and dispatch code.
- Cost / effort: low (~50 lines).

## Decision
We will use the FastEndpoints command bus for **commands** (Option A), because it is MIT, already in the stack and needs no new package. **Queries** use our own `IQueryHandler<TQuery, TResult>` (SharedKernel), registered by assembly scan and injected into endpoints directly; no bus for reads. Command middleware is used only for logging and tracing. Validation stays at the endpoint, business rules in the domain, transactions explicit in handlers (`ITransactionRunner`, SharedKernel). Handlers return `Result<T>`.

`FastEndpoints.Messaging.Core` 8.3.0 is verified to exist, so Application references only that package, never the main `FastEndpoints` package. All FastEndpoints packages are pinned to 8.3.x through S1-S4. Option C stays the exit target.

## Consequences
- Good: zero licence risk; small, explicit pipeline; handlers are plain classes, unit-tested with NSubstitute.
- Bad / trade-offs: two handler interfaces (commands vs queries) to learn; dispatch tied to FastEndpoints until an exit.
- Follow-up actions: architecture tests A7 (sealed, `Handler` suffix) and A8 (one handler per command). Exit to Option C is a rename of interfaces and `ExecuteAsync` calls.

## Revisit when
- FastEndpoints exit (ADR-0002).
- Messaging/outbox needs appear (consider WolverineFx, MIT).

## Links
- ADR-0001, ADR-0002
- [solution-structure.md](../solution-structure.md) section 6
