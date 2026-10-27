# ADR-0012: Run locally with Docker Compose in Development, with generated dev-only secrets

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Product owner, architect |
| Consulted | PRD G2, NFR-06, NFR-07, NFR-08, NFR-15, FR-021, P4; pre-mortem F8 |

Revised 2026-10-27 (architecture review R-2, R-4): `compose.prodlike.yaml` deferred to Security/Release; compose-chain spike checks added for S1a.

Revised 2026-10-27 (security S-D3, S-D7): API and dashboard bound to `127.0.0.1`; PostgreSQL, Valkey and OTLP not published (override example for DB tools); Valkey ACL user with a generated password; per-service secret mounts, files `0440`; generated `POSTGRES_PASSWORD`; `json-file` log rotation; Data Protection key volume.

## Context
G2/NFR-15: fresh clone to running API with the seed Admin in ≤ 15 min, one compose command plus migrations, zero manual steps. NFR-07: zero secrets in the repo; config from user-secrets or environment variables. FR-021: the seed Admin comes from configuration; startup fails if missing and no Admin exists. NFR-06 needs HTTPS and HSTS outside Development. NFR-08: the app role has no DDL and no DELETE; migrations run as a separate role. The pre-mortem (F8) found these goals clash: secrets only from user-secrets, HTTPS-only outside Development, and "no manual steps". P4 settled it: compose runs as Development and generates missing dev-only secrets on first run.

## Decision drivers
- Zero manual steps on a fresh clone (G2).
- Zero committed secrets (NFR-07).
- Least-privilege DB roles locally, same as later environments (NFR-08).
- Production behaviour (HTTPS, HSTS, headers) still tested (P4).

## Options considered

### Option 1: Docker Compose, Development, `secrets-init` + `migrator` one-shot services
- Good: one command; secrets generated per machine and never committed; migrations run as the owner role before the API.
- Bad: two small helper services and a shell script.

### Option 2: Committed dev-only secrets in `appsettings.Development.json`
- Good: simplest.
- Bad: secret scan noise; bad template habit; breaks NFR-07. Rejected.

### Option 3: Aspire AppHost as the main local path
- Good: one F5 start, dashboard, parameter secrets.
- Bad: needs the .NET SDK and Aspire workload/tooling; second orchestration model; not "one compose command".
- Cost / effort: medium; kept optional.

### Option 4: API applies migrations at startup
- Good: no migrator service.
- Bad: the app role would need DDL rights (breaks NFR-08); races with several instances.

## Decision
We will use Option 1.
- `docker compose up` starts: `secrets-init` (one-shot; writes missing values to volume `dev-secrets`: 64-byte random JWT key, seed Admin password, `POSTGRES_PASSWORD` for the `postgres` superuser, DB passwords for `demoapi_migrator` and `demoapi_app` (≥ 32 random characters, SCRAM-SHA-256), and the Valkey ACL password; never overwrites) → `postgres:18.6-trixie` (`POSTGRES_PASSWORD_FILE` from the generated file; init script creates both roles; migrator is not a superuser) and `valkey/valkey:9.1.2-alpine` (healthchecks) → `migrator` (one-shot EF Core migrations bundle, runs as the owner role, applies schema, views, functions, the procedure and grants) → `api` (`ASPNETCORE_ENVIRONMENT=Development`, HTTP on 8080, `depends_on: migrator: condition: service_completed_successfully`).
- The API reads the secret files with key-per-file configuration (`/run/secrets/dev`, read-only). Environment variables from `.env` override them. `.env.example` is committed and documents every key; `.env` is git-ignored.

**Compose hardening (security S-D7, threat model section 3 items 2-3, T-23, T-26, T-27, T-29)**
- **Ports.** The API is published only on `127.0.0.1:8080`, the Aspire dashboard UI only on `127.0.0.1:18888`. PostgreSQL (5432), Valkey (6379) and OTLP (18889) are **not published**; services reach them on the compose network. Reason: Docker can bypass the host firewall for published ports, and the Development API shows Scalar and detailed errors.
- **DB tools.** A commented `compose.override.yaml.example` shows how to publish PostgreSQL on `127.0.0.1:5432` (and Valkey on `127.0.0.1:6379`) for a local DB tool. Copying it to `compose.override.yaml` (git-ignored) is a deliberate choice.
- **Valkey ACL.** Valkey starts with an ACL file: the `default` user is off; the app user `demoapi` has the generated password, access to keys `products:*` and HybridCache's `__MSFT_HCT__*` tag-invalidation keys only (every L2 read checks them, so denying them breaks the cache with `NOPERM`), and no `@dangerous` commands. Valkey `maxmemory` + `allkeys-lru` (ADR-0009). The connection string carries the user and password from the secret file.
- **Per-service secret mounts.** Each service mounts only the files it needs (API: JWT key, seed Admin password, app DB password, Valkey password; migrator: migrator password; postgres: superuser and role passwords). Files are mode `0440`, owned by the UID/group of the reading service. Accepted risk: anyone with Docker access on the host can read them (`docker compose exec`); the host is the trust root (README note).
- **Logs.** Every service uses the `json-file` log driver with rotation (`max-size: 10m`, `max-file: 3`), so local logs, which may contain masked emails, age out.
- **Data Protection keys.** A named volume `dp-keys` holds the ASP.NET Core Data Protection key ring (`PersistKeysToFileSystem`), so confirm and reset links survive an API restart (S-D3, ADR-0008).
- **No sensitive logging.** Compose never sets EF `EnableSensitiveDataLogging` or Npgsql `Include Error Detail`; only tests may (ADR-0013).
- Seed Admin: `SeedAdmin:Email` from `.env` (default `admin@demo.local`), password from the generated file. A hosted service creates the pre-confirmed Admin only if no Admin exists. The password is never logged (NFR-10); README shows `docker compose exec api cat /run/secrets/dev/SeedAdmin__Password`.
- Outside Docker: `dotnet run` uses user-secrets (README lists the keys).
- Production-like checks: integration tests with environment `Production` assert HTTPS redirect, HSTS and security headers (the automated gate). A `compose.prodlike.yaml` with a dev certificate is **deferred** to the Security/Release phase.
- Day-1 spike in S1a (compose chain on a fresh Windows clone), each a pass/fail check:
  - `.gitattributes` forces LF line endings for `*.sh` (`*.sh text eol=lf`), so the scripts run inside Linux containers after a Windows checkout.
  - Secret files are readable by the non-root API user (`secrets-init` sets owner/mode to match the API image's UID, for example `chmod 0440` plus the right group).
  - When secrets are regenerated, the README says to run `docker compose down -v`, because PostgreSQL keeps the old role passwords in its volume.
  - The database is owned by `demoapi_migrator`; `demoapi_app` gets only the grants from the migrations.
  - Testcontainers mounts the same role script, so tests use the same roles.
- Aspire: optional `DemoApi.AppHost` (Aspire 13.6.3) after S4 at the earliest. Until then only the standalone Aspire dashboard runs in compose under profile `observability` (UI on `127.0.0.1:18888` with the dashboard's browser-token login kept on; OTLP 18889 inside the compose network only; ADR-0013).

## Consequences
- Good: G2 is met with one command; NFR-07 holds; DB roles match NFR-08 from day one; integration tests reuse the same role model.
- Bad / trade-offs: Development environment in compose shows Scalar and detailed health output locally; acceptable because it is local only.
- Bad / trade-offs: shell script in `deploy/compose/` must work on Windows hosts (it runs inside a Linux container, so it does).
- Bad / trade-offs: a developer who wants a DB tool must copy the override example; one extra step, on purpose.
- Follow-up actions: compose-chain spike on day 1 of S1a (also checks: ports bound to `127.0.0.1` only, Valkey rejects the `default` user, secret files `0440`); ST-10 and ST-12 in the integration tests; timed fresh-clone test at the end of S1a; secret scan in CI (CI outline); Security/Release phase decides on a prod-like compose profile.

## Revisit when
- A cloud or shared test environment is added (use a real secret store, for example Key Vault).
- The team moves to Aspire as the main local path.

## Links
- [solution-structure.md](../solution-structure.md) section 10
- ADR-0013
- PRD P4; pre-mortem F8
