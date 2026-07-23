param(
    [Parameter(Mandatory=$true)][string]$Project,
    [switch]$AsJson
)

. (Join-Path $PSScriptRoot "dual_paths.ps1")
$paths = Get-DualAgentPaths
$factoryRoot = $paths.FactoryRoot
$aliases = @{ "revit" = "RevitAddinSolution"; "navis" = "NavisAddinSolution"; "trend" = "TrendingUpdate" }
$projectName = if ($aliases.ContainsKey($Project)) { $aliases[$Project] } else { $Project }
$projectRoot = Join-Path $factoryRoot $projectName
$previousFactoryRoot = $env:AI_SOFTWARE_FACTORY_ROOT
$env:AI_SOFTWARE_FACTORY_ROOT = $factoryRoot
try {
    & python $paths.Harness $projectName reconcile-stale 2>$null | Out-Null
} finally {
    $env:AI_SOFTWARE_FACTORY_ROOT = $previousFactoryRoot
}
$manifestPath = Join-Path $projectRoot ".agent\state\review_run.json"
$pipelineStatusPath = Join-Path $projectRoot ".agent\state\pipeline_status.json"
$contextPath = Join-Path $projectRoot ".agent\context\TASK_CONTEXT.json"
$dualReportPath = Join-Path $projectRoot ".agent\reports\DUAL_AGENT_REPORT.md"

$manifest = if (Test-Path $manifestPath) { Get-Content -Raw $manifestPath | ConvertFrom-Json } else { $null }
$authority = if (Test-Path $pipelineStatusPath) { Get-Content -Raw $pipelineStatusPath | ConvertFrom-Json } else { $null }
if ($authority -and $manifest -and [string]$authority.run_id -ne [string]$manifest.run_id) {
    $authority = $null
}
$context = if (Test-Path $contextPath) { Get-Content -Raw $contextPath | ConvertFrom-Json } else { $null }
$dualStatus = ""
$dualReportTaskId = ""
if (Test-Path $dualReportPath) {
    $dualReportContent = Get-Content -Raw $dualReportPath
    $match = [regex]::Match($dualReportContent, '(?m)^## Status:\s*(\S+)')
    if ($match.Success) { $dualStatus = $match.Groups[1].Value.ToUpperInvariant() }
    $taskMatch = [regex]::Match($dualReportContent, '(?m)^- task_id:\s*(\S+)')
    if ($taskMatch.Success) { $dualReportTaskId = $taskMatch.Groups[1].Value }
}

$currentTaskId = if ($context) { [string]$context.task_id } else { "" }
if ($currentTaskId -and $dualReportTaskId -and $dualReportTaskId -ne $currentTaskId) {
    $dualStatus = ""
}

$staleRunning = $false
if ($manifest -and [string]$manifest.status -eq "RUNNING" -and $manifest.heartbeat_at) {
    try {
        $profilePath = Join-Path $projectRoot ".agent\project_profile.json"
        $reviewTimeoutSeconds = 180
        if (Test-Path $profilePath) {
            $profile = Get-Content -Raw $profilePath | ConvertFrom-Json
            if ($profile.codex_review.timeout_seconds) {
                $reviewTimeoutSeconds = [int]$profile.codex_review.timeout_seconds
            }
        }
        $heartbeat = [DateTime]::Parse([string]$manifest.heartbeat_at)
        $staleAfterSeconds = $reviewTimeoutSeconds + 60
        $staleRunning = ((Get-Date) - $heartbeat).TotalSeconds -gt $staleAfterSeconds
    } catch {
        $staleRunning = $false
    }
}

$reviewStatus = if ($authority) { [string]$authority.status } elseif ($manifest) { [string]$manifest.status } else { "NOT_STARTED" }
$taskStatus = if ($context) { [string]$context.status } else { "unknown" }
$effectiveStatus = if ($authority) {
    [string]$authority.status
} elseif ($taskStatus -eq "blocked_handoff") {
    "BLOCKED_HANDOFF"
} elseif ($staleRunning) {
    "STALE"
} elseif ($reviewStatus -in @("QUEUED", "PREPARING", "RUNNING")) {
    $reviewStatus
} elseif ($dualStatus) {
    $dualStatus
} elseif ($reviewStatus -eq "FAIL") {
    "BLOCKED_CODEX"
} else {
    $reviewStatus
}

$terminalStatuses = @(
    "PASS", "SMOKE_PASS", "FAIL", "BLOCKED", "BLOCKED_CODEX", "BLOCKED_HANDOFF",
    "BLOCKED_SCOPE", "BLOCKED_VERIFY", "BLOCKED_NO_DELTA", "BLOCKED_BASELINE",
    "INFRA_FAIL", "STALE", "CANCELLED", "NOT_STARTED"
)
$terminal = if ($authority -and $null -ne $authority.terminal) { [bool]$authority.terminal } else { $terminalStatuses -contains $effectiveStatus }
$result = [ordered]@{
    project = $projectName
    status = $effectiveStatus
    terminal = $terminal
    review_status = $reviewStatus
    pipeline_status = if ($authority) { [string]$authority.status } elseif ($dualStatus) { $dualStatus } else { "UNKNOWN" }
    task_status = $taskStatus
    run_id = if ($manifest) { [string]$manifest.run_id } else { "" }
    completed_at = if ($manifest) { [string]$manifest.completed_at } else { "" }
}

if ($AsJson) {
    $result | ConvertTo-Json -Compress
} else {
    Write-Output "Status: $effectiveStatus"
    Write-Output "Terminal: $terminal"
    Write-Output "ReviewStatus: $reviewStatus"
    Write-Output "PipelineStatus: $($result.pipeline_status)"
    if ($result.run_id) { Write-Output "RunId: $($result.run_id)" }
}

if ($effectiveStatus -in @("PASS", "SMOKE_PASS")) { exit 0 }
if ($terminal) { exit 2 }
exit 0
