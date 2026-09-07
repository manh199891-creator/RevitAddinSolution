param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src = Join-Path $root 'src'
$date = '2026-08-23'

function Read-JsonSafe([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { return $null }
}

function Read-MemoryField([string]$Path, [string]$Prefix) {
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    $line = Get-Content -LiteralPath $Path | Where-Object { $_ -like ($Prefix + '*') } | Select-Object -First 1
    if (-not $line) { return '' }
    return $line.Substring($Prefix.Length).Trim()
}

function Write-NewFile([string]$Path, [string]$Content) {
    $relative = $Path.Substring($root.Length + 1)
    if (Test-Path -LiteralPath $Path) {
        Write-Host ("[KEEP REPORT] {0}" -f $relative)
        return $false
    }
    Write-Host ("[CREATE     ] {0}" -f $relative)
    if ($Apply) {
        $parent = Split-Path -Parent $Path
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
    }
    return $true
}

$rows = New-Object System.Collections.Generic.List[object]
$dirs = Get-ChildItem -LiteralPath $src -Directory | Where-Object { $_.Name -like 'Antigravity.*' } | Sort-Object Name
$created = 0
$preserved = 0

foreach ($dir in $dirs) {
    $projectMd = Join-Path $dir.FullName 'PROJECT.md'
    $classification = Read-MemoryField $projectMd 'Classification:'
    $activity = Read-MemoryField $projectMd 'Activity:'
    if (-not $classification) { $classification = 'UNCLASSIFIED' }
    if (-not $activity) { $activity = 'UNKNOWN' }

    $manifestPath = Join-Path $dir.FullName 'smoke-tests\smoke-manifest.json'
    $lkgPath = Join-Path $dir.FullName 'smoke-tests\baseline\last-known-good.json'
    $manifest = Read-JsonSafe $manifestPath
    $lkg = Read-JsonSafe $lkgPath
    $smokeChecks = if ($manifest -and $manifest.smokeChecks) { @($manifest.smokeChecks) -join ', ' } else { 'NOT_APPLICABLE_OR_NOT_DEFINED' }
    $lkgStatus = if ($lkg -and $lkg.smokeStatus) { [string]$lkg.smokeStatus } elseif ($lkg -and $lkg.status) { [string]$lkg.status } else { 'NOT_APPLICABLE_OR_NOT_CAPTURED' }

    $legacyReportPath = Join-Path $dir.FullName ("docs\reports\{0}-artifact-structure-migration.md" -f $date)
    $hadDetailedMigration = Test-Path -LiteralPath $legacyReportPath
    $migrationText = if ($hadDetailedMigration) {
        'High-confidence legacy artifact migration is documented in this existing report and was preserved.'
    } else {
        'No high-confidence legacy project-specific artifact was identified for relocation in Phase 3; canonical ownership/memory/smoke structure was established without moving production source.'
    }

    $deferredText = if ($classification -eq 'DEFERRED_NO_CSPROJ') {
        'Directory has no .csproj. Build/smoke ownership remains deferred until the directory is classified as a real project or removed by a separate reviewed task.'
    } else {
        'Repository-level ambiguous/cross-solution artifacts remain centralized/deferred; no ambiguous artifact was guessed into this owner.'
    }

    $report = @(
        '# ' + $dir.Name + ' - Artifact Structure Migration',
        '',
        'Date: ' + $date,
        'Phase: 5 reporting / finalized agent-resumability structure',
        '',
        '## Project classification',
        '',
        '- Classification: ' + $classification,
        '- Activity: ' + $activity,
        '',
        '## Structure created / verified',
        '',
        '- PROJECT.md durable landing page',
        '- docs/plans/ROADMAP.md long-term milestone index',
        '- docs/{plans,design,acceptance,reports} ownership tree as applicable',
        '- smoke-tests contract as applicable to the project classification',
        '',
        '## Artifact migration',
        '',
        $migrationText,
        '',
        '## Repository-level artifacts intentionally retained',
        '',
        '- Cross-solution plans: docs/plans/',
        '- Cross-solution architecture/reports/specs/standards: repository docs/',
        '- Ambiguous artifacts remain at repository scope until ownership is proven.',
        '',
        '## Ambiguous / deferred',
        '',
        $deferredText,
        '',
        '## Smoke / rollback profile',
        '',
        '- Selected smoke checks: ' + $smokeChecks,
        '- Last-known-good status: ' + $lkgStatus,
        '- PENDING_* is intentionally not a PASS claim.',
        '',
        '## Verification status',
        '',
        '- Structural reporting state: READY_FOR_PHASE_6',
        '- Build/test verification: VERIFY_PENDING until Phase 6 actual commands complete.',
        '- Manual Revit/integration smoke: NOT_CLAIMED unless separate evidence exists.',
        '',
        '## Production behavior',
        '',
        'This governance/reporting migration does not intentionally modify production C#, XAML, namespace, assembly name, project reference, AddInId or Revit runtime behavior. Existing unrelated dirty production work is preserved.',
        ''
    ) -join [Environment]::NewLine

    if (Write-NewFile $legacyReportPath ($report + [Environment]::NewLine)) { $created++ } else { $preserved++ }

    $rows.Add([pscustomobject]@{
        Project = $dir.Name
        Type = $classification
        LocalDocs = if (Test-Path -LiteralPath (Join-Path $dir.FullName 'docs\plans\ROADMAP.md')) { 'YES' } else { 'NO' }
        Smoke = if ($manifest) { $lkgStatus } else { 'N/A' }
        Migrated = if ($hadDetailedMigration) { 'HIGH_CONFIDENCE_HISTORY' } else { 'STRUCTURE_ONLY' }
        Deferred = if ($classification -eq 'DEFERRED_NO_CSPROJ') { 'NO_CSPROJ' } else { 'AMBIGUOUS_REPO_ITEMS_RETAINED' }
        Status = if ($classification -eq 'DEFERRED_NO_CSPROJ') { 'DEFERRED' } else { 'VERIFY_PENDING' }
    })
}

$summaryLines = New-Object System.Collections.Generic.List[string]
$summaryLines.Add('# Add-in Artifact Governance Rollout')
$summaryLines.Add('')
$summaryLines.Add('Date: ' + $date)
$summaryLines.Add('Phase: 5 reporting')
$summaryLines.Add('')
$summaryLines.Add('This summary reflects the finalized agent-resumability structure. Project status remains VERIFY_PENDING until Phase 6 actual structural/build verification closes.')
$summaryLines.Add('')
$summaryLines.Add('| Project | Type | Local docs | Smoke contract/LKG | Migrated artifacts | Ambiguous/deferred | Status |')
$summaryLines.Add('|---|---|---|---|---|---|---|')
foreach ($row in $rows) {
    $summaryLines.Add(('| {0} | {1} | {2} | {3} | {4} | {5} | {6} |' -f $row.Project, $row.Type, $row.LocalDocs, $row.Smoke, $row.Migrated, $row.Deferred, $row.Status))
}
$summaryLines.Add('')
$summaryLines.Add('## Finalized durable navigation')
$summaryLines.Add('')
$summaryLines.Add('docs/projects/PROJECTS.md -> src/<Project>/PROJECT.md -> src/<Project>/docs/plans/ROADMAP.md -> relevant dated plan -> smoke/last-known-good -> source')
$summaryLines.Add('')
$summaryLines.Add('No manual Revit smoke or production promotion is claimed by this reporting phase.')
$summary = ($summaryLines -join [Environment]::NewLine) + [Environment]::NewLine
$summaryPath = Join-Path $root ("docs\reports\{0}-addin-artifact-governance-rollout.md" -f $date)
Write-Host ("[GENERATE   ] {0}" -f $summaryPath.Substring($root.Length + 1))
if ($Apply) { [System.IO.File]::WriteAllText($summaryPath, $summary, (New-Object System.Text.UTF8Encoding($false))) }

Write-Host ("Project reports created: {0}; preserved existing: {1}; projects: {2}" -f $created, $preserved, $rows.Count)
