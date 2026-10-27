# Saves an Azure DevOps PAT as the PERSONAL_ACCESS_TOKEN user environment variable
# in the base64 "pat:<token>" form that @azure-devops/mcp expects with "-a pat".
$secure = Read-Host "Paste your NEW Azure DevOps PAT (input is hidden), then press Enter" -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $secure).Password.Trim()

if ($plain.Length -lt 20) {
    Write-Host "That doesn't look like a token (too short). Nothing was saved." -ForegroundColor Red
    exit 1
}

$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("pat:$plain"))
[Environment]::SetEnvironmentVariable('PERSONAL_ACCESS_TOKEN', $b64, 'User')
$plain = $null

if ([Environment]::GetEnvironmentVariable('PERSONAL_ACCESS_TOKEN', 'User')) {
    Write-Host "Saved. Now close every Claude Code / terminal / VS Code window and start Claude Code again." -ForegroundColor Green
} else {
    Write-Host "Saving failed. Check whether a policy blocks user environment variables." -ForegroundColor Red
    exit 1
}
