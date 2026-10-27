#Requires -Version 7.0
<#
.SYNOPSIS
    Makes `dotnet run --project src/DemoApi.Api` work against the compose PostgreSQL and Valkey.

.DESCRIPTION
    Local development only. The API needs a connection string for PostgreSQL and Valkey, a JWT signing key and a seed Admin
    password. In Docker they come from the generated dev secrets; outside Docker they come from user secrets. This script:

    1. Adds PostgreSQL and Valkey host ports (127.0.0.1:5432 and 6379) to compose.override.yaml (git-ignored).
    2. Starts secrets-init, postgres, valkey and the migrator (creates the schema).
    3. Reads the generated dev secrets and stores them as user secrets of src/DemoApi.Api, with the hosts changed to localhost.

    Secret values are never printed. Run again after `docker compose down -v` (new secrets are generated).
    Stop the compose `api` container first if you want to use port 8080 yourself; `dotnet run` uses the ports in launchSettings.json.

.EXAMPLE
    pwsh deploy/scripts/Initialize-DotnetRun.ps1
    dotnet run --project src/DemoApi.Api
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repoRoot 'src/DemoApi.Api/DemoApi.Api.csproj'
$override = Join-Path $repoRoot 'compose.override.yaml'
# Original ports. If a PostgreSQL or Redis already runs on the machine, stop it or Docker cannot bind them.
$pgPort = 5432
$valkeyPort = 6379

$existing = if (Test-Path $override) { Get-Content $override -Raw } else { "services:`n" }
$added = @()
foreach ($service in @(@{ Name = 'postgres'; Port = $pgPort; Inner = 5432 }, @{ Name = 'valkey'; Port = $valkeyPort; Inner = 6379 })) {
    if ($existing -notmatch "(?m)^  $($service.Name):") {
        $existing = $existing.TrimEnd() + "`n  $($service.Name):`n    ports:`n      - `"127.0.0.1:$($service.Port):$($service.Inner)`"`n"
        $added += $service.Name
    }
}
if ($added.Count -gt 0) {
    Set-Content -Path $override -Value $existing -Encoding utf8
    Write-Host "Added host ports for $($added -join ', ') to compose.override.yaml (git-ignored)."
}

Push-Location $repoRoot
try {
    docker compose up -d postgres valkey
    if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }
    docker compose up --no-deps migrator 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The migrator failed. Run "docker compose up migrator" to see why.' }

    function Get-DevSecret([string]$path) {
        $value = (docker compose run --rm --no-deps -T --entrypoint cat secrets-init "/secrets/$path" 2>$null | Out-String).Trim()
        if (-not $value) { throw "Could not read the dev secret '$path'. Run 'docker compose up -d' once first." }
        $value
    }

    $postgres = (Get-DevSecret 'api/ConnectionStrings__Postgres') -replace 'Host=postgres;Port=5432', "Host=127.0.0.1;Port=$pgPort"
    $valkey = (Get-DevSecret 'api/ConnectionStrings__Valkey') -replace '^valkey:6379', "127.0.0.1:$valkeyPort"
    $secrets = [ordered]@{
        'ConnectionStrings:Postgres' = $postgres
        'ConnectionStrings:Valkey'   = $valkey
        'Jwt:SigningKey'             = Get-DevSecret 'api/Jwt__SigningKey'
        'SeedAdmin:Password'         = Get-DevSecret 'api/SeedAdmin__Password'
    }
    foreach ($entry in $secrets.GetEnumerator()) {
        dotnet user-secrets set $entry.Key $entry.Value --project $project | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not set user secret $($entry.Key)." }
    }
}
finally {
    Pop-Location
}

Write-Host 'User secrets are set. Start the API with: dotnet run --project src/DemoApi.Api'
Write-Host 'Dev Admin password: docker compose exec api cat /run/secrets/dev/SeedAdmin__Password'
