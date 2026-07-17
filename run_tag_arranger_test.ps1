param(
    [string]$TestFile = ""
)

# 1. Build project
Write-Host "Building project..." -ForegroundColor Cyan
& ".\_build_check.ps1"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Aborting test." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 2. Tạo file trigger
$triggerFile = "C:\temp\revit_auto_run.txt"
$triggerContent = @"
COMMAND: TagArrangerTestCommand
EXIT: TRUE
"@
Set-Content -Path $triggerFile -Value $triggerContent -Encoding UTF8
Write-Host "Created trigger file for TagArrangerTestCommand with Auto-Exit." -ForegroundColor Green

# 3. Chuẩn bị file an toàn (Copy ra chỗ khác để tránh bị khóa file)
$revitPath = "C:\Program Files\Autodesk\Revit 2024\Revit.exe"

# Tự động tìm file trong thư mục TestFiles nếu người dùng không truyền tham số
if ([string]::IsNullOrWhiteSpace($TestFile)) {
    $testFilesDir = ".\TestFiles"
    if (Test-Path $testFilesDir) {
        $foundFile = Get-ChildItem -Path $testFilesDir -Include *.rvt,*.rte -Recurse | Select-Object -First 1
        if ($foundFile) {
            $TestFile = $foundFile.FullName
            Write-Host "Found custom test file: $TestFile" -ForegroundColor Yellow
        }
    }
}

# Nếu vẫn không có file nào, dùng file mặc định của Revit
if ([string]::IsNullOrWhiteSpace($TestFile)) {
    $TestFile = "C:\ProgramData\Autodesk\RVT 2024\Templates\English\DefaultMetric.rte"
    Write-Host "No custom test file found. Using default template." -ForegroundColor DarkGray
}

$originalTemplate = $TestFile
$templatePath = "C:\temp\Test_Revit_File" + [System.IO.Path]::GetExtension($originalTemplate)
Copy-Item -Path $originalTemplate -Destination $templatePath -Force

Write-Host "Launching Revit with $templatePath... (This may take up to 30-60 seconds)" -ForegroundColor Cyan
$revitProcess = Start-Process -FilePath $revitPath -ArgumentList "`"$templatePath`"" -PassThru

# 4. Chờ đợi Revit tắt (do lệnh ExitRevit) hoặc tối đa 600 giây
$timeout = 600
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while (!$revitProcess.HasExited -and $sw.Elapsed.TotalSeconds -lt $timeout) {
    Start-Sleep -Seconds 2
    Write-Host "." -NoNewline
}
Write-Host ""

if (!$revitProcess.HasExited) {
    Write-Host "Revit did not exit within $timeout seconds. It might be hung." -ForegroundColor Red
} else {
    Write-Host "Revit closed successfully." -ForegroundColor Green
}

# 5. Đọc file log mới nhất
$logDir = "C:\temp\AntigravityLogs"
if (Test-Path $logDir) {
    $latestLog = Get-ChildItem -Path $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestLog) {
        Write-Host ""
        Write-Host "--- TEST RESULTS ($($latestLog.Name)) ---" -ForegroundColor Yellow
        Get-Content $latestLog.FullName
        Write-Host "-----------------------------------------" -ForegroundColor Yellow
    }
} else {
    Write-Host "Log directory not found." -ForegroundColor Red
}
