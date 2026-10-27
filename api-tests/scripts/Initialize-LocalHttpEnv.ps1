#Requires -Version 7.0
<#
.SYNOPSIS
    Creates the test identities for the .http files and writes the git-ignored private env files.

.DESCRIPTION
    Local compose stack only (http://localhost:8080, Development). Run once after "docker compose up -d",
    and again after "docker compose down -v" or after running the destructive file (it erases Customer C).

    1. Reads the dev Admin password from the api container (/run/secrets/dev/SeedAdmin__Password).
    2. Registers four generated users (POST /auth/register):
         Customer A  default customer caller
         Customer B  "other customer" for BOLA cases
         Customer C  throwaway account for destructive/customer-c-lifecycle.http (change password, erase)
         Unconfirmed never confirmed: login 403 email-not-confirmed; lock/unlock target
    3. Confirms A, B and C with the link the fake email sender writes to the api log.
    4. Writes the values to api-tests/http/http-client.private.env.json (httpyac) and
       api-tests/http/http-client.env.json.user (Visual Studio). Both are git-ignored.

    Passwords are random and never printed. Emails use a unique per-run domain on example.com.
    Uses 7 calls from the auth-strict rate-limit bucket (10 per minute per IP).

.EXAMPLE
    pwsh api-tests/scripts/Initialize-LocalHttpEnv.ps1
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:8080',
    [string]$ComposeService = 'api'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$httpDir = Join-Path $repoRoot 'api-tests/http'

if ($BaseUrl -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?$') {
    throw "This script only runs against a local API (localhost). Got: $BaseUrl"
}

function New-TestPassword {
    # 24 random characters from a URL-safe alphabet (15-128 code points required).
    $alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789-_'
    $bytes = [byte[]]::new(24)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

function Get-ConfirmLink([string]$maskedEmail, [datetime]$sinceUtc) {
    $since = $sinceUtc.AddSeconds(-2).ToString('yyyy-MM-ddTHH:mm:ssZ')
    for ($i = 0; $i -lt 20; $i++) {
        $lines = docker compose --project-directory $repoRoot logs $ComposeService --no-color --since $since 2>$null
        $line = $lines | Where-Object { $_.Contains("[FakeEmail] Confirm email for $maskedEmail") } | Select-Object -Last 1
        if ($line -match 'confirm-email\?userId=(?<userId>[0-9a-fA-F-]{36})&token=(?<token>\S+)') {
            return @{ userId = $Matches.userId; token = [Uri]::UnescapeDataString($Matches.token) }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "No confirmation link for $maskedEmail found in the '$ComposeService' log."
}

# 1. API reachable?
try { Invoke-RestMethod "$BaseUrl/health/live" -TimeoutSec 5 | Out-Null }
catch { throw "The API is not reachable at $BaseUrl. Start it with 'docker compose up -d'." }

# 2. Admin password from the container (never printed).
$adminPassword = (docker compose --project-directory $repoRoot exec -T $ComposeService cat /run/secrets/dev/SeedAdmin__Password 2>$null | Out-String).Trim()
if (-not $adminPassword) { throw "Could not read SeedAdmin__Password from the '$ComposeService' container." }

# 3. Users. Unique domain per run, so the masked email in the log ("a***@run123456.example.com") is unique.
$suffix = Get-Random -Minimum 100000 -Maximum 999999
$domain = "run$suffix.example.com"
$users = [ordered]@{
    A           = @{ Email = "a.api-test@$domain"; First = 'Test'; Last = 'Customer A'; Confirm = $true }
    B           = @{ Email = "b.api-test@$domain"; First = 'Test'; Last = 'Customer B'; Confirm = $true }
    C           = @{ Email = "c.api-test@$domain"; First = 'Test'; Last = 'Customer C'; Confirm = $true }
    Unconfirmed = @{ Email = "u.api-test@$domain"; First = 'Test'; Last = 'Unconfirmed'; Confirm = $false }
}

foreach ($key in $users.Keys) {
    $u = $users[$key]
    $u.Password = New-TestPassword
    $started = [datetime]::UtcNow
    $body = @{ email = $u.Email; password = $u.Password; firstName = $u.First; lastName = $u.Last } | ConvertTo-Json
    Invoke-RestMethod "$BaseUrl/api/v1/auth/register" -Method Post -ContentType 'application/json' -Body $body | Out-Null
    if ($u.Confirm) {
        $masked = "$($u.Email[0])***@$domain"
        $link = Get-ConfirmLink $masked $started
        $confirm = @{ userId = $link.userId; token = $link.token } | ConvertTo-Json
        Invoke-RestMethod "$BaseUrl/api/v1/auth/confirm-email" -Method Post -ContentType 'application/json' -Body $confirm | Out-Null
    }
    Write-Host "User $key registered$(if ($u.Confirm) { ' and confirmed' }): $($u.Email)"
}

# 4. Private env files (git-ignored). Same content for httpyac and Visual Studio.
$envJson = [ordered]@{
    local = [ordered]@{
        adminPassword       = $adminPassword
        customerAEmail      = $users.A.Email
        customerAPassword   = $users.A.Password
        customerBEmail      = $users.B.Email
        customerBPassword   = $users.B.Password
        customerCEmail      = $users.C.Email
        customerCPassword   = $users.C.Password
        unconfirmedEmail    = $users.Unconfirmed.Email
        unconfirmedPassword = $users.Unconfirmed.Password
    }
} | ConvertTo-Json -Depth 3

foreach ($name in 'http-client.private.env.json', 'http-client.env.json.user') {
    $path = Join-Path $httpDir $name
    git -C $repoRoot check-ignore -q $path
    if ($LASTEXITCODE -ne 0) { throw "$name is not git-ignored. Fix .gitignore before writing secrets." }
    Set-Content -Path $path -Value $envJson -Encoding utf8NoBOM
    Write-Host "Wrote $name (git-ignored)"
}
