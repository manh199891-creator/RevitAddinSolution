# Project File Tracking Summary

Updated: 2026-07-20

## Scope

Workspace:

`E:\Antigravity\RevitAddinSolution`

Muc dich file nay:

- Lam baseline de theo doi cac file/thu muc duoc tao trong qua trinh Codex/dual-agent lam viec.
- Tong hop cac khu vuc quan trong cua du an.
- Ghi lai cac file tai lieu/plan lien quan den Issue Manager va Navis migration.

## Current Git State

Tai thoi diem tao baseline nay, `git status --short` khong hien thay thay doi nao trong working tree.

Dieu nay co nghia:

- Khong co file uncommitted dang cho review theo Git.
- Neu co file moi duoc tao truoc do, chung co the da duoc commit, ignored, hoac nam trong trang thai Git khong bao cao.

## Top-Level Folders

| Folder | Muc dich |
| --- | --- |
| `.agents` | Skill/agent workflow noi bo cua repo. |
| `.brain` | Du lieu/ghi chu noi bo cu. |
| `.codex` | Cau hinh/du lieu lien quan Codex trong project. |
| `.codex-bcf-sample` | Mau BCF/BCF extracted dung de debug Issue Manager. |
| `.git` | Git repository metadata. |
| `.vscode` | Cau hinh VS Code. |
| `_addin_manager` | Khu vuc lien quan addin manager/deploy. |
| `_Installer` | Artifact/logic installer cu. |
| `_packages` | Package/deploy artifact. |
| `artifacts` | Output/artifact build hoac QA. |
| `assets` | Tai nguyen chung. |
| `docs` | Tai lieu ky thuat, plan, report, migration guide. |
| `installer` | Script/code tao installer. |
| `lib` | Thu vien DLL ngoai. |
| `plans` | Ke hoach task/chuc nang. |
| `scratch` | Thu nghiem/tam thoi/debug. |
| `scripts` | Script automation. |
| `src` | Source code cac Revit add-in/module. |
| `TestFiles` | File test mau. |
| `tests` | Test projects/scripts. |
| `Workflow` | Tai lieu workflow/orchestration. |

## Notable Files Recently Relevant To This Workstream

### Issue Manager

| File | Noi dung |
| --- | --- |
| `src\Antigravity.IssueManager\UI\IssueManagerWindow.xaml` | UI chinh: toolbar, buttons, checkbox `Section Box`, `Shared Coords`, list/detail issue. |
| `src\Antigravity.IssueManager\UI\IssueManagerWindow.xaml.cs` | Logic UI chinh: load/import/export, edit/save, shared coordinate conversion, dashboard, autosave. |
| `src\Antigravity.IssueManager\Services\BcfExporter.cs` | Export BCF/BCFzip, markup, viewpoint, snapshot, Trimble referenced model filtering. |
| `src\Antigravity.IssueManager\Services\BcfZipParser.cs` | Import BCF/BCFzip. |
| `src\Antigravity.IssueManager\Services\NavisworksXmlParser.cs` | Import Navisworks XML. |
| `src\Antigravity.IssueManager\Services\ExcelIssueExporter.cs` | Export Excel RFI register, mapping `AssignedTo` -> `Nguoi nhan`. |
| `src\Antigravity.IssueManager\Services\RevitIssueCreator.cs` | Tao issue tu Revit current 3D view/camera/section box. |
| `src\Antigravity.IssueManager\Services\RevitCameraSync.cs` | Show in model, camera sync, coordinate candidate selection. |
| `src\Antigravity.IssueManager\Handlers\IsolateClashElementsHandler.cs` | Button `Isolate Clash Elements`. |
| `src\Antigravity.IssueManager\Handlers\VerifyClashHandler.cs` | Button `Verify`. |
| `src\Antigravity.IssueManager\Handlers\ModelClashPointHandler.cs` | Button `Model Clash Point`. |

### Documentation Created/Used For Planning

| File | Noi dung |
| --- | --- |
| `docs\IssueManager_Features_And_Logic.md` | Tong hop tinh nang va logic cua Revit Issue Manager. |
| `docs\NavisIssueManager_Migration_Plan.md` | Plan chuyen Issue Manager sang Navisworks, gom UI/button/code logic. |
| `docs\Project_File_Tracking_Summary.md` | File baseline theo doi file/thu muc duoc tao. |

## Current Project Modules Under `src`

Repo gom nhieu module/add-in, trong do cac module chinh dang thay:

- `Antigravity.Main`
- `Antigravity.Core`
- `Antigravity.IssueManager`
- `Antigravity.IssueManager.Installer`
- `Antigravity.ZoneSplit`
- `Antigravity.Autojoin`
- `Antigravity.AutoDimWalls`
- `Antigravity.DrawWalls`
- `Antigravity.DrawFloors`
- `Antigravity.DrawColumns`
- `Antigravity.DrawBeams`
- `Antigravity.CheckFloorElevation`
- `Antigravity.HoanThien`
- `Antigravity.WallMepClash`
- `Antigravity.TagArranger`
- `Antigravity.ArchModeling`
- `Antigravity.CadVoidPlacer`
- `Antigravity.CadSleevePlacer`
- `Antigravity.DoorClearance`
- `Antigravity.BIMLink.*`
- `Antigravity.HatchPatterns.*`
- `Antigravity.AutoCAD.HatchBridge`

## How To Track New Files/Folders

### Git-Based Tracking

Chay:

```powershell
git status --short
```

Y nghia:

- `?? path` = file/folder moi chua tracked.
- `M path` = file da sua.
- `A path` = file moi da staged.
- `D path` = file bi xoa.

### Full File Baseline

Chay:

```powershell
rg --files | sort
```

Neu can so sanh truoc/sau mot task:

```powershell
rg --files | sort > before_files.txt
# run task
rg --files | sort > after_files.txt
Compare-Object (Get-Content before_files.txt) (Get-Content after_files.txt)
```

### Recent Files

Chay:

```powershell
Get-ChildItem -Path . -Recurse -File |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 50 FullName, LastWriteTime, Length
```

Dung cach nay khi task khong tao Git changes ro rang, hoac khi output nam trong `bin`, `obj`, `artifacts`, `scratch`.

## Recommended Dual-Agent Tracking Workflow

### Agent A - Implementer

- Tao/sua file.
- Chay build/test.
- Cap nhat `docs\Project_File_Tracking_Summary.md` neu tao folder/file moi quan trong.

### Agent B - Reviewer

- Chay `git status --short`.
- Kiem tra file moi co dung vi tri khong.
- Kiem tra file build/tam thoi co bi dua nham vao source khong.
- Review docs/plan co cap nhat danh sach file moi khong.

## Notes

- Cac file trong `bin`, `obj`, `.vs`, temporary folders nen khong xem la source artifact tru khi user yeu cau dong goi DLL.
- Cac file BCF/Excel output nen dat trong folder du lieu rieng, vi du `E:\GMD\BCF`, khong nen commit vao source repo.
- Khi chuyen sang du an Navis, nen tao root rieng du kien: `src\NavisIssueManager`.
