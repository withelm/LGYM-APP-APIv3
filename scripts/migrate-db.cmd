@echo off
setlocal

if not "%~1"=="" (
  set "ConnectionStrings__Postgres=%~1"
)

dotnet ef database update --project "LgymApi.Infrastructure" --startup-project "LgymApi.Api"
if errorlevel 1 exit /b %errorlevel%

endlocal
