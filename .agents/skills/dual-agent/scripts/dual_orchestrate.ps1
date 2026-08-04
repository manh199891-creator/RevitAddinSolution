[CmdletBinding()]
param(
    [string]$Action = "",

    [string]$Project = "",

    [string]$TaskId = "",
    [string]$Feature = "",

    [ValidateSet("research", "plan", "code", "release")]
    [string]$Mode = "code",

    [string]$Allowed = "",
    [string]$Forbidden = "",
    [string]$Artifacts = "",
    [ValidateRange(1, 3)]
    [int]$MaxCycles = 2,
    [switch]$SkipVerify,
    [switch]$Force,
    [string]$FixCommand = "",
    [string]$ResumeHypothesis = ""
)

$ErrorActionPreference = "Stop"

function Write-Usage {
    Write-Host "dual_orchestrate.ps1 usage:"
    Write-Host "  init:    -Action init -Project <alias> -TaskId <id> -Feature <name> -Mode <research|plan|code|release> [-Allowed <globs>] [-Artifacts <files>] [-Force]"
    Write-Host "  run:     -Action run -Project <alias> -TaskId <id> -Feature <name> -Mode <research|plan|code|release> [-MaxCycles 2] [-FixCommand <command>]"
    Write-Host "  status:  -Action status -Project <alias>"
    Write-Host "  reports: -Action reports -Project <alias>"
    Write-Host "  doctor:  -Action doctor -Project <alias>"
}

function Fail-Fast([string]$Message, [int]$Code = 64) {
    Write-Host "ERROR: $Message"
    Write-Usage
    exit $Code
}

if ([string]::IsNullOrWhiteSpace($Action)) {
    Fail-Fast "Missing required parameter: -Action. This bridge is non-interactive and will not prompt for missing values."
}

if ([string]::IsNullOrWhiteSpace($Project)) {
    Fail-Fast "Missing required parameter: -Project. Use the registered AI Software Factory project alias, for example: revit."
}

$validActions = @("init", "run", "status", "reports", "doctor")
if ($validActions -notcontains $Action) {
    Fail-Fast "Invalid -Action '$Action'. Expected one of: $($validActions -join ', ')."
}

$pipelineCandidates = @()
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$localPipeline = Join-Path $projectRoot ".agents\skills\dual-agent-pipeline"
if (Test-Path -LiteralPath (Join-Path $localPipeline "scripts\dual_init.ps1")) {
    $pipelineCandidates += $localPipeline
}

$factoryCandidates = @()
if ($env:AI_SOFTWARE_FACTORY_ROOT) {
    $factoryCandidates += $env:AI_SOFTWARE_FACTORY_ROOT
}
$factoryCandidates += "E:\AI_SOFTWARE_FACTORY"

$pipelineRoot = $null
foreach ($candidate in $pipelineCandidates) {
    if (Test-Path -LiteralPath (Join-Path $candidate "scripts\dual_init.ps1")) {
        $pipelineRoot = $candidate
        break
    }
}

foreach ($factoryRoot in $factoryCandidates) {
    if ($pipelineRoot) { break }
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
        if ($ResumeHypothesis) { $params.ResumeHypothesis = $ResumeHypothesis }
        & (Join-Path $pipelineRoot "scripts\dual_run.ps1") @params
    }
    "status" {
        & (Join-Path $pipelineRoot "scripts\dual_status.ps1") -Project $Project
    }
    "reports" {
        & (Join-Path $pipelineRoot "scripts\dual_read_reports.ps1") -Project $Project
    }
    "doctor" {
        $doctorScript = Join-Path $pipelineRoot "scripts\dual_doctor.ps1"
        if (Test-Path -LiteralPath $doctorScript) {
            & $doctorScript -Project $Project
        } else {
            $factoryRoot = Split-Path -Parent (Split-Path -Parent $pipelineRoot)
            & python (Join-Path $factoryRoot "harness.py") $Project doctor
        }
    }
}

exit $LASTEXITCODE
