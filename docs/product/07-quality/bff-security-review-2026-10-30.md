# Security review: DemoApi.Bff (2026-10-30)

Reviewer: `dotnet-security-reviewer` (fresh context, read-only). Saved by the main session because the agent has no write tool. Condensed from the agent's report; finding IDs and evidence are unchanged.

Scope: `web/DemoApi.Bff` and `web/DemoApi.Bff.Tests` on `feature/build-orders-app` (not merged). Checked against ADR-0015 (+ Amendment 1), threat model §10 (T-37..T-55, FT-01..FT-20, S-D9..S-D13), NFR-F09..F16 and F29..F39, OWASP ASVS 5.0, Top 10:2025, API Top 10:2023. Not run dynamically. Out of scope: the SPA (needs `react-security-reviewer`), the API, compose/Dockerfiles (no `web` service in compose yet).

## Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 8 |
| Info | 8 |

No finding gives an unauthenticated or cross-site attacker a way to read tokens, ride a session, bypass the allow-list or bypass CSRF. `dotnet package list --vulnerable --include-transitive`: none. 62 of 62 BFF tests pass.

## Findings

| ID | Sev | Finding | Evidence | Fix | Disposition |
|---|---|---|---|---|---|
| L-1 | Low | Single-flight entry stays in the dictionary when the last waiter aborts; the next caller gets a stale outcome (spurious 429/503/401, or rotated-out tokens held in memory). | `Session/RefreshCoordinator.cs:56-73` | The flight removes its own entry in a `finally` (compare-and-remove). Test with `RefreshGate`: cancel mid-refresh, release, assert empty and a fresh EP-05 call later. | Fix now |
| L-2 | Low | API-side tokens orphaned or left valid: EP-04 uses `RequestAborted`; `store.CreateAsync` failure never revokes; a lost refresh save drops the new pair unrevoked; logout with `Throttled`/`Unavailable` refresh sends no EP-06/07. | `Auth/BffEndpoints.cs:58,81`; `Session/RefreshCoordinator.cs:137-138`; `Session/SessionService.cs:77-91` | Login uses `CancellationToken.None`; revoke on `CreateAsync` failure; revoke the new pair when the save loses; do not spend the refresh budget on logout. Tests for each (incl. FT-05 race). | Fix now |
| L-3 | Low (Medium confidence) | Minimal hosting adds the developer exception page in `Development`; all tests run as `Production`. NFR-F39/T-30 allow none in any environment. | `Program.cs:1-6`, `BffHost.cs:86`, `launchSettings.json:6`, `BffFactory.cs:70` | Add a Development-environment test (forced 500, and a failing handler); ensure problem+json with no stack trace; run the container as `Production` with an explicit `Valkey:AllowInMemory` flag. | Fix now (test + mitigation) |
| L-4 | Low | Health endpoints are unauthenticated and unlimited; each `ready` call hits the API and Valkey. | `Security/Middleware.cs:170`; `Health/HealthChecks.cs:8-9,19,38-41` | Cache the ready result 5-10 s; `live` free; own generous limit for `ready`. | Fix now |
| L-5 | Low | Planned security events and OTel (T-55, NFR-F19) not built; CSRF rejections and failed logins are silent. | `Security/Middleware.cs:138-141`; `Auth/BffEndpoints.cs:87-99` | `[LoggerMessage]` events with hashed session IDs and masked IPs, never email; OTel with query redaction. | Deferred to FE-S0b (planned) |
| L-6 | Low | Data Protection `KeysPath` not validated outside Development; a misconfigured container uses an ephemeral key ring and every restart ends all sessions. | `BffHost.cs:40-48`; `Configuration/OptionsValidators.cs` | `ValidateOnStart` validator requiring an existing, writable `KeysPath` outside Development. Tests (FT-06). | Fix now |
| L-7 | Low (planned R2 gap) | Admin step-up (`reauth-required`, S-D11, NFR-F38) not implemented while EP-16/EP-19 are proxied. | `Proxy/AllowList.cs:45,48` | Store `SignedInAt`; gate EP-16/EP-19 at 15 min; FT-12. | Deferred: must be done before any Admin UI ships (R2) |
| L-8 | Low | Forwarded request headers use a block-list; `X-Real-IP`, `True-Client-IP`, `Via`, `Referer`, etc. reach the API. | `Proxy/ProxyTransforms.cs:10-20,40-47` | `CopyRequestHeaders = false` plus an allow-list (`Accept`, `Accept-Language`, `Content-Type`, `If-Match`, `If-None-Match`, `User-Agent`, BFF `traceparent`). Extend the strip test. | Fix now |

## Info

- I-1: `LastActivityAt` can be rewound by a refresh save (`RefreshCoordinator.cs:126-134`). Fail-safe. Merge the value inside `TryReplaceAsync`. Fix with L-1.
- I-2: `{id:guid}` accepts non-canonical forms (braces, 32 digits). API enforces its own. Optional regex constraint.
- I-3: `ApiBaseUrl` accepts any http(s). Require `https` outside Development/compose before hosting. No SSRF: destination is fixed, path comes from the allow-list.
- I-4: Docs differ from code: cookie is the raw 256-bit ID (not DP-protected), custom `SessionStore` not `ITicketStore`, no OTel. Update ADR-0015.
- I-5: `AllowedHosts` is `*`. Set when hosted.
- I-6: `Problems.SlugFor` maps non-listed statuses below 500 to the "forbidden" text (cosmetic).
- I-7: CSP lacks Trusted Types directive and report endpoint (S-D13, report-only first).
- I-8: Missing tests the NFRs call for: FT-05 (logout/abort during refresh), FT-06 (Valkey down, rotated DP keys), FT-07 (refresh 31st call, anonymous 60/min, IPv6 /64, API 429 pass-through), FT-12 (12 h absolute), FT-13 (logout-all, EP-37 ends session), FT-04 (allow-list vs `v1.json`, 1 MB limit → 413), FT-18 (Development run).

## Open questions

1. Should the BFF run as `Development` in compose with in-memory sessions as fallback? (Closes L-3; main session chose: `Production` plus an explicit flag.)
2. Is `ready` called by the SPA banner on a timer? If so, the L-4 cache matters more.
3. `Email:LinkBaseUrl` must be one of the allowed origins (NFR-F29); verify in compose or CI.

## Done well (keep)

256-bit CSPRNG session IDs and `__Host-` cookie flags; session entry bound to its key by Data Protection purpose and payload check; fixation handling (new ID per sign-in, planted cookie never adopted); single-flight refresh with version-checked save and logout always winning; CSRF (`X-CSRF`, one allow-listed `Origin`, `Sec-Fetch-Site`) before the rate limiter, no CORS; YARP allow-list with typed constraints, path guard and case-sensitive literals; request and response header stripping; 1 MB body limit; rate limiting keyed by `RemoteIpAddress` only (IPv6 /64), `X-Forwarded-For` never read; full security headers on every response; fixed error bodies, no API body passed on, no secrets in logs.

## Commands run

`dotnet package list --vulnerable --include-transitive`: none. `dotnet test` on `DemoApi.Bff.Tests`: 62 passed.
