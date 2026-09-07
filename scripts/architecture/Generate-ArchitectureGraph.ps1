$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$root = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
$configPath = Join-Path $scriptDir 'understand_config.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$sourceDir = Join-Path $root 'src'
$outFile = Join-Path $scriptDir $config.output_file

$nodes = @()
$edges = @()
$files = Get-ChildItem -LiteralPath $sourceDir -Recurse -File -Filter '*.cs' | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
}

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $className = if ($content -match 'class\s+(\w+)') { $matches[1] } else { $file.BaseName }
    $relativePath = $file.FullName.Substring($root.Length + 1).Replace('\','/')

    $nodes += @{
        id = $className
        path = $relativePath
        type = 'class'
    }

    $usingMatches = [regex]::Matches($content, 'using\s+([\w\.]+);')
    foreach ($match in $usingMatches) {
        $edges += @{
            source = $className
            target = $match.Groups[1].Value
            type = 'depends_on'
        }
    }
}

$graph = @{
    project = $config.project_name
    generatedAt = (Get-Date).ToString('o')
    nodes = $nodes
    edges = $edges
}

$graph | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outFile -Encoding UTF8
Write-Host "Generated architecture map at $outFile" -ForegroundColor Green
