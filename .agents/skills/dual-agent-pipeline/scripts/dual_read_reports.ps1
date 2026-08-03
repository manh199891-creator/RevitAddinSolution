param(
    [Parameter(Mandatory=$true)][string]$Project
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
if (Test-Path $manifestPath) {
    try {
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        Write-Output "--- PIPELINE METRICS ---"
        Write-Output "Cycle: $($manifest.pipeline_cycle) / $($manifest.pipeline_cycles_total)"
        Write-Output "Exit Code: $($manifest.exit_code)"
        Write-Output "Status: $($manifest.status)"
        if ($manifest.reason_code) {
            if ($manifest.reason_code -match "BLOCKED_NO_PROGRESS" -or $manifest.reason_code -match "OSCILLATION") {
                Write-Output "⚠️ WARNING: $($manifest.reason_code) - $($manifest.reason)"
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
    } catch {
        Write-Output "Failed to parse review_run.json"
    }
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
        $summary = $content | Where-Object { $_ -match "^## Status:" -or $_ -match "^## Summary:" }
        if ($summary) {
            Write-Output $summary
        } else {
            $firstLines = $content | Where-Object { $_.Trim() -ne "" } | Select-Object -First 2
            Write-Output $firstLines
        }
        Write-Output ""
    }
}
