$journalDir = "$env:LOCALAPPDATA\Autodesk\Revit\Autodesk Revit 2024\Journals"
$latest = Get-ChildItem -Path $journalDir -Filter *.txt | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "Processing: $($latest.FullName)"
Get-Content $latest.FullName | Select-String -Pattern 'Antigravity' -Context 5, 20
