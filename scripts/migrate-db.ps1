if ([string]::IsNullOrWhiteSpace($env:LGYM_MIGRATION_POSTGRES)) { throw "LGYM_MIGRATION_POSTGRES is required for offline Hangfire preparation." }

dotnet run --project "LgymApi.DataSeeder" -- --prepare-hangfire
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
