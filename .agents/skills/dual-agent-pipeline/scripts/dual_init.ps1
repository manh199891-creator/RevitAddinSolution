param(
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$TaskId,
    [Parameter(Mandatory=$true)][string]$Feature,
    [ValidateSet("research", "plan", "code", "release")][string]$Mode = "release",
    [string]$Artifacts = "",
    [string]$Allowed = "",
    [string]$Forbidden = "",
    [switch]$Force
)

. (Join-Path $PSScriptRoot "dual_paths.ps1")
$paths = Get-DualAgentPaths
$argsList = @($Project, "dual-init", "--task-id", $TaskId, "--feature", $Feature, "--mode", $Mode)

if ($Artifacts) {
    $argsList += "--artifacts"
    $argsList += $Artifacts
}

if ($Allowed) {
    $argsList += "--allowed"
    $argsList += $Allowed
}

if ($Forbidden) {
    $argsList += "--forbidden"
    $argsList += $Forbidden
}

if ($Force) {
    $argsList += "--force"
}

Invoke-DualAgentHarness -Paths $paths -Arguments $argsList
