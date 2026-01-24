param(
    [string]$ConnectionString = ""
)

if ($ConnectionString -ne "") {
    $env:ConnectionStrings__Postgres = $ConnectionString
}

dotnet ef database update --project "LgymApi.Infrastructure" --startup-project "LgymApi.Api"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
