param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src = Join-Path $root 'src'
$selected = 0
$skipped = 0

function Read-MemoryField([string]$Path, [string]$Prefix) {
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    $line = Get-Content -LiteralPath $Path | Where-Object { $_ -like ($Prefix + '*') } | Select-Object -First 1
    if (-not $line) { return '' }
    return $line.Substring($Prefix.Length).Trim()
}

$dirs = Get-ChildItem -LiteralPath $src -Directory | Where-Object { $_.Name -like 'Antigravity.*' } | Sort-Object Name
foreach ($dir in $dirs) {
    $readmePath = Join-Path $dir.FullName 'docs\README.md'
    if (-not (Test-Path -LiteralPath $readmePath)) {
        Write-Host ("[SKIP missing] {0}" -f $dir.Name)
        $skipped++
        continue
    }

    $raw = Get-Content -LiteralPath $readmePath -Raw
    $isGovernanceReadme = $raw -match [regex]::Escape('# ' + $dir.Name + ' Documentation') -and $raw -match 'Canonical project-specific artifacts live here:'
    if (-not $isGovernanceReadme) {
        Write-Host ("[KEEP custom] {0}" -f $dir.Name)
        $skipped++
        continue
    }

    $classification = Read-MemoryField (Join-Path $dir.FullName 'PROJECT.md') 'Classification:'
    if (-not $classification) { $classification = 'UNCLASSIFIED' }

    $lines = @(
        '# ' + $dir.Name + ' Documentation',
        '',
        'Owner project: ' + $dir.Name,
        'Classification: ' + $classification,
        '',
        'Canonical project-specific artifacts live here:',
        '',
        '- plans/ - implementation/feature plans.',
        '- design/ - technical design and module decisions.',
        '- acceptance/ - acceptance criteria and manual acceptance procedures.',
        '- reports/ - implementation, review, migration and closure reports.',
        '',
        'Repository-level docs/ is reserved for genuinely cross-solution concerns. .ai-bridge/ is transient handoff state only.',
        ''
    )
    $content = $lines -join [Environment]::NewLine

    Write-Host ("[REPAIR] {0}" -f $readmePath.Substring($root.Length + 1))
    if ($Apply) {
        [System.IO.File]::WriteAllText($readmePath, $content, (New-Object System.Text.UTF8Encoding($false)))
    }
    $selected++
}

Write-Host ("Governance docs README selected: {0}; skipped/custom: {1}" -f $selected, $skipped)
