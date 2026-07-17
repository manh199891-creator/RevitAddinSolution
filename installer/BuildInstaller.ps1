param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "_Installer"
$setupSource = Join-Path $PSScriptRoot "AntigravitySetup.cs"
$setupExe = Join-Path $outDir "AntigravitySetup.exe"

$cscCandidates = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "Không tìm thấy csc.exe của .NET Framework."
}

$modules = @(
    "Core",
    "Main",
    "DrawColumns",
    "DrawBeams",
    "DrawWalls",
    "DrawFloors",
    "Autojoin"
)

if (Test-Path $outDir) {
    Remove-Item -Path $outDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$resourceArgs = @()
foreach ($module in $modules) {
    $dll = Join-Path $root "src\Antigravity.$module\bin\$Configuration\Antigravity.$module.dll"
    if (-not (Test-Path $dll)) {
        throw "Thiếu DLL: $dll. Hãy build solution cấu hình $Configuration trước."
    }

    $resourceArgs += "/resource:$dll,Payload.Antigravity.$module.dll"
}

& $csc `
    /nologo `
    /target:exe `
    /platform:anycpu `
    /optimize+ `
    "/out:$setupExe" `
    $resourceArgs `
    $setupSource

if ($LASTEXITCODE -ne 0) {
    throw "Build installer thất bại."
}

@"
ANTIGRAVITY REVIT ADD-IN INSTALLER

File:
  AntigravitySetup.exe

Cách dùng:
  1. Copy AntigravitySetup.exe sang máy cần cài.
  2. Đảm bảo máy đó đã cài Autodesk Revit và đã mở Revit ít nhất một lần.
  3. Chạy AntigravitySetup.exe.
  4. Mở Revit và chọn Always Load nếu được hỏi.

Gỡ cài đặt:
  AntigravitySetup.exe /uninstall

Ghi chú:
  - Installer cài vào %AppData%\Autodesk\Revit\Addins\{version}\Antigravity.
  - Các tính năng đọc AutoCAD qua COM cần máy đích có cài AutoCAD.
  - Không đóng gói RevitAPI.dll/RevitAPIUI.dll vì Revit cung cấp các file này.
"@ | Set-Content -Path (Join-Path $outDir "README.txt") -Encoding UTF8

Write-Host "[OK] Đã tạo installer:" -ForegroundColor Green
Write-Host "     $setupExe"
