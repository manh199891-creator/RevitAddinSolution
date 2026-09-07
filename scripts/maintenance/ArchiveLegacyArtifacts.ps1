[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$ArchiveRoot = 'E:\Antigravity\RevitAddinArchive',
    [string]$RecoverBatch
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Antigravity.sln'))) {
    throw "Safety guard failed: Antigravity.sln was not found in $repoRoot"
}

$entries = @(
    '_addin_manager',
    '_Installer',
    '_packages',
    'RevitAddinSolution.7z',
    'Autojoin_Installer.zip',
    'Vilaiviet_RevitAddin_V13.zip',
    'VILAIVIET.exe',
    'VILAIVIET_Installer.exe'
)

function Get-EntryRecord {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$FullPath
    )

    if (Test-Path -LiteralPath $FullPath -PathType Leaf) {
        $item = Get-Item -LiteralPath $FullPath
        $hash = Get-FileHash -LiteralPath $FullPath -Algorithm SHA256
        return [pscustomobject]@{
            Path = $RelativePath
            Kind = 'File'
            FileCount = 1
            Bytes = [int64]$item.Length
            Sha256 = $hash.Hash
        }
    }

    $files = @(Get-ChildItem -LiteralPath $FullPath -File -Recurse -Force -ErrorAction SilentlyContinue)
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $bytes) { $bytes = 0 }
    return [pscustomobject]@{
        Path = $RelativePath
        Kind = 'Directory'
        FileCount = $files.Count
        Bytes = [int64]$bytes
        Sha256 = $null
    }
}

function Write-ArchiveManifest {
    param(
        [Parameter(Mandatory = $true)][string]$BatchRoot,
        [Parameter(Mandatory = $true)][object[]]$Records,
        [Parameter(Mandatory = $true)][string]$Mode
    )

    $manifest = [pscustomobject]@{
        SchemaVersion = 1
        CreatedAt = (Get-Date).ToString('o')
        Repository = $repoRoot
        ArchiveBatch = $BatchRoot
        Mode = $Mode
        Entries = $Records
    }

    $manifestPath = Join-Path $BatchRoot 'archive-manifest.json'
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $manifestPath,
        ($manifest | ConvertTo-Json -Depth 6),
        $utf8NoBom
    )
    return $manifestPath
}

Write-Host 'ANTIGRAVITY LEGACY ARTIFACT ARCHIVE - BATCH 2' -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor DarkGray
Write-Host 'Source code, tests, fixtures, docs, .brain and LOQN1 legacy DLLs are NOT in this allowlist.' -ForegroundColor DarkGray

if ($RecoverBatch) {
    $archiveRootFull = [System.IO.Path]::GetFullPath($ArchiveRoot).TrimEnd('\')
    $batchRoot = [System.IO.Path]::GetFullPath($RecoverBatch).TrimEnd('\')
    if (-not $batchRoot.StartsWith($archiveRootFull + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Recovery safety guard failed: batch must be inside $archiveRootFull"
    }
    if (-not (Test-Path -LiteralPath $batchRoot -PathType Container)) {
        throw "Recovery batch was not found: $batchRoot"
    }

    Write-Host "Recovery batch: $batchRoot" -ForegroundColor DarkGray
    $records = @()
    foreach ($relative in $entries) {
        $archived = Join-Path $batchRoot $relative
        if (-not (Test-Path -LiteralPath $archived)) {
            Write-Host "[RECOVERY MISSING] $relative" -ForegroundColor Red
            continue
        }
        $record = Get-EntryRecord -RelativePath $relative -FullPath $archived
        $records += $record
        Write-Host ("[RECOVER] {0} | files={1} | bytes={2}" -f $relative, $record.FileCount, $record.Bytes) -ForegroundColor Yellow
    }

    if ($records.Count -eq 0) {
        throw 'Recovery failed: no allowlisted artifacts were found in the archive batch.'
    }

    $manifestPath = Write-ArchiveManifest -BatchRoot $batchRoot -Records $records -Mode 'RECOVERED_AFTER_MOVE'
    Write-Host ''
    Write-Host "Batch 2 manifest recovered: $manifestPath" -ForegroundColor Green
    Write-Host "Recovered entries: $($records.Count) / $($entries.Count)" -ForegroundColor Green
    exit 0
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$batchRoot = Join-Path $ArchiveRoot "RevitAddinSolution-$stamp"
Write-Host "Archive target: $batchRoot" -ForegroundColor DarkGray
Write-Host ''

$records = @()
foreach ($relative in $entries) {
    $source = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $source)) {
        Write-Host "[SKIP MISSING] $relative" -ForegroundColor DarkGray
        continue
    }

    $record = Get-EntryRecord -RelativePath $relative -FullPath $source
    $records += $record
    Write-Host ("[{0}] {1} | files={2} | bytes={3}" -f ($(if ($Apply) { 'ARCHIVE' } else { 'WOULD ARCHIVE' })), $relative, $record.FileCount, $record.Bytes) -ForegroundColor Yellow
}

if (-not $Apply) {
    Write-Host ''
    Write-Host 'Preview only. Re-run with -Apply to move the allowlisted artifacts.' -ForegroundColor Cyan
    exit 0
}

if ($records.Count -eq 0) {
    throw 'Nothing to archive. If a previous run moved the files but failed before writing the manifest, use -RecoverBatch with that existing archive path.'
}

New-Item -ItemType Directory -Path $batchRoot -Force | Out-Null

foreach ($record in $records) {
    $source = Join-Path $repoRoot $record.Path
    $destination = Join-Path $batchRoot $record.Path
    $destinationParent = Split-Path -Parent $destination
    if ($destinationParent -and -not (Test-Path -LiteralPath $destinationParent)) {
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
    }
    if (Test-Path -LiteralPath $destination) {
        throw "Archive collision: $destination already exists. Nothing will be overwritten."
    }
    Move-Item -LiteralPath $source -Destination $destination
    if ((Test-Path -LiteralPath $source) -or -not (Test-Path -LiteralPath $destination)) {
        throw "Archive verification failed for $($record.Path)"
    }
}

$manifestPath = Write-ArchiveManifest -BatchRoot $batchRoot -Records $records -Mode 'MOVE_PRESERVE'

Write-Host ''
Write-Host "Batch 2 archive complete: $batchRoot" -ForegroundColor Green
Write-Host "Manifest: $manifestPath" -ForegroundColor Green
Write-Host "Archived entries: $($records.Count) / $($entries.Count)" -ForegroundColor Green
Write-Host 'No source files were deleted.' -ForegroundColor Green
