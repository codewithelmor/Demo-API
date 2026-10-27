# User stories 01: Auth & identity, Admin user and role management

Last updated: 2026-10-27 (security S-D2, S-D3, S-D6) | Status: Approved 2026-10-24
Scope: EP-01 to EP-21 (Auth and identity; Admin user and role management)
Stories: 21 (US-001 to US-021). 19 Must (S1: 9, S2: 10), 2 Could (Stretch)
Sources: [PRD](../prd.md) · [endpoint list](../endpoint-list.md) · [permission matrix](../permission-matrix.md) · [summary](../../00-summary.md)
Tracker: Azure DevOps Boards (export with `azdo-plan-to-work-items`)

## Contents

| US | EP | Title | Slice |
|---|---|---|---|
| US-001 | EP-01 | Register as a customer | S1 |
| US-002 | EP-02 | Confirm email | S1 |
| US-003 | EP-03 | Resend confirmation link | S1 |
| US-004 | EP-04 | Log in | S1 |
| US-005 | EP-05 | Refresh tokens with rotation and reuse detection | S1 |
| US-006 | EP-06 | Log out one session | S1 |
| US-007 | EP-07 | Log out all sessions | S1 |
| US-008 | EP-08 | Get current user | S1 |
| US-009 | EP-09 | Change own password | S1 |
| US-010 | EP-10 | Request a password reset | S2 |
| US-011 | EP-11 | Reset password with a token | S2 |
| US-012 | EP-12 | List users (Admin) | S2 |
| US-013 | EP-13 | Get a user (Admin) | S2 |
| US-014 | EP-14 | Lock a user (Admin) | S2 |
| US-015 | EP-15 | Unlock a user (Admin) | S2 |
| US-016 | EP-16 | Assign the Admin role | S2 |
| US-017 | EP-17 | Remove the Admin role | S2 |
| US-018 | EP-18 | List roles (Admin) | S2 |
| US-019 | EP-19 | Create an Admin user | S2 |
| US-020 | EP-20 | Revoke a user's sessions (Admin) | Stretch |
| US-021 | EP-21 | Send a password-reset link for a user (Admin) | Stretch |

## Common rules (apply to every story below; tested once per endpoint)
- Every 4xx/5xx is RFC 9457 ProblemDetails with `type`, `title`, `status`, `detail`, `instance`, `traceId`. Validation errors add `errors: { field: [messages] }`. No stack trace outside Development (FR-124, NFR-13).
- Body > 1 MB → 413. Non-JSON body → 415 (FR-137).
- Over the rate-limit policy → 429 ProblemDetails with `Retry-After`. 429 is checked before all other rules (FR-131, NFR-09). `auth-strict` = 10/min per IP; `refresh` = 30/min per IP; `general` = 300/min per user (60/min per IP when anonymous).
- All responses in this file carry `Cache-Control: no-store` (NFR-06). Server-side cache: none (FR-130).
- Every meaningful account change creates `AuditLogs` row(s) in the same transaction: user insert, email confirmed, password changed or reset, role assigned or removed, lock and unlock, account delete and restore. The actor is the user ID, `anonymous` or `system`. Password hashes, security stamps and token hashes are never in the audit row. A rollback leaves no audit row (FR-128, NFR-16).
- Not audited (decision 2026-10-24): refresh-token rows (issue, use, revoke) and the failed-login counter. These are recorded as structured log entries and metrics instead (no token values).
- Logs never contain passwords, tokens or token hashes. Emails are masked, except fake-sender lines in Development (FR-020, NFR-10).
- Refresh-token revocation (FR-015) sets `RevokedAt` and a revoke reason on every active token of the user.
- Admin endpoints (EP-12 to EP-21): a Customer gets 403 whether or not the `{userId}` exists. A Customer with an invalid body also gets 403, not 400. User resources have no ETag or `If-Match` (FR-129 covers products, customers and orders only).

---

### US-001 - Register as a customer (EP-01, Slice S1, Must)
**As a** visitor **I want** to register with my email and a password **so that** I can become a customer and use the API.
Traces: FR-001, FR-002, FR-003, FR-020, FR-128, NFR-09, NFR-10
Route: POST /api/v1/auth/register  ·  Rate limit: auth-strict  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an email that does not exist, When I post email, password (15-128 chars), firstName, lastName and optional phone, Then 202 with a fixed body. One transaction creates an unconfirmed Identity user, the `Customer` role assignment and the Customer record. Audit rows: Insert (user), RoleAssigned, Insert (customer), actor `anonymous`. The fake sender logs recipient, subject and a confirmation link (base URL from config, token valid 24 h). No password in logs.
- AC2 (validation) - Given a missing or invalid email, a password shorter than 15 or longer than 128 characters, or a missing firstName/lastName, When I post, Then 400 with `errors` for each failing field (`email`, `password`, `firstName`, `lastName`, `phone`).
- AC3 (password policy edge) - Given a 15-character password with only lowercase letters, or one with spaces and emoji, When I post, Then it is accepted (no composition rules, all Unicode allowed).
- AC4 (enumeration-safe) - Given the email already exists (confirmed or not), When I post, Then 202 with a body identical to AC1. No user, customer or audit row is created. The fake sender logs an "account already exists" notice instead of a link.
- AC5 (atomicity) - Given the Customer insert fails, When I post, Then no Identity user, role row, Customer record or audit row exists afterwards. The response is ProblemDetails with no exception text.
- AC6 (signed-in caller) - Given a caller with a valid Customer or Admin token, When they post, Then the behavior is the same as AC1/AC4 (matrix: Allow).
- AC7 (rate limit) - Given 10 register calls from one IP within 1 min, When an 11th is sent, Then 429 with `Retry-After`.
Not applicable: 401, 403, 404 (anonymous endpoint, enumeration-safe).

### US-002 - Confirm email (EP-02, Slice S1, Must)
**As a** newly registered user **I want** to confirm my email with the link I received **so that** I can log in.
Traces: FR-004, FR-128, NFR-09
Route: POST /api/v1/auth/confirm-email  ·  Rate limit: auth-strict  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an unconfirmed user and a valid token less than 24 h old, When I post userId and token, Then 204 and `EmailConfirmed = true`. Audit row Update (EmailConfirmed false → true), actor `anonymous`.
- AC2 (validation) - Given a missing userId or token, When I post, Then 400 with `errors` for `userId` / `token`.
- AC3 (already confirmed) - Given the user is already confirmed, When I post the same token again, Then 204. No change and no new audit row.
- AC4 (bad token, generic) - Given a wrong, tampered or expired (> 24 h, via FakeTimeProvider) token, an unknown userId, or a deleted user, When I post, Then 400 with the same generic `detail` in all cases. No change.
- AC5 (rate limit) - 11th call per IP within 1 min → 429.
Not applicable: 401, 403 (Allow for all), 404 (unknown user returns the generic 400 so nothing leaks).

### US-003 - Resend confirmation link (EP-03, Slice S1, Must)
**As a** user who lost or did not get the link **I want** a new confirmation link **so that** I can still confirm my email.
Traces: FR-005, FR-020, NFR-09
Route: POST /api/v1/auth/resend-confirmation  ·  Rate limit: auth-strict  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an existing, unconfirmed, non-deleted account, When I post its email, Then 202 with a fixed body, and the fake sender logs a new confirmation link (24 h).
- AC2 (validation) - Given a missing or malformed email, When I post, Then 400 with `errors.email`.
- AC3 (enumeration-safe) - Given an unknown, already confirmed, or deleted account, When I post, Then 202 with a body identical to AC1, and nothing is logged by the fake sender.
- AC4 (rate limit) - 11th call per IP within 1 min → 429.
Notes: No database write, so no audit row. Not applicable: 401, 403, 404.

### US-004 - Log in (EP-04, Slice S1, Must)
**As a** confirmed user **I want** to log in with email and password **so that** I get tokens to call the API.
Traces: FR-006, FR-007, FR-008, FR-009, FR-012, FR-015, NFR-05, NFR-06, NFR-09, NFR-12
Route: POST /api/v1/auth/login  ·  Rate limit: auth-strict  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path, Customer) - Given a confirmed, unlocked, non-deleted Customer, When I post the correct email and password, Then 200 with `accessToken`, `accessTokenExpiresAt` (now + 15 min), `refreshToken` and `refreshTokenExpiresAt` (now + 7 days). The JWT holds `sub`, `role` = Customer, `customer_id`, `jti`, `iat`, `exp`, `iss` and `aud`. The refresh token is stored only as a SHA-256 hash with a new family ID. The failed-login count resets to 0. No audit row (token issuance is logged, not audited).
- AC2 (happy path, Admin) - Given a confirmed Admin, When they log in, Then 200, the JWT has `role` = Admin and no `customer_id` claim.
- AC3 (validation) - Given a missing email or password, When I post, Then 400 with `errors` for those fields. The password policy is not checked at login.
- AC4 (generic 401) - Given an unknown email, a wrong password, a locked account, a deleted account, or an account with no password yet, When I post, Then 401 with identical `type`, `title` and `detail` in every case. The login-failure metric goes up by 1. The log line has a masked email.
- AC5 (403 email not confirmed) - Given an unconfirmed user and the correct password, When I post, Then 403 with problem type `email-not-confirmed`, no tokens, and no change to the failed count. With a wrong password the same user gets the generic 401 from AC4.
- AC6 (lockout) - Given 5 failed logins in a row, When I try a 6th time with the correct password, Then 401 (generic) and lockout end = now + 15 min. When the clock moves 15 min on (FakeTimeProvider), Then the correct password returns 200. A success after fewer than 5 failures resets the count to 0.
- AC7 (lockout keeps sessions, S-D2) - Given the user has active refresh tokens, When the 5th failure locks the account, Then the refresh tokens stay active (a refresh still returns 200), an audit row is written for the lockout itself, and security event `AccountLockedOut` is logged. Only an Admin lock (US-014) revokes sessions.
- AC10 (password checked first, S-D6) - Given an unconfirmed user, When I post a wrong password, Then the generic 401 (not 403). Given an unknown email, When I post, Then the response time matches a known email with a wrong password (dummy hash; median difference < 50 ms over 30 calls).
- AC8 (rate limit) - 11th call per IP within 1 min → 429. This counts separately from the per-account lockout.
- AC9 (headers) - Every response has `Cache-Control: no-store`, and no token appears in logs.
Not applicable: 404 (always the generic 401).

### US-005 - Refresh tokens with rotation and reuse detection (EP-05, Slice S1, Must)
**As a** signed-in client **I want** to swap my refresh token for a new token pair **so that** I stay signed in without sending my password again, and a stolen token is detected.
Traces: FR-010, FR-011, FR-012, NFR-05, NFR-09, NFR-12
Route: POST /api/v1/auth/refresh  ·  Rate limit: refresh  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path, rotation) - Given a valid, unused, unexpired refresh token, When I post it, Then 200 with a new access token and a new refresh token in the same family. The old token gets `UsedAt` set. The new token expires at min(now + 7 days, family start + 30 days). The access token shows the user's current roles. No audit row; a log entry without the token value.
- AC2 (validation) - Given an empty or missing `refreshToken`, When I post, Then 400 with `errors.refreshToken`.
- AC3 (reuse detection) - Given a token that was already used or revoked, When it is posted, Then 401, and every token in that family is revoked with reason `reuse-detected`. The newest legitimate token of that family then also returns 401. A warning log line (no token value) is written and the reuse metric goes up by 1.
- AC4 (expired or unknown) - Given an expired token, an unknown token, or a family older than 30 days, When I post, Then a generic 401 and no change to other tokens.
- AC5 (user state) - Given a token whose user is now locked, deleted or unconfirmed, When I post, Then 401 and no new tokens are issued.
- AC6 (race) - Given two parallel requests with the same unused token, When both are processed, Then exactly one returns 200. The other is treated as reuse (401, family revoked).
- AC7 (rate limit) - 31st call per IP within 1 min → 429.
Notes: The token is the credential; no access token is needed (matrix: Allow). Not applicable: 403, 404.

### US-006 - Log out one session (EP-06, Slice S1, Must)
**As a** signed-in user **I want** to revoke my current refresh token **so that** this device can no longer renew my session.
Traces: FR-013, FR-128, NFR-04
Route: POST /api/v1/auth/logout  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given a valid access token and one of my own active refresh tokens, When I post it, Then 204. The token is revoked with reason `logout` (logged, not audited). A later refresh with it returns 401.
- AC2 (validation) - Given a missing `refreshToken`, When I post, Then 400 with `errors.refreshToken`.
- AC3 (unauthenticated) - Given no access token, or an expired or invalid one, When I post, Then 401.
- AC4 (idempotent / not mine) - Given a token that is already revoked, unknown, or owned by another user, When I post it, Then 204 with no change. Another user's token stays active, and no audit row is written.
Notes: The access token stays valid until it expires (risk R1). Not applicable: 403 (Self for both roles), 404.

### US-007 - Log out all sessions (EP-07, Slice S1, Must)
**As a** signed-in user **I want** to end all my sessions **so that** a lost device or leaked token cannot renew access.
Traces: FR-014, FR-015, FR-128
Route: POST /api/v1/auth/logout-all  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given I have 3 active refresh tokens across 2 families, When I post, Then 204. All 3 are revoked with reason `logout-all` (logged, not audited). A refresh with any of them returns 401. Other users' tokens are unchanged.
- AC2 (nothing to revoke) - Given I have no active refresh tokens, When I post, Then 204.
- AC3 (unauthenticated) - Given no valid access token, When I post, Then 401.
Not applicable: 400 (no body), 403, 404.

### US-008 - Get current user (EP-08, Slice S1, Must)
**As a** signed-in user **I want** to see who I am signed in as **so that** a client can show my identity and roles.
Traces: FR-016, FR-009, NFR-05, NFR-06
Route: GET /api/v1/auth/me  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (Customer) - Given a valid Customer access token, When I call it, Then 200 with `userId`, `email`, `emailConfirmed`, `roles` = ["Customer"] and `customerId` (same as the token's `customer_id`).
- AC2 (Admin) - Given a valid Admin access token, When I call it, Then 200 with `roles` = ["Admin"] and `customerId` = null.
- AC3 (unauthenticated) - Given no token, an expired token (past `exp` + 30 s skew), a tampered signature, `alg: none` or a non-allow-listed algorithm, or a wrong `iss` or `aud`, When I call it, Then 401.
- AC4 (headers) - The response has `Cache-Control: no-store`.
Not applicable: 403 (Self for both roles), 404.

### US-009 - Change own password (EP-09, Slice S1, Must)
**As a** signed-in user **I want** to change my password **so that** I can keep my account secure.
Traces: FR-017, FR-003, FR-015, FR-128, NFR-10
Route: POST /api/v1/auth/change-password  ·  Rate limit: auth-strict (S-D2)  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given a valid access token, When I post the correct `currentPassword` and a valid `newPassword`, Then 204. The password and security stamp change. All my refresh tokens are revoked with reason `password-changed`. There is an audit row Update with no hash or stamp values. Login with the new password returns 200; the old one returns 401.
- AC2 (validation) - Given a missing `currentPassword`, or a `newPassword` shorter than 15 or longer than 128 characters, When I post, Then 400 with `errors` for those fields.
- AC3 (wrong current password) - Given a wrong `currentPassword`, When I post, Then 400 with `errors.currentPassword`. The password does not change and no tokens are revoked, but the failed-login count goes up by 1 (S-D2); the 5th wrong attempt in a row locks the account as in US-004 AC6.
- AC4 (unauthenticated) - Given no valid access token, When I post, Then 401.
- AC5 (privacy) - No password value appears in logs or in the audit log.
- AC6 (rate limit) - The 11th call per IP within 1 min (shared `auth-strict` bucket) → 429.
- AC7 (blocklist) - Given a `newPassword` on the common-password blocklist, When I post, Then 400 with `errors.newPassword` (FR-003).
Not applicable: 403, 404.

### US-010 - Request a password reset (EP-10, Slice S2, Must)
**As a** user who forgot my password, or an Admin-created user with no password **I want** to request a reset link **so that** I can set a new password.
Traces: FR-018, FR-020, NFR-09
Route: POST /api/v1/auth/forgot-password  ·  Rate limit: auth-strict  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an existing, non-deleted account (confirmed or not, locked or not, with or without a password), When I post its email, Then 202 with a fixed body. The fake sender logs a reset link (base URL from config, token valid 1 h, S-D3).
- AC2 (validation) - Given a missing or malformed email, When I post, Then 400 with `errors.email`.
- AC3 (enumeration-safe) - Given an unknown or deleted account, When I post, Then 202 with a body identical to AC1, and nothing is logged by the fake sender.
- AC4 (rate limit) - 11th call per IP within 1 min → 429.
Notes: No database write, so no audit row. Not applicable: 401, 403, 404.

### US-011 - Reset password with a token (EP-11, Slice S2, Must)
**As a** user with a reset link **I want** to set a new password **so that** I can sign in again.
Traces: FR-019, FR-003, FR-015, FR-066, FR-128, NFR-09
Route: POST /api/v1/auth/reset-password  ·  Rate limit: auth-strict  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given a valid reset token less than 1 h old, When I post email, token and a valid `newPassword`, Then 204. The password is set, `EmailConfirmed = true`, the failed-login count = 0, and an automatic lockout from failed logins (FR-008) is cleared. All refresh tokens are revoked with reason `password-reset`. Audit row Update (no hash or stamp). Login with the new password returns 200.
- AC2 (Admin-created or unconfirmed user) - Given an Admin-created user with no password and an unconfirmed email, When they reset, Then they can log in straight away (no separate confirm step).
- AC3 (Stretch, only with US-112) (restored customer) - Given a customer restored by an Admin (anonymized, locked) with a new email set, When they reset with the link sent to that email, Then the lockout is cleared and the email confirmed (FR-066).
- AC3b (Admin lock stays) - Given a user locked by an Admin (EP-14), When they reset their password, Then 204 and the password changes, but the Admin lock stays in place and login still returns 401 (generic). Only an Admin unlock (EP-15) removes it. The lock type (failed-logins, restored-customer, admin) is stored so the rule can be applied.
- AC4 (validation) - Given a missing email or token, or a `newPassword` outside 15-128 characters, When I post, Then 400 with `errors` for those fields.
- AC5 (bad token, generic) - Given a wrong, expired (> 1 h, via FakeTimeProvider) or already-used token, an unknown email, or a deleted account, When I post, Then 400 with the same generic `detail`. Nothing changes.
- AC6 (rate limit) - 11th call per IP within 1 min → 429.
Not applicable: 401, 403, 404.

### US-012 - List users (EP-12, Slice S2, Must)
**As an** Admin **I want** to list and filter user accounts **so that** I can find accounts to support or secure.
Traces: FR-030, FR-120, FR-121, FR-122, FR-123, FR-127, NFR-04
Route: GET /api/v1/admin/users  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin, When I call it without parameters, Then 200 with `{ items, page: 1, pageSize: 20, totalCount, totalPages }`. Deleted users are excluded. Each item has id, email, emailConfirmed, roles, isLocked, customerId, createdAt and isDeleted.
- AC2 (filters) - Given seeded users, When I filter by `email` (case-insensitive contains), `role` (Admin or Customer), `isLocked`, `emailConfirmed` or `includeDeleted=true`, Then only matching users return, and deleted users appear only with `includeDeleted=true`.
- AC3 (sorting) - Given `sort=-createdAt,email`, When I call it, Then items are ordered by that sort, with ID as the last tiebreaker.
- AC4 (validation) - Given `page=0`, `pageSize=101`, an unknown sort field, more than 3 sort fields, `role=Foo`, or an `email` filter shorter than 2 or longer than 100 characters, When I call it, Then 400 with `errors` for each parameter.
- AC5 (edge) - Given a page past the end, When I call it, Then 200 with empty `items` and the correct `totalCount`.
- AC6 (unauthenticated) - No valid token → 401.
- AC7 (forbidden) - Given a Customer, When they call it, Then 403.
Not applicable: 404 (collection).

### US-013 - Get a user (EP-13, Slice S2, Must)
**As an** Admin **I want** to see one user's account details **so that** I can check their status before acting.
Traces: FR-031, NFR-04
Route: GET /api/v1/admin/users/{userId}  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and an existing user, When I call it, Then 200 with id, email, emailConfirmed, roles, lockoutEnd, failedLoginCount, customerId (null for Admins), createdAt and isDeleted.
- AC2 (deleted user) - Given a soft-deleted user, When I call it, Then 200 with `isDeleted = true` and anonymized data.
- AC3 (unauthenticated) - No valid token → 401.
- AC4 (forbidden) - Given a Customer, When they call it with any userId (existing or not), Then 403.
- AC5 (not found) - Given an unknown userId, When an Admin calls it, Then 404.

### US-014 - Lock a user (EP-14, Slice S2, Must)
**As an** Admin **I want** to lock a user account **so that** a compromised or abusive account cannot sign in or refresh.
Traces: FR-032, FR-015, FR-128, NFR-04
Route: POST /api/v1/admin/users/{userId}/lock  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and an unlocked user, When I post with optional `until` and `reason`, Then 204. Lockout end = `until`, or no end if `until` is omitted. All the user's refresh tokens are revoked with reason `admin-lock`. Audit row Update with lockout end and reason, actor = the calling Admin. The user's next login and refresh return 401.
- AC2 (idempotent) - Given the user is already locked, When I lock again, Then 204. Lockout end is set to the new value, with an audit row only if a value changed.
- AC3 (validation) - Given `until` in the past or with no offset, or a `reason` over 500 characters, When I post, Then 400 with `errors` for `until` / `reason`.
- AC4 (unauthenticated) - No valid token → 401.
- AC5 (forbidden) - Given a Customer, When they post for any userId, Then 403.
- AC6 (not found) - Given an unknown userId, When an Admin posts, Then 404.
- AC7 (self) - Given an Admin, When they try to lock their own account, Then 409. Nothing changes.
- AC8 (last active Admin) - Given two Admins A and B who send lock requests for each other at the same time, When both are processed, Then at most one succeeds and the other returns 409, so at least one active Admin always remains.
Notes: Access tokens stay valid until they expire (R1).

### US-015 - Unlock a user (EP-15, Slice S2, Must)
**As an** Admin **I want** to unlock a user account **so that** a legitimate user can sign in again.
Traces: FR-033, FR-128, NFR-04
Route: POST /api/v1/admin/users/{userId}/unlock  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a locked, non-deleted user, When I post, Then 204. Lockout end = null and the failed-login count = 0. Audit row Update. The user can log in with the correct password (200).
- AC2 (idempotent) - Given the user is not locked, When I post, Then 204 with no change and no audit row.
- AC3 (deleted user) - Given a soft-deleted user, When I post, Then 409 with `detail` pointing to customer restore. Nothing changes.
- AC4 (unauthenticated) - No valid token → 401.
- AC5 (forbidden) - Customer → 403.
- AC6 (not found) - Unknown userId → 404.
Notes: Unlock does not restore revoked refresh tokens; the user must log in again.

### US-016 - Assign the Admin role (EP-16, Slice S2, Must)
**As an** Admin **I want** to give the Admin role to a staff account **so that** another staff member can manage the system.
Traces: FR-034, FR-015, FR-128, NFR-04, NFR-08
Route: PUT /api/v1/admin/users/{userId}/roles/{role}  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a user with no Customer record and no Admin role (for example a former Admin), When I call it with `role = Admin`, Then 204. The role row is added, with an audit row RoleAssigned (actor = caller). All the target's refresh tokens are revoked with reason `role-changed`. The next login token holds `role = Admin`.
- AC2 (Admin role only) - Given `role` is `Customer` or any value other than `Admin` (matched case-insensitively), When I call it, Then 400 with `errors.role`. Nothing changes.
- AC3 (Customer record) - Given the target has a Customer record (including a deleted customer), When I assign Admin, Then 409. No role row, no audit row, no revocation.
- AC4 (already has role) - Given the target is already an Admin, When I call it, Then 204, no audit row, and no token revocation.
- AC5 (unauthenticated) - No valid token → 401.
- AC6 (forbidden) - Customer → 403 for any userId and role.
- AC7 (not found) - Unknown userId → 404.

### US-017 - Remove the Admin role (EP-17, Slice S2, Must)
**As an** Admin **I want** to remove the Admin role from a staff account **so that** people who leave or change jobs lose admin access.
Traces: FR-035, FR-015, FR-128, NFR-04, NFR-08
Route: DELETE /api/v1/admin/users/{userId}/roles/{role}  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given Admins A (caller) and B, When A removes `Admin` from B, Then 204. The `AspNetUserRoles` row is removed (the only allowed DELETE), with an audit row RoleRemoved (actor = A). All B's refresh tokens are revoked with reason `role-changed`. After B logs in again, B gets 403 on admin endpoints.
- AC2 (self) - Given an Admin, When they remove their own Admin role, Then 409. Nothing changes.
- AC3 (last Admin) - Given only two Admins who remove each other's role at the same time, When both are processed, Then at most one succeeds and the other returns 409, so at least one Admin remains.
- AC4 (Admin role only) - Given `role` is any value other than `Admin`, When I call it, Then 400 with `errors.role`.
- AC5 (no-op) - Given the target does not have the Admin role, When I call it, Then 204, no audit row, and no revocation.
- AC6 (unauthenticated) - No valid token → 401.
- AC7 (forbidden) - Customer → 403.
- AC8 (not found) - Unknown userId → 404.

### US-018 - List roles (EP-18, Slice S2, Must)
**As an** Admin **I want** to see the roles and how many users have each **so that** I know how many Admins exist before I change roles.
Traces: FR-036, NFR-04
Route: GET /api/v1/admin/roles  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin, When I call it, Then 200 with `[{ name: "Admin", userCount }, { name: "Customer", userCount }]`, sorted by name. Counts exclude soft-deleted users.
- AC2 (edge) - Given a role with no users, When I call it, Then the role is listed with `userCount = 0`.
- AC3 (unauthenticated) - No valid token → 401.
- AC4 (forbidden) - Customer → 403.
Notes: Not paged (fixed, small list). Not applicable: 400, 404.

### US-019 - Create an Admin user (EP-19, Slice S2, Must)
**As an** Admin **I want** to create a new Admin account **so that** new staff can get access without a shared login.
Traces: FR-037, FR-020, FR-126, FR-128, NFR-04
Route: POST /api/v1/admin/users  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin, When I post email, firstName and lastName, Then 201 with `Location: /api/v1/admin/users/{id}` and the user body. The new user is unconfirmed, has no password, has the Admin role and no Customer record. Audit rows Insert and RoleAssigned, actor = caller. The fake sender logs a reset link (1 h; if it expires, the user uses forgot-password). Security event `AdminUserCreated`.
- AC2 (validation) - Given a missing or malformed email or missing names, When I post, Then 400 with `errors` for each field.
- AC3 (duplicate) - Given the email already belongs to a non-deleted user (case-insensitive), When I post, Then 409. No user, role row, audit row or link is created.
- AC4 (no password yet) - Given the new Admin has not reset their password, When they try to log in, Then 401 (generic). After the reset (US-011) they log in with 200 and `role = Admin`.
- AC5 (unauthenticated) - No valid token → 401.
- AC6 (forbidden) - Customer → 403.
Not applicable: 404.

### US-020 - Revoke a user's sessions (EP-20, Slice Stretch, Could)
**As an** Admin **I want** to revoke all of a user's refresh tokens **so that** a suspected token leak is contained without locking the account.
Traces: FR-038, FR-015, FR-128, NFR-04
Route: POST /api/v1/admin/users/{userId}/revoke-sessions  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a user with active refresh tokens, When I post, Then 204. All of them are revoked with reason `admin-revoked`. One audit row records the Admin action (actor = caller, target user); individual tokens are not audited. A refresh with any of them returns 401. The user can still log in.
- AC2 (nothing to revoke) - Given the user has no active tokens, When I post, Then 204.
- AC3 (self) - Given an Admin targets their own account, When they post, Then 204 and their own refresh tokens are revoked.
- AC4 (unauthenticated) - No valid token → 401.
- AC5 (forbidden) - Customer → 403.
- AC6 (not found) - Unknown userId → 404.
Notes: Access tokens stay valid until they expire (R1).

### US-021 - Send a password-reset link for a user (EP-21, Slice Stretch, Could)
**As an** Admin **I want** to trigger a reset link for a user **so that** I can help a user who cannot use forgot-password.
Traces: FR-039, FR-020, NFR-04
Route: POST /api/v1/admin/users/{userId}/password-reset-link  ·  Rate limit: general  ·  Cache: none
**Acceptance criteria**
- AC1 (happy path) - Given an Admin and a non-deleted user, When I post, Then 204 and the fake sender logs a reset link (1 h) for that user's email. The password and tokens are not changed until the reset is used.
- AC2 (deleted user) - Given a soft-deleted user, When I post, Then 409 and no link is logged.
- AC3 (unauthenticated) - No valid token → 401.
- AC4 (forbidden) - Customer → 403.
- AC5 (not found) - Unknown userId → 404.

## Definition of Ready / Done
Use PRD sections 14 and 15 as written.
