# Antigravity Issue Manager - Features And Logic

## Muc Dich

`Antigravity.IssueManager` la Revit add-in dung de quan ly issue/RFI noi bo, dong bo issue tu BCF/BCFzip va Navisworks XML, tao issue truc tiep trong Revit, xem lai viewpoint, markup anh, luu issue vao RVT va xuat lai BCF/Excel.

Cong cu duoc toi uu cho workflow kiem tra model trong Revit va dua issue sang Trimble Connect. Diem quan trong nhat voi Trimble la viewpoint phai dung he toa do, thuong la `Shared`, neu model tren Trimble dang theo shared coordinates.

## Thanh Phan Chinh

- UI chinh: `UI/IssueManagerWindow.xaml` va `UI/IssueManagerWindow.xaml.cs`
- Dialog tao/sua issue: `UI/CreateIssueDialog.xaml(.cs)`
- Dialog chon issue de export: `UI/ExportIssueSelectionDialog.xaml(.cs)`
- Model du lieu: `Models/IssueModel.cs`, `Models/ViewpointModel.cs`, `Models/IssueGroupModel.cs`
- Import BCF: `Services/BcfZipParser.cs`
- Export BCF: `Services/BcfExporter.cs`
- Import Navisworks XML: `Services/NavisworksXmlParser.cs`
- Xuat Excel: `Services/ExcelIssueExporter.cs`
- Luu issue vao RVT: `Services/IssueStorageService.cs`, `Handlers/SaveIssuesHandler.cs`
- Backup ngoai RVT: `Services/IssueBackupService.cs`
- Dong bo camera/viewpoint trong Revit: `Services/RevitCameraSync.cs`
- Tao issue tu view hien tai: `Services/RevitIssueCreator.cs`, `Handlers/CreateIssueHandler.cs`

## Du Lieu Issue

`IssueModel` luu cac truong:

- `IssueId`: GUID cua issue/topic.
- `IssueCode`: ma issue, vi du `GAMUD-200`, neu parser lay duoc tu title/description.
- `Title`, `DisplayTitle`, `Description`.
- `Status`, `Author`, `CreationDate`.
- `Level`, `AssignedTo`, `Folder`.
- `Distance`: thong tin rieng tu Navisworks XML.
- `Viewpoint`: du lieu camera, snapshot, element ids, clash files, clipping planes.

`ViewpointModel` luu:

- `CoordinateMode`: `Internal` hoac `Shared`.
- Camera: `CameraX/Y/Z`, `CameraDirectionX/Y/Z`, `CameraUpX/Y/Z`.
- Orthogonal camera: `IsOrthogonal`, `ViewToWorldScale`.
- Clash point: `HasClashPoint`, `ClashPointX/Y/Z`.
- `ElementIds`: Revit element id hoac authoring id/IFC guid.
- `ClashModelFiles`: ten model lien quan lay tu BCF/Navisworks.
- `ComponentIfcGuids`: map element id sang IFC GUID.
- `ClippingPlanes`: section box/clipping planes cua viewpoint.
- `SnapshotFilePath`, `SnapshotBase64`, `SnapshotFilePath2`, `SnapshotBase642`.

## UI Va Workflow Chinh

Toolbar chinh gom:

- `Load BCF`: import issue tu file `.bcfzip`.
- `Load XML`: import issue tu Navisworks XML.
- `Reload Backup`: load issue tu backup JSON.
- `Create Issue`: tao issue moi tu view hien tai trong Revit.
- `Export BCF`: xuat issue dang co thanh `.bcfzip`.
- `Export Excel`: xuat bang issue sang Excel.
- `New Folder`: tao folder/group noi bo.
- `Section Box`: khi tao issue, co luu clipping planes cua section box hay khong.
- `Shared Coords`: mac dinh dang bat; khi tao issue hoac save edit issue se dung/toi uu sang shared coordinates.
- `Flat List / Group By Element / Group By Folder`: che do hien thi list issue.

Khi chon issue, panel detail hien:

- Title, author, ngay tao, level, assigned, distance.
- Status va folder.
- Anh snapshot 3D/2D.
- Cac nut: `Edit`, `Show in Model`, `Isolate Clash Elements`, `Verify`, `Model Clash Point`, `Delete`.

## Tao Issue Tu Revit

Luồng tao issue:

1. User bam `Create Issue`.
2. Dialog nhap title, description, level, assigned, anh 2D/3D.
3. Neu user capture 3D, `CreateIssueHandler` goi `RevitIssueCreator.ExportCurrentViewSnapshot`.
4. Khi create, `CreateIssueHandler` goi `RevitIssueCreator.CreateIssueFromCurrentView`.
5. `RevitIssueCreator` lay camera tu active `View3D`.
6. Neu `Shared Coords` bat, camera point/vector va clipping planes duoc convert tu Revit internal coordinates sang shared BCF coordinates.
7. Neu `Section Box` bat va view co section box, luu 6 clipping planes vao viewpoint.
8. Tao `IssueModel` voi `CoordinateMode = "Shared"` hoac `"Internal"`.
9. UI them issue vao list va autosave.

Mac dinh hien tai: `Shared Coords` bat san trong UI.

## Edit Issue Va Convert Shared Coordinates

Khi user bam `Edit` roi `Save`:

1. Cap nhat title, level, assigned, description va duong dan snapshot.
2. Neu checkbox `Shared Coords` dang bat, goi logic convert issue viewpoint sang shared.
3. Convert cac du lieu sau:
   - `CameraViewPoint`
   - `CameraDirection`
   - `CameraUpVector`
   - `ClippingPlanes.Location`
   - `ClippingPlanes.Direction`
   - `ClashPoint`, neu co
4. Dat `Viewpoint.CoordinateMode = "Shared"`.
5. Neu issue da la `Shared`, khong convert lai de tranh nhan doi offset.
6. Refresh UI va autosave.

Dung workflow nay de sua issue cu da tao bang `Internal`: tick `Shared Coords`, bam `Edit`, bam `Save`, roi export lai BCF.

## Import BCF

`BcfZipParser.ParseBcfZip`:

1. Bung `.bcfzip` vao temp folder `%TEMP%/AntigravityBcf/{guid}`.
2. Moi folder con co `markup.bcf` duoc coi la mot issue.
3. Doc `markup.bcf`:
   - `Header/File/Filename` vao `Viewpoint.ClashModelFiles`.
   - `Topic.Guid`, `Title`, `Description`, `TopicStatus`, `CreationAuthor`.
   - Extract `IssueCode` tu title/description neu co dang `ABC-123`.
   - Doc `Viewpoints/Viewpoint` va `Viewpoints/Snapshot`.
   - Tim `Coordinate Mode: Internal|Shared` trong comment.
4. Doc `viewpoint.bcfv`:
   - Orthogonal/Perspective camera.
   - `CameraViewPoint`, `CameraDirection`, `CameraUpVector`.
   - `ViewToWorldScale` neu orthogonal.
   - `ClippingPlanes`.
   - `Components/Component` de lay `IfcGuid` va `AuthoringToolId`.
5. Gan snapshot path trong temp folder de UI hien anh.

Luu y: temp folder khong bi xoa ngay vi UI can load snapshot.

## Import Navisworks XML

`NavisworksXmlParser`:

- Parse clash test XML tu Navisworks.
- Lay title, description, distance, clash point.
- Tim anh snapshot tu duong dan href trong XML.
- Parse element id, IFC GUID, smart tags.
- Parse model file name bang nhieu heuristic:
  - `clashobject`
  - attribute/text co path model
  - regex cac duoi `.nwc`, `.nwd`, `.rvt`, `.ifc`, `.dwg`
- Luu ten model vao `Viewpoint.ClashModelFiles`.
- Sinh diagnostic/log de debug neu XML thieu item files/element ids/snapshot.

## Show In Model

`RevitCameraSync.SyncCamera`:

1. Lay identifiers tu `ElementIds` va `ComponentIfcGuids`.
2. Thu match element trong host document va linked documents.
3. Neu match du element va khong co clipping planes dung duoc:
   - isolate element
   - tao section box quanh element
   - zoom toi section box
4. Neu khong match du element:
   - convert camera/clipping tu BCF sang Revit internal coordinates.
   - thu nhieu candidate:
     - Shared theo `ProjectPosition inverse`
     - Shared theo `ProjectLocation.GetTransform inverse`
     - Internal raw coordinates
     - co bien the flip direction
   - chon candidate gan model box/target nhat.
   - apply camera orientation va section box.
5. Co log timing va debug de xem candidate coordinate nao duoc chon.

## Luu Tru Va Backup

### Luu Vao RVT

`IssueStorageService` luu list issue vao Revit `DataStorage` bang Extensible Storage:

- Schema GUID: `7B13F48C-21C5-47B2-8C7F-7F4F52F0B39C`
- Schema name: `Antigravity_IssueManager_Schema`
- Field: `IssuesJson`

`SaveIssuesHandler` chay trong Revit ExternalEvent:

1. Mo transaction `Save Antigravity Issues`.
2. Goi `IssueStorageService.SaveIssuesToDocument`.
3. Commit transaction.

### Backup JSON

`IssueBackupService` luu backup vao:

`%LOCALAPPDATA%/AntigravityIssueManager/Backups`

Cac file:

- `issues_autobackup_latest.json`: backup global moi nhat.
- `issues_autobackup_{projectHash}.json`: backup theo project.
- `issues_autobackup_yyyyMMdd_HHmmss.json`: backup timestamp.

Chi giu toi da 30 timestamp backups.

### Auto Load

Khi window loaded:

1. Load issue tu RVT Extensible Storage.
2. Restore snapshot tu base64 neu co.
3. Merge backup theo project neu co.
4. Co fallback sang latest backup neu can.
5. Refresh dashboard va UI.

## Snapshot Va Markup

- Snapshot 3D chinh: `Viewpoint.SnapshotFilePath`.
- Snapshot 2D/Clash image: `Viewpoint.SnapshotFilePath2`.
- Truoc khi luu/autosave/export Excel, app co the encode snapshot thanh base64 de luu ben trong JSON/RVT.
- `MarkupEditorWindow` mo anh snapshot va cho user ve markup, sau do luu ra file anh moi va cap nhat duong dan snapshot.

## Export BCF

`BcfExporter.ExportToBcf` tao file `.bcfzip` gom:

- `bcf.version`
- `project.bcfp`
- Moi issue mot folder GUID:
  - `markup.bcf`
  - `viewpoint.bcfv`
  - `snapshot.png` neu co snapshot

`markup.bcf` gom:

- `Header`
- `Topic`
- `Comment`
- `Viewpoints`

`viewpoint.bcfv` gom:

- `VisualizationInfo`
- `PerspectiveCamera` hoac `OrthogonalCamera`
- `ClippingPlanes` neu co

### Header/File/Filename

Trimble Connect doc `Header/File/Filename` nhu danh sach referenced model cua topic.

Neu ghi sai ten model, Trimble bao:

`This topic has the following referenced model(s) that were not found`

Va co the tu dong an/tat model theo referenced model bi loi.

Do do exporter hien tai chi cho ghi `Header/File/Filename` khi ten model co duoi coordination duoc ho tro:

- `.nwc`
- `.nwd`
- `.ifc`
- `.dwg`

Exporter bo qua:

- `.rvt`
- ten model khong co extension
- fallback tu Revit document/link title

Ly do: Trimble project dang dung model NWC/NWD/IFC, khong phai RVT name. Ghi `.rvt` hoac ten khong khop lam Trimble relink sai.

### Coordinate Mode

Comment trong BCF co dong:

`Coordinate Mode: Shared`

hoac:

`Coordinate Mode: Internal`

Day la metadata rieng cua add-in de parse lai va debug.

Voi Trimble project Gamuda hien tai, file thanh cong co nhieu viewpoint dung shared coordinates:

- X khoang `597000`
- Y khoang `2304600`

File loi cu ngay 22/6 co toan bo viewpoint internal:

- X khoang `2400`
- Y khoang `4200`

Vi vay issue tao/export cho Trimble nen dung `Shared Coords`.

## Export Excel

`ExcelIssueExporter` xuat danh sach issue sang `.xlsx`:

- Thong tin issue: title, status, author, date, level, assigned, distance.
- Noi dung description.
- Reference text gom element ids, model files, IFC GUID neu co.
- Anh snapshot neu co.
- Dinh dang workbook, sheet, row/column, image relationships.

## Dashboard Va Grouping

Dashboard dem:

- Total
- New
- Active
- Resolved
- Approved

List co the hien:

- Flat list.
- Group by element: gom cac issue co chung element id.
- Group by folder: gom theo `IssueModel.Folder`; folder mac dinh la `Uncategorized`.

## Status, Folder, Autosave

- Doi status trong combo se cap nhat issue, refresh list/dashboard va autosave.
- Doi folder hoac tao folder moi cung autosave.
- Delete issue xoa khoi list va autosave.
- Edit issue save xong autosave.
- Verify clash xong autosave neu issue thay doi.

## Verify Clash

`VerifyClashHandler`:

- Dung `Viewpoint.ElementIds`.
- Tim element trong Revit.
- Kiem tra clash/resolved theo logic hinh hoc.
- Cap nhat issue/status va thong bao ket qua qua callback.

## Isolate Clash Elements

`IsolateClashElementsHandler`:

- Lay `Viewpoint.ElementIds`.
- Chuyen string id thanh Revit `ElementId`.
- Isolate tam thoi cac element tim duoc trong active view.

## Model Clash Point

`ModelClashPointHandler`:

- Neu viewpoint co `HasClashPoint`, tao/visualize clash point trong model.
- Toa do clash point duoc doi tu meters sang Revit internal feet khi tao geometry.

## Cac Luu Y Debug Trimble

### 1. Loi Missing Related Model File Name

Thong bao:

`This topic is missing the related model file name`

Nguyen nhan:

- `markup.bcf` co `Header` rong, khong co `File/Filename`.

Anh huong:

- Trimble khong biet topic lien quan model nao.
- Tuy nhien, day khong nhat thiet la nguyen nhan view khong hien. File 19/6 thanh cong cung co the thieu Filename.

### 2. Loi Referenced Model Not Found

Thong bao:

`This topic has the following referenced model(s) that were not found`

Nguyen nhan:

- BCF ghi `Header/File/Filename` khong khop model trong Trimble.
- Vi du ghi `.rvt` hoac ten khong duoi trong khi Trimble dung `.nwc`.

Anh huong:

- Trimble co the bat/tat model theo referenced model sai.
- View co the trong/khong hien dung.

Fix:

- Khong ghi fallback `.rvt`.
- Chi ghi `.nwc`, `.nwd`, `.ifc`, `.dwg` neu biet chac dung ten.

### 3. Loi View Bay Xa Hoac Khong Hien

Nguyen nhan thuong gap:

- Issue o `Coordinate Mode: Internal` nhung Trimble model dang theo shared coordinates.
- Viewpoint X/Y nho khoang `2400/4200` trong khi model Trimble can X/Y khoang `597000/2304600`.

Fix:

- Bat `Shared Coords`.
- Voi issue cu: `Edit` -> `Save` de convert viewpoint sang Shared.
- Export lai BCF.

### 4. Kiem Tra File BCF

Mo `.bcfzip` nhu zip va kiem tra:

- `{topicGuid}/markup.bcf`
- `{topicGuid}/viewpoint.bcfv`

Trong `markup.bcf`, xem:

- `Header/File/Filename`
- comment `Coordinate Mode`

Trong `viewpoint.bcfv`, xem:

- `CameraViewPoint`
- `CameraDirection`
- `OrthogonalCamera/ViewToWorldScale`
- `ClippingPlanes`

## Workflow Khuyen Nghi Cho Trimble Connect

1. Mo Revit model dung project/shared coordinates.
2. Dam bao `Shared Coords` dang bat.
3. Tao issue moi hoac edit-save issue cu de convert sang Shared.
4. Export BCF.
5. Kiem tra file BCF:
   - `Coordinate Mode: Shared`
   - `CameraViewPoint.X/Y` nam trong vung shared cua project.
   - Khong co `Header/File/Filename` sai ten `.rvt`.
6. Upload vao Trimble.

## Trang Thai Code Gan Day

- `Shared Coords` mac dinh bat trong UI.
- `Edit Issue -> Save` co the convert issue cu tu Internal sang Shared.
- Exporter da chan ghi referenced model `.rvt` va ten khong co extension.
- Exporter chi ghi referenced model co extension `.nwc`, `.nwd`, `.ifc`, `.dwg`.
- Build da pass voi `dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj`.
