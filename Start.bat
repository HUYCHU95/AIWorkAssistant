@echo off
setlocal
if exist "%~dp0AIWork.Web.exe" goto launch
if exist "%~dp0publish\AIWorkAssistant\AIWork.Web.exe" (
  call "%~dp0publish\AIWorkAssistant\Start.bat"
  exit /b
)
echo Publish not found. Run publish-win-x64.bat first.
pause
exit /b 1
:launch
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start.ps1"
if errorlevel 1 pause
