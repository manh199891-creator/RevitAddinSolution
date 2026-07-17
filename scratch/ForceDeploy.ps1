$target = "C:\Users\Admin\AppData\Roaming\Autodesk\Revit\Addins\2024\Antigravity"
$src = "E:\Antigravity\RevitAddinSolution\RevitAddinSolution\src"

$modules = @("Main", "Core", "Autojoin")

foreach ($mod in $modules) {
    $dll = "$src\Antigravity.$mod\bin\Debug\Antigravity.$mod.dll"
    $dst = "$target\Antigravity.$mod.dll"
    
    if (Test-Path $dst) {
        $old = $dst + ".old"
        try {
            if (Test-Path $old) { Remove-Item $old -Force }
            Rename-Item $dst $old -Force
            Copy-Item $dll $dst -Force
            Write-Host "[OK] Updated $mod (old file renamed to .old)" -ForegroundColor Green
        } catch {
            Write-Host "[FAIL] Could not update $mod. File is strictly locked by Revit." -ForegroundColor Red
        }
    } else {
        Copy-Item $dll $dst -Force
        Write-Host "[OK] Copied new $mod" -ForegroundColor Green
    }
}
