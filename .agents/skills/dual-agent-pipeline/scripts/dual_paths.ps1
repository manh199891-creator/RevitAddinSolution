function Get-DualAgentPaths {
    $pipelineRoot = Split-Path -Parent $PSScriptRoot
    $skillsRoot = Split-Path -Parent $pipelineRoot
    $agentsRoot = Split-Path -Parent $skillsRoot
    $projectRoot = Split-Path -Parent $agentsRoot

    $localRuntimeRoot = Join-Path $projectRoot ".agents\runtime"
    $localFactoryRoot = Join-Path $projectRoot ".agents\factory"
    $localHarness = Join-Path $localRuntimeRoot "harness.py"

    if ((Test-Path -LiteralPath $localHarness) -and (Test-Path -LiteralPath $localFactoryRoot)) {
        return [pscustomobject]@{
            Mode = "local"
            ProjectRoot = $projectRoot
            PipelineRoot = $pipelineRoot
            RuntimeRoot = $localRuntimeRoot
            FactoryRoot = $localFactoryRoot
            Harness = $localHarness
        }
    }

    $factoryRoot = if ($env:AI_SOFTWARE_FACTORY_ROOT) {
        $env:AI_SOFTWARE_FACTORY_ROOT
    } else {
        "E:\AI_SOFTWARE_FACTORY"
    }
    $harness = Join-Path $factoryRoot "harness.py"

    return [pscustomobject]@{
        Mode = "factory"
        ProjectRoot = ""
        PipelineRoot = $pipelineRoot
        RuntimeRoot = $factoryRoot
        FactoryRoot = $factoryRoot
        Harness = $harness
    }
}

function Invoke-DualAgentHarness {
    param(
        [Parameter(Mandatory=$true)][object]$Paths,
        [Parameter(Mandatory=$true)][string[]]$Arguments
    )

    if (-not (Test-Path -LiteralPath $Paths.Harness)) {
        Write-Error "harness.py was not found at $($Paths.Harness)"
        exit 2
    }

    $previousFactoryRoot = $env:AI_SOFTWARE_FACTORY_ROOT
    $env:AI_SOFTWARE_FACTORY_ROOT = $Paths.FactoryRoot
    try {
        Write-Output "PipelineMode: $($Paths.Mode)"
        Write-Output "FactoryRoot: $($Paths.FactoryRoot)"
        Write-Output "RuntimeHarness: $($Paths.Harness)"
        & python $Paths.Harness @Arguments
        exit $LASTEXITCODE
    } finally {
        $env:AI_SOFTWARE_FACTORY_ROOT = $previousFactoryRoot
    }
}
