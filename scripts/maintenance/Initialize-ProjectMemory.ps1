param(
    [switch]$Apply,
    [switch]$ForceRefresh
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src = Join-Path $root 'src'
$reviewDate = '2026-08-23'
$managedMarker = '<!-- Managed-By: Initialize-ProjectMemory.ps1 -->'

function Write-ManagedFile([string]$Path, [string]$Content) {
    $relative = $Path.Substring($root.Length + 1)
    if (Test-Path -LiteralPath $Path) {
        if (-not $ForceRefresh) {
            Write-Host ("[KEEP FILE  ] {0}" -f $relative)
            return
        }
        Write-Host ("[REFRESH    ] {0}" -f $relative)
    } else {
        Write-Host ("[CREATE FILE] {0}" -f $relative)
    }

    if ($Apply) {
        $parent = Split-Path -Parent $Path
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
    }
}

function Get-FallbackType([string]$ProjectName, [bool]$HasProject) {
    if (-not $HasProject) { return 'DEFERRED_NO_CSPROJ' }
    switch ($ProjectName) {
        'Antigravity.DrawBeams' { return 'REVIT_FEATURE' }
        default { return 'UNCLASSIFIED_PROJECT' }
    }
}

function Get-Purpose([string]$Type, [string]$RuntimeBoundary) {
    switch ($Type) {
        'REVIT_FEATURE' { return 'Owns a Revit production feature and its durable engineering history.' }
        'REVIT_FEATURE_LEGACY_PACKAGING' { return 'Owns a Revit production feature; packaging identity requires separate legacy review.' }
        'REVIT_HOST' { return 'Composes the Revit host/ribbon and coordinates loading of Antigravity feature modules.' }
        'SHARED_CORE' { return 'Provides reusable shared/core behavior consumed by other Antigravity projects.' }
        'SHARED_CONTRACTS' { return 'Provides shared contracts/types consumed across related projects.' }
        'INTEGRATION_ADAPTER' { return ("Owns the {0} integration boundary and adapter-specific engineering history." -f $(if ($RuntimeBoundary) { $RuntimeBoundary } else { 'external-system' })) }
        'INSTALLER_SUPPORT' { return 'Owns installer/support packaging behavior rather than a Revit feature command.' }
        'TEST_ONLY' { return 'Owns test-only verification code; it is not a production Revit feature.' }
        'DEFERRED_NO_CSPROJ' { return 'Reserved/deferred source directory with no current .csproj; do not treat it as a buildable project until classified.' }
        default { return 'Project is not yet semantically classified beyond its current source identity.' }
    }
}

function To-RelativeForward([string]$FullPath) {
    return $FullPath.Substring($root.Length + 1).Replace('\','/')
}

function Get-PlanFiles([string]$ProjectDir) {
    $plansDir = Join-Path $ProjectDir 'docs\plans'
    if (-not (Test-Path -LiteralPath $plansDir)) { return @() }
    return @(Get-ChildItem -LiteralPath $plansDir -File -Filter '*.md' | Where-Object { $_.Name -notin @('README.md','ROADMAP.md') } | Sort-Object Name)
}

function Get-LatestReport([string]$ProjectDir) {
    $reportsDir = Join-Path $ProjectDir 'docs\reports'
    if (-not (Test-Path -LiteralPath $reportsDir)) { return $null }
    return Get-ChildItem -LiteralPath $reportsDir -File -Filter '*.md' | Where-Object { $_.Name -ne 'README.md' } | Sort-Object Name -Descending | Select-Object -First 1
}

function Get-SmokeStatus([string]$ProjectDir) {
    $path = Join-Path $ProjectDir 'smoke-tests\baseline\last-known-good.json'
    if (-not (Test-Path -LiteralPath $path)) { return 'NOT_APPLICABLE_OR_NOT_CREATED' }
    try {
        $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($json.smokeStatus) { return [string]$json.smokeStatus }
        if ($json.status) { return [string]$json.status }
    } catch { return 'INVALID_JSON' }
    return 'UNKNOWN'
}

function Get-Activity([string]$ProjectName, [string]$Type, [object[]]$Plans) {
    if ($Type -eq 'DEFERRED_NO_CSPROJ') { return 'DEFERRED' }
    if ($ProjectName -eq 'Antigravity.DrawBeams') { return 'ACTIVE_ROADMAP' }
    if ($Plans.Count -gt 0) { return 'PLAN_HISTORY_PRESENT_NO_ACTIVE_DECLARED' }
    return 'NO_ACTIVE_PLAN_RECORDED'
}

function Add-BulletLines([System.Collections.Generic.List[string]]$Lines, [object[]]$Items, [string]$EmptyText) {
    if ($Items.Count -eq 0) {
        $Lines.Add('- ' + $EmptyText)
        return
    }
    foreach ($item in $Items) { $Lines.Add('- ' + [string]$item) }
}

$rows = New-Object System.Collections.Generic.List[object]
$projectDirs = Get-ChildItem -LiteralPath $src -Directory | Where-Object { $_.Name -like 'Antigravity.*' } | Sort-Object Name

Write-Host 'PROJECT MEMORY INITIALIZER'
Write-Host ("Root : {0}" -f $root)
Write-Host ("Mode : {0}" -f $(if ($Apply) { 'APPLY' } else { 'PREVIEW' }))

foreach ($dir in $projectDirs) {
    $csproj = Get-ChildItem -LiteralPath $dir.FullName -File -Filter '*.csproj' | Where-Object { $_.DirectoryName -eq $dir.FullName } | Select-Object -First 1
    $hasProject = $null -ne $csproj
    $manifestPath = Join-Path $dir.FullName 'smoke-tests\smoke-manifest.json'
    $manifest = $null
    if (Test-Path -LiteralPath $manifestPath) {
        try { $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json } catch { }
    }

    $type = if ($manifest -and $manifest.classification) { [string]$manifest.classification } else { Get-FallbackType $dir.Name $hasProject }
    $runtimeBoundary = if ($manifest -and $manifest.runtimeBoundary) { [string]$manifest.runtimeBoundary } else { '' }
    $entrypoints = @()
    if ($manifest -and $manifest.entrypointSources) { $entrypoints = @($manifest.entrypointSources) }
    elseif ($manifest -and $manifest.entrypoints) { $entrypoints = @($manifest.entrypoints) }
    $dependencies = if ($manifest -and $manifest.requiredModules) { @($manifest.requiredModules) } else { @() }
    $focusedTest = if ($manifest -and $manifest.focusedTestProject) { [string]$manifest.focusedTestProject } else { '' }
    $assembly = if ($manifest -and $manifest.assembly) { [string]$manifest.assembly } else { '' }
    $plans = @(Get-PlanFiles $dir.FullName)
    $latestReport = Get-LatestReport $dir.FullName
    $smokeStatus = Get-SmokeStatus $dir.FullName
    $activity = Get-Activity $dir.Name $type $plans
    $purpose = Get-Purpose $type $runtimeBoundary
    $projectFile = if ($csproj) { To-RelativeForward $csproj.FullName } else { 'NONE' }
    $latestReportText = if ($latestReport) { 'src/' + $dir.Name + '/docs/reports/' + $latestReport.Name } else { 'NONE' }
    $testText = if ($focusedTest) { $focusedTest } else { 'NONE_DECLARED' }
    $assemblyText = if ($assembly) { $assembly } else { 'NONE_DECLARED' }
    $boundaryText = if ($runtimeBoundary) { $runtimeBoundary } else { 'NONE_DECLARED' }

    $projectLines = New-Object System.Collections.Generic.List[string]
    $projectLines.Add($managedMarker)
    $projectLines.Add('# ' + $dir.Name)
    $projectLines.Add('')
    $projectLines.Add('Classification: ' + $type)
    $projectLines.Add('Activity: ' + $activity)
    $projectLines.Add('Last reviewed: ' + $reviewDate)
    $projectLines.Add('')
    $projectLines.Add('## Purpose')
    $projectLines.Add('')
    $projectLines.Add($purpose)
    $projectLines.Add('')
    $projectLines.Add('## Agent resume entrypoints')
    $projectLines.Add('')
    $projectLines.Add('Read in this order before planning or production edits:')
    $projectLines.Add('')
    $projectLines.Add('1. Repository registry: docs/projects/PROJECTS.md')
    $projectLines.Add('2. This file: src/' + $dir.Name + '/PROJECT.md')
    $projectLines.Add('3. Project roadmap: src/' + $dir.Name + '/docs/plans/ROADMAP.md')
    $projectLines.Add('4. Active/detailed plan linked from the roadmap, if one is explicitly active')
    $projectLines.Add('5. Smoke contract: src/' + $dir.Name + '/smoke-tests/smoke-manifest.json when applicable')
    $projectLines.Add('6. Last-known-good: src/' + $dir.Name + '/smoke-tests/baseline/last-known-good.json when applicable')
    $projectLines.Add('7. Latest project report: ' + $latestReportText)
    $projectLines.Add('')
    $projectLines.Add('## Project identity')
    $projectLines.Add('')
    $projectLines.Add('- Project file: ' + $projectFile)
    $projectLines.Add('- Output artifact: ' + $assemblyText)
    $projectLines.Add('- Focused test project: ' + $testText)
    $projectLines.Add('- Runtime boundary: ' + $boundaryText)
    $projectLines.Add('')
    $projectLines.Add('### Declared entrypoints')
    $projectLines.Add('')
    Add-BulletLines $projectLines $entrypoints 'No executable entrypoint declared in the smoke manifest.'
    $projectLines.Add('')
    $projectLines.Add('### Declared dependencies')
    $projectLines.Add('')
    Add-BulletLines $projectLines $dependencies 'No requiredModules list declared in the smoke manifest.'
    $projectLines.Add('')
    $projectLines.Add('## Durable plan inventory')
    $projectLines.Add('')
    if ($plans.Count -eq 0) { $projectLines.Add('- No durable implementation plan recorded yet.') }
    else { foreach ($plan in $plans) { $projectLines.Add('- docs/plans/' + $plan.Name) } }
    $projectLines.Add('')
    $projectLines.Add('## Current verification state')
    $projectLines.Add('')
    $projectLines.Add('- Smoke / last-known-good status: ' + $smokeStatus)
    $projectLines.Add('- A PENDING_* state is not a PASS claim.')
    $projectLines.Add('- Manual Revit/integration acceptance requires actual evidence before promotion.')
    $projectLines.Add('')
    $projectLines.Add('## Resume checklist')
    $projectLines.Add('')
    $projectLines.Add('1. Read docs/plans/ROADMAP.md; do not infer current work from chat history alone.')
    $projectLines.Add('2. Read the latest relevant plan/design/report before touching source.')
    $projectLines.Add('3. Inspect the current worktree and protect unrelated dirty work.')
    $projectLines.Add('4. Revalidate S0/S1 before destructive/refactor work; run higher smoke levels when required.')
    $projectLines.Add('5. Preserve prior last-known-good identity until the replacement is actually verified.')
    $projectLines.Add('6. Keep durable project artifacts local; runtime mirrors remain transient.')
    $projectLines.Add('')
    $projectLines.Add('## Maintenance rule')
    $projectLines.Add('')
    $projectLines.Add('Keep this landing page short. Update Activity, roadmap links and major constraints when durable project state changes.')
    $projectContent = ($projectLines -join "`r`n") + "`r`n"
    Write-ManagedFile (Join-Path $dir.FullName 'PROJECT.md') $projectContent

    $roadmapLines = New-Object System.Collections.Generic.List[string]
    $roadmapLines.Add($managedMarker)
    $roadmapLines.Add('# ' + $dir.Name + ' Roadmap')
    $roadmapLines.Add('')
    $roadmapLines.Add('Status: ' + $activity)
    $roadmapLines.Add('Last reviewed: ' + $reviewDate)
    $roadmapLines.Add('')
    $roadmapLines.Add('## Current')
    $roadmapLines.Add('')
    if ($dir.Name -eq 'Antigravity.DrawBeams') {
        $roadmapLines.Add('The durable DrawBeams recognition roadmap exists in the plan inventory below. Read that plan for the authoritative DBR milestone state before resuming.')
    } elseif ($plans.Count -gt 0) {
        $roadmapLines.Add('Durable plan history exists, but no active milestone is declared by this index. Review plans and latest reports before choosing the next milestone.')
    } else {
        $roadmapLines.Add('No active milestone is recorded. Create a dated project-local plan only when new work is approved.')
    }
    $roadmapLines.Add('')
    $roadmapLines.Add('## Plan inventory')
    $roadmapLines.Add('')
    if ($plans.Count -eq 0) { $roadmapLines.Add('- No durable implementation plan recorded yet.') }
    else { foreach ($plan in $plans) { $roadmapLines.Add('- ' + $plan.Name) } }
    $roadmapLines.Add('')
    $roadmapLines.Add('## Next resume action')
    $roadmapLines.Add('')
    $roadmapLines.Add('- Read src/' + $dir.Name + '/PROJECT.md.')
    $roadmapLines.Add('- Review the latest relevant report and current source/worktree.')
    $roadmapLines.Add('- Confirm last-known-good/smoke state before destructive work.')
    $roadmapLines.Add('- For an approved new milestone, create YYYY-MM-DD-<feature>.md here and update this roadmap.')
    $roadmapLines.Add('')
    $roadmapLines.Add('## Completed / deferred milestones')
    $roadmapLines.Add('')
    $roadmapLines.Add('Do not infer completion from filenames. Record completed, paused, superseded or deferred milestones only when project evidence explicitly supports that state.')
    $roadmapLines.Add('')
    $roadmapLines.Add('## Roadmap maintenance')
    $roadmapLines.Add('')
    $roadmapLines.Add('This is the stable long-term index. Detailed implementation steps belong in dated plan files; closure evidence belongs in ../reports/.')
    $roadmapContent = ($roadmapLines -join "`r`n") + "`r`n"
    Write-ManagedFile (Join-Path $dir.FullName 'docs\plans\ROADMAP.md') $roadmapContent

    $rows.Add([pscustomobject]@{
        Project = $dir.Name
        Type = $type
        Activity = $activity
        Plans = $plans.Count
        Smoke = $smokeStatus
        Entry = ('src/{0}/PROJECT.md' -f $dir.Name)
    })
}

$registryPath = Join-Path $root 'docs\projects\PROJECTS.md'
$registryLines = New-Object System.Collections.Generic.List[string]
$registryLines.Add($managedMarker)
$registryLines.Add('# RevitAddinSolution Project Registry')
$registryLines.Add('')
$registryLines.Add('Last generated: ' + $reviewDate)
$registryLines.Add('')
$registryLines.Add('Solution-level landing page for humans and agents. It does not replace each project PROJECT.md or docs/plans/ROADMAP.md.')
$registryLines.Add('')
$registryLines.Add('| Project | Type | Activity | Plan files | Smoke/LKG | Entry |')
$registryLines.Add('|---|---|---|---:|---|---|')
foreach ($row in $rows) {
    $registryLines.Add(('| {0} | {1} | {2} | {3} | {4} | [{5}](../../{5}) |' -f $row.Project, $row.Type, $row.Activity, $row.Plans, $row.Smoke, $row.Entry))
}
$registryLines.Add('')
$registryLines.Add('## Agent resume protocol')
$registryLines.Add('')
$registryLines.Add('1. Start here to identify the owner project.')
$registryLines.Add('2. Open the project PROJECT.md.')
$registryLines.Add('3. Open its docs/plans/ROADMAP.md.')
$registryLines.Add('4. Open only the detailed plan/design/report needed for the current milestone.')
$registryLines.Add('5. Check smoke/last-known-good before destructive or production work.')
$registryLines.Add('')
$registryLines.Add('Activity is intentionally conservative. NO_ACTIVE_PLAN_RECORDED and PLAN_HISTORY_PRESENT_NO_ACTIVE_DECLARED mean an agent must not guess that old work is current.')
$registryContent = ($registryLines -join "`r`n") + "`r`n"

Write-Host ("[GENERATE   ] {0}" -f $registryPath.Substring($root.Length + 1))
if ($Apply) {
    $parent = Split-Path -Parent $registryPath
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [System.IO.File]::WriteAllText($registryPath, $registryContent, (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host ("Projects indexed: {0}" -f $rows.Count)
