$ErrorActionPreference = "Stop"

try {
    $acad = [System.Runtime.InteropServices.Marshal]::GetActiveObject("AutoCAD.Application")
    $doc = $acad.ActiveDocument
    $utility = $doc.Utility
    
    $ssetName = "HatchSel_" + [guid]::NewGuid().ToString().Substring(0,8)
    $sset = $doc.SelectionSets.Add($ssetName)
    
    $utility.Prompt("`nSelect hatches for diagnostic... ")
    $sset.SelectOnScreen()
    
    $results = @()
    
    for ($i = 0; $i -lt $sset.Count; $i++) {
        $entity = $sset.Item($i)
        if ($entity.ObjectName -eq "AcDbHatch") {
            
            $numLines = $entity.NumberOfPatternLines
            $lines = @()
            
            for ($j = 0; $j -lt $numLines; $j++) {
                $angle = $entity.GetPatternLineAngle($j)
                $basePt = $entity.GetPatternLineBasePoint($j)
                $offset = $entity.GetPatternLineOffset($j)
                $dashArray = $entity.GetPatternLineDashArray($j)
                
                $lines += @{
                    Index = $j
                    AngleRad = $angle
                    BaseX = $basePt[0]
                    BaseY = $basePt[1]
                    OffsetX = $offset[0]
                    OffsetY = $offset[1]
                    DashArray = $dashArray
                }
            }
            
            $results += @{
                Handle = $entity.Handle
                PatternName = $entity.PatternName
                PatternType = $entity.PatternType
                PatternScale = $entity.PatternScale
                PatternAngle = $entity.PatternAngle
                PatternDouble = $entity.PatternDouble
                NumberOfPatternLines = $numLines
                Lines = $lines
            }
        }
    }
    
    $sset.Delete()
    
    $json = $results | ConvertTo-Json -Depth 5
    $path = [System.IO.Path]::Combine([Environment]::GetFolderPath("Desktop"), "HatchDiagnostic.json")
    [System.IO.File]::WriteAllText($path, $json)
    
    Write-Host "Diagnostic saved to $path"
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
}
