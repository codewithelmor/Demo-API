#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the .http files against the local compose stack with the pinned httpyac CLI.

.DESCRIPTION
    Runs each area file top to bottom (`httpyac send <file> --all -e local`) in a fixed order and reports
    which files failed. The destructive file (erases Customer C) runs only with -IncludeDestructive.
    Needs the API on http://localhost:8080 and the private env file from Initialize-LocalHttpEnv.ps1.
    httpyac exit codes: 0 passed, 20 a test failed, 10 a request errored.
    No -v / verbose output: responses contain tokens.

.EXAMPLE
    pwsh api-tests/scripts/Invoke-HttpTests.ps1
    pwsh api-tests/scripts/Invoke-HttpTests.ps1 -Area products, orders
    pwsh api-tests/scripts/Invoke-HttpTests.ps1 -IncludeDestructive -JUnit
#>
[CmdletBinding()]
param(
    [ValidateSet('local')]
    [string]$Environment = 'local',
    [ValidateSet('health', 'auth', 'admin-users', 'products', 'customers', 'customers-me', 'orders', 'my-orders', 'reports')]
    [string[]]$Area = @('health', 'auth', 'admin-users', 'products', 'customers', 'customers-me', 'orders', 'my-orders', 'reports'),
    [switch]$IncludeDestructive,
    [switch]$JUnit
)

$ErrorActionPreference = 'Stop'
$httpyac = 'httpyac@6.16.7'
$httpDir = (Resolve-Path (Join-Path $PSScriptRoot '../http')).Path
$reportDir = Join-Path $PSScriptRoot '../reports'

if (-not (Test-Path (Join-Path $httpDir 'http-client.private.env.json'))) {
    throw 'api-tests/http/http-client.private.env.json is missing. Run api-tests/scripts/Initialize-LocalHttpEnv.ps1 first.'
}

$files = @($Area | ForEach-Object { "$_/$_.http" })
if ($IncludeDestructive) { $files += 'destructive/customer-c-lifecycle.http' }
if ($JUnit) { New-Item -ItemType Directory -Force $reportDir | Out-Null }

$failed = @()
Push-Location $httpDir
try {
    foreach ($file in $files) {
        Write-Host "== $file"
        $arguments = @('--yes', $httpyac, 'send', $file, '--all', '-e', $Environment, '--output', 'none', '--output-failed', 'short')
        if ($JUnit) {
            $name = [IO.Path]::GetFileNameWithoutExtension($file)
            npx @arguments --junit | Set-Content (Join-Path $reportDir "http-$name.junit.xml") -Encoding utf8NoBOM
        }
        else {
            npx @arguments
        }
        if ($LASTEXITCODE -ne 0) { $failed += "$file (exit $LASTEXITCODE)" }
    }
}
finally {
    Pop-Location
}

if ($failed.Count -gt 0) {
    Write-Host "Failed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "All $($files.Count) files passed." -ForegroundColor Green
