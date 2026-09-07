param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Move-Safe([string]$SourceRelative, [string]$TargetRelative) {
    $source = Join-Path $root $SourceRelative
    $target = Join-Path $root $TargetRelative
    if (-not (Test-Path -LiteralPath $source)) {
        Write-Host ("[SOURCE ABSENT] {0}" -f $SourceRelative)
        return
    }
    if (Test-Path -LiteralPath $target) {
        $a = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $b = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        if ($a -ne $b) { throw "Collision with different content: $TargetRelative" }
        Write-Host ("[DEDUP] {0} -> {1}" -f $SourceRelative, $TargetRelative)
        if ($Apply) { Remove-Item -LiteralPath $source -Force }
        return
    }
    Write-Host ("[MOVE] {0} -> {1}" -f $SourceRelative, $TargetRelative)
    if ($Apply) {
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Move-Item -LiteralPath $source -Destination $target
    }
}

Move-Safe 'docs\ZoneSplit_CodeX_Context.md' 'src\Antigravity.ZoneSplit\docs\design\ZoneSplit_CodeX_Context.md'
Move-Safe 'docs\ZoneSplit_Core.cs' 'src\Antigravity.ZoneSplit\docs\design\reference\ZoneSplit_Core.cs'
