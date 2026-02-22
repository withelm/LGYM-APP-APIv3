@echo off
setlocal

call "%~dp0migrate-db.cmd" "%~1"
if errorlevel 1 exit /b %errorlevel%

endlocal
