# Plan Chuyen Issue Manager Sang Navisworks

## Muc Tieu

Xay dung/phat trien `Navis Issue Manager` co workflow tuong tu `Antigravity.IssueManager` tren Revit:

- Quan ly danh sach issue/RFI.
- Load BCF/BCFzip va Navisworks XML.
- Tao issue truc tiep tu viewpoint hien tai trong Navisworks.
- Luu snapshot, markup, status, folder, assigned to.
- Show issue trong Navisworks.
- Isolate item lien quan.
- Verify clash/resolved neu co du geometry.
- Model clash point.
- Export BCF va Excel tuong thich Trimble Connect.

Plan nay chia theo 3 lop: noi dung nghiep vu, giao dien, va code/logic cho tung button.

## Trang Thai Hien Tai

Trong workspace hien tai khong thay folder `src/NavisIssueManager`, nen plan duoc lap dua tren:

- Code Revit Issue Manager hien co trong `src/Antigravity.IssueManager`.
- Cac loi thuc te khi xuat BCF sang Trimble Connect.
- Nhu cau lam tool tuong tu tren Navisworks.

Khi co source Navisworks plugin, can doi chieu lai:

- Ten project/csproj.
- Version Navisworks API.
- Cac class plugin entrypoint.
- Thu muc bundle/package `PackageContents.xml`.

## Nguyen Tac San Pham

Navis Issue Manager nen uu tien:

- Lam viec truc tiep voi Navisworks viewpoint, selection, saved viewpoints va clash result.
- Lay data item/model tu Navisworks thay vi tu Revit.
- Xuat BCF dung coordinate/viewpoint cua Navisworks de Trimble hien dung.
- Khong tu chen referenced model name neu khong chac khop model tren Trimble.
- Neu chen `Header/File/Filename`, chi chen ten model coordination that su nhu `.nwc`, `.nwd`, `.ifc`, `.dwg`.
- Giu UI gan voi Revit Issue Manager de user hoc mot lan dung duoc hai tool.

## Giao Dien De Xuat

### Main Window

Layout giu tuong tu Revit:

- Header:
  - Logo `AG`
  - Ten app: `AUTO ISSUE MANAGER @Vilai Viet`
  - Subtitle: `Manage, create and review BCF issues`

- Toolbar tren:
  - `Load BCF`
  - `Load XML`
  - `Reload Backup`
  - `Create Issue`
  - `Export BCF`
  - `Export Excel`
  - `New Folder`
  - `Section Box` hoac `Clip Box`
  - `Shared Coords` neu Navis model co he toa do project/shared can quan ly
  - Combobox view mode: `Flat List`, `Group By Element`, `Group By Folder`

- Cot trai:
  - List issue.
  - Moi item hien title, file/model summary, status.

- Panel phai:
  - Title/detail selected issue.
  - Status combo.
  - Folder combo.
  - New folder button.
  - 2 preview images:
    - Snapshot 3D/Viewpoint.
    - Snapshot 2D/annotation/clash image.
  - Action bar duoi:
    - `Edit`
    - `Show in Model`
    - `Isolate Clash Elements`
    - `Verify`
    - `Model Clash Point`
    - `Delete`

- Footer:
  - Dashboard: Total, New, Active, Resolved, Approved.

### Create/Edit Issue Dialog

Giu cac truong:

- `Title`
- `Level`
- `Assigned To`
- `Description`
- Preview anh 3D Navisworks snapshot.
- Preview anh 2D/chu thich.
- Button:
  - `Capture 3D` neu can chup lai viewpoint.
  - `Markup 3D`
  - `Choose Image`
  - `Markup 2D`
  - `Clear Image`
  - `Create Issue` / `Save Changes`
  - `Cancel`

### Markup Editor

Dung lai concept hien co:

- Pen
- Rect
- Arrow
- Text
- Undo
- Clear
- Save markup
- Cancel

## Data Model De Xuat

Co the dung lai `IssueModel` va `ViewpointModel`, nhung them/doi ten mot so truong Navis-specific neu can:

- `IssueModel`
  - `IssueId`
  - `IssueCode`
  - `Title`
  - `Level`
  - `Folder`
  - `AssignedTo`
  - `Description`
  - `Status`
  - `Author`
  - `CreationDate`
  - `Distance`
  - `Viewpoint`

- `ViewpointModel`
  - `CoordinateMode`
  - Camera position/direction/up.
  - Orthogonal/perspective + scale/FOV.
  - Clash point.
  - `ElementIds` hoac Navis item instance ids.
  - `ComponentIfcGuids`.
  - `ClashModelFiles`.
  - `ClippingPlanes`.
  - Snapshot paths/base64.
  - Navis-specific optional:
    - `SavedViewpointName`
    - `SelectionPaths`
    - `ModelItemInstanceGuids`
    - `SourceClashTestName`
    - `SourceClashResultName`

## Code Architecture De Xuat

### Project Structure

```text
src/NavisIssueManager/
  NavisIssueManager.csproj
  PackageContents.xml
  Plugin.cs
  UI/
    IssueManagerWindow.xaml
    IssueManagerWindow.xaml.cs
    CreateIssueDialog.xaml
    CreateIssueDialog.xaml.cs
    ExportIssueSelectionDialog.xaml
    MarkupEditorWindow.xaml
  Models/
    IssueModel.cs
    ViewpointModel.cs
    IssueGroupModel.cs
  Services/
    BcfZipParser.cs
    BcfExporter.cs
    NavisworksXmlParser.cs
    ExcelIssueExporter.cs
    IssueStorageService.cs
    IssueBackupService.cs
    NavisIssueCreator.cs
    NavisCameraSync.cs
    NavisSnapshotService.cs
    NavisSelectionResolver.cs
    NavisClashVerifier.cs
  Commands/
    CmdOpenIssueManager.cs
```

### Navisworks API Lop Can Co

- `NavisIssueCreator`
  - Lay current viewpoint.
  - Lay current selection.
  - Export snapshot.
  - Tao `IssueModel`.

- `NavisCameraSync`
  - Apply camera/viewpoint vao Navisworks.
  - Restore clipping/section box neu API ho tro.
  - Zoom to selection/clash point.

- `NavisSelectionResolver`
  - Map `ElementIds`, `IfcGuid`, `AuthoringToolId`, item path sang `ModelItem`.
  - Tim item trong loaded models.

- `NavisSnapshotService`
  - Capture current viewport image.
  - Normalize image path/temp folder.

- `NavisClashVerifier`
  - Neu co clash result/model item pair, kiem tra clash con ton tai.
  - Neu khong, fallback theo bounding box distance/intersection.

- `IssueStorageService`
  - Navisworks khong co Revit Extensible Storage.
  - De xuat luu JSON sidecar theo file NWF/NWD:
    - `%LOCALAPPDATA%/AntigravityIssueManager/Navis/{projectHash}.json`
    - Hoac cung folder voi project neu user cho phep.

## Button Plan Chi Tiet

### 1. Load BCF

Noi dung:

- Import `.bcfzip`.
- Tao issue list tu topic/viewpoint/snapshot.
- Giu snapshot trong temp folder.

UI:

- File dialog chon `.bcfzip`.
- Sau import refresh issue list, dashboard, group.
- Thong bao tong so issue import.

Code:

- Dung chung logic `BcfZipParser`.
- Sau parse:
  - Merge vao `_currentIssues`.
  - Restore snapshot.
  - `RefreshIssueViews()`.
  - `AutoSaveIssues()`.

Navis-specific:

- BCF viewpoint co the apply truc tiep vao Navis camera hon Revit.
- Can map `Components/Component IfcGuid/AuthoringToolId` sang `ModelItem`.

Pseudo:

```csharp
private void BtnLoadBcf_Click(...)
{
    string path = PickBcfZip();
    List<IssueModel> loaded = _bcfParser.ParseBcfZip(path);
    _currentIssues = MergeIssues(_currentIssues, loaded);
    RefreshIssueViews();
    AutoSaveIssues();
}
```

### 2. Load XML

Noi dung:

- Import Navisworks Clash XML.
- Lay clash result, clash point, item ids, model files, images.

UI:

- File dialog chon `.xml`.
- Sau import hien summary:
  - so issue
  - item files found
  - element ids found
  - snapshots found
  - diagnostic path neu thieu data

Code:

- Dung/port `NavisworksXmlParser`.
- Can bo sung parser cho field Navisworks neu Navis XML project thuc te khac.

Navis-specific:

- XML tu Navis co the gan truc tiep voi clash result trong current document neu ten test/result trung.
- Luu `SourceClashTestName`, `SourceClashResultName`.

### 3. Reload Backup

Noi dung:

- Load backup JSON moi nhat hoac project-specific.

UI:

- Confirm neu current issue list co data.
- Thong bao so issue restore.

Code:

- Port `IssueBackupService`.
- Voi Navis, project identifier nen la:
  - `Application.ActiveDocument.FileName`
  - Hoac NWF/NWD full path.

Pseudo:

```csharp
private void BtnReloadBackup_Click(...)
{
    string projectId = GetNavisProjectIdentifier();
    var backup = IssueBackupService.LoadLatestBackup(projectId);
    _currentIssues = MergeIssuesPreferBackup(_currentIssues, backup);
    RefreshIssueViews();
}
```

### 4. Create Issue

Noi dung:

- Tao issue tu current Navis viewpoint/selection.
- Chup snapshot 3D.
- Luu selected model items.
- Luu camera, clip/section box neu co.

UI:

- Mo `CreateIssueDialog`.
- User nhap title, level, assigned, description.
- Button capture/markup anh.

Code:

- `NavisIssueCreator.CreateIssueFromCurrentView`.
- Lay:
  - Current viewpoint camera.
  - Current selection.
  - Selected item ids/IFC GUID/model files.
  - Snapshot.

Navis-specific:

- Neu Navis API cung cap `SavedViewpoint`, co the copy current viewpoint.
- Neu dang chon clash result, lay clash point va hai clash items.

Pseudo:

```csharp
IssueModel issue = NavisIssueCreator.CreateIssueFromCurrentView(
    navisApp,
    title,
    description,
    includeClipPlanes,
    useSharedCoordinates,
    image2DPath,
    level,
    assignedTo);
```

### 5. Export BCF

Noi dung:

- Xuat selected/all issue thanh `.bcfzip`.
- Tuong thich Trimble Connect.

UI:

- Mo `ExportIssueSelectionDialog`.
- Save file dialog.
- Thong bao ket qua.

Code:

- Dung chung `BcfExporter`.
- Truoc export can validate:
  - `CoordinateMode`
  - camera not zero
  - snapshot exists
  - referenced model filenames hop le.

Rules Trimble:

- Khong ghi `.rvt`.
- Khong ghi filename khong extension.
- Chi ghi referenced model neu co duoi `.nwc`, `.nwd`, `.ifc`, `.dwg`.
- Neu khong chac ten model khop Trimble, de `Header` rong con tot hon ghi sai.

### 6. Export Excel

Noi dung:

- Xuat RFI register.
- Cot `Nguoi nhan` lay tu `IssueModel.AssignedTo`.

UI:

- Mo `ExportIssueSelectionDialog`.
- Save file dialog `.xlsx`.
- Thong bao ket qua.

Code:

- Dung chung `ExcelIssueExporter`.
- Dam bao mapping:
  - `Nguoi tao` = `Author`
  - `Nguoi nhan` = `AssignedTo`
  - `Tang` = `Level`
  - `Tieu de` = `Title`
  - `Noi dung cau hoi` = `Description`
  - `Trang thai` = `Status`

### 7. New Folder

Noi dung:

- Tao folder/group noi bo.
- Co the gan folder cho selected issue hoac chi them vao list folder.

UI:

- Dialog nhap folder name.

Code:

- Giu `_knownFolders`.
- Update `IssueModel.Folder`.
- Refresh group/list.
- Autosave.

### 8. Section Box / Clip Box Checkbox

Noi dung:

- Khi tao issue, co luu clip planes/section box cua current Navis viewpoint hay khong.

UI:

- Checkbox tren toolbar.
- Mac dinh `True` neu workflow can review local issue.

Code:

- `NavisIssueCreator` doc clip plane/section box tu current viewpoint.
- Luu vao `Viewpoint.ClippingPlanes`.

Navis-specific:

- Ten UI co the doi thanh `Clip Box` neu Navisworks API goi la clipping/sectioning.

### 9. Shared Coords Checkbox

Noi dung:

- Kiem soat he toa do khi tao/edit issue.

UI:

- Checkbox mac dinh bat neu Trimble project dung shared coordinates.

Code:

- Voi Navis, can xac dinh:
  - Current model coordinate da la shared hay internal?
  - Co transform nao tu model item/document sang shared?
- Neu Navis viewpoint da dung world/project coordinates dung voi Trimble, co the luu truc tiep va chi gan `CoordinateMode = "Shared"`.

Can test bang:

- X/Y trong BCF co khop project Trimble hay khong.
- View co hien dung sau upload Trimble hay khong.

### 10. View Mode Combo

Noi dung:

- `Flat List`
- `Group By Element`
- `Group By Folder`

UI:

- Combobox tren toolbar.

Code:

- `BuildIssueGroups()`.
- Group by element:
  - Revit dung element id.
  - Navis nen group by `IfcGuid`, `AuthoringToolId`, hoac model item instance id.

### 11. Status Combo

Noi dung:

- Doi status cua selected issue.

UI:

- Combo trong detail panel.

Code:

- Update `IssueModel.Status`.
- Refresh dashboard/list.
- Autosave.

### 12. Folder Combo va New Folder For Issue

Noi dung:

- Gan issue vao folder.
- Tao folder nhanh cho selected issue.

UI:

- Folder combo.
- Button `New`.

Code:

- `UpdateIssueFolder`.
- Autosave.

### 13. Edit

Noi dung:

- Sua title, level, assigned, description, anh.
- Neu `Shared Coords` bat, convert/normalize viewpoint sang shared neu can.

UI:

- Mo `CreateIssueDialog` o edit mode.
- Button text `Save Changes`.

Code:

- Update `IssueModel`.
- Cap nhat `Viewpoint.SnapshotFilePath` / `SnapshotFilePath2`.
- Navis version can co `ConvertIssueViewpointToSharedCoordinates` neu co transform ro rang.
- Autosave.

### 14. Show In Model

Noi dung:

- Dua Navis viewport ve issue viewpoint.
- Chon/highlight item lien quan neu tim duoc.

UI:

- Button trong action bar.

Code:

- `NavisCameraSync.SyncCamera`.
- Apply camera:
  - position
  - direction
  - up vector
  - FOV/orthographic scale
  - clip planes neu co
- Resolve items:
  - IFC GUID
  - AuthoringToolId
  - Model item path
  - Clash result reference
- Select/highlight items.

Pseudo:

```csharp
private void BtnShowInModel_Click(...)
{
    if (!CanNavigateIssue(_selectedIssue)) ShowInfo();
    NavisCameraSync.SyncCamera(_navisApp, _selectedIssue.Viewpoint);
}
```

### 15. Isolate Clash Elements

Noi dung:

- Isolate selected/related model items trong Navisworks.

UI:

- Button action.

Code:

- Resolve `Viewpoint.ElementIds`/`IfcGuid` thanh `ModelItemCollection`.
- Hide unselected hoac isolate selection.
- Zoom to selection.

Navis-specific:

- Dung Navisworks selection API va visibility override.
- Can co nut/logic restore visibility neu user can.

### 16. Verify

Noi dung:

- Kiem tra clash/issue con ton tai khong.

UI:

- Button action.
- Thong bao ket qua.

Code:

- Neu issue co source clash result:
  - Tim clash test/result trong Navis document.
  - Doc status/result distance.
- Neu khong co source:
  - Tim 2 item lien quan.
  - Check bounding box intersection/distance.
- Cap nhat `Status` neu resolved.
- Autosave.

### 17. Model Clash Point

Noi dung:

- Tao/visualize clash point trong Navisworks.

UI:

- Button action.

Code:

- Neu `Viewpoint.HasClashPoint`, tao marker/temporary viewpoint/annotation tai point.
- Neu Navis API khong tao geometry tam, co the:
  - tao saved viewpoint tai clash point
  - zoom camera toi point
  - select related items

### 18. Delete

Noi dung:

- Xoa issue khoi list noi bo.

UI:

- Confirm yes/no.

Code:

- Remove selected issue.
- Select issue ke tiep neu co.
- Refresh dashboard/list.
- Autosave.

### 19. Markup 3D / Markup 2D

Noi dung:

- Mo anh snapshot trong Markup Editor.
- Luu anh markup moi.

UI:

- Button trong detail panel va Create/Edit dialog.

Code:

- Dung lai `MarkupEditorWindow`.
- Sau save, cap nhat path anh tuong ung.
- Autosave.

### 20. Export Selection Dialog Buttons

Buttons:

- `Select All`
- `Select None`
- `Export`
- `Cancel`

Code:

- Dung lai `ExportIssueSelectionDialog`.
- Selected issues dua vao BCF/Excel exporter.

### 21. Folder Dialog Buttons

Buttons:

- `OK`
- `Cancel`

Code:

- Validate folder name khong rong.
- Return `FolderName`.

## Logic Luu Tru Cho Navis

Vi Navisworks khong co Revit Extensible Storage, can dung JSON:

### Option A - Local AppData

Path:

`%LOCALAPPDATA%/AntigravityIssueManager/Navis/{projectHash}/issues.json`

Uu diem:

- Khong sua file NWF/NWD.
- An toan.

Nhuoc diem:

- User chuyen may co the mat issue neu khong copy backup.

### Option B - Sidecar Gan File Navis

Path:

`{projectFile}.issues.json`

Uu diem:

- Di kem project.

Nhuoc diem:

- Can quyen ghi folder project.
- Co the lam roi folder deliverable.

Khuyen nghi:

- Dung AppData lam chinh.
- Co nut export/import backup neu can.

## BCF Compatibility Checklist

Truoc khi upload Trimble:

- `viewpoint.bcfv` co camera hop le.
- Neu project Trimble dung shared, X/Y phai o vung shared dung.
- `Header/File/Filename` khong ghi `.rvt`.
- Chi ghi referenced model khi khop ten model tren Trimble.
- Snapshot ton tai.
- Topic title/description khong rong.
- Status hop le: `Active`, `Resolved`, etc.

## Navis API Research Tasks

Can kiem tra trong Navisworks API:

1. Cach lay current viewpoint camera.
2. Cach set current viewpoint camera.
3. Cach export viewport snapshot.
4. Cach lay selected model items.
5. Cach lay IFC GUID / Entity Handle / Source file name cua model item.
6. Cach isolate/hide/unhide model items.
7. Cach tao marker/clash point.
8. Cach doc clash tests/results neu co Manage API.
9. Cach lay clipping planes/sectioning current viewpoint.
10. Cach tao ribbon/button/palette window bang plugin.

## Thu Tu Trien Khai

### Phase 1 - Scaffold Navis Plugin

- Tao project `NavisIssueManager`.
- Tao `PackageContents.xml`.
- Tao plugin entrypoint.
- Tao button mo Issue Manager window.
- Dung UI WPF cua Revit Issue Manager voi namespace moi.

### Phase 2 - Core Data/UI

- Port models.
- Port main window.
- Port create/edit dialog.
- Port markup editor.
- Port backup JSON.
- Port dashboard/group/status/folder logic.

### Phase 3 - Import/Export

- Port `BcfZipParser`.
- Port `BcfExporter`.
- Port `NavisworksXmlParser`.
- Port `ExcelIssueExporter`.
- Test Excel mapping `AssignedTo -> Nguoi nhan`.

### Phase 4 - Navis Viewpoint Integration

- Implement `NavisIssueCreator`.
- Implement snapshot capture.
- Implement camera capture.
- Implement `Show in Model`.
- Implement isolate selection.

### Phase 5 - Clash Workflow

- Parse/attach clash result references.
- Verify clash.
- Model clash point.

### Phase 6 - Trimble Validation

- Tao BCF tu Navis.
- Upload Trimble.
- Kiem tra:
  - view hien dung
  - model khong bi tat sai
  - no referenced model not found
  - snapshot dung
  - topic metadata dung

## Risk Va Cach Giam Thieu

- Sai toa do:
  - Log camera X/Y/Z va coordinate mode.
  - Test voi file Trimble that.

- Sai referenced model:
  - Khong ghi filename neu khong chac.
  - Chi cho `.nwc/.nwd/.ifc/.dwg`.

- Khong resolve duoc item:
  - Luu nhieu identifier: IFC GUID, AuthoringToolId, Navis path, source file.

- Snapshot mat file:
  - Luu base64 backup.
  - Restore snapshot khi load.

- Navis API khac Revit:
  - Tach service Navis-specific, giu model/exporter dung chung.

## Definition Of Done

Mot ban Navis Issue Manager dat yeu cau khi:

- Mo duoc window tu Navisworks ribbon/tool.
- Load BCF/XML thanh issue list.
- Tao issue tu current viewpoint/selection.
- Edit/save assigned/title/description/snapshot.
- Export Excel dung cot `Nguoi nhan`.
- Export BCF upload Trimble hien dung viewpoint.
- Show in Model dua ve dung viewpoint trong Navis.
- Isolate item lien quan.
- Autosave/backup hoat dong.
- Khong ghi referenced model sai lam Trimble an/tat model.
