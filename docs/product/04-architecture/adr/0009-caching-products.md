# ADR-0009: Cache product reads with HybridCache (L1 memory + Valkey L2) and a list generation key

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Architect |
| Consulted | Product owner (TTL and staleness), ADR-0011 owner (resilience) |

Revised 2026-10-27 (architecture review R-4): pending-invalidation replay list dropped (TTLs only); stale follow-up about ADR-0011 removed (ADR-0011 already has the outer 250 ms timeout and these TTLs).

Revised 2026-10-27 (security S-D1, S-D7): in-process security-stamp cache noted as the one exception to "no user-keyed cache"; list queries with `search` are not cached; `MemoryCache.SizeLimit`; Valkey `maxmemory` + `allkeys-lru`; Valkey ACL user with a password.

## Context
Product get-by-ID and list must be fast (NFR-01: p95 ≤ 20 ms cached, ≤ 100 ms uncached) and keep working with Valkey down (G3: p95 ≤ 300 ms, 0 × 5xx). Only product reads may be cached; customer, order, user, report and audit data never (FR-130). Any product write, stock change, soft delete or restore, and order create/cancel/item replace, must remove the affected entries before the response returns (FR-046, D15). Customers and Admins see different fields (`isDeleted` is Admin only, D9). The pre-mortem (F10) warns that list invalidation can become the most complex S1 code. Valkey 9.1 runs in Docker; one API instance this phase.

## Decision drivers
- Correctness after writes (no stale data after a successful write response).
- Valkey outage must never cause a 5xx or a slow response (≤ 250 ms cache timeout, NFR-03).
- Simple invalidation that a reader can follow; testable with Testcontainers.
- No personal data in the cache, by construction.
- Template quality: patterns that work for one and for several instances.

## Options considered

### Option 1: HybridCache (L1 in-memory + L2 `IDistributedCache` on Valkey)
- Good: built-in stampede protection per key, L1 keeps serving hot items when Valkey is down, System.Text.Json serialization, one API (`GetOrCreateAsync`, `SetAsync`, `RemoveAsync`). MIT, maintained by Microsoft.
- Bad: L1 is per instance; a removal on one instance does not clear L1 on others (no backplane). Tag invalidation semantics across instances and L2 outages are harder to reason about and test.
- Cost / effort: low.

### Option 2: `IDistributedCache` only (Valkey)
- Good: one shared store; explicit.
- Bad: every hit is a network round trip; no stampede protection; manual serialization; nothing served when Valkey is down.
- Cost / effort: low, but weaker on NFR-01 and G3.

### Option 3: ASP.NET Core output caching (with a Redis/Valkey store)
- Good: caches whole HTTP responses; tag eviction.
- Bad: skips authenticated requests by default, and every product endpoint is authenticated; enabling it means custom policies that vary by role, which is easy to get wrong (cached headers, one role's body served to another). Invalidation from order handlers (D15) would reach into the HTTP layer. DB fallback logic sits outside the handler.
- Cost / effort: medium, higher risk.

### List invalidation sub-decision
| Option | Good | Bad |
|---|---|---|
| Delete every list key (SCAN by prefix) | Exact | `SCAN`/`KEYS` on Valkey is slow and not available through `IDistributedCache` |
| HybridCache `RemoveByTagAsync("products:list")` | One call | Behaviour across instances and during an L2 outage is less visible; harder to prove in tests |
| **Generation key**: list keys include `g{generation}`; a write sets a new generation | Old lists become unreachable at once; works with any `IDistributedCache`; a slow reader that fills the cache with old data writes under the old generation, which nobody reads | One extra lookup per list read (L1-served); old entries live until their TTL |

## Decision
We will use **HybridCache with Valkey as L2** for product reads only, with **per-ID item keys** and a **list generation key**. Invalidation runs **after the DB commit and before the response**.

**What is cached**
- EP-23 get-by-ID: the role-specific `ProductResponse` plus its `ETag`, for non-deleted products only. Not-found and deleted products are not cached (no negative caching).
- EP-22 list: the role-specific `PagedResponse<ProductResponse>` (items + counts). **List queries with a `search` value are not cached** (S-D7, T-25): every distinct search term would add an entry, so an attacker could flood the cache. They always read the database.
- Admin `includeDeleted=true` on EP-22 and EP-23 **bypasses** the cache (rare, Admin-only, avoids a third scope).
- Nothing else. Only the Products feature may depend on `IAppCache` (architecture test).

**Keys** (prefix `products:v1:`; bump `v1` when the cached DTO shape changes)
| Entry | Key | L2 TTL | L1 TTL |
|---|---|---|---|
| Item | `products:v1:item:{scope}:{productId}` | 10 min | 1 min |
| List | `products:v1:list:{scope}:g{generation}:{queryHash}` | 2 min | 30 s |
| Generation | `products:v1:list-gen` (value: new GUID per write) | none (1 day max) | 5 s |

- `scope` = `customer` or `admin`. Each entry holds exactly what that role may see, so an Admin-only field can never reach a Customer from the cache (BOPLA).
- `queryHash` = SHA-256 (first 16 bytes, hex) of the **normalized** query: page, pageSize, sort (with the default made explicit), search (trimmed, lower-case), minPrice, maxPrice, inStock, in a fixed order. Unknown parameters are not in the key, so junk parameters cannot flood the cache. Search text is hashed, not stored in clear.
- The generation value is a new GUID on every write (not `INCR`), so no atomic counter or direct StackExchange.Redis call is needed, and two writers can never produce the same value.

**Invalidation ("Inval P")**
After `SaveChanges`/commit succeeds, before the response: remove the item keys (both scopes) for each affected product ID, and set a new generation. Triggers: EP-24..28 and the order writes EP-38, 41, 43, 45, 48, 49 (D15). Create only needs a new generation.
- Known race (items only): a reader that loaded the DB before the commit can write an old value after the removal. Bounded by the item TTL; lists are protected by the generation key. Accepted.

**Stampede protection**
HybridCache runs one factory call per key per instance; concurrent misses wait for it. Across instances, at most one DB call per instance per key. Enough for this load.

**Valkey down (NFR-03, G3)**
- L2 is `ResilientDistributedCache` (ADR-0011) around the StackExchangeRedis `IDistributedCache`: circuit breaker (≥ 50 % failures over ≥ 10 calls in 30 s → open 30 s), up to 3 fast retries (NFR-03), connection with `abortConnect=false`.
- **The 250 ms is a total budget per cache call, including retries** (an outer timeout around the retries, ADR-0011). With a per-attempt 250 ms only, a hanging Valkey could cost about 1 s per read before the breaker opens, which breaks the 300 ms p95 (NFR-01).
- Read failure, timeout or open breaker → treated as a miss; HybridCache L1 still serves hot keys; otherwise the DB answers.
- Write or remove failure → logged + metric. No replay list: the TTLs bound any staleness (items 10 min, lists 2 min; the generation key also expires). Valkey runs without persistence, so a Valkey restart empties it anyway.
- `/health/ready` reports `Degraded` (200).
- Metrics: hit/miss per entry type, L2 latency, L2 errors, breaker state (NFR-12).

**Size limits and Valkey settings (S-D7, T-23, T-25)**
- L1: `MemoryCache` with `SizeLimit` (Assumption: 10,000 entries; each entry has size 1). Full L1 evicts; it never grows without bound.
- Valkey: `maxmemory` (Assumption: 128 MB locally) with `maxmemory-policy allkeys-lru`, so a full cache evicts old keys instead of failing writes.
- Valkey runs with an ACL user and a generated password (ADR-0012); the default user is disabled and the app user cannot run `@dangerous` commands (`FLUSHALL`, `CONFIG`, `KEYS`).
- The `scope` part of a key comes only from the validated role claim; `includeDeleted` is dropped for Customers before the key is built.

**Never cached:** customer, user, order, report, audit and token data; anything keyed by user ID in Valkey; authorization decisions.

**One exception: the security-stamp cache (S-D1, ADR-0008).** The per-request stamp check keeps the user's stamp hash and deleted flag in an **in-process** memory cache for 30 s, keyed by user ID. It is not in Valkey, holds no personal data (a hash and a flag), is removed locally when this API changes the stamp, and fails closed. It is the only user-keyed cache allowed.

## Consequences
- Good: hot products are served from memory (sub-ms), Valkey adds a shared warm layer, and an outage degrades to DB reads without errors.
- Good: list invalidation is one write and easy to test (write → next list read misses).
- Bad / trade-offs: with several instances, L1 can be stale up to its TTL (1 min item, 30 s list, 5 s generation) because HybridCache has no backplane. Fine for one instance; documented for the template.
- Bad / trade-offs: two cache entries per product and per list query (one per scope).
- Bad / trade-offs: retries inside a 250 ms total budget rarely get all 3 attempts when Valkey hangs; they only help with fast failures (connection reset).
- Bad / trade-offs: if Valkey is down during a write, an old L2 entry can be served after Valkey returns, until its TTL ends (at most 10 min for an item, 2 min for a list). Accepted for one instance (A-D11).
- Follow-up actions: S1a spike on HybridCache failure handling with Valkey paused (pre-mortem F10). Tests: cache hit, invalidation per trigger (incl. D15 order writes), Valkey paused (200, no 5xx), scope separation (Admin entry never served to a Customer), `includeDeleted` bypass, `search` lists not cached, poisoned Valkey price ignored for orders, no PII in values (ST-15). Load test once in S1 (NFR-01).

## Revisit when
- More than one API instance runs → shorten L1 TTLs or add a pub/sub invalidation backplane.
- Cache hit ratio stays under 50 % (F10) → drop list caching and keep only items.
- HybridCache ships a supported distributed invalidation feature.

## Links
- PRD FR-046, FR-130, NFR-01, NFR-03, G3, D9, D13, D15; pre-mortem F10; US-100, US-101.
- [Endpoint catalog](../endpoint-catalog.md) section 1.12; ADR-0011 (resilience); ADR-0013 (observability).
- Microsoft.Extensions.Caching.Hybrid 10.11.0, Microsoft.Extensions.Caching.StackExchangeRedis 10.0.13 (stack-versions-2026-10-24.md).
