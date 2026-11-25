@echo off
rem Double-click uninstaller.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Uninstall %*
echo.
pause
