[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$Destination = 'E:\Antigravity\RevitAddinSolution-vNext',
    [string]$Branch = 'refactor/vnext-architecture'
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not (Test-Path -LiteralPath (Join-Path $source 'Antigravity.sln'))) {
    throw "Safety guard failed: Antigravity.sln was not found in $source"
}

$destinationFull = [System.IO.Path]::GetFullPath($Destination).TrimEnd('\')
$sourceFull = [System.IO.Path]::GetFullPath($source).TrimEnd('\')
if ($destinationFull.Equals($sourceFull, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination must be different from the source repository.'
}
if ($destinationFull.StartsWith($sourceFull + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination must not be inside the source repository.'
}

$head = (& git -C $source rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $head) { throw 'Unable to read source HEAD.' }
$currentBranch = (& git -C $source branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or -not $currentBranch) { throw 'Source repository is not on a named branch.' }
$origin = (& git -C $source remote get-url origin).Trim()
if ($LASTEXITCODE -ne 0 -or -not $origin) { throw 'Source origin remote is missing.' }

Write-Host 'ANTIGRAVITY VNEXT CLONE PREPARATION' -ForegroundColor Cyan
Write-Host "Source      : $sourceFull" -ForegroundColor DarkGray
Write-Host "Source HEAD : $head" -ForegroundColor DarkGray
Write-Host "Source branch: $currentBranch" -ForegroundColor DarkGray
Write-Host "Origin      : $origin" -ForegroundColor DarkGray
Write-Host "Destination : $destinationFull" -ForegroundColor DarkGray
Write-Host "vNext branch: $Branch" -ForegroundColor DarkGray
Write-Host ''
Write-Host 'The clone will preserve:' -ForegroundColor Green
Write-Host '  - full Git history and current local HEAD' -ForegroundColor DarkGray
Write-Host '  - current source/WIP working-tree files' -ForegroundColor DarkGray
Write-Host '  - untracked TagArranger plans and scratch diagnostics' -ForegroundColor DarkGray
Write-Host '  - local TestFiles and other non-generated fixtures' -ForegroundColor DarkGray
Write-Host 'The clone will exclude:' -ForegroundColor Yellow
Write-Host '  - .ai-bridge session/handoff state' -ForegroundColor DarkGray
Write-Host '  - .git from the file-overlay step (clone owns its own .git)' -ForegroundColor DarkGray
Write-Host '  - bin/obj/TestResults/.vs/__pycache__/.pytest_cache/.tmp' -ForegroundColor DarkGray
Write-Host '  - archived _packages/_Installer/_addin_manager and source-code legacy state' -ForegroundColor DarkGray
Write-Host '  - *.pyc/*.pyo and temporary logs' -ForegroundColor DarkGray

if (-not $Apply) {
    Write-Host ''
    if (Test-Path -LiteralPath $destinationFull) {
        Write-Host '[BLOCKED] Destination already exists.' -ForegroundColor Red
    } else {
        Write-Host '[READY] Preview only. Destination does not exist.' -ForegroundColor Green
    }
    Write-Host 'Re-run with -Apply to create the independent vNext repository.' -ForegroundColor Cyan
    exit 0
}

if (Test-Path -LiteralPath $destinationFull) {
    throw "Destination already exists: $destinationFull"
}

$destinationParent = Split-Path -Parent $destinationFull
if (-not (Test-Path -LiteralPath $destinationParent)) {
    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
}

Write-Host ''
Write-Host '[1/6] Cloning Git repository without hardlinks...' -ForegroundColor Cyan
& git clone --no-hardlinks --branch $currentBranch $sourceFull $destinationFull
if ($LASTEXITCODE -ne 0) { throw 'git clone failed.' }

Write-Host '[2/6] Restoring authoritative origin remote...' -ForegroundColor Cyan
& git -C $destinationFull remote set-url origin $origin
if ($LASTEXITCODE -ne 0) { throw 'Failed to restore origin URL.' }

Write-Host '[3/6] Creating dedicated vNext branch...' -ForegroundColor Cyan
& git -C $destinationFull switch -c $Branch
if ($LASTEXITCODE -ne 0) { throw "Failed to create branch $Branch." }

Write-Host '[4/6] Overlaying current cleaned working tree...' -ForegroundColor Cyan
$robocopyArgs = @(
    $sourceFull,
    $destinationFull,
    '/E',
    '/COPY:DAT',
    '/DCOPY:DAT',
    '/R:1',
    '/W:1',
    '/NFL',
    '/NDL',
    '/NJH',
    '/NJS',
    '/NP',
    '/XD',
    '.git',
    '.ai-bridge',
    '.vs',
    'bin',
    'obj',
    'TestResults',
    '__pycache__',
    '.pytest_cache',
    '.tmp',
    'source-code',
    '_packages',
    '_Installer',
    '_addin_manager',
    '/XF',
    '*.pyc',
    '*.pyo',
    '*.tmp'
)
& robocopy @robocopyArgs | Out-Null
$robocopyCode = $LASTEXITCODE
if ($robocopyCode -ge 8) {
    throw "robocopy failed with exit code $robocopyCode"
}

Write-Host '[5/6] Recording immutable vNext baseline metadata...' -ForegroundColor Cyan
$reportDir = Join-Path $destinationFull 'docs\reports\repository'
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir 'vnext-baseline-2026-08-22.md'
$report = @"
# RevitAddinSolution vNext Baseline

Created: $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK')

- Source workspace: `$sourceFull`
- Source branch: `$currentBranch`
- Source HEAD: `$head`
- Authoritative origin: `$origin`
- vNext workspace: `$destinationFull`
- vNext branch: `$Branch`

## Migration policy

The vNext workspace was created with `git clone --no-hardlinks`, then overlaid with the current cleaned BLUE working tree so important local WIP and fixtures are preserved without sharing Git object files through hardlinks.

Excluded from the overlay: `.ai-bridge`, `.git`, Visual Studio/build/test caches, Python bytecode caches, `.tmp`, the legacy `source-code` folder, and archived installer/package directories.

No production deployment was performed by this operation.
"@
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($reportPath, $report, $utf8NoBom)

Write-Host '[6/6] Verifying clone identity...' -ForegroundColor Cyan
$destHead = (& git -C $destinationFull rev-parse HEAD).Trim()
$destBranch = (& git -C $destinationFull branch --show-current).Trim()
$destOrigin = (& git -C $destinationFull remote get-url origin).Trim()
if ($destHead -ne $head) { throw "HEAD mismatch: source=$head destination=$destHead" }
if ($destBranch -ne $Branch) { throw "Branch mismatch: expected=$Branch actual=$destBranch" }
if ($destOrigin -ne $origin) { throw 'Origin URL mismatch after clone.' }
if (-not (Test-Path -LiteralPath (Join-Path $destinationFull 'Antigravity.sln'))) { throw 'Destination Antigravity.sln missing.' }
if (Test-Path -LiteralPath (Join-Path $destinationFull '.ai-bridge')) { throw '.ai-bridge should not have been overlaid into vNext.' }

Write-Host ''
Write-Host 'vNext clone complete.' -ForegroundColor Green
Write-Host "Workspace: $destinationFull" -ForegroundColor Green
Write-Host "Branch   : $destBranch" -ForegroundColor Green
Write-Host "HEAD     : $destHead" -ForegroundColor Green
Write-Host "Baseline : $reportPath" -ForegroundColor Green
Write-Host 'No production deployment was performed.' -ForegroundColor Green
