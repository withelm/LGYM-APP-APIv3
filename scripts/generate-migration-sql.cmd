@echo off
setlocal

set "OUTPUT=%~1"
set "FROM=%~2"
set "TO=%~3"

set "FROM_ARG="
set "TO_ARG="
set "OUTPUT_ARG="

if not "%FROM%"=="" set "FROM_ARG=--from "%FROM%""
if not "%TO%"=="" set "TO_ARG=--to "%TO%""
if not "%OUTPUT%"=="" set "OUTPUT_ARG=--output "%OUTPUT%""

dotnet ef migrations script --project "LgymApi.Infrastructure" --startup-project "LgymApi.Api" %FROM_ARG% %TO_ARG% %OUTPUT_ARG%
if errorlevel 1 exit /b %errorlevel%

endlocal
