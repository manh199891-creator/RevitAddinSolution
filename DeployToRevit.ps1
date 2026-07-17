# Deploy Add-in Antigravity vao Revit 2024
# Huong dan: Build Solution truoc, sau do chay script nay.
# LUU Y: Phai TAT Revit truoc khi chay script!

param(
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"

$revitVersion = "2024"
$addinFolder = "$env:APPDATA\Autodesk\Revit\Addins\$revitVersion"
$targetFolder = Join-Path $addinFolder "Antigravity"
$solutionRoot = $PSScriptRoot

Write-Host "--- BAT DAU CAI DAT ANTIGRAVITY ADDIN ---" -ForegroundColor Cyan

# 0. Kiem tra Revit co dang chay khong
$revitProc = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($revitProc) {
    Write-Host " [!] Revit dang chay (PID: $($revitProc.Id)). DLL se bi khoa!" -ForegroundColor Red
    Write-Host "     Hay tat Revit truoc roi chay lai script nay." -ForegroundColor Red
    if (-not $NoPause) { Read-Host "Nhan Enter de thoat" }
    exit 1
}

# 1. Tao thu muc dich cho DLLs
if (!(Test-Path $targetFolder)) {
    Write-Host "Tao thu muc: $targetFolder"
    New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
}

# 2. Danh sach cac module can copy
$modules = @(
    "Main",
    "Core",
    "DrawColumns",
    "DrawBeams",
    "DrawWalls",
    "DrawFloors",
    "Autojoin",
    "ZoneSplit",
    "AutoDimWalls",
    "CadVoidPlacer",
    "CadSleevePlacer",
    "TagArranger",
    "CheckFloorElevation",
    "IssueManager",
    "WallMepClash",
    "HoanThien",
    "ArchModeling",
    "HatchPatterns.Contracts"
)

Write-Host "Dang copy cac file DLL tu bin/Debug..." -ForegroundColor Yellow

foreach ($mod in $modules) {
    # SDK-style projects may leave a tiny reference/placeholder DLL directly
    # under bin\Debug. Prefer the target-framework output, which is the actual
    # Revit-loadable assembly (for example ZoneSplit targets net48).
    $dllPath = "$solutionRoot\src\Antigravity.$mod\bin\Debug\net48\Antigravity.$mod.dll"
    if (!(Test-Path $dllPath)) {
        $dllPath = "$solutionRoot\src\Antigravity.$mod\bin\Debug\netstandard2.0\Antigravity.$mod.dll"
    }
    if (!(Test-Path $dllPath)) {
        $dllPath = "$solutionRoot\src\Antigravity.$mod\bin\Debug\Antigravity.$mod.dll"
    }

    if (Test-Path $dllPath) {
        Copy-Item -Path $dllPath -Destination $targetFolder -Force
        Write-Host " [OK] Antigravity.$mod.dll" -ForegroundColor Green
    } else {
        Write-Host " [!] Thieu file: Antigravity.$mod.dll" -ForegroundColor Red
    }
}

# 2b. Copy DoorClearanceBox.dll (Co assembly name dac biet)
$doorClearanceDll = "$solutionRoot\src\Antigravity.DoorClearance\bin\Debug\DoorClearanceBox.dll"
if (!(Test-Path $doorClearanceDll)) {
    $doorClearanceDll = "$solutionRoot\src\Antigravity.DoorClearance\bin\Debug\net48\DoorClearanceBox.dll"
}
if (Test-Path $doorClearanceDll) {
    Copy-Item -Path $doorClearanceDll -Destination $targetFolder -Force
    Write-Host " [OK] DoorClearanceBox.dll" -ForegroundColor Green
} else {
    Write-Host " [!] Thieu file: DoorClearanceBox.dll" -ForegroundColor Red
}

# 3. Copy NuGet dependency DLLs (Serilog)
Write-Host "Dang copy cac dependency DLL..." -ForegroundColor Yellow
$depSource = "$solutionRoot\src\Antigravity.Core\bin\Debug"
$dependencies = @("Serilog.dll", "Serilog.Sinks.File.dll")

foreach ($dep in $dependencies) {
    $depPath = Join-Path $depSource $dep
    if (Test-Path $depPath) {
        Copy-Item -Path $depPath -Destination $targetFolder -Force
        Write-Host " [OK] $dep" -ForegroundColor Green
    } else {
        Write-Host " [!] Thieu dependency: $dep" -ForegroundColor Red
    }
}

# Dependencies used by ArchModeling commands. These must travel with the
# module; otherwise its ribbon buttons can load but fail when invoked.
$archDependencySource = "$solutionRoot\src\Antigravity.ArchModeling\bin\Debug"
$archDependencies = @(
    "ExcelDataReader.dll",
    "ExcelDataReader.DataSet.dll",
    "Newtonsoft.Json.dll",
    "JetBrains.Annotations.dll",
    "Nice3point.Revit.Extensions.dll"
)

foreach ($dep in $archDependencies) {
    $depPath = Join-Path $archDependencySource $dep
    if (Test-Path $depPath) {
        Copy-Item -Path $depPath -Destination $targetFolder -Force
        Write-Host " [OK] $dep" -ForegroundColor Green
    } else {
        Write-Host " [!] Thieu dependency: $dep" -ForegroundColor Red
    }
}

$netDxfPath = "$solutionRoot\src\Antigravity.CadVoidPlacer\bin\Debug\net48\netDxf.dll"
if (!(Test-Path $netDxfPath)) {
    $netDxfPath = "$solutionRoot\src\Antigravity.CadVoidPlacer\bin\Debug\netDxf.dll"
}
if (Test-Path $netDxfPath) {
    Copy-Item -Path $netDxfPath -Destination $targetFolder -Force
    Write-Host " [OK] netDxf.dll" -ForegroundColor Green
} else {
    Write-Host " [!] Thieu dependency: netDxf.dll" -ForegroundColor Red
}

# 4. Cai dat file .addin
Write-Host "Dang cai dat file .addin..." -ForegroundColor Yellow
$addinSource = "$solutionRoot\src\Antigravity.Main\Antigravity.addin"
if (Test-Path $addinSource) {
    Copy-Item -Path $addinSource -Destination (Join-Path $addinFolder "Antigravity.addin") -Force
    Write-Host " [OK] Antigravity.addin da duoc cap nhat." -ForegroundColor Green
} else {
    Write-Host " [!] Khong tim thay file Antigravity.addin" -ForegroundColor Red
}

$legacyDoorManifest = Join-Path $addinFolder "DoorClearanceBox.addin"
if (Test-Path $legacyDoorManifest) {
    Move-Item -Path $legacyDoorManifest -Destination (Join-Path $addinFolder "DoorClearanceBox.addin.disabled") -Force
    Write-Host " [OK] Da vo hieu hoa DoorClearanceBox.addin cu." -ForegroundColor Green
}

$legacyIssueManagerManifest = Join-Path $addinFolder "Antigravity.IssueManager.addin"
if (Test-Path $legacyIssueManagerManifest) {
    Move-Item -Path $legacyIssueManagerManifest -Destination (Join-Path $addinFolder "Antigravity.IssueManager.addin.disabled") -Force
    Write-Host " [OK] Da vo hieu hoa Antigravity.IssueManager.addin cu." -ForegroundColor Green
}

Write-Host ""
Write-Host "--- HOAN TAT ---" -ForegroundColor Cyan
Write-Host "Bay gio hay khoi dong Revit de kiem tra."
if (-not $NoPause) { Read-Host "Nhan Enter de thoat" }
