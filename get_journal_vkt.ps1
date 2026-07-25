$journalDir = "$env:LOCALAPPDATA\Autodesk\Revit\Autodesk Revit 2024\Journals"
$latest = Get-ChildItem -Path $journalDir -Filter *.txt | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $latest.FullName | Select-String -Pattern 'VKT' -Context 5, 20
