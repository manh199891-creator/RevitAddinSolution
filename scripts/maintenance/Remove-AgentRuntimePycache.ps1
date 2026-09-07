[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runtimeRoot = Join-Path $repositoryRoot '.agents\runtime'

if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'Antigravity.sln') -PathType Leaf)) {
    throw "Safety guard failed: Antigravity.sln was not found at repository root: $repositoryRoot"
}

if (-not (Test-Path -LiteralPath $runtimeRoot -PathType Container)) {
    throw "Safety guard failed: .agents\\runtime was not found: $runtimeRoot"
}

$runtimeCanonical = (Resolve-Path -LiteralPath $runtimeRoot).Path.TrimEnd('\')
$prefix = $runtimeCanonical + '\'
$targets = @(
    Get-ChildItem -LiteralPath $runtimeCanonical -Directory -Recurse -Force -ErrorAction Stop |
        Where-Object { $_.Name -eq '__pycache__' } |
        Sort-Object { $_.FullName.Length } -Descending
)

$removed = 0
foreach ($target in $targets) {
    $candidate = $target.FullName
    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Safety guard failed: cleanup target escaped .agents\\runtime: $candidate"
    }
    if ($target.Name -ne '__pycache__') {
        throw "Safety guard failed: cleanup target is not an exact __pycache__ directory: $candidate"
    }

    Remove-Item -LiteralPath $candidate -Recurse -Force
    $removed++
    Write-Host "[REMOVED] $candidate"
}

Write-Host "Agent runtime pycache cleanup complete. Removed directories: $removed"
