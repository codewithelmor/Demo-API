# Stack versions and licenses (checked 2026-10-24)

Source: live NuGet API, GitHub releases, official docs. Re-check before pinning versions in code.

| Component | Version | Released | License | Notes |
|---|---|---|---|---|
| .NET 10 runtime / ASP.NET Core 10 | 10.0.13 | 2026-10-13 | MIT | LTS, support ends 2028-11-14 |
| .NET 10 SDK | 10.0.402 | 2026-10-13 | MIT | |
| FastEndpoints (+ command bus) | 8.3.0 | 2026-08-20 | MIT | 9.0 is beta only. Maintainer has warned it may go "bugfix only" if sponsorship stays low |
| FastEndpoints.OpenApi | 8.3.0 | 2026-08-20 | MIT | Built on Microsoft.AspNetCore.OpenApi; use this |
| FastEndpoints.Swagger | 8.3.0 | 2026-08-20 | MIT | NSwag-based, feature-frozen, deprecated after 2026-11-10; do not use |
| FluentValidation | 12.2.0 | 2026-10-10 | Apache-2.0 | |
| EF Core | 10.0.13 | 2026-10-13 | MIT | |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | 2026-07-10 | PostgreSQL | |
| Npgsql (+ OpenTelemetry) | 10.0.3 | 2026-05-27 | PostgreSQL | |
| Dapper | 2.1.89 | 2026-09-23 | Apache-2.0 | "Dapper Plus" is a different, commercial product |
| PostgreSQL | 18.6 | 2026-08 | PostgreSQL | Image `postgres:18.6-trixie` |
| Redis Open Source | 8.10.2 | 2026-09-17 | RSALv2 / SSPLv1 / AGPLv3 | Not permissive since 8.0 |
| Valkey | 9.1.2 | 2026-09-01 | BSD-3-Clause | Drop-in Redis replacement; image `valkey/valkey:9.1.2-alpine` |
| StackExchange.Redis | 3.4.20 | 2026-10-23 | MIT | Works with Redis and Valkey |
| Microsoft.Extensions.Caching.Hybrid | 10.11.0 | 2026-10-14 | MIT | |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.13 | 2026-10-13 | MIT | |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.13 | 2026-10-13 | MIT | |
| Microsoft.IdentityModel.JsonWebTokens | 8.23.0 | 2026-09-18 | MIT | |
| ASP.NET Core Identity | 10.0.13 | 2026-10-13 | MIT | `MapIdentityApi` tokens are NOT JWTs |
| Polly.Core | 8.8.0 | 2026-09-14 | BSD-3-Clause | Maintenance fee on next major after 2026-11-16 for direct references |
| Microsoft.Extensions.Resilience / Http.Resilience | 10.11.0 | 2026-10-14 | MIT | Use these; Polly comes in transitively |
| MediatR | 14.2.0 | 2026-07-02 | RPL-1.5 or commercial | Do not use |
| Mediator (martinothamar) | 3.0.2 | 2026-03-22 | MIT | Alternative |
| WolverineFx | 6.52.0 | 2026-10-23 | MIT | Only if messaging/outbox needed |
| xunit.v3 | 4.0.2 | 2026-10-09 | Apache-2.0 | |
| Testcontainers (.PostgreSql, .Redis) | 4.16.0 | 2026-10-08 | MIT | Use Redis module with Valkey image |
| Respawn | 7.0.0 | 2025-11-30 | Apache-2.0 | |
| NSubstitute | 6.2.0 | 2026-08-11 | BSD-3-Clause | |
| Shouldly | 4.3.0 | 2025-01-23 | BSD-3-Clause | |
| FluentAssertions | 8.11.0 | 2026-09-14 | Xceed Community (non-commercial) | Do not use; AwesomeAssertions 9.6.0 (Apache-2.0) or Shouldly |
| Bogus | 35.6.5 | 2025-10-26 | MIT | |
| OpenTelemetry .NET | 1.20.1 | 2026-10-20 | Apache-2.0 | |
| Serilog.AspNetCore (optional) | 10.0.0 | 2025-11-28 | Apache-2.0 | |
| AspNetCore.HealthChecks.NpgSql / .Redis | 9.0.0 | 2024-12-19 | Apache-2.0 | Stale; consider small custom health checks |
| Aspire | 13.6.3 | 2026-10-21 | MIT | Optional local orchestration |
| Scalar.AspNetCore | 2.18.3 | 2026-10-23 | MIT | OpenAPI UI |

## License risks and recommendations
1. MediatR: avoid (reciprocal or paid). Use FastEndpoints command bus or Mediator (MIT).
2. FluentAssertions v8+: avoid. Use Shouldly or AwesomeAssertions.
3. Polly: do not reference directly; use Microsoft.Extensions.Resilience.
4. Redis 8: tri-license. Valkey 9.1 (BSD-3) is a drop-in alternative.
5. Dapper Plus is commercial; plain Dapper is fine.
6. FastEndpoints: use FastEndpoints.OpenApi + Scalar, stay on 8.3.0 stable; note funding risk.
7. Identity: `MapIdentityApi` bearer tokens are not JWTs; issue our own JWTs with JwtBearer.
8. Xabaril health checks are stale (maintenance risk, not license risk).

Unverified: exact PostgreSQL 18.6 release day; EnableRetryOnFailure details not re-read.
