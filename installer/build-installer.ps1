<#
.SYNOPSIS
    Builds the per-user Inno Setup installer for Lenovo Power Tray.

.DESCRIPTION
    1. Builds the native Smart Charge bridge (native\build.cmd → LenPower.dll).
    2. Publishes the app self-contained (win-x64, no trimming — trimming breaks WinUI 3).
    3. Compiles installer\LenovoPowerTray.iss with Inno Setup (ISCC.exe).

    Output: installer\Output\LenovoPowerTray-Setup.exe (per-user, no admin to install).

    Requires Inno Setup (ISCC). If missing, install it once:
        winget install JRSoftware.InnoSetup

.EXAMPLE
    .\build-installer.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [string] $Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$root         = Split-Path $installerDir -Parent
$proj         = Join-Path $root "LenovoTray.csproj"
$publishDir   = Join-Path $root "publish"
$iss          = Join-Path $installerDir "LenovoPowerTray.iss"

# ── 1. Native bridge (Smart Charge) ──────────────────────────────────────────
Write-Host "==> Building native bridge (LenPower.dll)..." -ForegroundColor Cyan
& (Join-Path $root "native\build.cmd")
if ($LASTEXITCODE -ne 0) { throw "native\build.cmd failed ($LASTEXITCODE)." }

# ── 2. Publish the app (self-contained, no trim) ─────────────────────────────
Write-Host "==> Publishing app (self-contained win-x64)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $proj `
    -c Release -r win-x64 --self-contained true `
    -p:WindowsAppSDKSelfContained=true -p:PublishTrimmed=false -p:PublishReadyToRun=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

if (-not (Test-Path (Join-Path $publishDir "LenPower.dll"))) {
    Write-Warning "LenPower.dll is not in the publish output — Smart Charge will show as Unavailable."
}

# ── 3. Locate Inno Setup compiler ────────────────────────────────────────────
$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    foreach ($p in @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",     # winget per-user install
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $p) { $iscc = $p; break }
    }
}
if (-not $iscc) {
    throw "Inno Setup (ISCC.exe) not found. Install it once with:  winget install JRSoftware.InnoSetup"
}

# ── 4. Compile the installer ─────────────────────────────────────────────────
Write-Host "==> Compiling installer with $iscc ..." -ForegroundColor Cyan
& $iscc "/DAppVersion=$Version" "/DPublishDir=$publishDir" $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)." }

$setup = Join-Path $installerDir "Output\LenovoPowerTray-Setup.exe"
Write-Host ""
Write-Host "Done -> $setup" -ForegroundColor Green
if (Test-Path $setup) {
    $sha = (Get-FileHash $setup -Algorithm SHA256).Hash
    Write-Host "SHA256: $sha  (use this in the winget manifest InstallerSha256)"
}
