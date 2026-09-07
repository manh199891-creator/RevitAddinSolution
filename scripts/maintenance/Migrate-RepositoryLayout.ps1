param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Ensure-Directory([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host ("[CREATE DIR ] {0}" -f $RelativePath)
        if ($Apply) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
    }
}

function Move-Safe([string]$SourceRelative, [string]$TargetRelative) {
    $source = Join-Path $root $SourceRelative
    $target = Join-Path $root $TargetRelative
    if (-not (Test-Path -LiteralPath $source)) { return }

    if (Test-Path -LiteralPath $target) {
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        if ($sourceHash -ne $targetHash) {
            throw "Destination collision with different content: $TargetRelative"
        }
        Write-Host ("[DEDUP      ] {0} -> {1}" -f $SourceRelative, $TargetRelative)
        if ($Apply) { Remove-Item -LiteralPath $source -Force }
        return
    }

    Write-Host ("[MOVE       ] {0} -> {1}" -f $SourceRelative, $TargetRelative)
    if ($Apply) {
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Move-Item -LiteralPath $source -Destination $target
    }
}

function Remove-EmptyDirectory([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { return }
    $children = @(Get-ChildItem -LiteralPath $path -Force)
    if ($children.Count -eq 0) {
        Write-Host ("[REMOVE DIR ] {0}" -f $RelativePath)
        if ($Apply) { Remove-Item -LiteralPath $path -Force }
    }
}

Write-Host 'REPOSITORY LAYOUT MIGRATION'
Write-Host ("Root : {0}" -f $root)
Write-Host ("Mode : {0}" -f $(if ($Apply) { 'APPLY' } else { 'PREVIEW' }))

foreach ($dir in @(
    'docs\projects',
    'docs\architecture',
    'docs\adr',
    'docs\plans',
    'docs\reports',
    'docs\standards',
    'docs\standards\ui',
    'docs\templates',
    'packaging',
    'artifacts',
    'scripts\architecture'
)) { Ensure-Directory $dir }

$legacyPlans = Join-Path $root 'docs\superpowers\plans'
if (Test-Path -LiteralPath $legacyPlans -PathType Container) {
    foreach ($file in Get-ChildItem -LiteralPath $legacyPlans -File | Sort-Object Name) {
        Move-Safe ('docs\superpowers\plans\' + $file.Name) ('docs\plans\' + $file.Name)
    }
}

Move-Safe 'docs\superpowers\tools\Generate-ArchitectureGraph.ps1' 'scripts\architecture\Generate-ArchitectureGraph.ps1'
Move-Safe 'docs\superpowers\tools\understand_config.json' 'scripts\architecture\understand_config.json'

$legacyUi = Join-Path $root 'docs\ui'
if (Test-Path -LiteralPath $legacyUi -PathType Container) {
    foreach ($file in Get-ChildItem -LiteralPath $legacyUi -File | Sort-Object Name) {
        Move-Safe ('docs\ui\' + $file.Name) ('docs\standards\ui\' + $file.Name)
    }
}

Move-Safe 'VilaiViet_UI_Guidelines.md' 'docs\standards\ui\VilaiViet_UI_Guidelines.md'
Move-Safe 'plans\clash_control_plan.md' 'docs\plans\clash_control_plan.md'

foreach ($dir in @(
    'docs\superpowers\plans',
    'docs\superpowers\tools',
    'docs\superpowers',
    'docs\ui',
    'plans'
)) { Remove-EmptyDirectory $dir }

Write-Host 'Repository layout migration complete.'
