param(
    [Parameter(Mandatory=$true)][string]$Project,
    [switch]$AsJson,
    [switch]$Full,
    [switch]$FindingsOnly
)

. (Join-Path $PSScriptRoot "dual_paths.ps1")
$paths = Get-DualAgentPaths
$factoryRoot = $paths.FactoryRoot

$aliases = @{
    "revit" = "RevitAddinSolution"
    "navis" = "NavisAddinSolution"
    "trend" = "TrendingUpdate"
}

$projectName = if ($aliases.ContainsKey($Project)) { $aliases[$Project] } else { $Project }
$projectRoot = Join-Path $factoryRoot $projectName

$manifestPath = Join-Path $projectRoot ".agent\state\review_run.json"
$manifest = $null
if (Test-Path $manifestPath) {
    try {
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    } catch {}
}

if ($AsJson) {
    $result = [ordered]@{
        project = $projectName
        task_id = if ($manifest) { $manifest.task_id } else { $null }
        mode = if ($manifest) { $manifest.review_purpose } else { $null }
        status = if ($manifest) { $manifest.status } else { $null }
        run_id = if ($manifest) { $manifest.run_id } else { $null }
        snapshot_hash = if ($manifest) { $manifest.snapshot_hash } else { $null }
        review_mode = if ($manifest) { $manifest.review_mode } else { $null }
        review_tier = if ($manifest) { $manifest.review_tier } else { $null }
        blocking_findings = if ($manifest -and $manifest.findings) { @($manifest.findings | Where-Object { $_.severity -in @("P0", "P1", "P2") -and $_.status -in @("OPEN", "STILL_OPEN", "REOPENED") }) } else { @() }
        advisory_findings = if ($manifest -and $manifest.findings) { @($manifest.findings | Where-Object { $_.severity -eq "P3" -or $_.status -in @("ADVISORY", "DEFERRED") }) } else { @() }
        resolved_findings = if ($manifest -and $manifest.findings) { @($manifest.findings | Where-Object { $_.status -eq "RESOLVED" }) } else { @() }
        new_findings = if ($manifest -and $null -ne $manifest.new_blocking_ids) { $manifest.new_blocking_ids } else { @() }
        progress = if ($manifest -and $null -ne $manifest.progress) { $manifest.progress } else { $null }
        pipeline_cycles = if ($manifest -and $null -ne $manifest.pipeline_cycle) { $manifest.pipeline_cycle } else { $null }
        codex_batch_count = if ($manifest -and $null -ne $manifest.codex_batch_count) { $manifest.codex_batch_count } else { $null }
        codex_process_invocations = if ($manifest -and $null -ne $manifest.codex_process_invocations) { $manifest.codex_process_invocations } else { $null }
        fixer_invocations = if ($manifest -and $null -ne $manifest.fixer_invocations) { $manifest.fixer_invocations } else { $null }
        fixer_result = $null
    }

    $handoffPath = Join-Path $projectRoot ".agent\reports\FIXER_COMMAND_REPORT.json"
    if (Test-Path $handoffPath) {
        try {
            $handoff = Get-Content $handoffPath -Raw | ConvertFrom-Json
            $result.fixer_result = $handoff
        } catch {}
    }

    $result | ConvertTo-Json -Depth 10 -Compress
    exit 0
}

if ($manifest) {
    Write-Output "--- PIPELINE METRICS ---"
    Write-Output "Cycle: $($manifest.pipeline_cycle) / $($manifest.pipeline_cycles_total)"
    Write-Output "Exit Code: $($manifest.exit_code)"
    Write-Output "Status: $($manifest.status)"
    if ($manifest.reason_code) {
        if ($manifest.reason_code -match "BLOCKED_NO_PROGRESS" -or $manifest.reason_code -match "OSCILLATION") {
            Write-Output "âš ï¸ WARNING: $($manifest.reason_code) - $($manifest.reason)"
        } else {
            Write-Output "Reason Code: $($manifest.reason_code)"
        }
    }

    if ($manifest.findings) {
        $blocking = $manifest.findings | Where-Object {
            ($_.severity -in @("P0", "P1", "P2")) -and ($_.status -in @("OPEN", "STILL_OPEN", "REOPENED"))
        }
        if ($blocking) {
            Write-Output ""
            Write-Output "--- BLOCKING FINDINGS ---"
            foreach ($f in $blocking) {
                Write-Output "[$($f.severity)] $($f.file):$($f.line) - $($f.title)"
            }
        }
    }
    Write-Output ""
}

if ($FindingsOnly) {
    if ($manifest -and $manifest.findings) {
        $manifest.findings | Select-Object finding_id, severity, status, file, line, title, body, required_test | ConvertTo-Json -Depth 5
    } else {
        Write-Output "[]"
    }
    exit 0
}

$reports = @(
    "DUAL_AGENT_REPORT.md",
    "CODEX_REVIEW.md",
    "FIXER_HANDOFF.md",
    "RELEASE_GATE_REPORT.md"
)

foreach ($report in $reports) {
    $path = Join-Path $projectRoot ".agent\reports\$report"
    if (Test-Path $path) {
        Write-Output "--- $report ---"
        Write-Output "Path: $path"

        $content = Get-Content $path
        if ($Full) {
            Write-Output $content
        } else {
            $summary = $content | Where-Object { $_ -match "^## Status:" -or $_ -match "^## Summary:" }
            if ($summary) {
                Write-Output $summary
            } else {
                $firstLines = $content | Where-Object { $_.Trim() -ne "" } | Select-Object -First 2
                Write-Output $firstLines
            }
        }
        Write-Output ""
    }
}
