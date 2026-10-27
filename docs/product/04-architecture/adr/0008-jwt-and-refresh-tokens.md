# ADR-0008: Issue our own HS256 access JWTs with rotating, hashed refresh tokens

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-10-24 |
| Deciders | Architect, product owner |
| Consulted | Security reviewer (`dotnet-security-reviewer`) in the Security phase |

Revised 2026-10-27 (architecture review R-3, R-5): refresh-token reuse revocation commits before the 401 is returned, with a test; the `customer_id` claim is listed as a template strip point (solution-structure section 13).

Revised 2026-10-27 (security S-D1, S-D2, S-D3, S-D6, S-D7): security-stamp check per request (`sst` claim, fails closed); automatic lockout no longer revokes sessions; confirm 24 h / reset 1 h with separate providers and a persisted key ring; strict refresh now with the "benign race" option recorded; equal work + response floor; password checked before confirmed/locked; HS256 key custody and rotation runbook.

## Context
The API needs bearer tokens for Customers and Admins (FR-006..015, NFR-05). The access token must carry `sub`, roles, `customer_id`, `jti`, `iat`, `exp`, `iss` and `aud` (FR-009). Refresh tokens must rotate, be stored only as SHA-256 hashes, live 7 days sliding with a 30-day family limit, and a reused token must revoke its whole family (FR-010..012). External identity providers, MFA and client credentials are non-goals. The stack is fixed: ASP.NET Core Identity for users and passwords, `Microsoft.AspNetCore.Authentication.JwtBearer` 10 for validation. The token code is the highest-risk code in the POC (pre-mortem), and it must be reusable as a template (G5, P7).

## Decision drivers
- Meet FR-009..015 and NFR-05 exactly, with tests for each rule.
- One process issues and validates tokens this phase. No other service validates them.
- Small and readable enough to copy into the next API; no paid or copyleft components (NFR-18).
- Secrets only from user-secrets or environment variables; generated on first local run (NFR-07, P4).
- Clear upgrade path when a second API or a web front end arrives.

## Options considered

### Option 1: Own minimal issuer (Identity for users, `JsonWebTokenHandler` for JWTs, own refresh-token table)
- Good: full control over claims, lifetimes, rotation and reuse detection. Standard JWTs validated by the stock JwtBearer handler. Small code (~3 classes).
- Bad: we own security-sensitive code; needs focused tests and a fresh-context security review.
- Cost / effort: medium. Token tests first in S1.

### Option 2: `MapIdentityApi` bearer tokens
- Good: built in, almost no code.
- Bad: tokens are opaque Data Protection tokens, not JWTs. No `customer_id` claim control, no `aud`/`iss`. Refresh tokens are not stored, so no rotation tracking, family revocation or reuse detection. Fixed endpoint shapes (`/login`, `/refresh`) that differ from our contract. Fails FR-009..012.
- Cost / effort: low, but does not meet requirements.

### Option 3: OpenIddict (Apache-2.0) as an embedded OAuth 2.0 / OIDC server
- Good: standards-based (token endpoint, JWKS, introspection, revocation), refresh-token rotation built in.
- Bad: large surface (OAuth flows, client registration, scopes) for a password-only API. Our contract (JSON login body, 403 `email-not-confirmed`, family revoke reasons) would bend to the OAuth token endpoint format (`application/x-www-form-urlencoded`, `error` codes). Harder to read as a template.
- Cost / effort: high for this scope.

### Option 4: External IdP (Entra ID, Keycloak, Auth0)
- Good: no token code of our own; MFA and federation come for free.
- Bad: explicit non-goal (PRD section 3). Breaks "fresh clone in ≤ 15 min, one compose command" or adds a heavy container. Does not show the patterns the POC must teach.
- Cost / effort: high.

### Signing algorithm sub-decision
| Option | Good | Bad |
|---|---|---|
| **HS256**, ≥ 256-bit random key from secrets, `kid` header, key ring (current + previous) | Simplest; one secret; fast; no key files or JWKS; fits one issuer = one validator; dev key generated on first run (P4) | Anyone who can validate can also mint tokens, so the key must never leave this API. Not suitable once other services validate our tokens |
| RS256 / ES256 with `kid`, rotation and a JWKS endpoint | Other services can validate with the public key only; standard for multi-service setups | Key generation, storage (PEM / Key Vault), rotation job and JWKS endpoint; more to test and to explain; no current consumer needs it |

## Decision
We will use **Option 1** with **HS256**, because it meets every token requirement with the least code, and the only validator of our tokens is this same API.

**Access token (JWT)**
- Created with `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler` (not the legacy `System.IdentityModel.Tokens.Jwt`).
- Header: `alg: HS256`, `typ: at+jwt` (RFC 9068), `kid` = configured key ID.
- Claims: `sub` (user ID), `role` (one claim per role), `customer_id` (Customers only), `sst` (first 16 bytes of SHA-256 of the user's security stamp, base64url; S-D1), `jti` (new GUID), `iat`, `nbf`, `exp` (= now + 15 min), `iss`, `aud`. No email, name or other personal data in the token.
- Key: `Jwt:SigningKey` (base64, ≥ 32 bytes of CSPRNG output), optional `Jwt:PreviousSigningKey` for rotation. Options use `ValidateOnStart`; a key under 256 bits fails startup. Dev key generated into the `dev-secrets` volume (ADR-0012).

**Validation (`JwtBearerOptions`)**
- `MapInboundClaims = false`; `NameClaimType = "sub"`, `RoleClaimType = "role"`.
- `ValidAlgorithms = ["HS256"]` (allow-list; blocks `alg: none` and algorithm confusion); `ValidTypes = ["at+jwt"]`.
- `ValidateIssuer`, `ValidIssuer = Jwt:Issuer`; `ValidateAudience`, `ValidAudience = Jwt:Audience`.
- `ValidateLifetime`, `RequireExpirationTime`, `RequireSignedTokens`, `ValidateIssuerSigningKey = true`; `ClockSkew = 30 s`.
- `IssuerSigningKeys` = current + previous key (matched by `kid`).
- `kid` is resolved only from the configured keys; `jku`/`x5u` headers are ignored; tokens are never read from the query string.
- **Security-stamp check (S-D1, closes R1).** `JwtBearerEvents.OnTokenValidated` reads the user's stamp hash and `is_deleted` by `sub` (one primary-key lookup) and compares it with `sst`. Mismatch, deleted or unknown user → 401. Results are cached **in process** for 30 s per user and the entry is removed locally whenever this API changes the stamp. It **fails closed**: if the database cannot answer, the request gets 401/503, never a pass. Window ≈ 0 on one instance, ≤ 30 s across instances.
- The stamp is rotated explicitly (`UpdateSecurityStampAsync`) on lock and unlock, role assign and remove, erase and delete, logout-all, and Admin revoke-sessions. Identity already rotates it on password change and reset. Assumption: Identity does not rotate it on role or lockout changes by itself; a test checks every trigger (ST-06).
- Failures return 401 ProblemDetails `unauthorized` (no `WWW-Authenticate` error detail beyond `Bearer`).

**Login order (T-03, S-D6)**
- The handler verifies the password **first** (`UserManager.CheckPasswordAsync`; for an unknown email it hashes against a fixed dummy hash so the work is the same). Only after a correct password does it check deleted, locked and email-confirmed. `SignIn.RequireConfirmedEmail` stays `false`; our handler enforces it.
- Result: unconfirmed + wrong password → generic 401; unconfirmed + correct password → 403 `email-not-confirmed` (FR-007). A wrong password calls `AccessFailedAsync`.

**Timing (SEC-3, S-D6)**
- Equal work: register hashes the password on both the new-email and existing-email paths; login hashes against a dummy for unknown users; forgot and resend do the same lookups on both paths.
- Response floor via `TimeProvider`: register 500 ms, forgot-password and resend-confirmation 250 ms (configurable). Test: 30 calls per path; all ≥ floor; median difference < 50 ms (ST-08).

**Confirm and reset tokens (S-D3)**
- Two separate Data Protection token providers: email confirmation **24 h**, password reset **1 h**. Both are bound to the security stamp, so a reset token is single-use.
- The Data Protection key ring is persisted (dev: named volume `dp-keys`, ADR-0012; production: Blob Storage + Key Vault). Links survive an API restart (ST-17). Links are built only from `Email:LinkBaseUrl`, never from the `Host` header.

**Refresh token**
- Format: 32 bytes from `RandomNumberGenerator`, base64url (43 chars). Opaque; no meaning inside.
- Storage (`RefreshTokens` table): `Id`, `UserId`, `FamilyId`, `TokenHash` (SHA-256, 32 bytes, unique index), `CreatedAt`, `ExpiresAt`, `FamilyExpiresAt`, `UsedAt`, `ReplacedById`, `RevokedAt`, `RevokeReason`. Plain SHA-256 is enough because the token has 256 bits of entropy (no slow hash needed). Not audited; logged without values (D4).
- Login starts a new family: `FamilyExpiresAt` = now + 30 days.
- Rotation (EP-05) in one transaction: hash the input; load the row with `SELECT … FOR UPDATE` (same row-lock pattern as stock, P3; `ExecuteUpdate` is banned, D11).
  - Unknown, expired, or family past `FamilyExpiresAt` → 401, no other change.
  - `UsedAt` or `RevokedAt` already set → **reuse**: revoke every active token of the family (`reuse-detected`), warning log, reuse metric, 401. **The revocation commits before the 401 is returned.** `ITransactionRunner` rolls back when the work returns a failure `Result`, so the refresh work returns a successful internal outcome `RefreshOutcome.ReuseDetected`; the runner commits, and only then does the handler map that outcome to 401. A test checks that after a reuse 401 the family's tokens are revoked in the database (a fresh read, not the same context).
  - User locked, deleted or unconfirmed → 401, no new tokens.
  - Otherwise set `UsedAt`, create the next token in the same family with `ExpiresAt = min(now + 7 days, FamilyExpiresAt)`, link `ReplacedById`, and issue a new access token with the user's **current** roles.
  - Two parallel requests with the same token: the second waits on the row lock, then sees `UsedAt` and is treated as reuse (US-005 AC6). This strict behaviour is the decision for now (SEC-2, S-D6); clients refresh one at a time.
- Revocation (sets `RevokedAt` + reason on all active tokens of the user): `logout` (one token), `logout-all`, `password-changed`, `password-reset`, `admin-lock`, `role-changed`, `account-deleted`, `admin-revoked` (stretch), `reuse-detected` (family). **Automatic failed-login lockout does not revoke sessions** (S-D2): otherwise anyone who knows an email could log the victim out with 5 bad passwords.
- Transport this phase: JSON body in and out (`TokenResponse`), `Cache-Control: no-store`. Never in URLs, query strings or logs. An HttpOnly cookie option comes with the web front end.

**Decided in the Security phase (threat model section 9)**
- **R1 (S-D1):** security-stamp check per request, as above. Options not chosen: 5-min access tokens (window still 5 min, 3× refresh load); accept 15 min (a demoted Admin keeps rights); deny list in Valkey (Valkey is optional, G3, so it would fail open or become a hard dependency).
- **SEC-2 (S-D6):** strict now. Recorded for a browser client: option C, **"benign race"**: a used token presented within N seconds of its `UsedAt` returns 401 `refresh-superseded` **without** revoking the family. It avoids random logouts when a browser sends two refreshes at once, at the cost of weaker reuse detection inside those N seconds. Adopt it by ADR when a browser front end arrives. A grace window that issues a sibling pair (family branches) is rejected.
- **SEC-3 and T-03 (S-D6):** equal work + response floor + password-first login order, as above.

**HS256 key custody and rotation (threat model section 3, item 1)**
- POC: a 64-byte random key per machine, generated into `dev-secrets`; every environment has its own key; the key never leaves this API.
- Planned rotation runbook: (1) add the new key as `Jwt:SigningKey` with a new `kid`, move the old key to `Jwt:PreviousSigningKey`; (2) keep the previous key for at least 16 min (15 min token life + 30 s skew); (3) remove the previous key.
- Emergency (key leaked): replace both keys at once. All access tokens die at once; refresh tokens keep working because they are not signed, so clients get new access tokens on the next refresh. Also rotate all security stamps if forged tokens may have been used.
- Production: key in Key Vault, read with managed identity; rotate every 90 days (Assumption) and on any suspicion. Move to ES256 with JWKS only when another service validates our tokens.

## Consequences
- Good: every FR-009..015 rule maps to one place (`JwtTokenIssuer`, `RefreshTokenStore`) and one test class. Standard JWT validation; no custom auth handler.
- Good: a database leak exposes only hashes that cannot be turned back into usable tokens.
- Bad / trade-offs: HS256 means the signing key is a "mint" key; if it leaks, an attacker can forge any role. Mitigated by secrets-only storage, ≥ 256 bits, rotation with `kid`, and no sharing with other services.
- Bad / trade-offs: refresh is one DB transaction with a row lock per call; fine at POC scale.
- Follow-up actions: token tests first in S1b (tampered signature, `alg: none`, wrong `typ`/`iss`/`aud`, expired + 30 s skew, reuse, reuse revocation persisted after the 401 (C4), race, family limit with `FakeTimeProvider`); the template strip guide removes the `customer_id` claim with the Customers feature (C5); fresh-context security review of `JwtTokenIssuer` and `RefreshTokenStore`; security tests ST-04 to ST-08 and ST-17 (threat model section 6); put the key rotation runbook in the README.
- Bad / trade-offs: the stamp check adds one primary-key lookup per user per 30 s and makes authenticated calls depend on PostgreSQL (fails closed). It is the one in-process, user-keyed cache (ADR-0009 exception).
- Bad / trade-offs: the response floor makes register, forgot and resend slightly slower (250-500 ms).

## Revisit when
- A second service or a gateway must validate our tokens → move to ES256/RS256 with a JWKS endpoint (key ring and `kid` already in place).
- A browser front end is added → refresh token in an HttpOnly, `SameSite=Strict` cookie plus antiforgery.
- MFA, external login or client credentials enter scope → re-evaluate OpenIddict or an external IdP.

## Links
- PRD FR-006..021, NFR-05, NFR-10, R1; user stories US-004..011; [endpoint catalog](../endpoint-catalog.md) EP-04..09.
- ADR-0012 (local setup and secrets), ADR-0010 (rate limiting of auth endpoints).
- RFC 9068 (JWT profile for OAuth 2.0 access tokens); RFC 8725 (JWT best current practices); OWASP API2:2023 Broken Authentication.
