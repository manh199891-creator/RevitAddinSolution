# ============================================================
#  PackageForDeployment.ps1
#  Script đóng gói tất cả file DLL để cài sang máy khác
#  Chạy trên máy phát triển SAU KHI đã Build Solution
# ============================================================

$solutionRoot = $PSScriptRoot
$buildConfig  = "Debug"
$outputDir    = Join-Path $solutionRoot "_Deploy"
$dllsDir      = Join-Path $outputDir "dlls"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   ANTIGRAVITY — PACKAGING FOR DEPLOYMENT" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# Tạo thư mục đóng gói
if (Test-Path $outputDir) { Remove-Item -Path $outputDir -Recurse -Force }
New-Item -ItemType Directory -Path $dllsDir -Force | Out-Null

# Danh sách module
$modules = @(
    "Core",
    "Main",
    "DrawColumns",
    "DrawBeams",
    "DrawWalls",
    "DrawFloors",
    "Autojoin",
    "ZoneSplit",
    "AutoDimWalls",
    "CadVoidPlacer",
    "CadSleevePlacer",
    "TagArranger"
)

Write-Host "`nĐang đóng gói các file DLL..." -ForegroundColor Yellow
$count = 0
foreach ($mod in $modules) {
    $src = Join-Path $solutionRoot "src\Antigravity.$mod\bin\$buildConfig\Antigravity.$mod.dll"
    if (!(Test-Path $src)) {
        $src = Join-Path $solutionRoot "src\Antigravity.$mod\bin\$buildConfig\net48\Antigravity.$mod.dll"
    }
    if (Test-Path $src) {
        Copy-Item $src -Destination $dllsDir -Force
        Write-Host "  [OK] Antigravity.$mod.dll" -ForegroundColor Green
        $count++
    } else {
        Write-Host "  [!]  Thiếu: Antigravity.$mod.dll — Bạn đã Build chưa?" -ForegroundColor Red
    }
}

$doorClearanceDll = Join-Path $solutionRoot "src\Antigravity.DoorClearance\bin\$buildConfig\DoorClearanceBox.dll"
if (!(Test-Path $doorClearanceDll)) {
    $doorClearanceDll = Join-Path $solutionRoot "src\Antigravity.DoorClearance\bin\$buildConfig\net48\DoorClearanceBox.dll"
}
if (Test-Path $doorClearanceDll) {
    Copy-Item $doorClearanceDll -Destination $dllsDir -Force
    Write-Host "  [OK] DoorClearanceBox.dll" -ForegroundColor Green
    $count++
} else {
    Write-Host "  [!]  Thieu: DoorClearanceBox.dll" -ForegroundColor Red
}

$dependencies = @(
    "src\Antigravity.Core\bin\$buildConfig\Serilog.dll",
    "src\Antigravity.Core\bin\$buildConfig\Serilog.Sinks.File.dll",
    "src\Antigravity.CadVoidPlacer\bin\$buildConfig\net48\netDxf.dll"
)
foreach ($relativePath in $dependencies) {
    $dep = Join-Path $solutionRoot $relativePath
    if (Test-Path $dep) {
        Copy-Item $dep -Destination $dllsDir -Force
        Write-Host "  [OK] $(Split-Path $dep -Leaf)" -ForegroundColor Green
    } else {
        Write-Host "  [!]  Thieu dependency: $(Split-Path $relativePath -Leaf)" -ForegroundColor Red
    }
}

# Copy script cài đặt vào gói
Copy-Item (Join-Path $solutionRoot "DeployToRevit_Universal.ps1") -Destination $outputDir -Force
Write-Host "  [OK] DeployToRevit_Universal.ps1" -ForegroundColor Green

# Tạo file README trong gói
@"
=== ANTIGRAVITY ADD-IN PACKAGE ===

Nội dung thư mục này:
  dlls\          : Các file DLL của Add-in
  DeployToRevit_Universal.ps1 : Script cài đặt tự động

Cách cài đặt trên máy đích:
  1. Copy toàn bộ thư mục này sang máy cần cài.
  2. Chuột phải vào DeployToRevit_Universal.ps1
  3. Chọn "Run with PowerShell"
  4. Script sẽ tự phát hiện Revit 2022/2023/2024 và cài đúng chỗ.
  5. Mở Revit -> chọn "Always Load" -> Tìm tab ANTIGRAVITY trên Ribbon.

Hỗ trợ: Revit 2022, 2023, 2024
"@ | Set-Content (Join-Path $outputDir "README.txt") -Encoding UTF8

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "  ĐÃ ĐÓNG GÓI XONG!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "`nThư mục gói: $outputDir" -ForegroundColor White
Write-Host "Đã đóng gói: $count/$($modules.Count) module" -ForegroundColor White
Write-Host "`nBây giờ bạn có thể copy thư mục '_Deploy' sang USB hoặc gửi cho đồng nghiệp." -ForegroundColor Yellow
Write-Host ""
Pause
