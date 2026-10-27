# Architecture review (devil's advocate)

Date: 2026-10-27 | Reviewer: devils-advocate (read only) | Status: Reviewed; changes R-1..R-5 accepted 2026-10-27
Related: [architecture](architecture.md) · [pre-mortem](../02-requirements/pre-mortem.md) · [summary](../00-summary.md)

**Verdict: go with changes.** The design holds together and follows the pre-mortem decisions. Two design defects needed fixing before S1 (database breaker tripped by 409s; `ITransactionRunner` breaking the dependency rule), and S1 is overloaded (17 endpoints plus the whole platform).

## 1. Contradictions and gaps

| # | Issue | Where | Fix |
|---|---|---|---|
| C1 | DB breaker counts business errors: `PostgresException` (23505, 23514, lock timeout) derives from `NpgsqlException`, so a burst of 409s could open the breaker and return 503 everywhere for 30 s | ADR-0011; data-model §6.1 | Count only connection failures and timeouts; test "10 duplicate SKUs leave the breaker closed" |
| C2 | `ITransactionRunner` declared in Application, implemented in BuildingBlocks (breaks rule A2) | solution-structure §5, §8 | Move interface to SharedKernel next to `ISqlRunner` |
| C3 | EF does not clear tracked entities between execution-strategy attempts (audit rows, Identity users from attempt 1 remain) | ADR-0011 | `ChangeTracker.Clear()` at the start of each attempt; forced-transient-failure test |
| C4 | Refresh-token reuse revocation could be rolled back with the failed result | ADR-0008; architecture §5a | Revocation commits before the 401; test |
| C5 | Customer concept leaks into kept template code (`ICurrentUser.CustomerId`, `customer_id` claim, `CustomerOnly` policy, register) | solution-structure §13, ADR-0008 | List every leak in the strip guide; strip CI job at end of S2 |
| C6 | Two Valkey-down designs (replay list vs TTL only); stale follow-up in ADR-0009 | ADR-0009 vs architecture §1, §9 | Drop the replay list; TTLs only |
| C7 | `restored-customer` lock type (stretch, P2) in the core schema | data-model §3.1 | Add with the stretch migration |
| C8 | Seed advisory lock needs raw SQL, which the rules ban; little value with one instance | data-model §7 | Drop; partial unique email index is the guard |
| C9 | Status and question drift across documents | several | One cleanup pass |
| C10 | Soft-delete filter on `AppUser` can silently drop deleted customers in joins/includes (Assumption) | data-model §3.2; ADR-0006 | Test Admin get of a deleted customer, including email |

## 2. Over-engineering cuts

| Item | Simplest option kept |
|---|---|
| Custom resilient execution strategy with AsyncLocal nesting | Plain `EnableRetryOnFailure` + a `DbConnectionInterceptor` breaker that counts only connection failures; 1 s connect timeout gives "503 within 5 s" (fixes C1) |
| Cache invalidation replay list | Dropped (C6) |
| Interceptor auto-touch of the order root | Explicit `order.Touch(now)` in domain methods |
| 14 architecture rules all in S1 | S1: core layer rules + banned APIs; the rest when first needed |
| Hand-written Postman per request + build-time OpenAPI export + CI ApiFiles stage | Postman collection generated from the exported OpenAPI (with environments, no secrets); `.http` hand-written; OpenAPI drift checked by an integration test against the committed file; ApiFiles CI stage after S4 |
| `compose.prodlike.yaml` with dev certificate | Deferred to Security/Release; integration tests in `Production` cover headers |
| Trigram + extra sort indexes in S1 | S1: PK, unique and FK indexes; the rest after `EXPLAIN` |
| Dev sample data (10,000 orders) in S1 | Moved to S3/S4 load tests |
| Seed advisory lock | Dropped (C8) |

## 3. S1 time-box risks

| Risk | Early warning | Spike / mitigation |
|---|---|---|
| Compose chain on a fresh Windows clone | First `docker compose up` needs a manual step | Day-1 spike: LF line endings for `*.sh` (`.gitattributes`), secret files readable by the non-root API user, `down -v` when secrets regenerate, database owned by `demoapi_migrator`; Testcontainers mounts the same role script |
| Identity + audit interceptor (F5) | > 2 workarounds | Week-1 spike incl. forced rollback and C3 |
| Token rotation | Flaky reuse test | Token tests first, incl. C4; `FakeTimeProvider` |
| HybridCache with L2 down | p95 > 300 ms or 5xx with Valkey paused | Half-day spike |
| FastEndpoints | 9.0 betas shipping | Verified `FastEndpoints.Messaging.Core` 8.3.0 exists; pin 8.3.x through S1-S4 |
| `EFCore.NamingConventions` for EF Core 10 | Not on NuGet | Check day 1; small convention as fallback |
| "503 within 5 s" | 4 attempts × 2 s connect timeout > 5 s | 1 s connect timeout + connection-level breaker |
| S1 size | Day 5 with no endpoint at full DoD | Split S1 into S1a walking skeleton and S1b auth |

## 4. Handed to the Security phase
R1, SEC-2, SEC-3, ADR 0014; HS256 key custody and rotation; dev secrets readable via `docker compose exec`; compose ports bound to localhost; Scalar in Development; rate-limit IP keying behind a proxy; lockout as DoS on a shared IP (A-D10); stale `customer_id` claim after erase; cache scope separation; Dapper sort allow-lists and `ILIKE` escaping; refresh token in body vs cookie; confirm/reset token lifetimes; audit log readers; erasure completeness incl. logs; DB role passwords and grant-drift test; `statement_timeout`/`lock_timeout` tuning.

## 5. Accepted changes (2026-10-27)

| ID | Change |
|---|---|
| R-1 | DB circuit breaker is connection-level (`DbConnectionInterceptor`), counts only connection failures/timeouts; plain `EnableRetryOnFailure`; 1 s connect timeout; tests "409 does not trip it" and "DB down → 503 within 5 s" (meets the brief's DB circuit breaker) |
| R-2 | S1 split into S1a walking skeleton (compose, CI, product create/get end to end with audit, cache, authz test, four spikes) and S1b auth; kill rule: > 2 days over → pre-mortem cut rule. Time-boxes set in Delivery |
| R-3 | `ITransactionRunner` in SharedKernel; `ChangeTracker.Clear()` per attempt; reuse revocation commits before 401; each has a test |
| R-4 | Cuts in section 2 applied; Postman collection generated from OpenAPI satisfies the brief (every endpoint, environments, no secrets) |
| R-5 | Template strip CI job at end of S2; Customer leaks listed in the strip guide; doc cleanup (C7, C9) and test for C10 |

Single instance assumed for the POC; the template documents adding an invalidation channel before scaling out (A-D11).
