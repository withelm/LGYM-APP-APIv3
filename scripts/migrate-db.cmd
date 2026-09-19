@echo off
setlocal

if "%LGYM_MIGRATION_POSTGRES%"=="" (
  echo LGYM_MIGRATION_POSTGRES is required for offline Hangfire preparation.
  exit /b 1
)

dotnet run --project "LgymApi.DataSeeder" -- --prepare-hangfire
if errorlevel 1 exit /b %errorlevel%

endlocal
