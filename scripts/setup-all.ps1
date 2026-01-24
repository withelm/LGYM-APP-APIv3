param(
    [string]$MongoConnection = "",
    [string]$MongoDatabase = "",
    [string]$PostgresConnection = ""
)

& "${PSScriptRoot}\migrate-db.ps1" -ConnectionString $PostgresConnection
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "${PSScriptRoot}\run-migrator.ps1" -MongoConnection $MongoConnection -MongoDatabase $MongoDatabase -PostgresConnection $PostgresConnection
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
