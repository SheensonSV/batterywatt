#Requires -Version 5.1
<#
    Builds BatteryWatt with the C# compiler that ships with Windows
    (.NET Framework 4.x). No SDK, no NuGet, no admin rights.

    Output: dist\BatteryWatt.exe
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\BatteryWatt.cs'
$dist = Join-Path $root 'dist'
$exe = Join-Path $dist 'BatteryWatt.exe'

if (-not (Test-Path $source)) { throw "Source not found: $source" }

$candidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "csc.exe not found. BatteryWatt targets .NET Framework 4.x, which is a Windows component."
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $csc /nologo /target:winexe /optimize+ /codepage:65001 `
    "/out:$exe" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll /r:System.Management.dll `
    $source

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) {
    throw "Build failed with exit code $LASTEXITCODE"
}

$size = (Get-Item $exe).Length
Write-Host ("Built {0} ({1:N0} bytes)" -f $exe, $size) -ForegroundColor Green
