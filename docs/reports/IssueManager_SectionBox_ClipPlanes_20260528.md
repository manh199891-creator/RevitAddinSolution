# Issue Manager - Section Box Clip Planes

Date: 2026-05-28

## Scope

Implemented the plan from `.awf-pipeline/inbox-ide/Plan_RevitToTrimbleConnectClipPlanes_v2.2.md` for `Antigravity.IssueManager`.

## Logic Implemented

- Added `ClippingPlaneModel` and `ViewpointModel.ClippingPlanes`.
- Added an `Include Section Box (Clip Planes)` checkbox in the Issue Manager toolbar. It is checked by default.
- When creating a new issue from a 3D view, the add-in now reads `View3D.GetSectionBox()` if the section box is active.
- The six section box faces are generated from local `BoundingBoxXYZ.Min/Max` with directions pointing outward from the retained section box:
  - min X direction -X
  - max X direction +X
  - min Y direction -Y
  - max Y direction +Y
  - min Z direction -Z
  - max Z direction +Z
- Plane locations and directions are transformed in this order:
  1. section box local coordinates
  2. section box `BoundingBoxXYZ.Transform`
  3. project/shared coordinate rotation and translation
  4. BCF meters for locations; normalized vectors for directions
- `viewpoint.bcfv` now writes:
  - existing camera data
  - `<ClippingPlanes>` with six `<ClippingPlane>` nodes when available
  - existing selected component data
- BCF import/autosave now parses and preserves existing `<ClippingPlane>` nodes.

## Files Changed

- `src/Antigravity.IssueManager/Models/ViewpointModel.cs`
- `src/Antigravity.IssueManager/Handlers/CreateIssueHandler.cs`
- `src/Antigravity.IssueManager/Services/RevitIssueCreator.cs`
- `src/Antigravity.IssueManager/Services/BcfExporter.cs`
- `src/Antigravity.IssueManager/Services/BcfZipParser.cs`
- `src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml`
- `src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml.cs`

## Build Result

Command:

```powershell
dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj -c Debug
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## DLL For RevitAddinManager

Use this DLL:

```text
E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager\bin\Debug\Antigravity.IssueManager.dll
```

Expected command class:

```text
Antigravity.IssueManager.Commands.CmdOpenIssueManager
```

## Notes

- Clipping planes are captured when a new issue is created. Export writes whatever is stored in each issue's viewpoint.
- Existing imported BCF files keep their clipping planes during load/autosave/export.
- If the active 3D view has no active section box, the BCF is exported without `<ClippingPlanes>`.
- Updated after Trimble Connect visual check: clip plane directions are intentionally outward, not inward.
