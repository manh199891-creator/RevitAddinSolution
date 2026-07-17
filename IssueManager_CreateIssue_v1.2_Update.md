# Issue Manager - Create Issue v1.2 Update

## Nguon yeu cau

File plan:

```text
.awf-pipeline\inbox-ide\Plan_CreateIssue_v1.2.md
```

Muc tieu v1.2:

- Sua logic camera khi tao issue tu Revit.
- Ho tro dung BCF orthogonal camera.
- Dao nguoc camera direction theo cach BCF can.
- Chup snapshot dung visible region cua view.
- Dam bao chi tao issue tu View3D.

## Diem logic quan trong trong v1.2

### 1. Chi ho tro View3D

Khi user bam `Create Issue`, code kiem tra active view:

```csharp
doc.ActiveView.ViewType != ViewType.ThreeD
```

Neu khong phai 3D view thi throw exception va UI hien loi.

### 2. Camera direction phai dao nguoc

Theo plan v1.2, Revit `ViewDirection` va BCF `CameraDirection` nguoc huong nhau.

Da ap dung:

```csharp
view3d.ViewDirection.Negate()
```

Sau do moi ap dung inverse rotation theo Project Base Point.

### 3. Orthogonal camera center

Khong dung `ActiveView.Origin` cho orthogonal view.

Da lay tam view bang:

```csharp
UIView.GetZoomCorners()
```

Sau do tinh trung diem:

```csharp
(corners[0] + corners[1]) * 0.5
```

### 4. ViewToWorldScale

Voi orthogonal view, da tinh `ViewToWorldScale` tu chieu cao visible region cua `UIView.GetZoomCorners()`, convert tu Revit internal feet sang meters.

### 5. Snapshot export

Da cap nhat `ImageExportOptions`:

```csharp
ExportRange = ExportRange.VisibleRegionOfCurrentView
ZoomType = ZoomFitType.FitToPage
PixelSize = 1000
HLRandWFViewsFileType = ImageFileType.PNG
ShadowViewsFileType = ImageFileType.PNG
```

Vi Revit tu them ten view vao ten file export, code do tim file thuc te theo prefix roi copy ve ten snapshot chuan trong temp folder.

## File da sua

### `Models\ViewpointModel.cs`

Duong dan:

```text
src\Antigravity.IssueManager\Models\ViewpointModel.cs
```

Da them:

```csharp
public bool IsOrthogonal { get; set; }
public double ViewToWorldScale { get; set; }
```

### `Services\RevitIssueCreator.cs`

Duong dan:

```text
src\Antigravity.IssueManager\Services\RevitIssueCreator.cs
```

Da cap nhat:

- Check active view la View3D.
- Lay `UIView` tu `UIDocument.GetOpenUIViews()`.
- Dung `view3d.IsPerspective` de phan biet perspective/orthogonal.
- Perspective camera point dung `view3d.Origin`.
- Orthogonal camera point dung trung diem `UIView.GetZoomCorners()`.
- `CameraDirection = view3d.ViewDirection.Negate()`.
- `CameraUp = view3d.UpDirection`.
- Ap dung inverse Project Base Point rotation cho point/vector.
- Export snapshot theo visible region.

### `Services\BcfExporter.cs`

Duong dan:

```text
src\Antigravity.IssueManager\Services\BcfExporter.cs
```

Da cap nhat:

Neu issue la orthogonal:

```xml
<OrthogonalCamera>
  <CameraViewPoint>...</CameraViewPoint>
  <CameraDirection>...</CameraDirection>
  <CameraUpVector>...</CameraUpVector>
  <ViewToWorldScale>...</ViewToWorldScale>
</OrthogonalCamera>
```

Neu khong:

```xml
<PerspectiveCamera>
  <CameraViewPoint>...</CameraViewPoint>
  <CameraDirection>...</CameraDirection>
  <CameraUpVector>...</CameraUpVector>
  <FieldOfView>60</FieldOfView>
</PerspectiveCamera>
```

### `Services\BcfZipParser.cs`

Duong dan:

```text
src\Antigravity.IssueManager\Services\BcfZipParser.cs
```

Da cap nhat parser de khi load BCF co:

```xml
<OrthogonalCamera>
```

thi set:

```csharp
issue.Viewpoint.IsOrthogonal = true;
issue.Viewpoint.ViewToWorldScale = ...
```

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

Warning nam o cac project khac, khong phat sinh tu `Antigravity.IssueManager`.

## Luu y khi test trong Revit

- Mo View3D truoc khi bam `Create Issue`.
- Voi orthogonal 3D view, BCF export se dung `<OrthogonalCamera>`.
- Voi perspective 3D view, BCF export se dung `<PerspectiveCamera>`.
- Nen select element truoc khi tao issue de BCF co `AuthoringToolId`.
- Snapshot nam trong temp folder va duoc copy vao `.bcfzip` khi export.
