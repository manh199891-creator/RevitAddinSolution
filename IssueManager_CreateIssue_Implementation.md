# Issue Manager - Create Issue va Export BCF

## Nguon yeu cau

File plan:

```text
.awf-pipeline\inbox-ide\Plan_CreateIssue_v1.0.md
```

Muc tieu:

- Tao issue moi truc tiep tu Revit active 3D view.
- Luu camera, selected element ids va snapshot vao `IssueModel`.
- Export toan bo danh sach issue ra file `.bcfzip`.

## File da them

### `Services\RevitIssueCreator.cs`

Duong dan:

```text
src\Antigravity.IssueManager\Services\RevitIssueCreator.cs
```

Chuc nang:

- Lay `UIApplication`, `UIDocument`, `Document`.
- Yeu cau active view phai la `View3D`.
- Lay `ViewOrientation3D` tu active 3D view.
- Convert toa do camera tu Revit internal feet sang meters.
- Xu ly nguoc Project Base Point transform de dua camera ve BCF world coordinates.
- Lay selection hien tai trong Revit:

```csharp
uiDoc.Selection.GetElementIds()
```

- Chuyen selected `ElementId` thanh string.
- Export snapshot active view bang `doc.ExportImage()`.
- Tao va return `IssueModel`.

### `Services\BcfExporter.cs`

Duong dan:

```text
src\Antigravity.IssueManager\Services\BcfExporter.cs
```

Chuc nang:

- Export `List<IssueModel>` thanh file `.bcfzip`.
- Tao root file:

```text
bcf.version
```

- Voi moi issue, tao folder theo `IssueId`.
- Trong moi folder tao:

```text
markup.bcf
viewpoint.bcfv
snapshot.png
```

- `viewpoint.bcfv` ghi selected element ids vao:

```xml
<Component AuthoringToolId="...">
  <OriginatingSystem>Autodesk Revit 2024</OriginatingSystem>
</Component>
```

### `UI\CreateIssueDialog.xaml`

Duong dan:

```text
src\Antigravity.IssueManager\UI\CreateIssueDialog.xaml
```

Chuc nang:

- Dialog nho de nhap title va description.
- Title la bat buoc.
- Co 2 nut:

```text
Create Issue
Cancel
```

### `UI\CreateIssueDialog.xaml.cs`

Duong dan:

```text
src\Antigravity.IssueManager\UI\CreateIssueDialog.xaml.cs
```

Chuc nang:

- Validate title khong rong.
- Expose properties:

```csharp
public string IssueTitle { get; private set; }
public string IssueDescription { get; private set; }
```

## File da sua

### `UI\IssueManagerWindow.xaml`

Duong dan:

```text
src\Antigravity.IssueManager\UI\IssueManagerWindow.xaml
```

Da them vao toolbar:

```text
+ Create Issue
Export BCF
```

### `UI\IssueManagerWindow.xaml.cs`

Duong dan:

```text
src\Antigravity.IssueManager\UI\IssueManagerWindow.xaml.cs
```

Da them:

```csharp
BtnCreateIssue_Click
BtnExportBcf_Click
```

`BtnCreateIssue_Click`:

- Mo `CreateIssueDialog`.
- Goi `RevitIssueCreator.CreateIssueFromCurrentView(...)`.
- Add issue moi vao `_currentIssues`.
- Refresh `LvIssues`.
- Select issue moi.

`BtnExportBcf_Click`:

- Kiem tra danh sach issue khong rong.
- Mo `SaveFileDialog`.
- Goi `BcfExporter.ExportToBcf(...)`.

## Build verification

### Build rieng project IssueManager

Lenh:

```powershell
dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj
```

Ket qua:

```text
Build succeeded.
0 Warning(s), 0 Error(s)
```

### Build solution

Lenh:

```powershell
dotnet build Antigravity.sln
```

Ket qua:

```text
Build succeeded.
25 Warning(s), 0 Error(s)
```

Warning nam o cac project khac, chu yeu:

- `netDxf 3.0.0` khong co, restore sang `netDxf 3.0.1`.
- Mot so reference native/no metadata trong cac project khac.

Khong co error tu `Antigravity.IssueManager`.

## Luu y khi test trong Revit

- Can mo mot 3D view truoc khi bam `Create Issue`.
- Nen select element truoc khi tao issue neu muon issue co `AuthoringToolId`.
- Snapshot duoc export vao temp folder bang Revit `ExportImage`.
- File BCF export ra se uu tien dung Revit `ElementId` trong `AuthoringToolId`, nen khi import lai vao workflow Revit se de map hon so voi chi co `IfcGuid`.
