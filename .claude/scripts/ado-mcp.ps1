# Starts the Azure DevOps MCP server with the PAT read from the Windows user
# environment, so it works even when the parent process started before the
# variable was set. The token never appears in any config file.
$env:PERSONAL_ACCESS_TOKEN = [Environment]::GetEnvironmentVariable('PERSONAL_ACCESS_TOKEN', 'User')
& npx -y @azure-devops/mcp codewithelmor -d core work work-items -a pat
exit $LASTEXITCODE
