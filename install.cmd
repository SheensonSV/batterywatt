@echo off
rem Double-click installer. Runs install.ps1 with the execution policy bypassed,
rem so it works even when scripts are blocked by group policy.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
echo.
pause
