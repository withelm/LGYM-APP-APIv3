@echo off
setlocal

call "%~dp0migrate-db.cmd" "%~1"
if errorlevel 1 exit /b %errorlevel%

call "%~dp0run-migrator.cmd" "%~2" "%~3" "%~1"
if errorlevel 1 exit /b %errorlevel%

endlocal
