# API tests

Tests for the Orders API from the outside, over HTTP, against the local compose stack (`http://localhost:8080`).
The contracts are `docs/product/04-architecture/endpoint-catalog.md` and `docs/product/02-requirements/permission-matrix.md`.
The integration tests in `tests/` are the gate. These files are the manual and `ApiFiles` stage layer (test strategy section 2).

| What | Where |
|---|---|
| `.http` files, one per area | `http/<area>/<area>.http`: `health`, `auth`, `admin-users`, `products`, `customers`, `customers-me`, `orders`, `my-orders`, `reports` |
| Destructive flow (erases Customer C) | `http/destructive/customer-c-lifecycle.http` |
| Environment (no secrets) | `http/http-client.env.json` (`local` only: `baseUrl`, `adminEmail`) |
| Private values (git-ignored) | `http/http-client.private.env.json` (httpyac), `http/http-client.env.json.user` (Visual Studio); `*.example` shows the keys |
| httpyac settings | `http/httpyac.config.js`: `# @expect` checks and Visual Studio chaining syntax |
| OpenAPI document | `openapi/v1.json` (how to export: `openapi/README.md`) |
| Scripts | `scripts/Initialize-LocalHttpEnv.ps1`, `Invoke-HttpTests.ps1`, `Find-ApiSecrets.ps1`, `Compare-ApiCoverage.ps1` |
| Reports (git-ignored) | `reports/` |

Stretch endpoints EP-20, 21, 28, 34, 44 and 49 are not built and have no requests.

## Run

Needs Docker, PowerShell 7 and Node.js (for `npx`). The httpyac version is pinned: `httpyac@6.16.7`.

```powershell
docker compose up -d                                         # API on http://localhost:8080
pwsh api-tests/scripts/Initialize-LocalHttpEnv.ps1           # once per database: test users + private env files
pwsh api-tests/scripts/Invoke-HttpTests.ps1                  # all areas, top to bottom
pwsh api-tests/scripts/Invoke-HttpTests.ps1 -Area products   # one area
pwsh api-tests/scripts/Invoke-HttpTests.ps1 -IncludeDestructive -JUnit   # plus the Customer C flow; JUnit XML in reports/
```

One file by hand, from `api-tests/http`:

```powershell
npx --yes httpyac@6.16.7 send products/products.http --all -e local
```

httpyac exit codes: `0` passed, `20` a `# @expect` check failed, `10` a request errored.
Do not use `-v` or `--output body`: responses contain access and refresh tokens.

- **Visual Studio 2022 17.8+ / 2026:** open a `.http` file and pick `local`. Send the requests named at the top of the
  file first (for example `loginAdmin`), then the others. Visual Studio reads `http-client.env.json.user`.
- **VS Code:** install the **httpyac** extension. It reads `http-client.env.json`, `http-client.private.env.json` and `httpyac.config.js`.

Each file logs in by itself, so files run in any order. Inside a file, requests run top to bottom and chain values
(`{{loginAdmin.response.body.$.accessToken}}`, `{{createProduct.response.headers.ETag}}`).

## Test identities and where secrets come from

No password or token is in a committed file. `Initialize-LocalHttpEnv.ps1`:

1. Reads the dev Admin password from the container: `docker compose exec api cat /run/secrets/dev/SeedAdmin__Password`.
   The Admin email is `admin@demoapi.local` (`SEED_ADMIN_EMAIL` in compose).
2. Registers four generated users on a unique `run<n>.example.com` domain, each with a random password:
   **Customer A** (default caller), **Customer B** (the "other customer" for BOLA cases),
   **Customer C** (throwaway for the destructive file) and an **unconfirmed** user (login 403, lock and unlock target).
3. Confirms A, B and C with the link that the fake email sender writes to the `api` log.
4. Writes the values to the two private env files. It refuses to write if a file is not git-ignored.

Passwords are never printed. Run the script again after `docker compose down -v`, and before every run of the destructive file.
Tokens come from `POST /auth/login` inside each file and live only in memory.

## Rate limits

`auth-strict` allows 10 calls per minute per IP, in one bucket shared by register, confirm, resend, login, change password,
forgot, reset and erase. A full run makes about 30 such calls (`auth.http` alone makes about 16), so later requests return 429.
For local runs, raise the limit in your git-ignored `compose.override.yaml`:

```yaml
services:
  api:
    environment:
      RateLimiting__AuthStrict__PermitLimit: "1000"
```

Or run one area per minute. Rate-limit behaviour itself is tested in the integration tests (ST-09), not here.

## What the files cover

Every built endpoint has a success request where one is possible, plus the key negative cases from the catalog:
401 (no or invalid token), 403 (Customer on Admin routes, Admin on `/customers/me` routes), 400 (validation,
malformed IDs, dates without an offset), 404 (unknown and soft-deleted), BOLA 404 (Customer B on Customer A's
order in `my-orders.http`), 428 (missing or `*` If-Match), 412 (stale ETag), and 409 business rules (duplicate SKU or
email, stock, status transitions, admin safeguards, open orders).

Not automated: EP-02 and EP-11 success, because they need the token from the email link in the `api` log. The setup
script covers EP-02 success.

Data: the files create their own products, customers and orders, and clean up with soft deletes and cancellations.
No order is left Pending (the cap is 5 per customer). Rows are never deleted, so test data builds up until
`docker compose down -v`.

## Checks

```powershell
pwsh api-tests/scripts/Find-ApiSecrets.ps1 -Path api-tests                                   # secrets scan
pwsh api-tests/scripts/Compare-ApiCoverage.ps1 -OpenApi api-tests/openapi/v1.json -FailOnUntested   # drift report
dotnet run ~/.claude/skills/api-http-files/scripts/Test-HttpSyntax.cs -- api-tests/http     # Visual Studio parser check
```

The drift report lists `POST /api/v1/audit-logs` as an unknown route. That is on purpose: it is the 405 case for EP-55.

## Postman

Deferred. The plan is to generate the collection from `openapi/v1.json` once the document lists the real status codes
(see `openapi/README.md`, "Known gaps"). The environment will be named `local`, the same as here, and will hold no secret values.

## Never

- Never commit tokens, passwords or private env files. Never run these files against anything but the local stack.
- Never use real customer data. All names, emails and phone numbers are generated or fictional (`+1202555xxxx`).
