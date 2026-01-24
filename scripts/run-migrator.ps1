param(
    [string]$MongoConnection = "",
    [string]$MongoDatabase = "",
    [string]$PostgresConnection = ""
)

if ($MongoConnection -ne "") {
    $env:Mongo__ConnectionString = $MongoConnection
}

if ($MongoDatabase -ne "") {
    $env:Mongo__Database = $MongoDatabase
}

if ($PostgresConnection -ne "") {
    $env:ConnectionStrings__Postgres = $PostgresConnection
}

dotnet run --project "LgymApi.Migrator"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
