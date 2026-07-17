$ErrorActionPreference = "Continue"

try {
    $acad = [System.Runtime.InteropServices.Marshal]::GetActiveObject("AutoCAD.Application")
    $doc = $acad.ActiveDocument
    
    $sset = $doc.ActiveSelectionSet
    
    for ($i = 0; $i -lt $sset.Count; $i++) {
        $entity = $sset.Item($i)
        if ($entity.ObjectName -eq "AcDbHatch") {
            Write-Host "Hatch: $($entity.PatternName)"
            try {
                $numLines = $entity.NumberOfPatternLines
                Write-Host "  NumberOfPatternLines: $numLines"
            } catch {
                Write-Host "  Error reading NumberOfPatternLines: $($_.Exception.Message)"
            }
        }
    }
}
catch {
    Write-Host "Main Error: $($_.Exception.Message)"
}
