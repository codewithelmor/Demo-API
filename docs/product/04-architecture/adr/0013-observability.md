# ADR-0013: Use built-in logging with OpenTelemetry, custom health checks, and the Aspire dashboard locally

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Architect |
| Consulted | PRD NFR-10, NFR-12, NFR-13, NFR-14, FR-136, D10; threat model T-26, T-27, T-28 |

Revised 2026-10-27 (security S-D5, S-D7): security events with fixed event IDs (incl. audit-log queries); `Microsoft.AspNetCore.Hosting.Diagnostics` at Warning; no sensitive-data logging in appsettings or compose; dashboard bound to localhost, OTLP not published.

## Context
NFR-12 needs structured logs with trace ID and user ID (ID only), OpenTelemetry traces for HTTP, DB and cache, and metrics: request rate, duration and errors; cache hit ratio; rate-limit rejections; login failures; refresh-token reuse events. NFR-13: every error response carries a `traceId` that matches the logs. NFR-10: no passwords or tokens in logs; emails masked. FR-136/D10: `live` and `ready` health checks; Valkey down = Degraded (200), PostgreSQL down = 503; detail only in Development. Xabaril health packages are stale (9.0.0, 2024). Local only; no hosted monitoring.

## Decision drivers
- Fewest packages that meet NFR-12 (template simplicity, NFR-18).
- One trace ID in logs, traces and ProblemDetails.
- A local place to see traces and metrics with no cost.

## Options considered

### Logging
- **Option L1: built-in `ILogger` + JSON console + OpenTelemetry logs exporter.** Good: no extra package; trace and span IDs added automatically; scopes for user ID. Bad: fewer sinks and enrichers than Serilog.
- **Option L2: Serilog.AspNetCore 10.0.0.** Good: rich sinks, enrichers, request logging. Bad: second logging pipeline next to OTel; more config; more to strip in the template.

### Health checks
- **Option H1: own `PostgresHealthCheck` (`SELECT 1` through `ISqlRunner`, timeout 1 s) and `ValkeyHealthCheck` (`PING`, timeout 250 ms).** Good: ~30 lines each; no stale packages. Bad: our code.
- **Option H2: AspNetCore.HealthChecks.NpgSql / .Redis 9.0.0.** Good: ready-made. Bad: stale since 2024; extra dependencies.

### Local sink
- **Option S1: standalone Aspire dashboard container (compose profile `observability`, UI 18888, OTLP 18889).** Good: free, one container, logs + traces + metrics. Bad: in-memory only.
- **Option S2: Jaeger + Prometheus + Grafana.** Good: persistent, full featured. Bad: three containers and config to maintain.

## Decision
We will use L1, H1 and S1.
- **Logs:** built-in logging, JSON console formatter, OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Middleware adds a scope with `UserId` (ID only). Emails are masked through one helper; the fake email sender in Development is the only exception (NFR-10). Request/response bodies are never logged.
- **Traces:** OpenTelemetry 1.20.1 with ASP.NET Core and HttpClient instrumentation, `Npgsql.OpenTelemetry` for DB spans (EF and Dapper both use Npgsql), and an `ActivitySource("DemoApi.Cache")` in our cache wrapper (no prerelease Redis instrumentation). ProblemDetails `traceId` = `Activity.Current.TraceId`.
- **Metrics:** built-in ASP.NET Core meters (`http.server.request.duration` gives rate, duration and errors by status; `aspnetcore.rate_limiting.*` gives rejections by policy), runtime metrics, plus our `Meter("DemoApi")`:

| Metric | Type | Tags | Source |
|---|---|---|---|
| `demoapi.cache.requests` | counter | `result` = hit/miss/error, `cache` = product/product-list | Cache wrapper; hit ratio = hit / (hit + miss) |
| `demoapi.cache.circuit_open` | counter | `dependency` | Resilience `OnOpened` |
| `demoapi.auth.login_failures` | counter | `reason` = invalid/locked/unconfirmed | Login handler |
| `demoapi.auth.lockouts` | counter | `type` = failed-logins/admin | Login, lock handlers |
| `demoapi.auth.refresh_token_reuse` | counter | none | Refresh handler (family revoked) |
| `demoapi.ratelimit.rejections` | counter | `policy` | `OnRejected` (backup if built-in tags differ; **verify** in S1) |
| `demoapi.db.circuit_open` | counter | none | Resilience `OnOpened` |

- **Security events (S-D5, S-D7, threat model T-28).** One logger category `DemoApi.Security`, source-generated `[LoggerMessage]` methods with **fixed event IDs**, so alerts and queries do not depend on message text. Fields are IDs only (user ID, target user ID, customer ID, policy, reason code, trace ID); never emails, names, passwords or tokens.

| Event ID | Name | Level | When |
|---|---|---|---|
| 9001 | `LoginSucceeded` | Information | Login issues tokens |
| 9002 | `LoginFailed` | Warning | Any failed login (`reason`: invalid, locked, unconfirmed, deleted) |
| 9003 | `AccountLockedOut` | Warning | Automatic lockout after 5 failures |
| 9004 | `RefreshTokenReuse` | Warning | Reuse detected, family revoked |
| 9005 | `StampCheckRejected` | Information | Access token rejected by the security-stamp check (ADR-0008) |
| 9006 | `PasswordChanged` / 9007 `PasswordReset` | Information | EP-09 / EP-11 succeeded |
| 9008 | `SessionsRevoked` | Information | Logout-all or Admin revoke-sessions |
| 9010 | `AdminRoleAssigned` / 9011 `AdminRoleRemoved` | Warning | EP-16 / EP-17 |
| 9012 | `AdminUserCreated` | Warning | EP-19 |
| 9013 | `UserLockedByAdmin` / 9014 `UserUnlockedByAdmin` | Information | EP-14 / EP-15 |
| 9020 | `CustomerErased` | Information | EP-33 / EP-37 |
| 9030 | `AuditLogQueried` | Information | Every EP-55 call (filters as codes and IDs only) |
| 9040 | `RateLimitRejected` | Information | 429 (policy, partition type; masked IP) |

  The list is the contract; new events take the next free ID. Test ST-19 checks the events for role assign, Admin create, audit-log query, refresh reuse and lockout.
- **Keep PII out of framework logs (S-D7, T-26).** `Microsoft.AspNetCore.Hosting.Diagnostics` is set to `Warning`, because its "Request starting" lines include query strings such as `?search=smith`. ProblemDetails `instance` has no query string. OTel URL query redaction is on (Assumption: default; verify in S1a). EF `EnableSensitiveDataLogging` and Npgsql `Include Error Detail` are **never** set in appsettings or compose; only the test host may set them. HTTP logging middleware is off. Test ST-14 seeds PII and token values and scans captured logs.
- **Health:** `/health/live` (no checks, process only) and `/health/ready` (Postgres: Unhealthy → 503; Valkey: Degraded → 200). Own small JSON body (`status`, and per-check detail only in Development), no rate limit, ≤ 1 s (NFR-14).
- **Local view:** `docker compose --profile observability up` starts `aspire-dashboard`; the API exports OTLP to `http://aspire-dashboard:18889` inside the compose network (port not published). The UI is bound to `127.0.0.1:18888` with browser-token login (ADR-0012). Without the profile, the API only writes console logs (Docker `json-file` rotation).

## Consequences
- Good: few packages; one trace ID everywhere; metrics list covers NFR-12; template is easy to read.
- Bad / trade-offs: no persistent local history; fewer log sinks than Serilog (a client can add Serilog later without code changes in handlers).
- Follow-up actions: integration test that a 4xx/5xx `traceId` appears in a captured log line; test that no log line contains a password, JWT, refresh token, unmasked email, phone or search term (NFR-10, ST-14); security-event tests (ST-19); confirm built-in rate-limiting metric tags and drop the custom counter if they are sufficient.

## Revisit when
- The API is deployed to a hosted environment (choose an OTLP backend such as Azure Monitor).
- A client standard requires Serilog.

## Links
- [solution-structure.md](../solution-structure.md) sections 5 and 10
- ADR-0011, ADR-0012
- PRD NFR-10, NFR-12, NFR-13, FR-136, D10
