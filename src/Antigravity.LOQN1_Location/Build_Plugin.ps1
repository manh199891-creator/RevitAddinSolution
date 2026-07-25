# PowerShell Script Build DLL and Install to Revit 2024
$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " COMPILE AND INSTALL ADD-IN VILAIVIET-LOQN1-LOCATION " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$projectDir = $PSScriptRoot
$csprojPath = Join-Path $projectDir "LOQN1_Location_element.csproj"
$revitAddinDir = "$env:APPDATA\Autodesk\Revit\Addins\2024"

if (-not (Test-Path $csprojPath)) {
    Write-Host "[ERROR] Project file not found: $csprojPath" -ForegroundColor Red
    exit 1
}

try {
    Write-Host "[1/2] Compiling C# code to DLL..." -ForegroundColor Yellow
    Set-Location $projectDir
    
    dotnet build $csprojPath -c Release
    
    $dllPath = Join-Path $projectDir "bin\Release\VILAIVIET_LOQN1_Location.dll"
    
    if (Test-Path $dllPath) {
        Write-Host "[OK] Build successful: $dllPath" -ForegroundColor Green
        
        Write-Host "[2/2] Copying Add-in to Revit 2024..." -ForegroundColor Yellow
        if (-not (Test-Path $revitAddinDir)) {
            New-Item -ItemType Directory -Path $revitAddinDir -Force | Out-Null
        }
        
        Copy-Item -Path $dllPath -Destination $revitAddinDir -Force
        Copy-Item -Path (Join-Path $projectDir "VILAIVIET-LOQN1-Location.addin") -Destination $revitAddinDir -Force
        
        $logoPath = Join-Path $projectDir "VILAIVIET-Logo.png"
        if (Test-Path $logoPath) {
            Copy-Item -Path $logoPath -Destination $revitAddinDir -Force
        }
        
        Write-Host "[SUCCESS] Installation complete! Please open Revit 2024 to use the Add-in." -ForegroundColor Green
    } else {
        Write-Host "[ERROR] DLL file not found after build." -ForegroundColor Red
    }
}
catch {
    Write-Host "[ERROR] An error occurred during compilation." -ForegroundColor Red
    Write-Host $_.Exception.Message
}
