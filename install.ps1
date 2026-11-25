#Requires -Version 5.1
<#
    Installs BatteryWatt for the current user.

    Usage:
        powershell -ExecutionPolicy Bypass -File .\install.ps1
        powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall

    Installs to %LOCALAPPDATA%\Programs\BatteryWatt, enables autostart
    (HKCU\...\Run, no admin needed) and starts the tray app.
#>
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [switch]$NoAutostart,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA 'Programs\BatteryWatt'
$destExe = Join-Path $dest 'BatteryWatt.exe'
$startupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'BatteryWatt.lnk'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

function Remove-Shortcuts {
    if (Test-Path $startupLnk) {
        Remove-Item $startupLnk -Force -ErrorAction SilentlyContinue
    }
}

if ($Uninstall) {
    if (Test-Path $destExe) { & $destExe --autostart off 2>$null }
    Get-Process BatteryWatt -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 600

    Remove-ItemProperty -Path $runKey -Name 'BatteryWatt' -ErrorAction SilentlyContinue
    Remove-Shortcuts
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue }

    Write-Host 'BatteryWatt removed.' -ForegroundColor Yellow
    Write-Host 'Note: a running process cannot delete its own folder; if removal failed, reboot and delete it manually.' -ForegroundColor DarkGray
    return
}

& (Join-Path $root 'build.ps1')

Get-Process BatteryWatt -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $root 'dist\BatteryWatt.exe') $destExe -Force

if (-not $NoAutostart) {
    & $destExe --autostart on | Out-Null
}

Remove-Shortcuts

if (-not $NoLaunch) {
    Start-Process -FilePath $destExe
}

Write-Host ''
Write-Host 'BatteryWatt installed.' -ForegroundColor Green
Write-Host "  Exe:       $destExe"
Write-Host ("  Autostart: {0}" -f $(if ($NoAutostart) { 'skipped' } else { 'enabled (HKCU Run)' }))
Write-Host '  Tray icon may start hidden under the "^" arrow - drag it onto the taskbar to pin it.'
