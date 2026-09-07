Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectId = 'revit-addin-solution'
$OwnerRelativePath = 'src/Antigravity.DrawBeams'
$LocalOrchestratorRoot = 'E:\chatgpt-local-orchestrator'
$RequestPath = Join-Path $PSScriptRoot '..\results\.publish-request.json'
$ExpectedFiles = @(
    'Antigravity.Core.dll',
    'Antigravity.DrawBeams.dll',
    'JetBrains.Annotations.dll',
    'Nice3point.Revit.Extensions.dll',
    'Serilog.dll',
    'Serilog.Sinks.File.dll'
)

function Fail([string]$Code, [string]$Message) {
    throw "[$Code] $Message"
}

function Read-JsonFile([string]$Path, [string]$Code) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Fail $Code "Missing file: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { Fail $Code "Invalid JSON: $Path" }
}

function Assert-UnderRoot([string]$Candidate, [string]$Root, [string]$Code) {
    $candidateFull = [System.IO.Path]::GetFullPath($Candidate).TrimEnd('\')
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    if (-not $candidateFull.StartsWith($rootFull + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail $Code "Path escapes approved root: $candidateFull"
    }
    return $candidateFull
}

function Get-HashUpper([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

if (-not (Test-Path -LiteralPath $RequestPath -PathType Leaf)) {
    Fail 'PUBLISH_REQUEST_MISSING' "Create $RequestPath before publishing."
}

$request = Read-JsonFile $RequestPath 'PUBLISH_REQUEST_INVALID'
if ($request.schemaVersion -ne 1) { Fail 'PUBLISH_REQUEST_INVALID' 'schemaVersion must be 1.' }
if ($request.projectId -ne $ProjectId) { Fail 'PUBLISH_REQUEST_INVALID' "projectId must be $ProjectId." }
if ($request.workflowId -notmatch '^WF-[A-Za-z0-9-]{8,100}$') { Fail 'PUBLISH_REQUEST_INVALID' 'workflowId is invalid.' }
if ($request.taskId -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$') { Fail 'PUBLISH_REQUEST_INVALID' 'taskId is invalid.' }
if ($null -eq $request.expectedSha256) { Fail 'PUBLISH_REQUEST_INVALID' 'expectedSha256 is required.' }

$workflowId = [string]$request.workflowId
$taskId = [string]$request.taskId
$jobId = "$workflowId-$taskId"
$jobsRoot = Join-Path $LocalOrchestratorRoot 'apps\bridge\runtime\jobs'
$worktreesRoot = Join-Path $LocalOrchestratorRoot 'apps\bridge\runtime\worktrees'
$jobRoot = Assert-UnderRoot (Join-Path $jobsRoot $jobId) $jobsRoot 'PUBLISH_JOB_PATH_INVALID'
$jobStatePath = Join-Path $jobRoot 'job-state.json'
$reviewPath = Join-Path $jobRoot 'review-package.json'

$job = Read-JsonFile $jobStatePath 'PUBLISH_JOB_STATE_INVALID'
if ($job.projectId -ne $ProjectId -or $job.state -ne 'COMPLETED' -or $job.executionStatus -ne 'COMPLETED' -or [int]$job.exitCode -ne 0) {
    Fail 'PUBLISH_JOB_NOT_VERIFIED' 'Job must be COMPLETED with completed execution and exitCode=0.'
}
if ($job.metadata.workflowId -ne $workflowId -or $job.metadata.workflowTaskId -ne $taskId) {
    Fail 'PUBLISH_JOB_IDENTITY_MISMATCH' 'Job workflow/task identity does not match the request.'
}

$review = Read-JsonFile $reviewPath 'PUBLISH_REVIEW_INVALID'
if ($review.status -ne 'PASS' -or $review.finalReviewStatus -ne 'PASS' -or
    $review.execution.executionStatus -ne 'COMPLETED' -or [int]$review.execution.exitCode -ne 0 -or
    $review.verification.tests.status -ne 'PASS') {
    Fail 'PUBLISH_REVIEW_NOT_PASS' 'Review package and tests must be PASS.'
}

$worktreePath = Assert-UnderRoot ([string]$job.worktreePath) $worktreesRoot 'PUBLISH_WORKTREE_INVALID'
$expectedWorktreeLeaf = "$workflowId-$taskId"
if ((Split-Path -Leaf $worktreePath) -ne $expectedWorktreeLeaf) {
    Fail 'PUBLISH_WORKTREE_IDENTITY_MISMATCH' 'worktreePath does not match workflow/task identity.'
}

$sourcePackage = Assert-UnderRoot (Join-Path $worktreePath "$OwnerRelativePath\smoke-tests\results\$workflowId\package") $worktreePath 'PUBLISH_SOURCE_PATH_INVALID'
if (-not (Test-Path -LiteralPath $sourcePackage -PathType Container)) { Fail 'PUBLISH_SOURCE_MISSING' "Missing source package: $sourcePackage" }

$sourceNames = @(Get-ChildItem -LiteralPath $sourcePackage -File | Select-Object -ExpandProperty Name | Sort-Object)
$expectedNames = @($ExpectedFiles | Sort-Object)
if (($sourceNames -join '|') -ne ($expectedNames -join '|')) {
    Fail 'PUBLISH_SOURCE_FILESET_INVALID' 'Source package must contain exactly the approved six DLLs and no extra files.'
}

$hashes = [ordered]@{}
foreach ($name in $ExpectedFiles) {
    $expectedHash = [string]$request.expectedSha256.$name
    if ($expectedHash -notmatch '^[A-Fa-f0-9]{64}$') { Fail 'PUBLISH_REQUEST_INVALID' "Missing or invalid expected SHA-256 for $name." }
    $actual = Get-HashUpper (Join-Path $sourcePackage $name)
    if ($actual -ne $expectedHash.ToUpperInvariant()) { Fail 'PUBLISH_SOURCE_HASH_MISMATCH' "$name does not match the requested verified hash." }
    $hashes[$name] = $actual
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$ownerRoot = Assert-UnderRoot (Join-Path $repoRoot $OwnerRelativePath) $repoRoot 'PUBLISH_OWNER_PATH_INVALID'
$workflowResultRoot = Join-Path $ownerRoot "smoke-tests\results\$workflowId"
$destination = Join-Path $workflowResultRoot 'package'
$receiptPath = Join-Path $workflowResultRoot 'publish-receipt.json'

$status = 'PUBLISHED'
if (Test-Path -LiteralPath $destination -PathType Container) {
    $destNames = @(Get-ChildItem -LiteralPath $destination -File | Select-Object -ExpandProperty Name | Sort-Object)
    if (($destNames -join '|') -ne ($expectedNames -join '|')) { Fail 'PUBLISH_DESTINATION_CONFLICT' 'Existing destination package has a different file set.' }
    foreach ($name in $ExpectedFiles) {
        if ((Get-HashUpper (Join-Path $destination $name)) -ne $hashes[$name]) {
            Fail 'PUBLISH_DESTINATION_CONFLICT' "Existing destination differs for $name; overwrite is forbidden."
        }
    }
    $status = 'ALREADY_PUBLISHED'
} else {
    New-Item -ItemType Directory -Path $workflowResultRoot -Force | Out-Null
    $tempPath = Join-Path $workflowResultRoot ('.package-publish-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        New-Item -ItemType Directory -Path $tempPath | Out-Null
        foreach ($name in $ExpectedFiles) {
            Copy-Item -LiteralPath (Join-Path $sourcePackage $name) -Destination (Join-Path $tempPath $name)
        }
        foreach ($name in $ExpectedFiles) {
            if ((Get-HashUpper (Join-Path $tempPath $name)) -ne $hashes[$name]) {
                Fail 'PUBLISH_TEMP_HASH_MISMATCH' "Temporary copy differs for $name."
            }
        }
        [System.IO.Directory]::Move($tempPath, $destination)
    } finally {
        if (Test-Path -LiteralPath $tempPath -PathType Container) { Remove-Item -LiteralPath $tempPath -Recurse -Force }
    }
}

$receipt = [ordered]@{
    schemaVersion = 1
    projectId = $ProjectId
    owner = $OwnerRelativePath
    workflowId = $workflowId
    taskId = $taskId
    jobId = $jobId
    status = $status
    sourcePackage = $sourcePackage
    destinationPackage = $destination
    publishedAt = (Get-Date).ToUniversalTime().ToString('o')
    sha256 = $hashes
    productionAdvanced = $false
    lkgAdvanced = $false
}
$receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding UTF8

[ordered]@{
    status = $status
    workflowId = $workflowId
    destinationPackage = $destination
    receipt = $receiptPath
    files = $ExpectedFiles.Count
    sha256 = $hashes
} | ConvertTo-Json -Depth 6 -Compress
