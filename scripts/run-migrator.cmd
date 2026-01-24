@echo off
setlocal

if not "%~1"=="" (
  set "Mongo__ConnectionString=%~1"
)
if not "%~2"=="" (
  set "Mongo__Database=%~2"
)
if not "%~3"=="" (
  set "ConnectionStrings__Postgres=%~3"
)

dotnet run --project "LgymApi.Migrator"
if errorlevel 1 exit /b %errorlevel%

endlocal
