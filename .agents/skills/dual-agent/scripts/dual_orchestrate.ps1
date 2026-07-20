[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("init", "run", "status", "reports")]
    [string]$Action,

    [Parameter(Mandatory=$true)]
    [string]$Project,

    [string]$TaskId = "",
    [string]$Feature = "",

    [ValidateSet("research", "plan", "code", "release")]
    [string]$Mode = "code",

    [string]$Allowed = "",
    [string]$Forbidden = "",
    [string]$Artifacts = "",
    [int]$MaxCycles = 2,
    [switch]$SkipVerify,
    [switch]$Force,
    [string]$FixCommand = ""
)

$factoryCandidates = @()
if ($env:AI_SOFTWARE_FACTORY_ROOT) {
    $factoryCandidates += $env:AI_SOFTWARE_FACTORY_ROOT
}
$factoryCandidates += "E:\AI_SOFTWARE_FACTORY"

$pipelineRoot = $null
foreach ($factoryRoot in $factoryCandidates) {
    $candidate = Join-Path $factoryRoot "skills\dual-agent-pipeline"
    if (Test-Path -LiteralPath (Join-Path $candidate "scripts\dual_init.ps1")) {
        $pipelineRoot = $candidate
        break
    }
}

if (-not $pipelineRoot) {
    throw "dual-agent-pipeline was not found. Set AI_SOFTWARE_FACTORY_ROOT to the factory directory."
}

if ($Action -in @("init", "run") -and ([string]::IsNullOrWhiteSpace($TaskId) -or [string]::IsNullOrWhiteSpace($Feature))) {
    throw "TaskId and Feature are required for action '$Action'."
}

switch ($Action) {
    "init" {
        $params = @{
            Project = $Project
            TaskId = $TaskId
            Feature = $Feature
            Mode = $Mode
        }
        if ($Allowed) { $params.Allowed = $Allowed }
        if ($Forbidden) { $params.Forbidden = $Forbidden }
        if ($Artifacts) { $params.Artifacts = $Artifacts }
        if ($Force) { $params.Force = $true }
        & (Join-Path $pipelineRoot "scripts\dual_init.ps1") @params
    }
    "run" {
        $params = @{
            Project = $Project
            TaskId = $TaskId
            Feature = $Feature
            Mode = $Mode
            MaxCycles = $MaxCycles
        }
        if ($Artifacts) { $params.Artifacts = $Artifacts }
        if ($SkipVerify) { $params.SkipVerify = $true }
        if ($FixCommand) { $params.FixCommand = $FixCommand }
        & (Join-Path $pipelineRoot "scripts\dual_run.ps1") @params
    }
    "status" {
        & (Join-Path $pipelineRoot "scripts\dual_status.ps1") -Project $Project
    }
    "reports" {
        & (Join-Path $pipelineRoot "scripts\dual_read_reports.ps1") -Project $Project
    }
}

exit $LASTEXITCODE
