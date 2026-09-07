param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function New-ProjectSpec {
    param(
        [string]$Name,
        [string]$Type,
        [string[]]$SmokeChecks,
        [string[]]$EntrypointSources = @(),
        [string]$FocusedTestProject = $null,
        [string]$RuntimeBoundary = $null
    )
    [pscustomobject]@{
        Name = $Name
        Type = $Type
        SmokeChecks = $SmokeChecks
        EntrypointSources = $EntrypointSources
        FocusedTestProject = $FocusedTestProject
        RuntimeBoundary = $RuntimeBoundary
    }
}

$projects = @(
    New-ProjectSpec 'Antigravity.ArchModeling' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('Commands/DrawWallFromCadCommand.cs','Commands/DrawFloorCeilFromCadCommand.cs')
    New-ProjectSpec 'Antigravity.AutoCAD.HatchBridge' 'INTEGRATION_ADAPTER' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_AUTOCAD_INTEGRATION_WHEN_AVAILABLE') @('ExportHatchDefinitionsCommand.cs') $null 'AutoCAD'
    New-ProjectSpec 'Antigravity.AutoDimWalls' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('AutoDimCommand.cs')
    New-ProjectSpec 'Antigravity.AutoFoundation' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('Commands/AutoFoundationCommand.cs') 'tests/Antigravity.Core.Geometry.Tests/Antigravity.Core.Geometry.Tests.csproj'
    New-ProjectSpec 'Antigravity.Autojoin' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('AutoJoinCommand.cs','AutoJoinIntegrationTestCommand.cs')
    New-ProjectSpec 'Antigravity.BIMLink.Core' 'SHARED_CORE' @('S0_STRUCTURAL','S1_BUILD_TEST') @()
    New-ProjectSpec 'Antigravity.BIMLink.Etabs' 'INTEGRATION_ADAPTER' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_ETABS_INTEGRATION_WHEN_AVAILABLE') @('EtabsAdapter.cs') $null 'ETABS'
    New-ProjectSpec 'Antigravity.BIMLink.Revit' 'INTEGRATION_ADAPTER' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_INTEGRATION_WHEN_AVAILABLE') @('RevitExtractor.cs') $null 'Revit'
    New-ProjectSpec 'Antigravity.CadSleevePlacer' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('AppCommand.cs')
    New-ProjectSpec 'Antigravity.CadVoidPlacer' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('AppCommand.cs')
    New-ProjectSpec 'Antigravity.CheckFloorElevation' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('CheckFloorElevationCommand.cs')
    New-ProjectSpec 'Antigravity.Core' 'SHARED_CORE' @('S0_STRUCTURAL','S1_BUILD_TEST') @() 'tests/Antigravity.Core.Geometry.Tests/Antigravity.Core.Geometry.Tests.csproj'
    New-ProjectSpec 'Antigravity.DoorClearance' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('App.cs','Commands/CreateClearanceBoxCommand.cs','Commands/ClashControlCommand.cs')
    New-ProjectSpec 'Antigravity.DrawColumns' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('CreateColumnCommand.cs')
    New-ProjectSpec 'Antigravity.DrawFloors' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('CreateFloorCommand.cs')
    New-ProjectSpec 'Antigravity.DrawWalls' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('CreateWallCommand.cs')
    New-ProjectSpec 'Antigravity.Formwork' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('Commands/AutoFormworkCommand.cs')
    New-ProjectSpec 'Antigravity.Formwork.Core' 'SHARED_CORE' @('S0_STRUCTURAL','S1_BUILD_TEST') @()
    New-ProjectSpec 'Antigravity.HatchPatterns.Contracts' 'SHARED_CONTRACTS' @('S0_STRUCTURAL','S1_BUILD_TEST') @()
    New-ProjectSpec 'Antigravity.HatchPatterns.Core' 'SHARED_CORE' @('S0_STRUCTURAL','S1_BUILD_TEST') @()
    New-ProjectSpec 'Antigravity.HoanThien' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('Commands/HoanThienCommand.cs') 'tests/Antigravity.HoanThien.Tests/Antigravity.HoanThien.Tests.csproj'
    New-ProjectSpec 'Antigravity.IssueManager' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('App.cs','Commands/CmdOpenIssueManager.cs')
    New-ProjectSpec 'Antigravity.IssueManager.Installer' 'INSTALLER_SUPPORT' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_INSTALLER_PACKAGE_SMOKE') @('Program.cs')
    New-ProjectSpec 'Antigravity.LOQN1_Location' 'REVIT_FEATURE_LEGACY_PACKAGING' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('App.cs','Command.cs')
    New-ProjectSpec 'Antigravity.Main' 'REVIT_HOST' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_RIBBON_HOST_STARTUP') @('App.cs')
    New-ProjectSpec 'Antigravity.SharedParamMapper' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('ParamMapperCommand.cs')
    New-ProjectSpec 'Antigravity.TagArranger' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('TagArrangeCommand.cs') 'tests/Antigravity.TagArranger.RevitTests/Antigravity.TagArranger.RevitTests.csproj'
    New-ProjectSpec 'Antigravity.WallMepClash' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('WallMepClashCommand.cs') 'src/Antigravity.WallMepClash.Tests/Antigravity.WallMepClash.Tests.csproj'
    New-ProjectSpec 'Antigravity.WallMepClash.Tests' 'TEST_ONLY' @('S0_STRUCTURAL','S1_TEST_EXECUTION') @() 'src/Antigravity.WallMepClash.Tests/Antigravity.WallMepClash.Tests.csproj'
    New-ProjectSpec 'Antigravity.ZoneSplit' 'REVIT_FEATURE' @('S0_STRUCTURAL','S1_BUILD_TEST','S2_REVIT_LOAD','S3_COMMAND_SAFE_CANCEL','S4_CRITICAL_PATH') @('App.cs','Commands/ZoneProcessCommand.cs','Commands/ExportCommand.cs') 'src/Antigravity.ZoneSplit/tests/ZoneSplit.Tests.csproj'
)

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        if ($Apply) { New-Item -ItemType Directory -Path $Path -Force | Out-Null }
        Write-Host ("[CREATE DIR ] {0}" -f $Path.Substring($root.Length + 1))
    }
}

function Ensure-TextFile([string]$Path, [string]$Content) {
    if (Test-Path -LiteralPath $Path) {
        Write-Host ("[KEEP FILE  ] {0}" -f $Path.Substring($root.Length + 1))
        return
    }
    if ($Apply) {
        $parent = Split-Path -Parent $Path
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
    }
    Write-Host ("[CREATE FILE] {0}" -f $Path.Substring($root.Length + 1))
}

function Get-OutputArtifact([string]$ProjectDir, [string]$ProjectName) {
    $csproj = Join-Path $ProjectDir ($ProjectName + '.csproj')
    $assemblyName = $ProjectName
    $extension = '.dll'
    try {
        [xml]$xml = Get-Content -LiteralPath $csproj -Raw
        $assembly = @($xml.Project.PropertyGroup.AssemblyName | Where-Object { $_ }) | Select-Object -First 1
        if ($assembly) { $assemblyName = [string]$assembly }
        $outputType = @($xml.Project.PropertyGroup.OutputType | Where-Object { $_ }) | Select-Object -First 1
        if ($outputType -and ([string]$outputType -in @('Exe','WinExe'))) { $extension = '.exe' }
    } catch { }
    return ($assemblyName + $extension)
}

function Get-TypeDescription([string]$Type) {
    switch ($Type) {
        'REVIT_FEATURE' { 'Revit production feature add-in' }
        'REVIT_FEATURE_LEGACY_PACKAGING' { 'Revit production feature add-in with legacy packaging that requires separate review' }
        'REVIT_HOST' { 'Revit host/composition root' }
        'SHARED_CORE' { 'Shared/core library' }
        'SHARED_CONTRACTS' { 'Shared contracts library' }
        'INTEGRATION_ADAPTER' { 'Integration/adapter project' }
        'INSTALLER_SUPPORT' { 'Installer/support executable' }
        'TEST_ONLY' { 'Test-only project' }
        default { $Type }
    }
}

$processedProjects = 0
$skippedProjects = 0

Write-Host 'ANTIGRAVITY PROJECT GOVERNANCE INITIALIZER - PHASE 2'
Write-Host ("Root : {0}" -f $root)
Write-Host ("Mode : {0}" -f $(if ($Apply) { 'APPLY' } else { 'PREVIEW' }))
Write-Host ''

foreach ($project in $projects) {
    $projectDir = Join-Path $root ('src\' + $project.Name)
    $csproj = Join-Path $projectDir ($project.Name + '.csproj')
    if (-not (Test-Path -LiteralPath $projectDir) -or -not (Test-Path -LiteralPath $csproj)) {
        Write-Warning ("DEFER {0}: directory or csproj missing" -f $project.Name)
        $skippedProjects++
        continue
    }

    Write-Host ("--- {0} [{1}] ---" -f $project.Name, $project.Type)
    $docsDir = Join-Path $projectDir 'docs'
    $smokeDir = Join-Path $projectDir 'smoke-tests'
    @($docsDir, (Join-Path $docsDir 'plans'), (Join-Path $docsDir 'design'), (Join-Path $docsDir 'acceptance'), (Join-Path $docsDir 'reports'),
      $smokeDir, (Join-Path $smokeDir 'baseline'), (Join-Path $smokeDir 'scripts'), (Join-Path $smokeDir 'fixtures'), (Join-Path $smokeDir 'results')) | ForEach-Object { Ensure-Directory $_ }

    $typeDescription = Get-TypeDescription $project.Type
    $docsReadmeLines = @(
        '# ' + $project.Name + ' Documentation',
        '',
        'Owner project: ' + $project.Name,
        'Classification: ' + $typeDescription,
        '',
        'Canonical project-specific artifacts live here:',
        '',
        '- plans/ - implementation/feature plans.',
        '- design/ - technical design and module decisions.',
        '- acceptance/ - acceptance criteria and manual acceptance procedures.',
        '- reports/ - implementation, review, migration and closure reports.',
        '',
        'Repository-level docs/ is reserved for genuinely cross-solution concerns. .ai-bridge/ is transient handoff state only.'
    )
    $docsReadme = ($docsReadmeLines -join [Environment]::NewLine) + [Environment]::NewLine
    Ensure-TextFile (Join-Path $docsDir 'README.md') $docsReadme

    foreach ($kind in @('plans','design','acceptance','reports')) {
        $title = switch ($kind) { 'plans' {'Plans'} 'design' {'Technical Design'} 'acceptance' {'Acceptance'} 'reports' {'Reports'} }
        Ensure-TextFile (Join-Path $docsDir ($kind + '\README.md')) ("# {0} {1}`r`n`r`nCanonical artifacts owned by `{0}` for this category belong in this folder.`r`n" -f $project.Name, $title)
    }

    $outputArtifact = Get-OutputArtifact $projectDir $project.Name
    $entrypoints = @($project.EntrypointSources)
    $checks = @($project.SmokeChecks)
    $manifest = [ordered]@{
        schemaVersion = 1
        project = $project.Name
        classification = $project.Type
        projectFile = ('src/{0}/{0}.csproj' -f $project.Name)
        assembly = $outputArtifact
        entrypointSources = $entrypoints
        focusedTestProject = $project.FocusedTestProject
        runtimeBoundary = $project.RuntimeBoundary
        smokeChecks = $checks
        timeoutSeconds = 600
        status = 'PENDING_CAPTURE'
    } | ConvertTo-Json -Depth 5
    Ensure-TextFile (Join-Path $smokeDir 'smoke-manifest.json') ($manifest + "`r`n")

    $smokeReadme = @"
# $($project.Name) Smoke Contract

Classification: $typeDescription

This contract is intentionally truthful: checks are requirements, not PASS claims. A check becomes PASS only after actual execution evidence exists.

Required checks:
$(($checks | ForEach-Object { '- `' + $_ + '`' }) -join "`r`n")

Last-known-good identity is stored in `baseline/last-known-good.json`. Until verified, it remains `PENDING_CAPTURE` or `PENDING_REVALIDATION`.
Generated smoke output belongs in `results/` and is Git ignored.
"@
    Ensure-TextFile (Join-Path $smokeDir 'README.md') $smokeReadme

    $lkg = [ordered]@{
        schemaVersion = 1
        project = $project.Name
        gitCommit = $null
        gitRef = $null
        branch = $null
        releaseId = $null
        assemblyVersion = $null
        verifiedAt = $null
        smokeStatus = 'PENDING_CAPTURE'
        note = 'No verified project-local rollback baseline has been captured by Phase 2. Do not promote this status without actual verification evidence.'
    } | ConvertTo-Json -Depth 4
    Ensure-TextFile (Join-Path $smokeDir 'baseline\last-known-good.json') ($lkg + "`r`n")

    $expected = @"
# $($project.Name) Expected Behavior Baseline

Status: PENDING_CAPTURE

Phase 2 creates the ownership contract only; it does not assert runtime behavior has been revalidated.

## Invariants during governance migration

- Production source behavior is unchanged.
- Namespace, assembly name and project references are unchanged.
- Existing runtime/command/integration behavior remains the reference until a project-specific smoke baseline is captured.
- Any future destructive refactor must preserve a verified rollback identity before promotion.

## Project classification

$typeDescription

Required smoke checks are defined in `../smoke-manifest.json` and must be executed according to project type rather than copied blindly from another add-in.
"@
    Ensure-TextFile (Join-Path $smokeDir 'baseline\expected-behavior.md') $expected
    Ensure-TextFile (Join-Path $smokeDir 'scripts\README.md') ("# {0} Smoke Scripts`r`n`r`nPlace repeatable project-specific smoke automation here. Do not copy another project's command/test paths without verification.`r`n" -f $project.Name)
    Ensure-TextFile (Join-Path $smokeDir 'fixtures\README.md') ("# {0} Smoke Fixtures`r`n`r`nStore small project-specific fixture metadata here. Reference large external fixtures instead of duplicating binaries in Git.`r`n" -f $project.Name)
    Ensure-TextFile (Join-Path $smokeDir 'results\.gitkeep') ""
    $processedProjects++
}

Write-Host ''
Write-Host 'REFERENCE VALIDATION'
$drawBeams = Join-Path $root 'src\Antigravity.DrawBeams'
$drawRequired = @(
    'docs\plans\2026-08-22-drawbeams-recognition-engine-v14.md',
    'smoke-tests\README.md',
    'smoke-tests\smoke-manifest.json',
    'smoke-tests\baseline\last-known-good.json',
    'smoke-tests\baseline\expected-behavior.md'
)
foreach ($relative in $drawRequired) {
    $full = Join-Path $drawBeams $relative
    if (Test-Path -LiteralPath $full) { Write-Host ("[DRAWBEAMS OK] {0}" -f $relative) }
    else { Write-Warning ("DRAWBEAMS missing reference artifact: {0}" -f $relative) }
}

$emptyInstaller = Join-Path $root 'src\Antigravity.Installer'
if ((Test-Path -LiteralPath $emptyInstaller) -and -not (Get-ChildItem -LiteralPath $emptyInstaller -Filter '*.csproj' -File -ErrorAction SilentlyContinue)) {
    Write-Host '[DEFERRED] Antigravity.Installer - empty directory/no csproj; no fake project smoke contract created.'
}

Write-Host ''
Write-Host ("Projects processed: {0}; deferred/missing: {1}" -f $processedProjects, $skippedProjects)
if (-not $Apply) { Write-Host 'Preview only. Re-run with -Apply to create missing governance artifacts.' }
else { Write-Host 'Phase 2 structure creation complete. Existing project-local files were preserved.' }
