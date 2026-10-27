# ADR-0010: Rate limit with the built-in ASP.NET Core limiter and three fixed-window policies

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Architect |
| Consulted | Product owner (limits), Security phase |

Revised 2026-10-27 (security S-D2, S-D7): EP-09 change password moves to `auth-strict`; startup guard for forwarded headers; global concurrency limiter on `/reports/*` (4 at a time).

## Context
NFR-09 and FR-131 require three policies: `auth-strict` 10 requests/min per IP for register, login, confirm, resend, forgot, reset and erase, as **one shared bucket per IP** (D16); `refresh` 30/min per IP; `general` 300/min per signed-in user and 60/min per IP when anonymous. A rejected call returns 429 ProblemDetails with `Retry-After`, and 429 is checked before 401 (permission matrix). Health endpoints have no limit (D10). The built-in limiters have no `TimeProvider`, so tests must use short real windows (P8). One API instance runs locally; there is no reverse proxy this phase.

## Decision drivers
- Exact, explainable numbers that match NFR-09 and are easy to test.
- 429 before 401, but the per-user `general` partition needs the authenticated user.
- No extra packages; works with FastEndpoints endpoints.
- Correct client IP when a proxy is added later, without trusting spoofed headers today.
- Fast, deterministic tests (suite ≤ 5 min, NFR-16).

## Options considered

### Option 1: Built-in `Microsoft.AspNetCore.RateLimiting` (in-memory, per instance)
- Good: in the framework; named policies; partitioned limiters; `OnRejected` hook; endpoint metadata (`RequireRateLimiting`, `DisableRateLimiting`) works with FastEndpoints. A policy shares its partitions across all endpoints that use it, which gives D16's shared bucket for free.
- Bad: per instance (limits multiply with instances); no `TimeProvider`.
- Cost / effort: low.

### Option 2: Distributed limiter in Valkey (Lua script or a library)
- Good: one limit across instances.
- Bad: extra code or package; Valkey becomes a hard dependency for auth endpoints (conflicts with "works when Valkey is down"); slower.
- Cost / effort: medium. Not needed for one instance.

### Option 3: FastEndpoints `Throttle()`
- Good: one line per endpoint.
- Bad: keyed by header/IP only, no per-user partition or shared named buckets; ties the policy to FastEndpoints (exit path, ADR-0002).
- Cost / effort: low, but does not meet D16 or the per-user rule.

### Algorithm sub-decision
| Algorithm | Good | Bad |
|---|---|---|
| **Fixed window** | Matches "N per minute" exactly; simple; `Retry-After` from lease metadata; predictable tests | Burst at the window edge (up to 2N in a short span) |
| Sliding window (segments) | Smooths edge bursts | Harder to explain and test with 1-2 s windows; `Retry-After` less precise |
| Token bucket | Good for steady API traffic with bursts | Limits are "rate + burst", not "N per minute"; does not map 1:1 to NFR-09 |

## Decision
We will use the **built-in limiter** with **fixed-window** partitioned policies, plus **one global concurrency limiter for reports**.

| Policy | Endpoints | Partition key | Limit | Queue |
|---|---|---|---|---|
| `auth-strict` | EP-01, 02, 03, 04, **09**, 10, 11, 37 | `ip:{clientIp}` (one bucket shared by all 8) | 10 / 60 s | 0 |
| `refresh` | EP-05 | `ip:{clientIp}` | 30 / 60 s | 0 |
| `general` | All other `/api/v1` endpoints (group default) | Signed in: `user:{sub}` → 300 / 60 s. Anonymous: `anon:{clientIp}` → 60 / 60 s | as listed | 0 |
| none | EP-56, EP-57, OpenAPI/Scalar (Development only) | - | `DisableRateLimiting()` | - |
| global `reports` (concurrency) | `/api/v1/reports/*` (EP-50, 52, 53, 54) | one shared partition `reports` | **4 requests at a time** | 0 |

- **EP-09 change password on `auth-strict` (S-D2, T-05).** A stolen access token could otherwise try 300 passwords a minute on `general`. A wrong current password also calls `AccessFailedAsync`, so it counts toward the FR-008 lockout (same as EP-37).
- **Reports concurrency (S-D7, T-21).** Reports are the most expensive queries. `GlobalLimiter` partitions by path: requests under `/api/v1/reports/` share one `ConcurrencyLimiter` (4 permits, no queue); every other path gets `NoLimiter`. A global limiter is used because an endpoint can carry only one named policy, and reports keep `general`. The 5th parallel report gets 429 ProblemDetails with `Retry-After: 1`. Assumption: 4 fits the default pool size; tune in the S4 load test.

- **Client IP:** `HttpContext.Connection.RemoteIpAddress`. IPv6 addresses are keyed by their /64 prefix (one client often owns a whole /64). **Assumption.**
- **`sub` comes only from a validated token.** An invalid or expired token yields no user, so the caller is limited as anonymous by IP.
- **Pipeline order:** `UseForwardedHeaders` (only when proxies are configured) → `UseRouting` → `UseAuthentication` → **`UseRateLimiter`** → `UseAuthorization` → FastEndpoints. Authentication only reads the token; it never rejects. So the limiter knows the user, and 429 still comes before the 401/403 from authorization.
- **Rejection (`OnRejected`):** status 429, `Retry-After` = lease `RetryAfter` metadata rounded up to whole seconds (fallback: window length), body ProblemDetails `type: /problems/rate-limited` with `traceId`, `Cache-Control: no-store`. Increment the rate-limit metric with the policy name; log policy and partition type (never the token, never the full IP outside Development: masked).
- **Behind a proxy (later):** enable `ForwardedHeadersOptions` with `XForwardedFor | XForwardedProto`, `ForwardLimit = 1`, and `KnownProxies` / `KnownNetworks` from configuration. With an empty list the middleware is not added, so a client cannot spoof `X-Forwarded-For` today.
- **Startup guard (S-D7, T-31):** if `ForwardedHeaders:Enabled` is true but both `KnownProxies` and `KnownNetworks` are empty, startup fails with a clear message. This stops a later configuration from trusting `X-Forwarded-For` from anyone. Test: by default an `X-Forwarded-For` header does not change the rate-limit partition (ST-09).
- **Configuration:** `RateLimiting:{policy}:PermitLimit` and `:WindowSeconds` (and `AnonymousPermitLimit` for `general`) from `appsettings.json`, validated on start.
- **Per-account brute force** is handled by Identity lockout (5 failures → 15 min, FR-008), independent of the IP limit. EP-09 and EP-37 wrong passwords count toward it. Automatic lockout does not revoke sessions (S-D2, ADR-0008).

**Tests (P8)**
- `WebApplicationFactory` overrides the options: e.g. `auth-strict` 3 per 1 s, `general` 5 / 3 per 1 s. Tests: N+1 → 429 with `Retry-After` and ProblemDetails; after the window → 2xx again; shared bucket across login, register, change password and erase (D16); 5th parallel report → 429 (reports limit set to 1 in tests); forwarded-headers guard fails startup; two users from one IP have separate `general` buckets; anonymous over the limit on a protected endpoint gets 429, not 401; health never 429.
- Limiter tests run in their own collection (no parallel runs) so real time does not make them flaky. All other tests use high limits.

## Consequences
- Good: zero extra packages; numbers match NFR-09 one to one; the D16 shared bucket is a natural result of named policies.
- Good: limits are per user when signed in, so users behind one NAT do not block each other on `general`.
- Bad / trade-offs: edge bursts up to 2× in a short span (fixed window). Accepted: auth endpoints are also protected by account lockout.
- Bad / trade-offs: in-memory and per instance. With N instances the effective limit is N × limit, and limits reset on restart.
- Bad / trade-offs: anonymous users behind one NAT share 10/min on auth endpoints (e.g. an office). Accepted for the POC.
- Follow-up actions: rate-limit metric and log in ADR-0013 (the metric also counts report-concurrency rejections). Security phase done (S-D2, S-D7). Before production: higher per-IP auth limit plus a per-account limiter keyed by the hashed normalized email (threat model section 3 item 6).

## Revisit when
- More than one API instance, or a gateway in front → distributed limiter or gateway limits; configure forwarded headers.
- Real traffic shows legitimate users hitting 429, or edge bursts show up in auth logs → sliding window for `auth-strict`.
- .NET adds `TimeProvider` support to the limiters → switch tests to `FakeTimeProvider`.

## Links
- PRD FR-131, NFR-09, D10, D16, P8; US-292; permission matrix (check order).
- [Endpoint catalog](../endpoint-catalog.md) sections 1.9 and 1.11; ADR-0008 (auth endpoints); ADR-0013 (metrics).
- [FixedWindowRateLimiterOptions (no TimeProvider)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting.fixedwindowratelimiteroptions?view=net-10.0); OWASP API4:2023 Unrestricted Resource Consumption.
