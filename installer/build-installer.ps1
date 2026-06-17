<#
.SYNOPSIS
  One-shot build of the MangaFlow installer.

.DESCRIPTION
  1. Publishes MangaFlow.App as Release / win-x64 / self-contained.
  2. Compiles installer\MangaFlow.iss with Inno Setup (ISCC.exe).

  Run from anywhere:
    powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1

.PARAMETER SkipPublish
  Reuse the existing publish output instead of rebuilding it.

.PARAMETER Version
  Override the installer version (default 1.0.0). Also pass into the .iss define.
#>
param(
    [switch]$SkipPublish,
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

# Resolve repo root = parent of this script's folder.
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Split-Path -Parent $scriptDir
$csproj    = Join-Path $repoRoot "MangaFlow.App\MangaFlow.App.csproj"
$issFile   = Join-Path $scriptDir "MangaFlow.iss"

Write-Host "Repo root: $repoRoot" -ForegroundColor Cyan

# --- 1. Publish ---------------------------------------------------------------
if (-not $SkipPublish) {
    Write-Host "`n[1/2] Publishing Release win-x64 (self-contained)..." -ForegroundColor Green
    & dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishProfile=win-x64
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }
} else {
    Write-Host "`n[1/2] Skipping publish (reusing existing output)." -ForegroundColor Yellow
}

$publishDir = Join-Path $repoRoot "MangaFlow.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"
if (-not (Test-Path (Join-Path $publishDir "MangaFlow.App.exe"))) {
    throw "Publish output not found at $publishDir. Run without -SkipPublish first."
}

# --- 2. Compile installer -----------------------------------------------------
Write-Host "`n[2/2] Compiling installer with Inno Setup..." -ForegroundColor Green

$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php"
}

& $iscc "/DMyAppVersion=$Version" $issFile
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)." }

$output = Join-Path $scriptDir "Output\MangaFlow-Setup-$Version.exe"
Write-Host "`nDone. Installer: $output" -ForegroundColor Cyan
