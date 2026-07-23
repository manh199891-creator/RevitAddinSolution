param(
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$TaskId,
    [Parameter(Mandatory=$true)][string]$Feature,
    [ValidateSet("research", "plan", "code", "release")][string]$Mode = "release",
    [string]$Artifacts = "",
    [int]$MaxCycles = 2,
    [switch]$SkipVerify,
    [string]$FixCommand = "",
    [string]$ResumeHypothesis = ""
)

. (Join-Path $PSScriptRoot "dual_paths.ps1")
$paths = Get-DualAgentPaths
$argsList = @($Project, "dual", "--task-id", $TaskId, "--feature", $Feature, "--mode", $Mode, "--max-cycles", $MaxCycles)

if ($Artifacts) {
    $argsList += "--artifacts"
    $argsList += $Artifacts
}

if ($SkipVerify) {
    $argsList += "--skip-verify"
}

if ($FixCommand) {
    $argsList += "--fix-command"
    $argsList += $FixCommand
}

if ($ResumeHypothesis) {
    $argsList += "--resume-hypothesis"
    $argsList += $ResumeHypothesis
}

Invoke-DualAgentHarness -Paths $paths -Arguments $argsList
