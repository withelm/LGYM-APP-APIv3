param(
    [string]$OutputPath = "",
    [string]$FromMigration = "",
    [string]$ToMigration = ""
)

$arguments = @("ef", "migrations", "script", "--project", "LgymApi.Infrastructure", "--startup-project", "LgymApi.Api")

if ($FromMigration -ne "") {
    $arguments += "--from"
    $arguments += $FromMigration
}

if ($ToMigration -ne "") {
    $arguments += "--to"
    $arguments += $ToMigration
}

if ($OutputPath -ne "") {
    $arguments += "--output"
    $arguments += $OutputPath
}

dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
