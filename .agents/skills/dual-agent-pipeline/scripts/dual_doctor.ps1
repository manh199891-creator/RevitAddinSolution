param(
    [Parameter(Mandatory=$true)][string]$Project
)

. (Join-Path $PSScriptRoot "dual_paths.ps1")
$paths = Get-DualAgentPaths
Invoke-DualAgentHarness -Paths $paths -Arguments @($Project, "doctor")
