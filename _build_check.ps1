$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe"
if (!(Test-Path $msbuild)) {
    $msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
}
if (!(Test-Path $msbuild)) {
    $found = Get-ChildItem -Path "C:\Program Files\Microsoft Visual Studio" -Filter "MSBuild.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if ($found) { $msbuild = $found }
}

$sln = "$PSScriptRoot\Antigravity.sln"
Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan
& $msbuild $sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /nologo /v:minimal
