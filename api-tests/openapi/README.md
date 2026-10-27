# OpenAPI document

`v1.json` is the Orders API OpenAPI 3.1 document (FastEndpoints.OpenApi on Microsoft.AspNetCore.OpenApi).
It is the input for the drift report (`api-tests/scripts/Compare-ApiCoverage.ps1`), the planned OpenAPI drift test
(test strategy section 2) and the deferred Postman collection.

| File | Purpose |
|---|---|
| `v1.json` | Committed document. 51 operations (S1-S4); stretch endpoints EP-20, 21, 28, 34, 44, 49 are not built |
| `.spectral.yaml` | Lint rules (kit template), for `npx @stoplight/spectral-cli lint v1.json` |

## How it is exported

The document is served only in Development, at `GET /openapi/v1.json` (anonymous, not rate limited).
With the compose stack running:

```powershell
Invoke-WebRequest http://localhost:8080/openapi/v1.json -OutFile api-tests/openapi/v1.json
```

Then remove the `servers` entry, so the file does not depend on the host or port, and write it with 2-space indentation:

```powershell
node -e "const fs=require('fs');const p='api-tests/openapi/v1.json';const d=JSON.parse(fs.readFileSync(p));delete d.servers;fs.writeFileSync(p,JSON.stringify(d,null,2)+'\n')"
```

Review the diff in the pull request. A changed document means the API contract changed.

## How the current file was made (2026-10-27)

Docker was not used. The API was built into a scratch folder and run on `127.0.0.1:5199` in Development, with
dummy, non-secret configuration (no database, a random JWT key, seed Admin off). The document was then fetched.

At that time the API could not start. `UseRateLimiter()` is called, but `AddRateLimiter()` is never registered
(`src/BuildingBlocks/DemoApi.BuildingBlocks/RateLimiting/RateLimitingSetup.cs`). For the export only, a scratch
`IHostingStartup` added `services.AddRateLimiter()`. This does not change the document. Re-export the file the normal
way once that bug is fixed.

## Known gaps in the document (compared with the endpoint catalog and the code)

- Creates are documented as `200`, but the code returns `201` (EP-19, 24, 29, 38, 45).
- Anti-enumeration endpoints are documented as `200`, but they return `202` (EP-01, 03, 10).
- No `404`, `409`, `412`, `428` or `415` responses are documented on any operation. Login (EP-04) has no `401`/`403`;
  refresh (EP-05) has no `401`. Readiness (EP-57) has no `503`.
- `403` is listed on `Authenticated` routes, where no signed-in role is ever denied (EP-06..09, 22, 23).
