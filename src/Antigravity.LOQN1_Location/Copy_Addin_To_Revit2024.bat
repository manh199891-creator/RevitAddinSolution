@echo off
chcp 65001 > nul
echo ========================================================
echo TỰ ĐỘNG CÀI ĐẶT ADD-IN VILAIVIET-LOQN1-LOCATION VÀO REVIT 2024
echo ========================================================
echo.

set "DEST_DIR=%APPDATA%\Autodesk\Revit\Addins\2024"

if not exist "%DEST_DIR%" (
    mkdir "%DEST_DIR%"
)

copy /Y "%~dp0VILAIVIET-LOQN1-Location.addin" "%DEST_DIR%\"

echo [OK] Đã copy file VILAIVIET-LOQN1-Location.addin vào thư mục:
echo      %DEST_DIR%
echo.
echo Hãy mở Revit 2024 để sử dụng Add-in!
echo.
pause
