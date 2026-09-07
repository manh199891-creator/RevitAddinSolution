param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sln = Join-Path $repoRoot 'Antigravity.sln'

$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild)) {
    $msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
}
if (-not (Test-Path -LiteralPath $msbuild)) {
    $found = Get-ChildItem -Path 'C:\Program Files\Microsoft Visual Studio' -Filter 'MSBuild.exe' -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if ($found) { $msbuild = $found }
}
if (-not (Test-Path -LiteralPath $msbuild)) {
    throw 'MSBuild.exe was not found under Visual Studio installation directories.'
}
if (-not (Test-Path -LiteralPath $sln)) {
    throw "Solution was not found: $sln"
}

Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan
& $msbuild $sln /restore /t:Build /p:Configuration=$Configuration /p:Platform='Any CPU' /p:DeployToRevitAddins=false /nologo /v:minimal
$buildExitCode = $LASTEXITCODE
if ($buildExitCode -ne 0) {
    Write-Error "Antigravity solution build failed with exit code $buildExitCode."
}
exit $buildExitCode
