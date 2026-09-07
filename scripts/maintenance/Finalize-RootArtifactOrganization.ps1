param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$legacyBuild = Join-Path $root 'scripts\build\Build-Solution.ps1'
$canonicalBuild = Join-Path $root 'scripts\verification\Build-Solution.ps1'
$legacyBuildDir = Join-Path $root 'scripts\build'

if (-not (Test-Path -LiteralPath $canonicalBuild -PathType Leaf)) {
    throw "Canonical build script is missing: $canonicalBuild"
}

if (Test-Path -LiteralPath $legacyBuild -PathType Leaf) {
    Write-Host ("[{0}] scripts\build\Build-Solution.ps1" -f $(if ($Apply) { 'DELETE' } else { 'WOULD DELETE' })) -ForegroundColor Yellow
    if ($Apply) {
        Remove-Item -LiteralPath $legacyBuild -Force
    }
}

if ($Apply -and (Test-Path -LiteralPath $legacyBuildDir -PathType Container)) {
    $remaining = @(Get-ChildItem -LiteralPath $legacyBuildDir -Force)
    if ($remaining.Count -eq 0) {
        Remove-Item -LiteralPath $legacyBuildDir -Force
    }
}

Write-Host 'Canonical build entrypoint: scripts\verification\Build-Solution.ps1' -ForegroundColor Green
