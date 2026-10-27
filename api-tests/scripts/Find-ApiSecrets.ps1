#Requires -Version 7.0
<#
.SYNOPSIS
    Scans API test files (.http, environment files, Postman collections and environments, test data, reports)
    for secrets, and optionally the Git history of those files. Read-only; prints masked values only.

.DESCRIPTION
    Finds:
      - tokens by shape: JWTs, Postman API keys (PMAK-), GitHub tokens, AWS access keys, Slack tokens, Google API keys,
        Azure storage account keys and SAS signatures, private key blocks;
      - literal Authorization (Bearer/Basic) and API key headers (X-Api-Key, Api-Key, Ocp-Apim-Subscription-Key, ...);
      - secret-named variables with a literal value (password, secret, client_secret, api_key, token, ...) in
        http-client.env.json, Postman v3 YAML ("- key: token" + "value: ..."), and Postman v2.1 JSON;
      - Postman variables of type "secret" that have a value;
      - connection strings with a literal Password=.
    Placeholders are fine: {{variable}}, {{$processEnv NAME}}, {{vault:name}}, <placeholder>, ${NAME}, empty values.
    Values are masked in the output (first 4 characters and the length). Exit code 1 when anything is found.
    For a full-repository scan (all file types) also run gitleaks (MIT): gitleaks git --redact.

.PARAMETER Path
    Folder to scan. Default: the current folder.

.PARAMETER IncludeHistory
    Also scan lines added to matching files in every commit (git log -p). Run it from the repository root.

.PARAMETER ReportPath
    Optional Markdown report file.

.EXAMPLE
    pwsh Find-ApiSecrets.ps1
.EXAMPLE
    pwsh Find-ApiSecrets.ps1 -IncludeHistory -ReportPath api-tests/reports/secrets-report.md
#>
[CmdletBinding()]
param(
    [string]$Path = '.',
    [switch]$IncludeHistory,
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

# Keep these patterns in sync with hooks/user/guard-api-secrets.ps1.
$shapePatterns = [ordered]@{
    'JSON Web Token'              = 'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'
    'Postman API key'             = 'PMAK-[A-Za-z0-9]{20,}-[A-Za-z0-9]{20,}'
    'GitHub token'                = '\b(gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{40,})'
    'AWS access key'              = '\b(AKIA|ASIA)[0-9A-Z]{16}\b'
    'Slack token'                 = '\bxox[abprs]-[A-Za-z0-9-]{10,}'
    'Google API key'              = '\bAIza[0-9A-Za-z_-]{35}\b'
    'Azure storage account key'   = 'AccountKey=[A-Za-z0-9+/]{40,}={0,2}'
    'SAS signature'               = '[?&]sig=[A-Za-z0-9%+/=]{20,}'
    'Private key'                 = '-----BEGIN (RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'
}
$headerPatterns = [ordered]@{
    'Authorization header with a literal value' = '(?im)^\s*Authorization\s*:\s*(Bearer|Basic|ApiKey|Token)\s+(?<v>[^\s{][^\s]{7,})'
    'API key header with a literal value'       = '(?im)^\s*(X-Api-Key|Api-Key|X-Functions-Key|Ocp-Apim-Subscription-Key|X-Auth-Token)\s*:\s*(?<v>[^\s{][^\s]{7,})'
}
$secretName = '(?i)^(password|passwd|pwd|secret|client_?secret|api_?key|apikey|subscription_?key|access_?token|refresh_?token|id_?token|bearer_?token|token|auth_?token|private_?key|connection_?string|.*(password|secret|token|apikey|api_key))$'

function Test-Placeholder([string]$value) {
    $v = $value.Trim().Trim('"', "'")
    if ($v.Length -lt 6) { return $true }
    return ($v -match '^(\{\{|\$\{|\$|<|%|#\{|__|\*{3}|changeme|example|placeholder|xxx)' -or $v -match '^\{\{.*\}\}$')
}

function Hide([string]$value) {
    $v = $value.Trim().Trim('"', "'")
    if ($v.Length -le 4) { return '****' }
    return $v.Substring(0, 4) + "... ($($v.Length) chars)"
}

function Find-InText([string]$text, [string]$where, [string]$fileName) {
    $found = [System.Collections.Generic.List[object]]::new()
    $lines = $text -split "`r?`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        foreach ($name in $shapePatterns.Keys) {
            $m = [regex]::Match($line, $shapePatterns[$name])
            if ($m.Success) { $found.Add([pscustomobject]@{ Where = "${where}:$($i + 1)"; Kind = $name; Value = (Hide $m.Value) }) }
        }
        foreach ($name in $headerPatterns.Keys) {
            $m = [regex]::Match($line, $headerPatterns[$name])
            if ($m.Success -and -not (Test-Placeholder $m.Groups['v'].Value)) { $found.Add([pscustomobject]@{ Where = "${where}:$($i + 1)"; Kind = $name; Value = (Hide $m.Groups['v'].Value) }) }
        }
        # "name": "value" (http-client.env.json, Postman v2.1 JSON, settings files)
        foreach ($m in [regex]::Matches($line, '"(?<k>[\w.-]+)"\s*:\s*"(?<v>[^"]*)"')) {
            if ($m.Groups['k'].Value -match $secretName -and -not (Test-Placeholder $m.Groups['v'].Value)) {
                $found.Add([pscustomobject]@{ Where = "${where}:$($i + 1)"; Kind = "secret-named value '$($m.Groups['k'].Value)'"; Value = (Hide $m.Groups['v'].Value) })
            }
        }
        # Postman v2.1 JSON: { "key": "token", "value": "..." }
        $kv = [regex]::Match($line, '"key"\s*:\s*"(?<k>[^"]+)"\s*,\s*"value"\s*:\s*"(?<v>[^"]*)"')
        if ($kv.Success -and $kv.Groups['k'].Value -match $secretName -and -not (Test-Placeholder $kv.Groups['v'].Value)) {
            $found.Add([pscustomobject]@{ Where = "${where}:$($i + 1)"; Kind = "secret-named Postman variable '$($kv.Groups['k'].Value)'"; Value = (Hide $kv.Groups['v'].Value) })
        }
        # Postman v3 YAML: "- key: token" followed (within 4 lines) by "value: ..."
        $yk = [regex]::Match($line, '^\s*-\s*key:\s*[''"]?(?<k>[^''"\s]+)')
        if ($yk.Success) {
            $isSecretName = $yk.Groups['k'].Value -match $secretName
            $isSecretType = $false; $value = $null; $valueLine = $i
            for ($j = $i + 1; $j -lt [Math]::Min($lines.Count, $i + 6); $j++) {
                if ($lines[$j] -match '^\s*-\s') { break }
                if ($lines[$j] -match '^\s*value:\s*(?<v>.*)$') { $value = $Matches['v']; $valueLine = $j }
                if ($lines[$j] -match '^\s*type:\s*secret\s*$') { $isSecretType = $true }
            }
            if ($null -ne $value -and ($isSecretName -or $isSecretType) -and -not (Test-Placeholder $value)) {
                $kind = if ($isSecretType) { "Postman secret variable '$($yk.Groups['k'].Value)' has a value" } else { "secret-named Postman variable '$($yk.Groups['k'].Value)'" }
                $found.Add([pscustomobject]@{ Where = "${where}:$($valueLine + 1)"; Kind = $kind; Value = (Hide $value) })
            }
        }
        # Connection strings
        $pw = [regex]::Match($line, '(?i)(?:^|[;"''\s])(password|pwd)\s*=\s*(?<v>[^;"''\s]+)')
        if ($pw.Success -and $line -match '(?i)(server|host|data source)\s*=' -and -not (Test-Placeholder $pw.Groups['v'].Value)) {
            $found.Add([pscustomobject]@{ Where = "${where}:$($i + 1)"; Kind = 'connection string with a password'; Value = (Hide $pw.Groups['v'].Value) })
        }
    }
    return $found
}

$include = '*.http', '*.rest', 'http-client*.json*', '*.env.json', '*.env.json.user', '*.environment.yaml', '*.request.yaml', '*.example.yaml',
'definition.yaml', '*.globals.yaml', '*.postman_collection.json', '*.postman_environment.json', '*.postman_globals.json',
'.env', '.env.*', '*.csv', 'httpyac.config.js', '.httpyac.js', '*.bru', '*.hurl', 'resources.yaml'
$skipDirs = '[\\/](node_modules|bin|obj|\.git)[\\/]'

$findings = [System.Collections.Generic.List[object]]::new()
$root = Resolve-Path $Path
$files = Get-ChildItem $root -Recurse -File -Force -Include $include -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch $skipDirs }
# JSON files in test data and report folders too
$files += Get-ChildItem $root -Recurse -File -Force -Include '*.json', '*.xml', '*.html', '*.yaml' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch $skipDirs -and $_.FullName -match '[\\/](api-tests[\\/](data|reports)|postman|\.postman)[\\/]' }
foreach ($file in $files | Sort-Object FullName -Unique) {
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName)
    foreach ($f in Find-InText (Get-Content $file.FullName -Raw) $relative $file.Name) { $findings.Add($f) }
}

if ($IncludeHistory) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git not found.' }
    $spec = @('*.http', '*.rest', '*http-client*.json*', '*.environment.yaml', '*.request.yaml', 'definition.yaml', '*.postman_*.json', '*.env', '*.env.*', '*.csv', '*/postman/*', '*/.postman/*')
    $log = & git -C $root log -p --all --no-color --unified=0 --format='commit %H' -- @spec 2>$null
    $commit = ''; $file = ''; $added = [System.Text.StringBuilder]::new()
    $flushHistory = {
        if ($added.Length -gt 0) {
            foreach ($f in Find-InText $added.ToString() "$file@$($commit.Substring(0, 8))" (Split-Path $file -Leaf)) { $f.Where = $f.Where -replace ':\d+$', ''; $findings.Add($f) }
            [void]$added.Clear()
        }
    }
    foreach ($line in $log) {
        if ($line -match '^commit ([0-9a-f]{40})') { & $flushHistory; $commit = $Matches[1]; continue }
        if ($line -match '^\+\+\+ b/(.+)$') { & $flushHistory; $file = $Matches[1]; continue }
        if ($line -match '^\+(?!\+\+)(.*)$') { [void]$added.AppendLine($Matches[1]) }
    }
    & $flushHistory
}

$unique = $findings | Sort-Object Where, Kind -Unique
if ($unique.Count -eq 0) {
    Write-Host "No secrets found in $($files.Count) API test file(s)$(if ($IncludeHistory) { ' or their Git history' })." -ForegroundColor Green
}
else {
    $unique | Format-Table Where, Kind, Value -AutoSize | Out-String -Width 220 | Write-Host
    Write-Host "$($unique.Count) possible secret(s). Rotate real secrets first, then remove them (see api-secrets-handling)." -ForegroundColor Red
}

if ($ReportPath) {
    $md = [System.Text.StringBuilder]::new()
    [void]$md.AppendLine('# API test secrets scan').AppendLine()
    [void]$md.AppendLine("Scanned ``$root`` on $(Get-Date -Format 'yyyy-MM-dd HH:mm')$(if ($IncludeHistory) { ', including Git history' }). Values are masked.").AppendLine()
    if ($unique.Count -eq 0) { [void]$md.AppendLine('No findings.') }
    else {
        [void]$md.AppendLine('| Where | Kind | Value (masked) |').AppendLine('|---|---|---|')
        foreach ($f in $unique) { [void]$md.AppendLine("| ``$($f.Where)`` | $($f.Kind) | ``$($f.Value)`` |") }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $ReportPath -Parent) | Out-Null
    Set-Content -Path $ReportPath -Value $md.ToString() -Encoding utf8NoBOM
    Write-Host "Report: $ReportPath"
}

if ($unique.Count -gt 0) { exit 1 }
exit 0
