$sourceDir = "src"
$outFile = ".awf-pipeline\architecture_map.json"
$config = Get-Content ".awf-pipeline\understand_config.json" -Raw | ConvertFrom-Json

$nodes = @()
$edges = @()

$files = Get-ChildItem -Path $sourceDir -Recurse -Include *.cs

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $className = ""
    if ($content -match "class\s+(\w+)") {
        $className = $matches[1]
    } else {
        $className = $file.BaseName
    }
    
    $nodes += @{
        id = $className
        path = $file.FullName.Replace("E:\Antigravity\RevitAddinSolution\", "")
        type = "class"
    }

    $usingMatches = [regex]::Matches($content, "using\s+([\w\.]+);")
    foreach ($match in $usingMatches) {
        $edges += @{
            source = $className
            target = $match.Groups[1].Value
            type = "depends_on"
        }
    }
}

$graph = @{
    nodes = $nodes
    edges = $edges
}

$graph | ConvertTo-Json -Depth 5 | Set-Content $outFile
Write-Host "Generated architecture map at $outFile" -ForegroundColor Green
