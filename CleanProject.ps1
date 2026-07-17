# Script dọn dẹp dự án Revit trước khi nén/gửi đi
Write-Host "Đang dọn dẹp dự án..." -ForegroundColor Cyan

# Xóa bin và obj
Get-ChildItem -Path . -Include bin,obj -Recurse | ForEach-Object {
    Write-Host "Đang xóa: $($_.FullName)" -ForegroundColor Yellow
    Remove-Item -Path $_.FullName -Recurse -Force
}

# Xóa thư mục .vs
if (Test-Path ".vs") {
    Write-Host "Đang xóa thư mục .vs..." -ForegroundColor Yellow
    Remove-Item -Path ".vs" -Recurse -Force
}

Write-Host "Xong! Dự án đã sẵn sàng để nén (Zip)." -ForegroundColor Green
Pause
