param(
    [string]$PostgresConnection = ""
)

& "${PSScriptRoot}\migrate-db.ps1" -ConnectionString $PostgresConnection
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
