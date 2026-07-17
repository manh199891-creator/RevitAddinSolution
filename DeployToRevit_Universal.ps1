# ============================================================
#  DeployToRevit_Universal.ps1
#  Cài đặt Antigravity Add-in cho MỌI phiên bản Revit
#  Hỗ trợ 2 kịch bản:
#    A) Máy phát triển: có Visual Studio, có thư mục src\
#    B) Máy đích:       chỉ có thư mục dlls\ (sau khi đóng gói)
#  Cách dùng: Chuột phải -> Run with PowerShell
# ============================================================

$scriptDir = $PSScriptRoot
$modules   = @("Core", "Main", "DrawColumns", "DrawBeams", "DrawWalls", "DrawFloors", "Autojoin", "ZoneSplit", "AutoDimWalls", "CadVoidPlacer", "CadSleevePlacer", "TagArranger", "CheckFloorElevation", "IssueManager", "WallMepClash", "HoanThien")

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   ANTIGRAVITY ADD-IN — UNIVERSAL INSTALLER" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# === STEP 1: Xac dinh nguon DLL (may phat trien hay may dich) ===
$dllSource = $null
$sourceMode = ""

# Kịch bản B: Thư mục dlls\ ngay bên cạnh script (máy đích)
$dllsFolder = Join-Path $scriptDir "dlls"
if (Test-Path (Join-Path $dllsFolder "Antigravity.Main.dll")) {
    $dllSource = $dllsFolder
    $sourceMode = "DEPLOYMENT (Thư mục dlls\)"
}

# Kịch bản A: Thư mục src\ tồn tại (máy phát triển)
if ($dllSource -eq $null) {
    $devDll = Join-Path $scriptDir "src\Antigravity.Main\bin\Debug\Antigravity.Main.dll"
    if (Test-Path $devDll) {
        $dllSource = $scriptDir  # sẽ xây dựng path từ src\ bên dưới
        $sourceMode = "DEVELOPMENT (Build từ Visual Studio - Debug)"
    }
    
    # Thử Release nếu Debug không có
    if ($dllSource -eq $null) {
        $relDll = Join-Path $scriptDir "src\Antigravity.Main\bin\Release\Antigravity.Main.dll"
        if (Test-Path $relDll) {
            $dllSource = $scriptDir
            $sourceMode = "DEVELOPMENT (Build từ Visual Studio - Release)"
        }
    }
}

# Không tìm thấy DLL
if ($dllSource -eq $null) {
    Write-Host ""
    Write-Host "[LỖI] Không tìm thấy file DLL!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Hãy kiểm tra một trong hai trường hợp sau:" -ForegroundColor Yellow
    Write-Host "  Nếu bạn là LẬP TRÌNH VIÊN:" -ForegroundColor White
    Write-Host "    -> Mở Antigravity.sln trong Visual Studio" -ForegroundColor White
    Write-Host "    -> Nhấn Ctrl+Shift+B (Build Solution)" -ForegroundColor White
    Write-Host "    -> Chạy lại script này" -ForegroundColor White
    Write-Host ""
    Write-Host "  Nếu bạn là NGƯỜI DÙNG CUỐI:" -ForegroundColor White
    Write-Host "    -> Đảm bảo thư mục 'dlls\' nằm cùng chỗ với script này" -ForegroundColor White
    Write-Host "    -> Liên hệ lập trình viên để lấy file 'dlls\'" -ForegroundColor White
    Write-Host ""
    Pause
    exit 1
}

Write-Host ""
Write-Host "[OK] Chế độ: $sourceMode" -ForegroundColor Green

# === STEP 2: Ham lay duong dan DLL theo che do ===
function Get-DllPath($modName) {
    if ($dllsFolder -and (Test-Path (Join-Path $dllsFolder "Antigravity.$modName.dll"))) {
        return Join-Path $dllsFolder "Antigravity.$modName.dll"
    }
    # Thử Debug rồi Release
    $debugPath   = Join-Path $scriptDir "src\Antigravity.$modName\bin\Debug\Antigravity.$modName.dll"
    $releasePath = Join-Path $scriptDir "src\Antigravity.$modName\bin\Release\Antigravity.$modName.dll"
    if (Test-Path $debugPath)   { return $debugPath }
    if (Test-Path $releasePath) { return $releasePath }
    return $null
}

# === STEP 3: Tim cac phien ban Revit da cai ===
$revitAddinsBase = Join-Path $env:APPDATA "Autodesk\Revit\Addins"
$installedVersions = @()

if (Test-Path $revitAddinsBase) {
    $installedVersions = Get-ChildItem -Path $revitAddinsBase -Directory |
        Where-Object { $_.Name -match '^\d{4}$' } |
        Select-Object -ExpandProperty Name |
        Sort-Object
}

if ($installedVersions.Count -eq 0) {
    Write-Host "[LỖI] Không tìm thấy bất kỳ phiên bản Revit nào trên máy!" -ForegroundColor Red
    Write-Host "       Hãy mở Revit ít nhất một lần trước khi cài Add-in." -ForegroundColor Yellow
    Pause
    exit 1
}

Write-Host ""
Write-Host "Đã phát hiện các phiên bản Revit:" -ForegroundColor Yellow
foreach ($ver in $installedVersions) {
    Write-Host "  -> Revit $ver" -ForegroundColor White
}

# === STEP 4: Cai dat cho tung phien ban ===
$totalSuccess = 0
$totalFail    = 0

foreach ($ver in $installedVersions) {
    Write-Host ""
    Write-Host "--- Cài đặt cho Revit $ver ---" -ForegroundColor Cyan
    
    $addinFolder = Join-Path $revitAddinsBase $ver
    $targetFolder = Join-Path $addinFolder "Antigravity"
    
    if (!(Test-Path $targetFolder)) {
        New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
    }
    
    $successCount = 0
    $failCount    = 0
    
    foreach ($mod in $modules) {
        $src = Get-DllPath $mod
        if ($src -ne $null) {
            Copy-Item -Path $src -Destination $targetFolder -Force
            Write-Host "  [OK] Antigravity.$mod.dll" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  [!]  Bỏ qua: Antigravity.$mod.dll" -ForegroundColor DarkYellow
            $failCount++
        }
    }
    
    # Tạo file .addin với đường dẫn tuyệt đối
    $mainDllPath = Join-Path $targetFolder "Antigravity.Main.dll"
    $addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Antigravity</Name>
    <Assembly>$mainDllPath</Assembly>
    <AddInId>88888888-9999-0000-AAAA-BBBBCCCCDDDD</AddInId>
    <FullClassName>Antigravity.Main.App</FullClassName>
    <VendorId>ANTIGRAVITY</VendorId>
    <VendorDescription>Antigravity Structural Tools</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    $addinFilePath = Join-Path $addinFolder "Antigravity.addin"
    $addinContent | Set-Content -Path $addinFilePath -Encoding UTF8
    Write-Host "  [OK] Antigravity.addin" -ForegroundColor Green
    
    $statusColor = if ($failCount -eq 0) { "Green" } else { "Yellow" }
    Write-Host "  => Revit $ver`: $successCount DLL OK, $failCount bỏ qua." -ForegroundColor $statusColor
    
    $totalSuccess += $successCount
    $totalFail    += $failCount
}

# === STEP 5: Tong ket ===
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  HOÀN TẤT CÀI ĐẶT!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Đã cài cho: Revit $($installedVersions -join ', ')" -ForegroundColor White
Write-Host "Tổng DLL:   $totalSuccess thành công / $totalFail bỏ qua" -ForegroundColor White
Write-Host ""
Write-Host "Bước tiếp theo:" -ForegroundColor Yellow
Write-Host "  1. Mở Revit." -ForegroundColor White
Write-Host "  2. Khi hỏi 'Always Load' -> chọn 'Always Load'." -ForegroundColor White
Write-Host "  3. Tìm tab 'ANTIGRAVITY' trên thanh Ribbon." -ForegroundColor White
Write-Host ""
Pause
