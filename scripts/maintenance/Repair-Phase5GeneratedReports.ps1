param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src = Join-Path $root 'src'
$date = '2026-08-23'
$signature = 'Phase: 5 reporting / finalized agent-resumability structure'

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

$repaired = 0
$skipped = 0
$dirs = Get-ChildItem -LiteralPath $src -Directory | Where-Object { $_.Name -like 'Antigravity.*' } | Sort-Object Name

foreach ($dir in $dirs) {
    $reportPath = Join-Path $dir.FullName ("docs\reports\{0}-artifact-structure-migration.md" -f $date)
    if (-not (Test-Path -LiteralPath $reportPath)) {
        Write-Host ("[SKIP missing] {0}" -f $dir.Name)
        $skipped++
        continue
    }

    $raw = Get-Content -LiteralPath $reportPath -Raw
    if ($raw -notmatch [regex]::Escape($signature)) {
        Write-Host ("[KEEP detailed] {0}" -f $dir.Name)
        $skipped++
        continue
    }

    $projectMd = Join-Path $dir.FullName 'PROJECT.md'
    $classification = Read-MemoryField $projectMd 'Classification:'
    $activity = Read-MemoryField $projectMd 'Activity:'
    if (-not $classification) { $classification = 'UNCLASSIFIED' }
    if (-not $activity) { $activity = 'UNKNOWN' }

    $manifest = Read-JsonSafe (Join-Path $dir.FullName 'smoke-tests\smoke-manifest.json')
    $lkg = Read-JsonSafe (Join-Path $dir.FullName 'smoke-tests\baseline\last-known-good.json')
    $smokeChecks = if ($manifest -and $manifest.smokeChecks) { @($manifest.smokeChecks) -join ', ' } else { 'NOT_APPLICABLE_OR_NOT_DEFINED' }
    $lkgStatus = if ($lkg -and $lkg.smokeStatus) { [string]$lkg.smokeStatus } elseif ($lkg -and $lkg.status) { [string]$lkg.status } else { 'NOT_APPLICABLE_OR_NOT_CAPTURED' }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# ' + $dir.Name + ' - Artifact Structure Migration')
    $lines.Add('')
    $lines.Add('Date: ' + $date)
    $lines.Add('Phase: 5 reporting / finalized agent-resumability structure')
    $lines.Add('')
    $lines.Add('## Project classification')
    $lines.Add('')
    $lines.Add('- Classification: ' + $classification)
    $lines.Add('- Activity: ' + $activity)
    $lines.Add('')
    $lines.Add('## Structure created / verified')
    $lines.Add('')
    $lines.Add('- PROJECT.md durable landing page')
    $lines.Add('- docs/plans/ROADMAP.md long-term milestone index')
    $lines.Add('- docs/{plans,design,acceptance,reports} ownership tree as applicable')
    $lines.Add('- smoke-tests contract as applicable to the project classification')
    $lines.Add('')
    $lines.Add('## Artifact migration')
    $lines.Add('')
    $lines.Add('No high-confidence legacy project-specific artifact was identified for relocation in Phase 3 for this generated-report owner; canonical ownership/memory/smoke structure was established without moving production source.')
    $lines.Add('')
    $lines.Add('## Repository-level artifacts intentionally retained')
    $lines.Add('')
    $lines.Add('- Cross-solution plans: docs/plans/')
    $lines.Add('- Cross-solution architecture/reports/specs/standards: repository docs/')
    $lines.Add('- Ambiguous artifacts remain at repository scope until ownership is proven.')
    $lines.Add('')
    $lines.Add('## Ambiguous / deferred')
    $lines.Add('')
    if ($classification -eq 'DEFERRED_NO_CSPROJ') {
        $lines.Add('Directory has no .csproj. Build/smoke ownership remains deferred until separately classified.')
    } else {
        $lines.Add('Repository-level ambiguous/cross-solution artifacts were not guessed into this owner.')
    }
    $lines.Add('')
    $lines.Add('## Smoke / rollback profile')
    $lines.Add('')
    $lines.Add('- Selected smoke checks: ' + $smokeChecks)
    $lines.Add('- Last-known-good status: ' + $lkgStatus)
    $lines.Add('- PENDING_* is intentionally not a PASS claim.')
    $lines.Add('')
    $lines.Add('## Verification status at Phase 5 close')
    $lines.Add('')
    $lines.Add('- Structural reporting state: READY_FOR_PHASE_6')
    $lines.Add('- Build/test verification: VERIFY_PENDING at Phase 5 close.')
    $lines.Add('- Manual Revit/integration smoke: NOT_CLAIMED unless separate evidence exists.')
    $lines.Add('')
    $lines.Add('## Phase 6 closure pointer')
    $lines.Add('')
    $lines.Add('Current governance verification is recorded in docs/reports/2026-08-23-addin-artifact-governance-rollout.md. This file remains a truthful Phase 5 snapshot and must not be interpreted as current VERIFY_PENDING state.')
    $lines.Add('')
    $lines.Add('## Production behavior')
    $lines.Add('')
    $lines.Add('This governance/reporting migration did not intentionally modify production C#, XAML, namespace, assembly name, project reference, AddInId or Revit runtime behavior. Existing unrelated dirty production work was preserved.')
    $lines.Add('')

    $content = ($lines -join [Environment]::NewLine) + [Environment]::NewLine
    Write-Host ("[REPAIR] {0}" -f $reportPath.Substring($root.Length + 1))
    if ($Apply) {
        [System.IO.File]::WriteAllText($reportPath, $content, (New-Object System.Text.UTF8Encoding($false)))
    }
    $repaired++
}

Write-Host ("Phase5 generated reports selected: {0}; skipped/preserved: {1}" -f $repaired, $skipped)
