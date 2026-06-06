# Manual verification harness for the Lenovo Power Manager bridge.
#
#   Run from an ELEVATED PowerShell (Windows PowerShell 5.1 or pwsh 7), in native\:
#       .\test-read.ps1
#
# Loads LenPower.dll and reads the primary battery charge threshold.
# rc=0 means the RPC call to the Lenovo Power Manager succeeded.

$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot 'LenPower.dll'
if (-not (Test-Path $dll)) {
    Write-Host "LenPower.dll not found - run build.cmd first." -ForegroundColor Red
    return
}

# Single-quoted here-string: no PowerShell interpolation, ASCII only -> parses the
# same in Windows PowerShell 5.1 and pwsh 7. The DLL is loaded by full path via
# LoadLibrary, so the [DllImport] only needs the base name.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LenPowerTest {
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibrary(string path);

    [DllImport("LenPower.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int LenGetChargeThreshold(int battery, out int capable, out int enabled, out int start, out int stop);
}
'@

if ([LenPowerTest]::LoadLibrary($dll) -eq [IntPtr]::Zero) {
    Write-Host "Failed to load LenPower.dll (LastError=$([ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error()).Message))" -ForegroundColor Red
    return
}

$cap = 0; $en = 0; $st = 0; $sp = 0
$rc = [LenPowerTest]::LenGetChargeThreshold(1, [ref]$cap, [ref]$en, [ref]$st, [ref]$sp)

Write-Host ""
Write-Host "LenGetChargeThreshold(battery=1) ->" -ForegroundColor Cyan
Write-Host ("  rc       = {0}  {1}" -f $rc, $(if ($rc -eq 0) { '(success)' } else { '(RPC error / driver missing / not elevated)' }))
Write-Host "  capable  = $cap"
Write-Host "  enabled  = $en"
Write-Host "  start    = $st %"
Write-Host "  stop     = $sp %"
Write-Host ""
if ($rc -eq 0 -and $en -ne 0) { Write-Host "Threshold ACTIVE: charges $st% to $sp%." -ForegroundColor Green }
elseif ($rc -eq 0)           { Write-Host "Threshold OFF: charges to 100%." -ForegroundColor Yellow }
