#Requires -Version 7.0
<#
.SYNOPSIS
    Adds sample Products, two Customers and Orders to the local dev stack, every order line pointing at a live product.

.DESCRIPTION
    Local development only (the script refuses a non-localhost API). Everything goes through the API, like a user would, so
    the audit rows, stock reservation and status rules all apply. No database writes.

    1. Admin (seed Admin, password read from the api container) creates 8 products with SKU SEED-001..SEED-008.
    2. Two Customers register and confirm their e-mail (the link is read from the api log, the dev stack uses a fake
       e-mail sender). They sign in with -CustomerPassword.
    3. The Customers place orders for those products; Admin moves some on to Confirmed, Shipped and Completed, and
       some are cancelled. At most 2 orders per Customer stay Pending (the API allows 5).

    Safe to run twice: it stops when a product with SKU SEED-001 already exists. Products are never deleted by this
    script, so order lines never show "Product no longer available". Run `docker compose down -v` to start clean.

.EXAMPLE
    docker compose up -d
    pwsh deploy/scripts/Seed-DevData.ps1
    # then sign in as seed.alice@alice.example.test with the -CustomerPassword value
#>
[CmdletBinding()]
param(
    [string] $ApiUrl = 'http://localhost:8080',
    [string] $AdminEmail = 'admin@demoapi.local',
    # Dev-only sample password (15+ characters, the API rule). Not a secret: it only exists on this machine's dev stack.
    [string] $CustomerPassword = 'Seed customer passphrase 1'
)

$ErrorActionPreference = 'Stop'
$api = $ApiUrl.TrimEnd('/')
if (([Uri] $api).Host -notin @('localhost', '127.0.0.1')) {
    throw "Refusing to seed $api. This script is for the local dev stack only."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$baseHeaders = @{ 'X-CSRF' = '1' }

function Invoke-Api([string] $Method, [string] $Path, $Body = $null, [string] $Token = $null) {
    $headers = $baseHeaders.Clone()
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = "$api$Path"; Headers = $headers; SkipHttpErrorCheck = $true; StatusCodeVariable = 'status' }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 5); $params.ContentType = 'application/json' }
    $result = Invoke-RestMethod @params
    # Status only in the error: a body could echo credentials.
    if ($status -ge 400) { throw "$Method $Path returned $status" }
    return $result
}

function Get-Token([string] $Email, [string] $Password) {
    (Invoke-Api POST '/api/v1/auth/login' @{ email = $Email; password = $Password }).accessToken
}

function New-ConfirmedCustomer([string] $Name, [string] $Password) {
    $email = "seed.$Name@$Name.example.test"
    $startedAt = (Get-Date).ToUniversalTime().ToString('o')
    Invoke-Api POST '/api/v1/auth/register' @{ firstName = (Get-Culture).TextInfo.ToTitleCase($Name); lastName = 'Sample'; email = $email; password = $Password } | Out-Null

    # The masked address in the log line ("s***@<domain>") is unique per account, so a link is never taken from another account.
    $mask = "s***@$Name.example.test"
    $link = $null
    for ($attempt = 0; $attempt -lt 40 -and -not $link; $attempt++) {
        $log = docker compose -f (Join-Path $repoRoot 'compose.yaml') logs api --since $startedAt --no-log-prefix 2>&1 | Out-String
        $line = ($log -split "`n") | Where-Object { $_ -like '*Confirm email for*' -and $_ -like "*$mask*" } | Select-Object -Last 1
        if ($line -match 'https?://\S+') { $link = $Matches[0] } else { Start-Sleep -Milliseconds 500 }
    }
    if (-not $link) { throw 'No confirmation link in the API log. Is the stack running with the fake e-mail sender?' }

    $query = [System.Web.HttpUtility]::ParseQueryString(([Uri] $link).Query)
    Invoke-Api POST '/api/v1/auth/confirm-email' @{ userId = $query['userId']; token = $query['token'] } | Out-Null
    return $email
}

Add-Type -AssemblyName System.Web
Push-Location $repoRoot
try {
    $adminPassword = (docker compose exec -T api cat /run/secrets/dev/SeedAdmin__Password 2>$null | Out-String).Trim()
    if (-not $adminPassword) { throw 'Could not read the Admin password. Is the api container running (docker compose up -d)?' }
    $admin = Get-Token $AdminEmail $adminPassword

    $existing = Invoke-Api GET '/api/v1/products?search=SEED-001&pageSize=50' -Token $admin
    if (@($existing.items | Where-Object { $_.sku -eq 'SEED-001' }).Count -gt 0) {
        Write-Host 'Seed data already exists (SEED-001 found). Nothing to do. Use "docker compose down -v" to start clean.'
        return
    }

    $catalog = @(
        @('Mechanical keyboard', 79.90), @('Wireless mouse', 24.50), @('USB-C dock', 119.00), @('27 inch monitor', 229.99),
        @('Laptop stand', 34.00), @('Webcam HD', 49.95), @('Desk lamp', 29.90), @('Noise-cancelling headset', 149.00)
    )
    $products = @()
    for ($i = 0; $i -lt $catalog.Count; $i++) {
        $products += Invoke-Api POST '/api/v1/products' @{
            name = $catalog[$i][0]; sku = ('SEED-{0:D3}' -f ($i + 1)); description = 'Sample product for local development.'
            price = $catalog[$i][1]; stockQuantity = 200
        } -Token $admin
    }
    Write-Host "Created $($products.Count) products."

    $alice = New-ConfirmedCustomer 'alice' $CustomerPassword
    $bob = New-ConfirmedCustomer 'bob' $CustomerPassword
    $tokens = @{ $alice = (Get-Token $alice $CustomerPassword); $bob = (Get-Token $bob $CustomerPassword) }
    Write-Host "Created customers $alice and $bob."

    function New-Order([string] $Email, [int[]] $Picks, [int] $Quantity, [string] $Final) {
        $items = @($Picks | ForEach-Object { @{ productId = $products[$_].id; quantity = $Quantity } })
        $order = Invoke-Api POST '/api/v1/customers/me/orders' @{ items = $items } -Token $tokens[$Email]
        foreach ($step in @('Confirmed', 'Shipped', 'Completed')) {
            if ($Final -eq 'Pending' -or $Final -eq 'Cancelled') { break }
            Invoke-Api POST "/api/v1/orders/$($order.id)/status" @{ status = $step } -Token $admin | Out-Null
            if ($step -eq $Final) { break }
        }
        if ($Final -eq 'Cancelled') { Invoke-Api POST "/api/v1/customers/me/orders/$($order.id)/cancel" -Token $tokens[$Email] | Out-Null }
    }

    New-Order $alice @(0, 1) 1 'Completed'
    New-Order $alice @(3) 1 'Shipped'
    New-Order $alice @(2, 4) 2 'Confirmed'
    New-Order $alice @(6) 3 'Pending'
    New-Order $alice @(5) 1 'Cancelled'
    New-Order $bob @(7, 1) 1 'Completed'
    New-Order $bob @(0) 2 'Shipped'
    New-Order $bob @(3, 6) 1 'Pending'
    New-Order $bob @(4) 1 'Cancelled'
    Write-Host 'Created 9 orders in mixed statuses.'
    Write-Host "Sign in as $alice or $bob with the -CustomerPassword value."
}
finally {
    Pop-Location
}
