param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src = Join-Path $root 'src'

$errors = New-Object System.Collections.Generic.List[string]
$validated = 0
$deferred = New-Object System.Collections.Generic.List[string]

$projectDirs = Get-ChildItem -LiteralPath $src -Directory | Where-Object { $_.Name -like 'Antigravity.*' }
foreach ($dir in $projectDirs) {
    $csproj = Get-ChildItem -LiteralPath $dir.FullName -Filter '*.csproj' -File -ErrorAction SilentlyContinue | Where-Object { $_.DirectoryName -eq $dir.FullName } | Select-Object -First 1
    if (-not $csproj) {
        $deferred.Add($dir.Name)
        continue
    }

    $required = @(
        'docs\README.md',
        'docs\plans\README.md',
        'docs\design\README.md',
        'docs\acceptance\README.md',
        'docs\reports\README.md',
        'smoke-tests\README.md',
        'smoke-tests\smoke-manifest.json',
        'smoke-tests\baseline\last-known-good.json',
        'smoke-tests\baseline\expected-behavior.md',
        'smoke-tests\scripts\README.md',
        'smoke-tests\fixtures\README.md',
        'smoke-tests\results\.gitkeep'
    )

    if ($dir.Name -eq 'Antigravity.DrawBeams') {
        $required = @(
            'docs\README.md',
            'docs\plans\2026-08-22-drawbeams-recognition-engine-v14.md',
            'docs\design\README.md',
            'docs\acceptance\README.md',
            'docs\reports\README.md',
            'smoke-tests\README.md',
            'smoke-tests\smoke-manifest.json',
            'smoke-tests\baseline\last-known-good.json',
            'smoke-tests\baseline\expected-behavior.md',
            'smoke-tests\scripts\README.md',
            'smoke-tests\fixtures\README.md',
            'smoke-tests\results\.gitkeep'
        )
    }

    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $dir.FullName $relative))) {
            $errors.Add("$($dir.Name): missing $relative")
        }
    }

    $manifestPath = Join-Path $dir.FullName 'smoke-tests\smoke-manifest.json'
    $lkgPath = Join-Path $dir.FullName 'smoke-tests\baseline\last-known-good.json'
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.project -ne $dir.Name) { $errors.Add("$($dir.Name): manifest project mismatch '$($manifest.project)'") }
        if (-not $manifest.smokeChecks -or @($manifest.smokeChecks).Count -eq 0) { $errors.Add("$($dir.Name): smokeChecks empty") }
    } catch {
        $errors.Add("$($dir.Name): invalid smoke-manifest.json - $($_.Exception.Message)")
    }

    try {
        $lkg = Get-Content -LiteralPath $lkgPath -Raw | ConvertFrom-Json
        if ($dir.Name -ne 'Antigravity.DrawBeams' -and $lkg.project -ne $dir.Name) { $errors.Add("$($dir.Name): last-known-good project mismatch '$($lkg.project)'") }
        if ($lkg.smokeStatus -eq 'PASS') { $errors.Add("$($dir.Name): forbidden unverified PASS in last-known-good") }
    } catch {
        $errors.Add("$($dir.Name): invalid last-known-good.json - $($_.Exception.Message)")
    }

    $validated++
}

Write-Host ("Validated projects : {0}" -f $validated)
Write-Host ("Deferred directories: {0}" -f $deferred.Count)
foreach ($name in $deferred) { Write-Host ("[DEFERRED] {0}" -f $name) }

if ($errors.Count -gt 0) {
    Write-Host ("Errors: {0}" -f $errors.Count)
    foreach ($error in $errors) { Write-Host ("[ERROR] {0}" -f $error) }
    exit 1
}

Write-Host 'PHASE2_GOVERNANCE_VALIDATION_PASS'
