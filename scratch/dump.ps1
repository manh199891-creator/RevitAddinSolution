$acad = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll')
$t = $acad.GetType('Autodesk.AutoCAD.DatabaseServices.PatternDefinition')
$m = $t.GetMethod('GetDashes')
Write-Host "$($m.ReturnType.FullName)"
