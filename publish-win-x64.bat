@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-win-x64.ps1" %*
exit /b %ERRORLEVEL%
