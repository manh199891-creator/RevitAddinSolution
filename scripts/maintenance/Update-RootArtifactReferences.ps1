param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$replacements = [ordered]@{
    '_build_check.ps1' = 'scripts/verification/Build-Solution.ps1'
    'DeployToRevit.ps1' = 'scripts/deploy/Deploy-ToRevit.ps1'
    'DeployToRevit_Universal.ps1' = 'scripts/deploy/Deploy-ToRevit-Universal.ps1'
    'PackageForDeployment.ps1' = 'scripts/deploy/Package-ForDeployment.ps1'
    'run_tag_arranger_test.ps1' = 'src/Antigravity.TagArranger/smoke-tests/scripts/Run-TagArrangerTest.ps1'
}

$targets = @()
$agents = Join-Path $root '.agents\AGENTS.md'
if (Test-Path -LiteralPath $agents -PathType Leaf) { $targets += Get-Item -LiteralPath $agents }

foreach ($folder in @('docs','src')) {
    $full = Join-Path $root $folder
    if (Test-Path -LiteralPath $full -PathType Container) {
        $targets += Get-ChildItem -LiteralPath $full -File -Filter '*.md' -Recurse -ErrorAction SilentlyContinue
    }
}

$changed = 0
foreach ($file in ($targets | Sort-Object FullName -Unique)) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $updated = $content
    foreach ($entry in $replacements.GetEnumerator()) {
        $updated = $updated.Replace($entry.Key, $entry.Value)
    }

    if ($updated -ne $content) {
        $relative = $file.FullName.Substring($root.Length).TrimStart('\')
        Write-Host ("[{0}] {1}" -f $(if ($Apply) { 'UPDATE' } else { 'WOULD UPDATE' }), $relative) -ForegroundColor Yellow
        if ($Apply) {
            [System.IO.File]::WriteAllText($file.FullName, $updated, $utf8NoBom)
        }
        $changed++
    }
}

Write-Host "Reference files changed: $changed" -ForegroundColor Green
