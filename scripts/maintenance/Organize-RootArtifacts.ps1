param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not (Test-Path -LiteralPath (Join-Path $root 'Antigravity.sln'))) {
    throw "Safety guard failed: Antigravity.sln was not found in $root"
}

$moves = [ordered]@{
    '_build_check.ps1' = 'scripts\verification\Build-Solution.ps1'
    'CleanProject.ps1' = 'scripts\maintenance\CleanProject.ps1'
    'ArchiveLegacyArtifacts.ps1' = 'scripts\maintenance\ArchiveLegacyArtifacts.ps1'
    'DeployToRevit.ps1' = 'scripts\deploy\Deploy-ToRevit.ps1'
    'DeployToRevit_Universal.ps1' = 'scripts\deploy\Deploy-ToRevit-Universal.ps1'
    'PackageForDeployment.ps1' = 'scripts\deploy\Package-ForDeployment.ps1'
    'dump_hatch.ps1' = 'scripts\diagnostics\autocad\Dump-Hatch.ps1'
    'run_tag_arranger_test.ps1' = 'src\Antigravity.TagArranger\smoke-tests\scripts\Run-TagArrangerTest.ps1'

    'BCF_Comparison_CheckPhanDe_vs_IssuesExport_20260612_1416.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\BCF_Comparison_CheckPhanDe_vs_IssuesExport_20260612_1416.md'
    'CreateIssueDialog_Redesign_Summary.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\CreateIssueDialog_Redesign_Summary.md'
    'IssueManager_BCF_ID_Summary.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\IssueManager_BCF_ID_Summary.md'
    'IssueManager_CreateIssue_Implementation.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\IssueManager_CreateIssue_Implementation.md'
    'IssueManager_CreateIssue_v1.2_Update.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\IssueManager_CreateIssue_v1.2_Update.md'
    'IssueManager_CreateIssue_v1.3_Update.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\IssueManager_CreateIssue_v1.3_Update.md'
    'IssueManager_Excel_Paste_Font_Fix_Handoff.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\IssueManager_Excel_Paste_Font_Fix_Handoff.md'
    'IssueStorageError_Summary.md' = 'src\Antigravity.IssueManager\docs\reports\legacy-root\IssueStorageError_Summary.md'
}

$deletes = @(
    'dump_hatch2.ps1',
    'get_journal.ps1',
    'get_journal_anti.ps1',
    'get_journal_latest.ps1',
    'get_journal_vilai.ps1',
    'get_journal_vkt.ps1',
    'test_serialize.cs',
    'TestParse.cs',
    'TestZip.cs',
    'update_colors.ps1'
)

Write-Host 'ANTIGRAVITY ROOT ARTIFACT ORGANIZER' -ForegroundColor Cyan
Write-Host "Root: $root" -ForegroundColor DarkGray
Write-Host ("Mode: {0}" -f $(if ($Apply) { 'APPLY' } else { 'PREVIEW' })) -ForegroundColor DarkGray
Write-Host ''

foreach ($entry in $moves.GetEnumerator()) {
    $source = Join-Path $root $entry.Key
    $destination = Join-Path $root $entry.Value
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        Write-Host "[SKIP MISSING] $($entry.Key)" -ForegroundColor DarkGray
        continue
    }
    if (Test-Path -LiteralPath $destination) {
        throw "Destination collision: $($entry.Value)"
    }

    Write-Host ("[{0}] {1} -> {2}" -f $(if ($Apply) { 'MOVE' } else { 'WOULD MOVE' }), $entry.Key, $entry.Value) -ForegroundColor Yellow
    if ($Apply) {
        $parent = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $parent)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
        Move-Item -LiteralPath $source -Destination $destination
        if ((Test-Path -LiteralPath $source) -or -not (Test-Path -LiteralPath $destination)) {
            throw "Move verification failed: $($entry.Key)"
        }
    }
}

Write-Host ''
foreach ($relative in $deletes) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Write-Host "[SKIP MISSING] $relative" -ForegroundColor DarkGray
        continue
    }
    Write-Host ("[{0}] {1}" -f $(if ($Apply) { 'DELETE' } else { 'WOULD DELETE' }), $relative) -ForegroundColor DarkYellow
    if ($Apply) {
        Remove-Item -LiteralPath $path -Force
        if (Test-Path -LiteralPath $path) {
            throw "Delete verification failed: $relative"
        }
    }
}

Write-Host ''
Write-Host 'Root organization completed for the explicit allowlist.' -ForegroundColor Green
