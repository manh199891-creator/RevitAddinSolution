# ============================================================
# Package-ForDeployment.ps1
# Package Antigravity DLLs for installation on another machine.
# Run on the development machine after building the solution.
# ============================================================

$ErrorActionPreference = 'Stop'
$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$buildConfig  = 'Debug'
$outputDir    = Join-Path $solutionRoot '_Deploy'
$dllsDir      = Join-Path $outputDir 'dlls'

Write-Host '============================================' -ForegroundColor Cyan
Write-Host '   ANTIGRAVITY - PACKAGING FOR DEPLOYMENT' -ForegroundColor Cyan
Write-Host '============================================' -ForegroundColor Cyan

if (Test-Path -LiteralPath $outputDir) { Remove-Item -LiteralPath $outputDir -Recurse -Force }
New-Item -ItemType Directory -Path $dllsDir -Force | Out-Null

$modules = @(
    'Core',
    'Main',
    'DrawColumns',
    'DrawBeams',
    'DrawWalls',
    'DrawFloors',
    'Autojoin',
    'ZoneSplit',
    'AutoDimWalls',
    'CadVoidPlacer',
    'CadSleevePlacer',
    'TagArranger'
)

Write-Host "`nPackaging DLL files..." -ForegroundColor Yellow
$count = 0
foreach ($mod in $modules) {
    $src = Join-Path $solutionRoot "src\Antigravity.$mod\bin\$buildConfig\Antigravity.$mod.dll"
    if (-not (Test-Path -LiteralPath $src)) {
        $src = Join-Path $solutionRoot "src\Antigravity.$mod\bin\$buildConfig\net48\Antigravity.$mod.dll"
    }
    if (Test-Path -LiteralPath $src) {
        Copy-Item -LiteralPath $src -Destination $dllsDir -Force
        Write-Host "  [OK] Antigravity.$mod.dll" -ForegroundColor Green
        $count++
    } else {
        Write-Host "  [!] Missing: Antigravity.$mod.dll - build the solution first." -ForegroundColor Red
    }
}

$doorClearanceDll = Join-Path $solutionRoot "src\Antigravity.DoorClearance\bin\$buildConfig\DoorClearanceBox.dll"
if (-not (Test-Path -LiteralPath $doorClearanceDll)) {
    $doorClearanceDll = Join-Path $solutionRoot "src\Antigravity.DoorClearance\bin\$buildConfig\net48\DoorClearanceBox.dll"
}
if (Test-Path -LiteralPath $doorClearanceDll) {
    Copy-Item -LiteralPath $doorClearanceDll -Destination $dllsDir -Force
    Write-Host '  [OK] DoorClearanceBox.dll' -ForegroundColor Green
    $count++
} else {
    Write-Host '  [!] Missing: DoorClearanceBox.dll' -ForegroundColor Red
}

$dependencies = @(
    "src\Antigravity.Core\bin\$buildConfig\Serilog.dll",
    "src\Antigravity.Core\bin\$buildConfig\Serilog.Sinks.File.dll",
    "src\Antigravity.CadVoidPlacer\bin\$buildConfig\net48\netDxf.dll"
)
foreach ($relativePath in $dependencies) {
    $dep = Join-Path $solutionRoot $relativePath
    if (Test-Path -LiteralPath $dep) {
        Copy-Item -LiteralPath $dep -Destination $dllsDir -Force
        Write-Host "  [OK] $(Split-Path $dep -Leaf)" -ForegroundColor Green
    } else {
        Write-Host "  [!] Missing dependency: $(Split-Path $relativePath -Leaf)" -ForegroundColor Red
    }
}

$universalInstaller = Join-Path $PSScriptRoot 'Deploy-ToRevit-Universal.ps1'
if (-not (Test-Path -LiteralPath $universalInstaller)) {
    throw "Universal deploy script was not found: $universalInstaller"
}
Copy-Item -LiteralPath $universalInstaller -Destination (Join-Path $outputDir 'DeployToRevit_Universal.ps1') -Force
Write-Host '  [OK] DeployToRevit_Universal.ps1' -ForegroundColor Green

@"
=== ANTIGRAVITY ADD-IN PACKAGE ===

Contents:
  dlls\                        : Add-in DLL files
  DeployToRevit_Universal.ps1 : Automatic installation script

Installation:
  1. Copy this whole directory to the destination machine.
  2. Run DeployToRevit_Universal.ps1 with PowerShell.
  3. The script detects installed Revit versions and installs the add-in.
"@ | Set-Content (Join-Path $outputDir 'README.txt') -Encoding UTF8

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host '  PACKAGE COMPLETE' -ForegroundColor Green
Write-Host '============================================' -ForegroundColor Cyan
Write-Host "`nPackage directory: $outputDir" -ForegroundColor White
Write-Host "Packaged modules: $count/$($modules.Count)" -ForegroundColor White
