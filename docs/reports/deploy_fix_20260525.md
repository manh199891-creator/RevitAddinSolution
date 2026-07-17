# Antigravity Revit Add-in Fix & Deploy Report

Date: 2026-05-25  
Workspace: `E:\Antigravity\RevitAddinSolution`  
Target deploy folder: `C:\Users\Admin\AppData\Roaming\Autodesk\Revit\Addins\2024`

## Summary

Da review, sua cac loi logic/deploy co rui ro cao trong Revit add-in solution, build lai, chay test hien co va deploy lai vao thu muc Addins cua Revit 2024.

Trang thai hien tai:

- Build solution: passed.
- ZoneSplit tests: passed, 13/13.
- Deploy vao Revit Addins 2024: completed.
- Manifest chinh dang dung: `Antigravity.addin`.
- Manifest cu `DoorClearanceBox.addin` da duoc doi thanh `DoorClearanceBox.addin.disabled` de tranh Revit load trung add-in cu.

## Code Changes

### 1. AutoJoin DMU AddInId

File: `src\Antigravity.Autojoin\Services\AutoJoinUpdater.cs`

Thay doi:

- Bo hard-code `AddInId` cu `656A34AB-B82E-4973-9F7F-EE8A0B43C3E1`.
- `AutoJoinUpdater.Register(...)` bay gio nhan `AddInId` tu `application.ActiveAddInId`.
- `UpdaterId` duoc tao theo instance updater thay vi static hard-code.

Ly do:

- Manifest chinh cua add-in la `Antigravity.addin` voi AddInId `88888888-9999-0000-AAAA-BBBBCCCCDDDD`.
- Hard-code GUID khac manifest co the lam DMU register/unregister khong on dinh trong Revit session.

### 2. DoorClearance Updater Registration

File: `src\Antigravity.Main\App.cs`

Thay doi:

- `Antigravity.Main` dang ky truc tiep `DoorClearanceBox.Updaters.DoorChangeUpdater`.
- DoorClearance updater dung cung `application.ActiveAddInId` cua add-in chinh.
- Them unregister updater trong `OnShutdown`.
- Automation hook `C:\temp\revit_auto_run.txt` chi con active trong `#if DEBUG`.

Ly do:

- Deploy chinh chi dung `Antigravity.addin`; neu phu thuoc `DoorClearanceBox.addin` rieng thi updater co the khong chay.
- Giam rui ro Revit load hai application/add-in khac nhau cho cung mot bo command.
- Tranh automation/test hook chay ngoai y muon trong Release.

### 3. ZoneSplit Clears Stale Zone Values

File: `src\Antigravity.ZoneSplit\Services\ZoneVolumeProcessor.cs`

Thay doi:

- Khi reset BIM volume parameters, dong thoi clear:
  - `BIM_ZoneID`
  - `BIM_ZoneName`

Ly do:

- Truoc day element tung thuoc zone nhung lan sau khong con intersect zone nao van giu `BIM_ZoneID` cu.
- Viec nay gay sai du lieu BIM va report sau khi zone geometry thay doi.

### 4. Platform Target x64

Files:

- `src\Antigravity.Main\Antigravity.Main.csproj`
- `src\Antigravity.Core\Antigravity.Core.csproj`
- `src\Antigravity.DrawBeams\Antigravity.DrawBeams.csproj`
- `src\Antigravity.DrawColumns\Antigravity.DrawColumns.csproj`
- `src\Antigravity.DrawWalls\Antigravity.DrawWalls.csproj`
- `src\Antigravity.DrawFloors\Antigravity.DrawFloors.csproj`
- `src\Antigravity.Autojoin\Antigravity.Autojoin.csproj`

Thay doi:

- Them `<PlatformTarget>x64</PlatformTarget>` cho cac project con thieu.

Ly do:

- Revit 2024 la x64.
- Truoc do build co warning mismatch giua `MSIL` va `AMD64`.

### 5. Deploy Script

File: `DeployToRevit.ps1`

Thay doi:

- Them `param([switch]$NoPause)`.
- Them `$ErrorActionPreference = "Stop"` de copy fail thi script fail that, khong bao OK gia.
- Copy day du cac module:
  - `Antigravity.Main.dll`
  - `Antigravity.Core.dll`
  - `Antigravity.DrawColumns.dll`
  - `Antigravity.DrawBeams.dll`
  - `Antigravity.DrawWalls.dll`
  - `Antigravity.DrawFloors.dll`
  - `Antigravity.Autojoin.dll`
  - `Antigravity.ZoneSplit.dll`
  - `Antigravity.AutoDimWalls.dll`
  - `Antigravity.CadVoidPlacer.dll`
  - `Antigravity.CadSleevePlacer.dll`
  - `Antigravity.TagArranger.dll`
  - `DoorClearanceBox.dll`
- Copy dependency:
  - `Serilog.dll`
  - `Serilog.Sinks.File.dll`
  - `netDxf.dll`
- Copy `Antigravity.addin`.
- Neu thay `DoorClearanceBox.addin` cu thi rename thanh `DoorClearanceBox.addin.disabled`.

Ly do:

- Script cu thieu nhieu DLL ma ribbon dang reference.
- DoorClearance manifest rieng co the lam Revit load trung/old application.

### 6. Package Script

File: `PackageForDeployment.ps1`

Thay doi:

- Them cac module con thieu vao package:
  - `ZoneSplit`
  - `AutoDimWalls`
  - `CadVoidPlacer`
  - `CadSleevePlacer`
  - `TagArranger`
  - `DoorClearanceBox.dll`
- Them copy dependency `Serilog`, `Serilog.Sinks.File`, `netDxf`.
- Ho tro output path co `net48`.

Ly do:

- Goi deploy sang may khac truoc do se thieu DLL cho cac ribbon button moi.

## Deploy Result

Command used:

```powershell
powershell -ExecutionPolicy Bypass -File .\DeployToRevit.ps1 -NoPause
```

Deploy output summary:

- Copied all listed Antigravity module DLLs.
- Copied `DoorClearanceBox.dll`.
- Copied `Serilog.dll`, `Serilog.Sinks.File.dll`, `netDxf.dll`.
- Updated `Antigravity.addin`.
- Disabled old `DoorClearanceBox.addin`.

Root Addins folder after deploy includes:

- `Antigravity\`
- `Antigravity.addin`
- `DoorClearanceBox.addin.disabled`

Main deployed folder includes:

- `Antigravity.Main.dll`
- `Antigravity.Core.dll`
- `Antigravity.Autojoin.dll`
- `Antigravity.ZoneSplit.dll`
- `Antigravity.AutoDimWalls.dll`
- `Antigravity.TagArranger.dll`
- `Antigravity.CadVoidPlacer.dll`
- `Antigravity.CadSleevePlacer.dll`
- `DoorClearanceBox.dll`
- `netDxf.dll`
- `Serilog.dll`
- `Serilog.Sinks.File.dll`

## Verification

Build:

```powershell
dotnet build Antigravity.sln -c Debug
```

Result:

- Passed.
- 0 errors.
- Remaining warnings are non-blocking:
  - `NU1603`: `netDxf 3.0.0` not found, `3.0.1` resolved.
  - `MSB3246`: Revit SDK package contains native/non-.NET metadata references in some projects.

Test:

```powershell
dotnet test src\Antigravity.ZoneSplit\tests\ZoneSplit.Tests.csproj -c Debug
```

Result:

- Passed.
- Failed: 0.
- Passed: 13.
- Skipped: 0.

## Notes For Next Runtime Check

Can kiem tra trong Revit 2024:

1. Revit load `Antigravity.addin` without duplicate DoorClearance prompt.
2. Ribbon tab `VILAIVIET` hien day du button.
3. AutoJoin realtime DMU co the bat/tat va join theo rule.
4. DoorClearance updater flag stale DirectShape khi sua width/height cua door/window.
5. ZoneSplit khong con giu `BIM_ZoneID` cu khi element khong con intersect zone.

## Remaining Technical Debt

- Nhieu chuoi UI/comment trong source dang bi mojibake UTF-8. Chua sua trong dot nay de tranh pham vi thay doi qua rong.
- Nhieu `catch { }` trong cac module CAD/Draw van dang nuot loi runtime. Nen xu ly sau bang report/log per element.
- `netDxf` package nen pin len version thuc te dang resolve, vi build hien dang canh bao `3.0.0 -> 3.0.1`.
