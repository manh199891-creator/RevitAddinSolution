# Workflow: Release & Versioning
Mục tiêu: Build, đóng gói và phân phối add-in đúng chuẩn, hỗ trợ nhiều phiên bản Revit.

---

## Version scheme: SemVer + Revit year

```
Format: {Major}.{Minor}.{Patch}-r{RevitYear}
Ví dụ:  1.3.0-r2024

Major  → breaking change (thay đổi API, xóa feature)
Minor  → tính năng mới, backward compatible
Patch  → bug fix
r{year} → Revit target (2022, 2023, 2024, 2025, 2026)
```

---

## Cấu trúc multi-target build

```xml
<!-- Antigravity.Main.csproj -->
<PropertyGroup>
  <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
  <!-- net48   → Revit 2022, 2023, 2024 -->
  <!-- net8.0  → Revit 2025, 2026       -->
  <Version>$(ADDIN_VERSION)</Version>
</PropertyGroup>

<ItemGroup Condition="'$(TargetFramework)'=='net48'">
  <PackageReference Include="RevitAPI" Version="2024.*" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2024.*" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)'=='net8.0-windows'">
  <PackageReference Include="RevitAPI" Version="2025.*" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2025.*" />
</ItemGroup>
```

---

## Build script (PowerShell)

```powershell
# build-release.ps1
param(
    [string]$Version = "1.0.0",
    [string[]]$RevitYears = @("2022","2023","2024","2025")
)

foreach ($year in $RevitYears) {
    Write-Host "Building for Revit $year..."

    $framework = if ([int]$year -ge 2025) { "net8.0-windows" } else { "net48" }

    dotnet build Antigravity.Main.csproj `
        -c Release `
        -f $framework `
        -p:ADDIN_VERSION="$Version-r$year" `
        -o "dist/$year/"

    # Copy .addin manifest
    Copy-Item "_Installer/Antigravity.addin" "dist/$year/"
}

Write-Host "Build complete. Output: ./dist/"
```

---

## .addin manifest versioning

```xml
<!-- Antigravity.addin — update VendorId và Assembly path -->
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Antigravity</Name>
    <Assembly>Antigravity.Main.dll</Assembly>
    <AddInId><!-- GUID không đổi qua versions --></AddInId>
    <FullClassName>Antigravity.Main.App</FullClassName>
    <VendorId>ANTIGRAVITY</VendorId>
    <VendorDescription>Antigravity BIM Tools</VendorDescription>
  </AddIn>
</RevitAddIns>
```

> ⚠️ `AddInId` (GUID) phải giữ nguyên qua mọi version — đây là identity của add-in với Revit.

---

## CHANGELOG.md format

```markdown
## [1.3.0] - 2025-01-15
### Added
- DrawColumns: hỗ trợ đọc symbol mở ở tỷ lệ 1:50
- Settings: thêm option chọn family mặc định

### Fixed  
- DrawWalls: crash khi wall không có location curve (#42)
- Autojoin: không join đúng khi wall song song (#38)

### Changed
- Nâng Nice3point.Revit.Toolkit lên 2024.1.0

## [1.2.1] - 2025-01-02
### Fixed
- Memory leak khi đóng document (#35)
```

---

## Release checklist

```
### Pre-release
- [ ] Chạy full unit test suite → tất cả pass
- [ ] Chạy smoke test trên Revit target versions
- [ ] Review CHANGELOG.md — đủ thông tin
- [ ] Version bump trong .csproj và CHANGELOG
- [ ] Commit "chore: release v{version}"

### Build
- [ ] Chạy build-release.ps1 với version đúng
- [ ] Kiểm tra output dist/ có đủ file:
      - Antigravity.Main.dll
      - Antigravity.*.dll (dependencies)
      - Antigravity.addin
      - Resources/ (icons, families)

### Package
- [ ] Zip từng thư mục year riêng:
      Antigravity_v1.3.0_Revit2024.zip
- [ ] Test cài đặt trên máy sạch (không có build env)

### Publish
- [ ] Create GitHub Release với tag v{version}
- [ ] Upload tất cả .zip lên release
- [ ] Ghi release notes từ CHANGELOG
```

---

## Install locations

| OS | Revit Add-ins path |
|---|---|
| Windows (all users) | `C:\ProgramData\Autodesk\Revit\Addins\{year}\` |
| Windows (current user) | `%APPDATA%\Autodesk\Revit\Addins\{year}\` |

> Khuyến nghị: cài vào `ProgramData` (all users) cho môi trường production.
