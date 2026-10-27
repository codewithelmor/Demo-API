#Requires -Version 7.0
<#
.SYNOPSIS
    Compares an OpenAPI document with the .http files and Postman collections that test it (drift report). Read-only.

.DESCRIPTION
    Reads:
      - the OpenAPI document (JSON): operations (method + path), response status codes, security, request body schemas;
      - .http / .rest files: request lines, "# @expect <status>" comments, JSON bodies;
      - Postman v3 collections (*.request.yaml) and v2.1 exports (*.postman_collection.json): method, URL,
        statuses checked in scripts (pm.response.to.have.status(201), pm.expect(pm.response.code).to.eql(201)).
    Reports, per operation: which test files call it, which documented status codes are tested, missing 401/403
    tests for protected operations, and body properties that are not in the request schema.
    Also lists requests whose route is not in the OpenAPI document (wrong route or removed endpoint).
    Writes a Markdown report when -ReportPath is given; never changes test files.

.PARAMETER OpenApi
    Path to the OpenAPI JSON document (for example api-tests/openapi/orders-api.json).

.PARAMETER HttpRoot
    Folder with .http files. Default: api-tests/http. Missing folders are skipped.

.PARAMETER PostmanRoot
    Folder with Postman collections. Default: postman/collections. Missing folders are skipped.

.PARAMETER ExcludeCollection
    Wildcard for collection folder names to skip. Default: '* (generated)', the collections that
    "postman spec generate collection" creates and "postman spec sync collection" owns. They have no tests.

.PARAMETER ReportPath
    Optional Markdown report file (for example api-tests/reports/drift-report.md).

.PARAMETER FailOnUntested
    Exit 1 when an operation has no request in any test file.

.PARAMETER FailOnUnknown
    Exit 1 when a test file calls a route that is not in the OpenAPI document.

.EXAMPLE
    pwsh Compare-ApiCoverage.ps1 -OpenApi api-tests/openapi/orders-api.json -ReportPath api-tests/reports/drift-report.md
.EXAMPLE
    pwsh Compare-ApiCoverage.ps1 -OpenApi api-tests/openapi/orders-api.json -FailOnUntested -FailOnUnknown   # CI gate
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OpenApi,
    [string]$HttpRoot = 'api-tests/http',
    [string]$PostmanRoot = 'postman/collections',
    [string]$ExcludeCollection = '* (generated)',
    [string]$ReportPath,
    [switch]$FailOnUntested,
    [switch]$FailOnUnknown
)

$ErrorActionPreference = 'Stop'
$methods = 'get', 'put', 'post', 'delete', 'patch', 'head', 'options'

function Get-Segments([string]$path) {
    $clean = ($path -split '\?')[0].Trim('/')
    if ($clean -eq '') { return @() }
    return @($clean -split '/' | ForEach-Object {
            if ($_ -match '^\{[^}]*\}$' -or $_ -match '^:\w+$' -or $_ -match '^\{\{.+\}\}$' -or
                $_ -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$' -or $_ -match '^\d+$') { '{}' }
            else { $_.ToLowerInvariant() }
        })
}

function ConvertTo-RequestPath([string]$url) {
    $u = $url.Trim().Trim("'", '"')
    $u = $u -replace '^[a-zA-Z][a-zA-Z0-9+.-]*://[^/]+', ''          # https://host:port
    $u = $u -replace '^\{\{[^}]+\}\}', ''                             # {{baseUrl}} / {{HostAddress}}
    if ($u -match '^\{\{[^}]+\}\}$' -or $u -match '^\{\{[^}]+\}\}\?') { return $null }   # whole path from a variable
    if (-not $u.StartsWith('/')) { $u = '/' + $u }
    return $u
}

function Test-PathMatch([string[]]$a, [string[]]$b) {
    if ($a.Count -ne $b.Count) { return $false }
    for ($i = 0; $i -lt $a.Count; $i++) { if ($a[$i] -ne $b[$i] -and $a[$i] -ne '{}' -and $b[$i] -ne '{}') { return $false } }
    return $true
}

function Resolve-Schema($schema, $doc) {
    $guard = 0
    while ($schema -and $schema.Contains('$ref') -and $guard++ -lt 10) {
        $name = ($schema['$ref'] -split '/')[-1]
        $schema = $doc.components.schemas[$name]
    }
    return $schema
}

function Get-JsonKeys([string]$body) {
    if ([string]::IsNullOrWhiteSpace($body)) { return $null }
    $prepared = [regex]::Replace($body, '(?<!")\{\{[^}]+\}\}(?!")', '0')   # unquoted {{var}} -> number
    try { $parsed = $prepared | ConvertFrom-Json -AsHashtable -ErrorAction Stop } catch { return $null }
    if ($parsed -is [System.Collections.IDictionary]) { return @($parsed.Keys) }
    return $null
}

# 1. OpenAPI operations -------------------------------------------------------------------------
if (-not (Test-Path $OpenApi)) { throw "OpenAPI document not found: $OpenApi" }
$doc = Get-Content $OpenApi -Raw | ConvertFrom-Json -AsHashtable -Depth 100
$globalSecurity = @($doc.security).Where({ $_ }).Count -gt 0
$operations = [System.Collections.Generic.List[object]]::new()
foreach ($path in $doc.paths.Keys) {
    foreach ($method in $methods) {
        $op = $doc.paths[$path][$method]
        if (-not $op) { continue }
        $secured = if ($op.Contains('security')) { @($op.security).Where({ $_ -and $_.Count -gt 0 }).Count -gt 0 } else { $globalSecurity }
        $bodySchema = $null
        if ($op.requestBody -and $op.requestBody.content) {
            $json = $op.requestBody.content.Keys | Where-Object { $_ -match 'json' } | Select-Object -First 1
            if ($json) { $bodySchema = Resolve-Schema $op.requestBody.content[$json].schema $doc }
        }
        $operations.Add([pscustomobject]@{
                Method = $method.ToUpperInvariant(); Path = $path; Segments = (Get-Segments $path)
                OperationId = $op.operationId; Documented = @($op.responses.Keys | Sort-Object); Secured = $secured
                BodySchema = $bodySchema; Http = [System.Collections.Generic.List[string]]::new()
                Postman = [System.Collections.Generic.List[string]]::new(); Tested = [System.Collections.Generic.HashSet[string]]::new()
                BodyNotes = [System.Collections.Generic.List[string]]::new()
            })
    }
}

# 2. Requests from test files ---------------------------------------------------------------------
$requests = [System.Collections.Generic.List[object]]::new()

if (Test-Path $HttpRoot) {
    foreach ($file in Get-ChildItem $HttpRoot -Recurse -File -Include '*.http', '*.rest') {
        $lines = Get-Content $file.FullName
        $block = $null
        $flush = {
            if ($block -and $block.Method) {
                $requests.Add([pscustomobject]@{ Source = 'http'; Where = $block.Where; Method = $block.Method; Url = $block.Url
                        Title = $block.Title; Statuses = $block.Statuses; Body = ($block.BodyLines -join "`n") })
            }
        }
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line -match '^###') {
                & $flush
                $block = @{ Title = $line.TrimStart('#').Trim(); Statuses = @(); BodyLines = [System.Collections.Generic.List[string]]::new(); Method = $null; InBody = $false; Headers = $false }
                continue
            }
            if (-not $block) { $block = @{ Title = ''; Statuses = @(); BodyLines = [System.Collections.Generic.List[string]]::new(); Method = $null; InBody = $false; Headers = $false } }
            if ($line -match '^\s*(#|//)\s*@expect\s+(.+)$') { $block.Statuses += @($Matches[2] -split '[\s,|]+' | Where-Object { $_ -match '^\d{3}$' }); continue }
            if ($line -match '^\s*(#|//)') { continue }
            if (-not $block.Method -and $line -match '^(GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)\s+(\S+)') {
                $block.Method = $Matches[1]; $block.Url = $Matches[2]; $block.Where = "$([IO.Path]::GetRelativePath((Get-Location), $file.FullName)):$($i + 1)"; $block.Headers = $true; continue
            }
            if ($block.Method -and $block.Headers -and $line.Trim() -eq '') { $block.Headers = $false; $block.InBody = $true; continue }
            if ($block.InBody) { $block.BodyLines.Add($line) }
        }
        & $flush
    }
}

function Test-Excluded([System.IO.FileInfo]$file) {
    $relative = [IO.Path]::GetRelativePath((Resolve-Path $PostmanRoot), $file.FullName)
    $collectionName = ($relative -split '[\\/]')[0]
    return ($ExcludeCollection -and $collectionName -like $ExcludeCollection)
}

if (Test-Path $PostmanRoot) {
    foreach ($file in Get-ChildItem $PostmanRoot -Recurse -File -Filter '*.request.yaml' | Where-Object { -not (Test-Excluded $_) }) {
        $text = Get-Content $file.FullName -Raw
        $method = [regex]::Match($text, '(?m)^method:\s*(\w+)').Groups[1].Value
        $url = [regex]::Match($text, "(?m)^url:\s*['""]?([^'""\r\n]+)").Groups[1].Value
        if (-not $method -or -not $url) { continue }
        $statuses = @([regex]::Matches($text, 'to\.have\.status\((\d{3})\)|response\.code\)\.to\.(?:eql|equal)\((\d{3})\)') |
            ForEach-Object { if ($_.Groups[1].Value) { $_.Groups[1].Value } else { $_.Groups[2].Value } })
        $body = [regex]::Match($text, '(?ms)^body:\s*\n\s+type:\s*json\s*\n\s+content:\s*\|-?\s*\n(.*?)(?=^\S)').Groups[1].Value
        $requests.Add([pscustomobject]@{ Source = 'postman'; Where = [IO.Path]::GetRelativePath((Get-Location), $file.FullName)
                Method = $method.ToUpperInvariant(); Url = $url; Title = ($file.BaseName -replace '\.request$', ''); Statuses = $statuses; Body = $body })
    }
    foreach ($file in Get-ChildItem $PostmanRoot -Recurse -File -Filter '*.postman_collection.json') {
        $collection = Get-Content $file.FullName -Raw | ConvertFrom-Json -AsHashtable -Depth 100
        $stack = [System.Collections.Generic.Stack[object]]::new()
        foreach ($item in @($collection.item)) { $stack.Push($item) }
        while ($stack.Count -gt 0) {
            $item = $stack.Pop()
            if ($item.item) { foreach ($child in @($item.item)) { $stack.Push($child) }; continue }
            if (-not $item.request) { continue }
            $raw = if ($item.request.url -is [string]) { $item.request.url } else { $item.request.url.raw }
            $scripts = (@($item.event) | ForEach-Object { @($_.script.exec) -join "`n" }) -join "`n"
            $statuses = @([regex]::Matches($scripts, 'to\.have\.status\((\d{3})\)') | ForEach-Object { $_.Groups[1].Value })
            $requests.Add([pscustomobject]@{ Source = 'postman'; Where = "$($file.Name): $($item.name)"; Method = "$($item.request.method)".ToUpperInvariant()
                    Url = $raw; Title = $item.name; Statuses = $statuses; Body = $item.request.body.raw })
        }
    }
}

# 3. Match requests to operations -----------------------------------------------------------------
$unknown = [System.Collections.Generic.List[object]]::new()
$dynamic = [System.Collections.Generic.List[object]]::new()
foreach ($request in $requests) {
    $path = ConvertTo-RequestPath $request.Url
    if (-not $path) { $dynamic.Add($request); continue }
    $segments = Get-Segments $path
    $candidates = @($operations | Where-Object { $_.Method -eq $request.Method -and (Test-PathMatch $_.Segments $segments) })
    # Prefer the operation with the most literal segment matches (/orders/search over /orders/{id}).
    $match = $candidates | Sort-Object { @(for ($i = 0; $i -lt $segments.Count; $i++) { if ($_.Segments[$i] -eq $segments[$i]) { 1 } }).Count } -Descending | Select-Object -First 1
    if (-not $match) { $unknown.Add($request); continue }
    $label = "$($request.Title) ($($request.Where))"
    if ($request.Source -eq 'http') { $match.Http.Add($label) } else { $match.Postman.Add($label) }
    foreach ($s in $request.Statuses) { [void]$match.Tested.Add([string]$s) }
    if ($match.BodySchema -and $match.BodySchema.properties) {
        $keys = Get-JsonKeys $request.Body
        if ($keys) {
            $known = @($match.BodySchema.properties.Keys)
            $extra = @($keys | Where-Object { $known -notcontains $_ })
            $missing = @(@($match.BodySchema.required) | Where-Object { $_ -and $keys -notcontains $_ })
            $isNegative = @($request.Statuses | Where-Object { $_ -match '^4' }).Count -gt 0
            if (($extra.Count -or $missing.Count) -and -not $isNegative) {
                $match.BodyNotes.Add("$($request.Title): " + (@(
                            if ($extra.Count) { "not in schema: $($extra -join ', ')" }
                            if ($missing.Count) { "missing required: $($missing -join ', ')" }) -join '; '))
            }
        }
    }
}

# 4. Report ---------------------------------------------------------------------------------------
$rows = foreach ($op in $operations) {
    $missingStatuses = @($op.Documented | Where-Object { $_ -match '^\d{3}$' -and -not $op.Tested.Contains($_) })
    $authGaps = @()
    # Protected operations need a 401 test; a 403 test when the document lists 403 (a policy or role is required).
    if ($op.Secured -and -not $op.Tested.Contains('401')) { $authGaps += '401' }
    if ($op.Documented -contains '403' -and -not $op.Tested.Contains('403')) { $authGaps += '403' }
    [pscustomobject]@{
        Operation = "$($op.Method) $($op.Path)"; Http = $op.Http.Count; Postman = $op.Postman.Count
        Documented = $op.Documented -join ' '; Tested = (@($op.Tested) | Sort-Object) -join ' '
        Missing = $missingStatuses -join ' '; AuthGaps = $authGaps -join ' '
        Status = if (($op.Http.Count + $op.Postman.Count) -eq 0) { 'UNTESTED' } elseif ($missingStatuses.Count -or $authGaps.Count -or $op.BodyNotes.Count) { 'GAPS' } else { 'ok' }
        Notes = $op.BodyNotes -join ' | '
    }
}

$rows | Format-Table Operation, Http, Postman, Documented, Tested, Missing, AuthGaps, Status -AutoSize | Out-String -Width 220 | Write-Host
if ($unknown.Count) {
    Write-Host 'Requests whose route is not in the OpenAPI document (wrong route or removed endpoint):' -ForegroundColor Yellow
    $unknown | ForEach-Object { Write-Host "  $($_.Method) $($_.Url)  [$($_.Where)]" -ForegroundColor Yellow }
}
if ($dynamic.Count) { Write-Host "$($dynamic.Count) request(s) build the whole path from a variable (for example a Location header) and were not matched." }
foreach ($row in $rows | Where-Object Notes) { Write-Host "Body drift, $($row.Operation): $($row.Notes)" -ForegroundColor Yellow }

$untested = @($rows | Where-Object Status -eq 'UNTESTED').Count
$gaps = @($rows | Where-Object Status -eq 'GAPS').Count
Write-Host ("{0} operations: {1} ok, {2} with gaps, {3} untested; {4} unknown request(s)." -f $rows.Count, @($rows | Where-Object Status -eq 'ok').Count, $gaps, $untested, $unknown.Count)

if ($ReportPath) {
    $md = [System.Text.StringBuilder]::new()
    [void]$md.AppendLine('# API test drift report').AppendLine()
    [void]$md.AppendLine("OpenAPI: ``$OpenApi``. Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm') by Compare-ApiCoverage.ps1.").AppendLine()
    [void]$md.AppendLine("$($rows.Count) operations: $(@($rows | Where-Object Status -eq 'ok').Count) ok, $gaps with gaps, $untested untested; $($unknown.Count) unknown request(s).").AppendLine()
    [void]$md.AppendLine('| Operation | .http | Postman | Documented | Tested | Missing | 401/403 missing | Status |')
    [void]$md.AppendLine('|---|---|---|---|---|---|---|---|')
    foreach ($r in $rows) { [void]$md.AppendLine("| ``$($r.Operation)`` | $($r.Http) | $($r.Postman) | $($r.Documented) | $($r.Tested) | $($r.Missing) | $($r.AuthGaps) | $($r.Status) |") }
    if ($unknown.Count) {
        [void]$md.AppendLine().AppendLine('## Requests not in the OpenAPI document').AppendLine()
        foreach ($u in $unknown) { [void]$md.AppendLine("- ``$($u.Method) $($u.Url)`` in ``$($u.Where)``") }
    }
    $notes = @($rows | Where-Object Notes)
    if ($notes.Count) {
        [void]$md.AppendLine().AppendLine('## Request bodies that do not match the schema').AppendLine()
        foreach ($n in $notes) { [void]$md.AppendLine("- ``$($n.Operation)``: $($n.Notes)") }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $ReportPath -Parent) | Out-Null
    Set-Content -Path $ReportPath -Value $md.ToString() -Encoding utf8NoBOM
    Write-Host "Report: $ReportPath"
}

if (($FailOnUntested -and $untested -gt 0) -or ($FailOnUnknown -and $unknown.Count -gt 0)) { exit 1 }
exit 0
