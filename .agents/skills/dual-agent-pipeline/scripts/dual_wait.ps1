param(
    [Parameter(Mandatory=$true)][string]$Project,
    [ValidateRange(10, 3600)][int]$TimeoutSeconds = 300,
    [ValidateRange(1, 60)][int]$PollSeconds = 5
)

$statusScript = Join-Path $PSScriptRoot "dual_status.ps1"
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $jsonText = & $statusScript -Project $Project -AsJson
    $state = $jsonText | ConvertFrom-Json
    if ($state.terminal) {
        Write-Output $jsonText
        if ($state.status -in @("PASS", "PASS_WITH_ADVISORIES", "SMOKE_PASS")) { exit 0 }
        exit 2
    }
    Start-Sleep -Seconds $PollSeconds
} while ([DateTime]::UtcNow -lt $deadline)

Write-Output (@{ project = $Project; status = "WAIT_TIMEOUT"; terminal = $true } | ConvertTo-Json -Compress)
exit 3
