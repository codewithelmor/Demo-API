# Solution structure: Orders API

| Field | Value |
|---|---|
| Status | Approved |
| Date | 2026-10-24 (revised 2026-10-27, architecture review R-1..R-5; security S-D3, S-D7 compose hardening) |
| Inputs | [PRD](../02-requirements/prd.md) (sections 7, 15, 16, 17), [endpoint list](../02-requirements/endpoint-list.md), [pre-mortem](../02-requirements/pre-mortem.md), [stack versions](../stack-versions-2026-10-24.md) |
| ADRs | [0001 structure](adr/0001-solution-structure.md), [0002 FastEndpoints + OpenAPI](adr/0002-fastendpoints-and-openapi.md), [0003 command bus](adr/0003-cqrs-command-bus.md), [0011 resilience](adr/0011-resilience.md), [0012 local setup and secrets](adr/0012-local-setup-and-secrets.md), [0013 observability](adr/0013-observability.md) |

Planning only. No code exists yet. Names use the root namespace `DemoApi`.
Assumption: the solution is called `DemoApi` (from the repo name). It avoids a clash with the `Orders` feature, and the `dotnet new` template (P7) replaces it with `sourceName`.

## 1. The rule in one sentence

**Clean Architecture decides the projects. Vertical slices decide the folders inside each project.**

- Projects exist only where a compile-time boundary enforces a rule: Domain has no framework; Application has no ASP.NET Core or Npgsql; building blocks have no domain; Api is the only composition root.
- Inside Domain, Application, Infrastructure and Api, the first folder level is the **business feature** (`Products`, `Customers`, `Orders`, `Reports`, `Auth`, `AdminUsers`). Technical folders (`Commands/`, `Handlers/`, `Validators/`) are not used.
- Stripping a feature for the template (P7, F2) = delete the folder with the same name in each project, plus its migrations and tests. Section 13 lists the steps.

## 2. Projects

| Project | Kind | References (projects) | Main packages | Purpose |
|---|---|---|---|---|
| `src/BuildingBlocks/DemoApi.SharedKernel` | classlib | none | none | Pure .NET primitives: `Entity`, `IAuditable`, `IDeletable`, `[PersonalData]`, `Result`/`Error`, paging and sort types, `IDateTimeProvider`, `ICurrentUser`, `IAppCache`, `IQueryHandler<,>`, `ISqlRunner`, `ITransactionRunner`. |
| `src/BuildingBlocks/DemoApi.BuildingBlocks` | classlib (`FrameworkReference Microsoft.AspNetCore.App`) | SharedKernel | EF Core, Npgsql EF, Identity EF, HybridCache, StackExchangeRedis cache, Resilience, OpenTelemetry, FastEndpoints, FastEndpoints.OpenApi, Scalar | Reusable plumbing with **no domain references**: audit and soft-delete interceptors, `AuditLog` entity, connection-level DB circuit breaker interceptor, transaction and SQL runners, cache, health checks, ProblemDetails, rate limiting, security headers, JWT bearer setup, OpenAPI setup, OTel setup, `ICurrentUser` and `IDateTimeProvider` implementations. |
| `src/DemoApi.Domain` | classlib | SharedKernel | none | Entities, value objects, domain errors, status machine. Feature folders. |
| `src/DemoApi.Application` | classlib | Domain, SharedKernel | FastEndpoints.Messaging.Core, Microsoft.EntityFrameworkCore, Dapper | Commands + handlers, queries + handlers, response DTOs, mapping, SQL for Dapper reads. Feature folders. |
| `src/DemoApi.Infrastructure` | classlib | Application, BuildingBlocks | Npgsql EF, Identity EF, JwtBearer, JsonWebTokens | `AppDbContext` (`IdentityDbContext`), entity configurations, migrations (incl. views, functions, the stored procedure), Identity, JWT issuer, refresh-token store, fake email sender, seed Admin. |
| `src/DemoApi.Api` | web | Application, Infrastructure, BuildingBlocks | FastEndpoints | FastEndpoints endpoints, request DTOs, validators, OpenAPI summaries, `Program.cs` (composition root). |
| `src/DemoApi.AppHost` (optional, not in S1) | Aspire AppHost | Api | Aspire 13.6.3 | One-command F5 start with dashboard. See ADR-0012. |

Test projects are in section 9.

### Dependency rules

```
                 ┌────────────── DemoApi.Api (composition root) ──────────────┐
                 ▼                          ▼                                  ▼
       DemoApi.Infrastructure ──► DemoApi.Application ──► DemoApi.Domain     BuildingBlocks
                 │                          │                    │               │
                 └──────► BuildingBlocks    └──────────► SharedKernel ◄──────────┘
```

| From | May reference | Must not reference |
|---|---|---|
| SharedKernel | BCL only | everything else, any NuGet package |
| BuildingBlocks | SharedKernel, framework packages | `DemoApi.Domain`, `.Application`, `.Infrastructure`, `.Api` (P7) |
| Domain | SharedKernel | EF Core, ASP.NET Core, FastEndpoints, Dapper, Npgsql, BuildingBlocks |
| Application | Domain, SharedKernel, `FastEndpoints.Messaging.Core`, EF Core (LINQ, `DbSet`), Dapper | ASP.NET Core, `FastEndpoints` (main package), Npgsql, Infrastructure, BuildingBlocks |
| Infrastructure | Application, BuildingBlocks | Api |
| Api `Features/**` | Application, BuildingBlocks | Infrastructure, `DbContext`, Domain entities (enums allowed) |
| Api `Program.cs` / `DependencyInjection` | all | n/a |

Feature folders must not reference each other (`Application.Features.Orders` must not use `Application.Features.Products`). Shared logic goes to Domain or `Application/Common`.

Why Application may reference EF Core and Dapper: queries are written as EF projections or Dapper SQL inside the slice. An `IAppDbContext` (DbSets) and `ISqlRunner` abstraction keep Npgsql and connection handling in Infrastructure. A repository layer is not added (no swap of ORM is planned).

## 3. Root files

| File | Content |
|---|---|
| `DemoApi.slnx` | All projects, with `src/` and `tests/` solution folders. |
| `global.json` | SDK `10.0.402`, `rollForward: latestFeature`. |
| `Directory.Build.props` | `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `AnalysisLevel latest-recommended`, `AnalysisModeSecurity All`, `EnforceCodeStyleInBuild true`, `NuGetAuditMode all`, `RestorePackagesWithLockFile true`, `ContinuousIntegrationBuild` in CI, `InvariantGlobalization true`. `BannedSymbols.txt` + `Microsoft.CodeAnalysis.BannedApiAnalyzers` for every `src/` project. |
| `Directory.Packages.props` | `ManagePackageVersionsCentrally true`, `CentralPackageTransitivePinningEnabled true`. Versions below. |
| `nuget.config` | `<clear />`, nuget.org only, package source mapping `*`. |
| `BannedSymbols.txt` | `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `TimeProvider.System` (outside BuildingBlocks), `EntityFrameworkQueryableExtensions.ExecuteDelete*`, `ExecuteUpdate*`, `Thread.Sleep`, `Newtonsoft.Json`. |
| `.editorconfig`, `.gitignore`, `.gitattributes` | From `dotnet new`. `.gitattributes` forces `*.sh text eol=lf` (ADR-0012). `.gitignore` adds `.env`, `compose.override.yaml`, `*.pfx`, `appsettings.*.local.json`, `secrets/`. |
| `.config/dotnet-tools.json` | `dotnet-ef 10.0.13`, licence tool (see section 11). |
| `.env.example` | See section 10. |
| `compose.yaml` | Section 10. (`compose.prodlike.yaml` is deferred to Security/Release, ADR-0012.) |
| `azure-pipelines.yml` | Section 11. |
| `README.md`, `CLAUDE.md` | Section 12. |

### Directory.Packages.props versions

Versions from [stack-versions-2026-10-24.md](../stack-versions-2026-10-24.md) unless marked. Rows marked **verify** are not in that file; check NuGet before pinning.

| Package | Version | Used by |
|---|---|---|
| FastEndpoints | 8.3.0 (pinned to 8.3.x through S1-S4) | Api, BuildingBlocks |
| FastEndpoints.Messaging.Core | 8.3.0 (verified 2026-10-27) | Application |
| FastEndpoints.OpenApi | 8.3.0 | BuildingBlocks |
| Scalar.AspNetCore | 2.18.3 | BuildingBlocks |
| FluentValidation | 12.2.0 | Api (via FastEndpoints `Validator<T>`) |
| Microsoft.EntityFrameworkCore / .Relational / .Design | 10.0.13 | Application, Infrastructure |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | BuildingBlocks, Infrastructure |
| Npgsql / Npgsql.OpenTelemetry | 10.0.3 | BuildingBlocks |
| EFCore.NamingConventions (snake_case) | 10.0.x (**verify** on S1a day 1; fallback: a small snake_case model convention) | Infrastructure |
| Dapper | 2.1.89 | Application |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 | Infrastructure |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.13 | BuildingBlocks |
| Microsoft.IdentityModel.JsonWebTokens | 8.23.0 | Infrastructure |
| Microsoft.Extensions.Caching.Hybrid | 10.11.0 | BuildingBlocks |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.13 | BuildingBlocks |
| StackExchange.Redis | 3.4.20 | BuildingBlocks (health check, pinned) |
| Microsoft.Extensions.Resilience | 10.11.0 | BuildingBlocks |
| Microsoft.Extensions.Http.Resilience | 10.11.0 | BuildingBlocks (future outgoing HTTP) |
| Microsoft.Extensions.Configuration.KeyPerFile | 10.0.13 | Api (compose dev secrets) |
| OpenTelemetry.Extensions.Hosting, .Exporter.OpenTelemetryProtocol | 1.20.1 | BuildingBlocks |
| OpenTelemetry.Instrumentation.AspNetCore, .Http, .Runtime | 1.20.x (**verify**, instrumentation packages version separately) | BuildingBlocks |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | all `src/` |
| xunit.v3 | 4.0.2 | tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | Integration, Architecture |
| Microsoft.Extensions.TimeProvider.Testing | 10.x (**verify**) | Unit, Integration |
| Testcontainers.PostgreSql / Testcontainers.Redis | 4.16.0 | Integration |
| Respawn | 7.0.0 | Integration |
| NSubstitute | 6.2.0 | Unit |
| Shouldly | 4.3.0 | all tests |
| Bogus | 35.6.5 | Unit, Integration |
| TngTech.ArchUnitNET.xUnitV3 | 0.13.4 | Architecture |

Not referenced: Polly (comes transitively through Microsoft.Extensions.Resilience only), MediatR, FluentAssertions, FastEndpoints.Swagger, Xabaril health checks, Serilog (see ADR-0013).

## 4. Folder tree with one worked slice

The worked slice is **Products / CreateProduct** (EP-24, FR-040). File names only; pseudo-signatures in comments.

```
DemoApi.slnx
global.json  Directory.Build.props  Directory.Packages.props  nuget.config  BannedSymbols.txt
.env.example  compose.yaml  azure-pipelines.yml  README.md  CLAUDE.md
deploy/
  docker/Dockerfile                     # targets: api (chiseled, non-root), migrator (EF bundle)
  compose/init-dev-secrets.sh           # P4: generates missing dev-only secrets
  compose/postgres/01-roles.sh          # creates migrator and app roles from secret files
  compose/aspire-dashboard.env          # optional OTel dashboard settings
docs/product/...                        # planning docs, ADRs
api-tests/
  openapi/v1.json                       # committed; an integration test compares it with /openapi/v1.json
  http/                                 # .http files, hand-written, one folder per feature
    http-client.env.json                # local env, no secrets
    products/create-product.http
  postman/                              # collection generated from openapi/v1.json + environments (no secrets)
src/
  BuildingBlocks/
    DemoApi.SharedKernel/
      Domain/        Entity.cs  IAuditable.cs  IDeletable.cs  PersonalDataAttribute.cs  IDomainEvent.cs
      Results/       Result.cs  Error.cs  ErrorType.cs           # Validation, NotFound, Conflict, Forbidden, PreconditionFailed...
      Time/          IDateTimeProvider.cs                        # DateTimeOffset UtcNow { get; }
      Security/      ICurrentUser.cs                             # Guid? UserId; Guid? CustomerId; bool IsAdmin; string AuditName
      Caching/       IAppCache.cs                                # GetOrCreateAsync, RemoveAsync, BumpListGenerationAsync (ADR-0009)
      Data/          ISqlRunner.cs                               # Task<T> RunAsync<T>(Func<DbConnection,CancellationToken,Task<T>>, ct)
                     ITransactionRunner.cs                       # Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken,Task<Result<T>>>, ct)
      Queries/       IQueryHandler.cs                            # Task<TResult> HandleAsync(TQuery, ct)
      Paging/        PageRequest.cs  PagedResponse.cs  SortSpec.cs  SortParser.cs
    DemoApi.BuildingBlocks/
      Auditing/      AuditLog.cs  AuditLogConfiguration.cs  AuditingInterceptor.cs
      SoftDelete/    SoftDeleteInterceptor.cs  SoftDeleteModelBuilderExtensions.cs
      Persistence/   PostgresCircuitBreakerInterceptor.cs  SqlRunner.cs  TransactionRunner.cs  ModelBuilderExtensions.cs
      Time/          SystemDateTimeProvider.cs                    # wraps TimeProvider
      Security/      HttpCurrentUser.cs  JwtBearerSetup.cs  JwtOptions.cs  Roles.cs  Policies.cs
      Caching/       AppHybridCache.cs  ResilientDistributedCache.cs  CacheOptions.cs  CacheMetrics.cs
      Resilience/    ResiliencePipelineNames.cs  ResilienceSetup.cs  ResilienceOptions.cs
      Errors/        ResultHttpExtensions.cs  GlobalExceptionHandler.cs  ProblemDetailsSetup.cs  ProblemTypes.cs
      RateLimiting/  RateLimitPolicies.cs  RateLimitingSetup.cs  RateLimitingOptions.cs
      Http/          SecurityHeadersMiddleware.cs  ETagExtensions.cs  RequestLimitsSetup.cs  CorsSetup.cs
      Health/        PostgresHealthCheck.cs  ValkeyHealthCheck.cs  HealthEndpoints.cs  HealthResponseWriter.cs
      Observability/ OpenTelemetrySetup.cs  AppMeters.cs  LoggingSetup.cs
      OpenApi/       OpenApiSetup.cs  EndpointConventions.cs     # FastEndpoints.OpenApi + Scalar (Development only)
      ServiceDefaults.cs                                          # AddBuildingBlocks(config), UseBuildingBlocks()
  DemoApi.Domain/
    Products/   Product.cs  Sku.cs  ProductErrors.cs              # Product.Create(...) : Result<Product>
    Customers/  Customer.cs  CustomerErrors.cs
    Orders/     Order.cs  OrderItem.cs  OrderStatus.cs  OrderStatusHistory.cs  OrderErrors.cs
  DemoApi.Application/
    Common/     IAppDbContext.cs  IEmailSender.cs  DependencyInjection.cs   # AddApplication()
    Features/
      Products/
        Common/          ProductResponse.cs  ProductMappings.cs  ProductCacheKeys.cs
                         # ProductMappings: Expression<Func<Product,ProductResponse>> Projection; ProductResponse ToResponse(this Product)
        CreateProduct/   CreateProductCommand.cs                  # sealed record : ICommand<Result<ProductResponse>>
                         CreateProductHandler.cs                  # sealed : ICommandHandler<CreateProductCommand, Result<ProductResponse>>
        GetProductById/  GetProductByIdQuery.cs  GetProductByIdHandler.cs     # IQueryHandler, EF projection + IAppCache
        ListProducts/    ListProductsQuery.cs  ListProductsHandler.cs  ProductSortFields.cs
        UpdateProduct/ DeleteProduct/ RestoreProduct/ ...
      Auth/ AdminUsers/ Customers/ Orders/
      Reports/
        GetSalesReport/  GetSalesReportQuery.cs  GetSalesReportHandler.cs  SalesReportResponse.cs  GetSalesReport.sql
  DemoApi.Infrastructure/
    DependencyInjection.cs                                        # AddInfrastructure(config)
    Persistence/
      AppDbContext.cs                                             # IdentityDbContext<AppUser,AppRole,Guid>, IAppDbContext
      Configurations/Products/ProductConfiguration.cs  Customers/...  Orders/...
      Migrations/                                                 # incl. views, functions, 1 procedure, role grants
      Sql/ Views/ Functions/ Procedures/                          # .sql files embedded and applied by migrations
    Identity/   AppUser.cs  AppRole.cs  JwtTokenIssuer.cs  RefreshToken.cs  RefreshTokenStore.cs  SeedAdminHostedService.cs
    Email/      FakeEmailSender.cs
  DemoApi.Api/
    Program.cs  DependencyInjection.cs  appsettings.json  appsettings.Development.json
    Features/
      Products/
        ProductsGroup.cs                                          # FastEndpoints Group: route "products", tag, default auth
        CreateProduct/
          CreateProductEndpoint.cs        # sealed : Endpoint<CreateProductRequest, ProductResponse>
                                          # Configure(): Post("/"), Group<ProductsGroup>, Policies(AdminOnly), Summary<...>
                                          # HandleAsync: req.ToCommand().ExecuteAsync(ct) -> Send result via ResultHttpExtensions
          CreateProductRequest.cs         # sealed record: Name, Sku, Description?, Price, StockQuantity
          CreateProductValidator.cs       # sealed : Validator<CreateProductRequest>  (input shape, FR-040 limits)
          CreateProductSummary.cs         # sealed : Summary<CreateProductEndpoint>  (summary, description, 201/400/401/403/409/429)
          CreateProductMapping.cs         # static CreateProductCommand ToCommand(this CreateProductRequest)
        GetProductById/ ListProducts/ UpdateProduct/ DeleteProduct/ RestoreProduct/
      Auth/ AdminUsers/ Customers/ CustomersMe/ Orders/ CustomersMeOrders/ Reports/ AuditLogs/
  DemoApi.AppHost/ (optional)
tests/
  DemoApi.UnitTests/
    Domain/Products/ProductTests.cs
    Application/Products/CreateProductHandlerTests.cs
    Api/Products/CreateProductValidatorTests.cs
  DemoApi.IntegrationTests/
    Infrastructure/ ApiFactory.cs  PostgresValkeyFixture.cs  DatabaseReset.cs  TestUsers.cs  AuthzMatrix.cs
    Products/CreateProductTests.cs        # 201 + Location + ETag, 400, 409 duplicate SKU, audit row, cache invalidation
    Authorization/EndpointAuthzMatrixTests.cs   # table-driven: every endpoint x Anonymous/Customer/Admin
  DemoApi.ArchitectureTests/
    LayerTests.cs  SliceTests.cs  BuildingBlocksTests.cs  EndpointDoDTests.cs  DomainModelTests.cs  ContractTests.cs
```

### What happens in CreateProduct, end to end

1. FastEndpoints binds `CreateProductRequest` and runs `CreateProductValidator` (shape: lengths, SKU pattern, price range, 2 decimals). Failure: 400 ProblemDetails with `errors`.
2. Endpoint maps to `CreateProductCommand` and calls `ExecuteAsync(ct)` (command bus).
3. Handler: `Product.Create(...)` (domain rules) → check SKU unique among non-deleted → `db.Products.Add` → `SaveChangesAsync` (audit interceptor writes the `AuditLogs` row in the same transaction) → after commit `cache.BumpListGenerationAsync()` (ADR-0009) → `Result.Success(product.ToResponse())`. Unique-index race maps to `ProductErrors.DuplicateSku` (409).
4. Endpoint maps `Result` to 201 + `Location` + `ETag`, or to ProblemDetails.

## 5. Where cross-cutting pieces live

| Concern | Interface (SharedKernel) | Implementation / setup (BuildingBlocks unless noted) | Notes |
|---|---|---|---|
| Time | `IDateTimeProvider` | `SystemDateTimeProvider` (wraps `TimeProvider`) | Tests: `FakeTimeProvider`. `DateTime.Now/UtcNow` banned at build time. |
| Current user | `ICurrentUser` | `HttpCurrentUser` (claims `sub`, `role`, `customer_id`) | Outside HTTP (seed, migrations) returns `system`. |
| Auditing | `IAuditable`, `[PersonalData]` | `AuditingInterceptor` (`SaveChangesInterceptor`), `AuditLog` entity + config | Same transaction; PII fields written as `"changed"` (P1). |
| Soft delete | `IDeletable` | `SoftDeleteInterceptor` + named global query filter convention | `ExecuteDelete/Update` banned (D11). |
| Transactions | `ITransactionRunner` | `TransactionRunner` (`CreateExecutionStrategy`; each attempt: `ChangeTracker.Clear()`, `BeginTransaction`, work, commit on success `Result`) | ADR-0011. |
| Dapper reads | `ISqlRunner` | `SqlRunner` (EF connection, same execution strategy) | ADR-0011. |
| DB circuit breaker | — | `PostgresCircuitBreakerInterceptor` (`DbConnectionInterceptor`; counts only connection failures and timeouts) | ADR-0011. |
| Cache | `IAppCache` | `AppHybridCache` over HybridCache; L2 = `ResilientDistributedCache` over Valkey | Product reads only (FR-130). Failures = miss + metric. |
| Auth (validate) | — | `JwtBearerSetup`, `Policies`, `Roles` | Algorithm allow-list, skew 30 s (NFR-05). |
| Auth (issue) | — | Infrastructure/Identity: `JwtTokenIssuer`, `RefreshTokenStore` | Domain-agnostic; kept in the template. |
| Rate limiting | — | `RateLimitingSetup` (`auth-strict`, `refresh`, `general`), options-driven windows | 429 ProblemDetails + `Retry-After`. Tests use 1-2 s windows (P8). |
| ProblemDetails | `Result`, `Error` | `ResultHttpExtensions`, `GlobalExceptionHandler`, FastEndpoints error options | RFC 9457 with `traceId`; no exception text outside Development. |
| ETag / If-Match | — | `ETagExtensions` (from `xmin`) | 428 / 412. |
| Security headers, CORS, limits | — | `SecurityHeadersMiddleware`, `CorsSetup`, `RequestLimitsSetup` | HSTS + HTTPS redirect outside Development. 1 MB body, JSON only. |
| Health | — | `PostgresHealthCheck` (`SELECT 1`), `ValkeyHealthCheck` (`PING`), `HealthEndpoints` | `/health/live`, `/health/ready`; custom, not Xabaril (ADR-0013). |
| OpenTelemetry | — | `OpenTelemetrySetup`, `AppMeters` | ADR-0013. |
| OpenAPI | — | `OpenApiSetup` (FastEndpoints.OpenApi, Scalar in Development) | ADR-0002. |
| Resilience | — | `ResilienceSetup` (`valkey`, `postgres-connection` breaker, HTTP standard handler) | ADR-0011. No DB retry here: `EnableRetryOnFailure` is the only DB retry layer. |
| Email | `IEmailSender` (Application/Common) | Infrastructure/Email `FakeEmailSender` | Logs links, never passwords. |

`Program.cs` stays short: `AddBuildingBlocks(config)`, `AddApplication()`, `AddInfrastructure(config)`, `AddFastEndpoints()`; then `UseBuildingBlocks()`, `UseFastEndpoints(...)`, `MapHealthEndpoints()`.

## 6. How CQRS is applied

- **One database.** CQRS here means separate write and read paths in code, not separate stores.
- **Commands** (state changes): `sealed record XxxCommand : ICommand<Result<T>>` + `sealed class XxxHandler : ICommandHandler<XxxCommand, Result<T>>`. The endpoint calls `command.ExecuteAsync(ct)` (FastEndpoints command bus). Handlers load tracked entities through `IAppDbContext`, call domain methods, and `SaveChangesAsync`. Multi-step writes (register, order create, customer erase) use `ITransactionRunner`.
- **Queries** (reads): `sealed record XxxQuery` + `sealed class XxxHandler : IQueryHandler<XxxQuery, TResult>`, injected into the endpoint and called directly (no bus). Two kinds:
  - EF projection: `AsNoTracking()` + `Select(ProductMappings.Projection)` for simple reads, paging, sorting.
  - Dapper: SQL in the slice (`.sql` embedded resource or a const) run through `ISqlRunner`, for views, functions and the stored procedure (S4).
- **Cross-cutting on commands** uses FastEndpoints command middleware only for logging/tracing (`ICommandMiddleware<,>`). Validation runs at the endpoint. Domain rules run in the domain. No hidden transaction behaviour.
- **Expected errors** return `Result`/`Error`. Exceptions are for bugs and infrastructure failures only.
- **Stock locking** (S3): order handlers run inside `ITransactionRunner` and lock products with `FromSql("SELECT … FOR UPDATE")` in product ID order (P3). `Order` domain methods call `order.Touch(now)` on every item or status change, so the order row is updated and the `xmin` ETag changes (ADR-0007).

## 7. Keeping FastEndpoints thin (exit path)

FastEndpoints is used for: endpoint classes, binding, `Validator<T>`, `Summary<T>`, groups, and the command bus. Rules that keep the exit cheap (ADR-0002, ADR-0003):

1. Endpoints contain no business logic: bind → map → `ExecuteAsync` or query handler → map `Result` to HTTP. Target ≤ 30 lines per endpoint.
2. Handlers depend only on `FastEndpoints.Messaging.Core` interfaces (`ICommand<T>`, `ICommandHandler<,>`), not on the main `FastEndpoints` package or ASP.NET Core. Architecture test enforces it.
3. Query handlers use our own `IQueryHandler<,>`.
4. Validators inherit `Validator<T>`, which is a FluentValidation `AbstractValidator<T>`. Exit = change the base class.
5. Response DTOs are plain records in Application. Request DTOs are plain records in Api; FastEndpoints binding attributes (`[RouteParam]`, `[FromHeader]`) are allowed only on Api request DTOs.
6. `Result` → HTTP mapping is one extension class in BuildingBlocks.

Exit to Minimal APIs: endpoints become `MapPost` lambdas or static handlers; validators get an endpoint filter; `Summary<T>` becomes `.WithSummary/.Produces`; commands are dispatched through a 30-line hand-written dispatcher or the `Mediator` package. Domain, Application handlers, Infrastructure and tests (except endpoint DoD test) do not change. Assumption: ~1-2 days per 50 endpoints, mostly mechanical.

## 8. Architecture tests

Tool: TngTech.ArchUnitNET.xUnitV3 (Apache-2.0). Build-time bans: BannedApiAnalyzers. Each rule must be seen failing once on a deliberate violation.

**S1 has only the core layer rules plus the banned APIs** (A1-A5, A10, A11). Each other rule is added when the code it guards first appears (column "Added"). The list is the target, not an S1 deliverable.

| # | Rule | How | Added |
|---|---|---|---|
| A1 | SharedKernel references nothing but the BCL | Referenced assemblies check | S1a |
| A2 | BuildingBlocks does not depend on `DemoApi.Domain/Application/Infrastructure/Api` (P7) | ArchUnit | S1a |
| A3 | Domain does not depend on Application, Infrastructure, Api, EF Core, ASP.NET Core, FastEndpoints, Dapper, Npgsql | ArchUnit, `Types(true)` for framework namespaces | S1a |
| A4 | Application does not depend on Infrastructure, Api, BuildingBlocks, ASP.NET Core, `FastEndpoints` main assembly, Npgsql | ArchUnit | S1a |
| A5 | Api `Features` do not depend on Infrastructure or `DbContext` | ArchUnit | S1a |
| A6 | Feature folders do not depend on each other (Application and Api), except `Common` | ArchUnit slice rule | When the second feature folder appears (S1b) |
| A7 | Handlers are `sealed`, end with `Handler`, live in `Application.Features` | ArchUnit | S1b (first handlers beyond Products) |
| A8 | Each `ICommand<T>` has exactly one handler | Reflection | S1b |
| A9 | Every endpoint has an explicit auth rule (`Roles`/`Policies`/`AllowAnonymous`), a validator (when the request has properties), an OpenAPI summary and description (P5, NFR-17) | `EndpointDoDTests`: build the app with `WebApplicationFactory` (no DB, hosted services off), read FastEndpoints endpoint definitions from `EndpointDataSource` | S1b (before auth endpoints multiply) |
| A10 | No `ExecuteDelete`/`ExecuteUpdate` (D11, NFR-08) | BannedApiAnalyzers (build error) + ArchUnit method-call rule as backup | S1a (build-time ban) |
| A11 | No `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow` (FR-135) | BannedApiAnalyzers + ArchUnit backup | S1a (build-time ban) |
| A12 | No entity returned from endpoints: endpoint response types and `*Response` records contain no Domain entity types (enums allowed) | Reflection over `Endpoint<TReq,TRes>` generic args, recursive property walk | S2 (first `/me` responses) |
| A13 | Domain entities: no public setters; every business entity implements `IAuditable` and `IDeletable` | ArchUnit + reflection | S2 (second aggregate) |
| A14 | Every `DateTime` property in contracts is `DateTimeOffset` | Reflection (cheap guard for FR-125) | When first needed |

## 9. Test projects

| Project | Packages | Scope | Needs Docker |
|---|---|---|---|
| `tests/DemoApi.UnitTests` | xunit.v3, NSubstitute, Shouldly, Bogus, TimeProvider.Testing | Domain methods, handlers with substitutes, validators, sort/paging parsers | No |
| `tests/DemoApi.IntegrationTests` | xunit.v3, Mvc.Testing, Testcontainers.PostgreSql, Testcontainers.Redis (Valkey image `valkey/valkey:9.1.2-alpine`), Respawn, Shouldly, Bogus | HTTP through `WebApplicationFactory`; authz matrix (all endpoints × 3 roles); BOLA; audit rows; soft delete; cache fallback (Valkey paused); stock concurrency (20 parallel orders); Dapper views with deleted data; erasure search; rate limits with 1-2 s windows; production headers with environment `Production`; OpenAPI drift (`/openapi/v1.json` vs committed file); DB breaker (10 duplicate SKUs leave it closed; DB down → 503 within 5 s); forced transient failure (no duplicate rows after retry); refresh reuse revocation persisted after the 401; C10 Admin get of a deleted customer incl. email | Yes |
| `tests/DemoApi.ArchitectureTests` | xunit.v3, ArchUnitNET, Mvc.Testing, Shouldly | Section 8 | No |
| `api-tests/` (folder, not a project) | httpyac CLI, Postman CLI/Newman | Hand-written `.http` per endpoint; Postman collection generated from the committed OpenAPI file (with environments, no secrets); committed `openapi/v1.json` | Runs against a running API |

Integration fixture: one PostgreSQL 18.6 and one Valkey container per test run (assembly fixture), the same role script as compose, migrations applied once by the migrator role, tests connect as the **app role** (so a hidden DELETE fails as in production), Respawn resets data between test classes (excluding `__EFMigrationsHistory` and seeded roles). Target: full suite ≤ 5 min (NFR-16).

## 10. Configuration, secrets and local run

### Configuration sources (later wins)

1. `appsettings.json`: non-secret defaults (JWT issuer/audience/lifetimes, rate-limit windows, cache TTLs, resilience numbers, currency `USD`, `Cors:AllowedOrigins: []`, `Email:LinkBaseUrl`, `Reports:LowStockThreshold: 10`).
2. `appsettings.{Environment}.json`: Development turns on Scalar and detailed health output.
3. User secrets (Development, `dotnet run` outside Docker).
4. Key-per-file `/run/secrets/dev` (optional; compose only, ADR-0012).
5. Environment variables (`ConnectionStrings__Postgres`, `Jwt__SigningKey`, ...).

Secrets: `ConnectionStrings:Postgres`, `ConnectionStrings:PostgresMigrator`, `ConnectionStrings:Valkey`, `Jwt:SigningKey`, `SeedAdmin:Email`, `SeedAdmin:Password`. All options use `ValidateOnStart` (JWT key ≥ 256 bits, connection strings present). Missing seed config with no Admin = startup fails (FR-021).

### `.env.example` (committed; `.env` is git-ignored)

`COMPOSE_PROJECT_NAME`, `API_HTTP_PORT=8080`, `SEED_ADMIN_EMAIL=admin@demo.local`, and empty optional overrides `JWT_SIGNING_KEY=`, `SEED_ADMIN_PASSWORD=`, `POSTGRES_PASSWORD=`, `POSTGRES_APP_PASSWORD=`, `VALKEY_PASSWORD=` with a comment: "leave empty; generated on first run into the `dev-secrets` volume". No PostgreSQL or Valkey port settings: those ports are not published (see `compose.override.yaml.example`).

### compose.yaml services

All services use the `json-file` log driver with rotation (`max-size: 10m`, `max-file: 3`). Secret files are mounted per service, mode `0440` (ADR-0012).

| Service | Image | Ports | Notes |
|---|---|---|---|
| `secrets-init` | `alpine` (pin tag) | none | One-shot. Runs `init-dev-secrets.sh`: writes missing values to volume `dev-secrets` (random 64-byte JWT key, seed Admin password, `POSTGRES_PASSWORD`, migrator and app role passwords, Valkey ACL password). Never overwrites. Sets owner and mode `0440` per file. |
| `postgres` | `postgres:18.6-trixie` | **not published** | `POSTGRES_PASSWORD_FILE` from the generated file. Init script creates `demoapi_migrator` (owner, not superuser) and `demoapi_app` roles (SCRAM-SHA-256). Healthcheck `pg_isready`. Volume `pgdata`. |
| `valkey` | `valkey/valkey:9.1.2-alpine` | **not published** | ACL file: `default` user off; user `demoapi` with the generated password, keys `products:*`, no `@dangerous`. `maxmemory` + `allkeys-lru`. Healthcheck `valkey-cli ping` with auth. No persistence. |
| `migrator` | build target `migrator` (EF migration bundle) | none | One-shot, runs as migrator role, applies migrations + grants. Mounts only the migrator password. |
| `api` | build target `api` | `127.0.0.1:8080` | `ASPNETCORE_ENVIRONMENT=Development`, HTTP only. Mounts only its own secrets read-only. Volume `dp-keys` for the Data Protection key ring. No sensitive-data logging. `depends_on`: postgres/valkey healthy, migrator completed successfully. |
| `aspire-dashboard` (profile `observability`) | `mcr.microsoft.com/dotnet/aspire-dashboard` (pin tag, **verify**) | UI `127.0.0.1:18888`; OTLP 18889 **not published** | Browser-token login kept on. API exports with `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889` inside the compose network, only when the profile is on. |

`compose.override.yaml.example` (committed, all lines commented): publishes PostgreSQL on `127.0.0.1:5432` and Valkey on `127.0.0.1:6379` for local DB tools. Copy to `compose.override.yaml` (git-ignored) to use it.

`docker compose up` = secrets → database → migrations → API, no manual steps (G2, NFR-15). The dev admin password is read with `docker compose exec api cat /run/secrets/dev/SeedAdmin__Password` (README). Anyone with Docker access on the host can read the dev secrets; the host is the trust root (accepted, threat model section 3 item 2).

HTTPS redirect, HSTS and security headers are proven by integration tests with environment `Production`. A `compose.prodlike.yaml` with a dev certificate is deferred to the Security/Release phase (ADR-0012).

## 11. Azure Pipelines outline

`azure-pipelines.yml`, Microsoft-hosted `ubuntu-latest` (Docker available). Triggers: PRs to `main`, pushes to `main`.

| Stage | Steps | Fails when |
|---|---|---|
| Build | Use .NET SDK from `global.json`; `dotnet restore --locked-mode`; `dotnet build -c Release` (warnings as errors); `dotnet format --verify-no-changes`; `dotnet list package --vulnerable --include-transitive` | Build warning, format drift, lock file drift, vulnerable package |
| Test | Unit + Architecture tests (no Docker); Integration tests (Testcontainers); publish TRX + coverage | Any failing test; suite time over budget reported as a warning |
| SecretScan | `gitleaks detect` over the full history (pinned version, `--redact`), `.gitleaks.toml` allow-list for test fixtures | Any finding (NFR-07) |
| Licence | Licence tool over the restored graph against an allow-list (MIT, Apache-2.0, BSD-2/3, PostgreSQL, MS-.NET-Library for runtime packages) | Any package outside the list (NFR-18) |
| TemplateStrip (from end of S2) | Copy the repo, delete the sample-domain folders and Customer leaks (section 13), build and test the copy | The stripped copy does not build or a test fails |
| ApiFiles (after S4) | Start API + containers, run `.http` files with httpyac and the generated Postman collection | Any failing request |

No separate OpenAPI stage and no build-time export: OpenAPI drift is an integration test in the Test stage.

Assumption: licence tool = `nuget-license` (MIT) as a local dotnet tool; **verify** current version before pinning.

## 12. README outline

1. What this is (reference API and future `dotnet new` template), stack and versions link.
2. Prerequisites: Docker Desktop, .NET SDK 10.0.402 (only for running tests and `dotnet run`).
3. Quick start: `cp .env.example .env` (optional) → `docker compose up -d` → open `http://localhost:8080/scalar` → get the seed Admin password.
4. Running outside Docker: user-secrets keys list, `dotnet run --project src/DemoApi.Api`.
5. Configuration reference: every key, default, secret or not.
6. Tests: unit, architecture, integration (Docker), `.http` files; refreshing `openapi/v1.json` and regenerating the Postman collection.
7. Observability: `docker compose --profile observability up`, dashboard URL.
8. Regenerating dev secrets (`docker compose down -v`).
9. Project structure and dependency rules (link to this document and ADRs).
10. Adding a feature (checklist from the DoD).
11. Stripping the sample domain (link to the template guide, P7).
12. Troubleshooting (ports in use, reset volumes, regenerate dev secrets).

## 13. Strip path for the template (P7)

Kept in the template: SharedKernel, BuildingBlocks, Auth, AdminUsers, Products (as the sample slice), health, all test infrastructure, compose, pipeline.
Removed: `Customers`, `CustomersMe`, `Orders`, `CustomersMeOrders`, `Reports`, `AuditLogs` folders in each project and in `tests/` and `api-tests/`; their entity configurations; migrations regenerated as one `Initial` migration.
Customer leaks into kept code (architecture review C5). Each is a listed edit, not a folder delete:

| Leak | Where | Template edit |
|---|---|---|
| `ICurrentUser.CustomerId` | SharedKernel `ICurrentUser`, BuildingBlocks `HttpCurrentUser` | Remove the property and its claim read |
| `customer_id` claim | Infrastructure `JwtTokenIssuer` (ADR-0008) | Stop adding the claim |
| `CustomerOnly` policy | BuildingBlocks `Policies` | Remove the policy |
| `Customer` role | BuildingBlocks `Roles`, role seed in migrations | Keep `Admin`; keep or rename a generic `User` role (decide at extraction) |
| Register creates a Customer record (FR-001) | Application `Auth/Register` | Create only the user and role |
| `GET me` returns `customerId` (FR-016) | Application `Auth/GetMe` response | Remove the field |

Proof: the `TemplateStrip` CI job (from the end of S2) deletes the folders and applies these edits on a copy, then builds and tests it. Any new Customer reference in kept code fails that job.

## 14. Slice S1 split (architecture review R-2)

S1 is split in two. Time-boxes are set in the Delivery phase. Kill rule: more than 2 days over a time-box → the pre-mortem cut rule applies (stretch first).

| Part | Content |
|---|---|
| **S1a walking skeleton** | Solution and root files; compose chain (secrets-init → postgres → migrator → api); Azure Pipelines (Build, Test, SecretScan, Licence); product create and get end to end with audit row, cache and an authz test; core architecture rules + banned APIs; OpenAPI drift test. Spikes: (1) compose chain on a fresh Windows clone (ADR-0012 checks), (2) Identity + audit interceptor incl. forced rollback and `ChangeTracker.Clear()` retry, (3) HybridCache with L2 down, (4) `EFCore.NamingConventions` for EF Core 10. |
| **S1b auth** | Register, confirm and resend, login, refresh with rotation and reuse detection, logout, logout-all, me, change password, seed Admin; rest of product CRUD, soft delete and restore; rate limiting. Token tests first (ADR-0008). |

## 15. Advice (fixed stack concerns)

1. **Project count.** Six source projects is more than the right-sizing ladder suggests for one developer. It is justified only by the template goal (P7). For a small client app built from the template, merging Domain + Application is a valid option; the strip guide should say so.
2. **FastEndpoints funding (R6, F7).** Stay on 8.3.x; do not adopt 9.0 beta. Watch for releases; if none for 3 months, write the exit ADR (pre-mortem kill criterion). The rules in section 7 keep the exit mechanical.
3. **`FastEndpoints.Messaging.Core`** 8.3.0 is verified to exist (2026-10-27). Application references only it. All FastEndpoints packages stay on 8.3.x through S1-S4.
4. **Database circuit breaker (P10).** Kept because the brief requires it, but on a single local instance it adds little. It is a small `DbConnectionInterceptor` that counts only connection failures and timeouts, never business errors, and it never retries (ADR-0011).
5. **HybridCache failure behaviour** with an unavailable L2 is not verified. Spike in S1a (F10). The `ResilientDistributedCache` decorator makes the behaviour ours regardless.
6. **OpenTelemetry instrumentation packages** for StackExchange.Redis and EF Core have been prerelease for a long time. Do not reference prerelease packages; use Npgsql.OpenTelemetry for DB spans and our own `Activity` in the cache wrapper.
7. **Aspire** adds a second way to run locally. Keep compose as the only supported path until S4; add the AppHost only if it saves time.
