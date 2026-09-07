param(
    [string]$Pattern,
    [int]$ContextBefore = 5,
    [int]$ContextAfter = 20,
    [string]$RevitVersion = '2024',
    [switch]$PathOnly
)

$ErrorActionPreference = 'Stop'
$journalDir = Join-Path $env:LOCALAPPDATA ("Autodesk\Revit\Autodesk Revit {0}\Journals" -f $RevitVersion)

if (-not (Test-Path -LiteralPath $journalDir -PathType Container)) {
    throw "Revit journal directory was not found: $journalDir"
}

$latest = Get-ChildItem -LiteralPath $journalDir -File -Filter '*.txt' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $latest) {
    throw "No Revit journal file was found in: $journalDir"
}

Write-Output ("Journal: {0}" -f $latest.FullName)
Write-Output ("LastWriteTime: {0:o}" -f $latest.LastWriteTime)

if ($PathOnly -or [string]::IsNullOrWhiteSpace($Pattern)) {
    return
}

Select-String -LiteralPath $latest.FullName -Pattern $Pattern -Context $ContextBefore, $ContextAfter
