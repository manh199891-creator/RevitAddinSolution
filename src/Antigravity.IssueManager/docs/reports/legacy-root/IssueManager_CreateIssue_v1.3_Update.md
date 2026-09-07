# Issue Manager - Create Issue & Export BCF v1.3 Update

## Nguon yeu cau

File plan:

```text
.awf-pipeline\inbox-ide\Plan_CreateIssue_v1.3.md
```

Muc tieu v1.3:

- Dam bao file `.bcfzip` import len Trimble Connect bat duoc nut `View 3D`.
- Xuat `IfcGuid` that cua Revit element, khong dung ma ao.
- Giu dung cau truc XML BCF 2.1 cho `markup.bcf` va `viewpoint.bcfv`.
- Khong lam hong logic camera/snapshot da fix o v1.2.

## Phat hien chinh

Ban sua truoc do da co cau truc BCF 2.1 dung hon, nhung van con rui ro lon:

```text
Component IfcGuid duoc sinh ngau nhien hoac fallback tu Guid.NewGuid()
```

Day la ma hop format 22 ky tu, nhung khong phai ma dinh danh cua element trong model Revit/NWC/IFC. Trimble Connect co the doc duoc XML, nhung khong match duoc component voi model that, dan den View 3D bi mo hoac khong isolate dung element.

## Logic v1.3 da chot

### 1. RevitIssueCreator phai lay IFC GUID that

File:

```text
src\Antigravity.IssueManager\Services\RevitIssueCreator.cs
```

Khi tao issue tu selection trong Revit:

```csharp
Guid exportGuid = ExportUtils.GetExportId(doc, id);
componentIfcGuids[id.ToString()] = BcfExporter.ToIfcGuid(exportGuid);
```

Trong do:

- `AuthoringToolId` van la `ElementId` dang string.
- `IfcGuid` la compressed IFC GUID 22 ky tu tao tu export id that cua Revit.

### 2. ViewpointModel luu mapping ElementId -> IfcGuid

File:

```text
src\Antigravity.IssueManager\Models\ViewpointModel.cs
```

Da them:

```csharp
public Dictionary<string, string> ComponentIfcGuids { get; set; } = new Dictionary<string, string>();
```

Mapping nay giup exporter biet element nao tuong ung voi IFC GUID nao khi render XML.

### 3. BcfExporter khong duoc sinh Component IfcGuid ao

File:

```text
src\Antigravity.IssueManager\Services\BcfExporter.cs
```

Exporter chi ghi component khi co:

- `ComponentIfcGuids[ElementId]` hop le, hoac
- `ElementIds` da la IFC GUID 22 ky tu tu BCF import.

Khong con fallback:

```csharp
Guid.NewGuid().ToString("N").Substring(0, 22)
```

Neu khong co IFC GUID that, component do se khong duoc ghi vao `<Components>`. Cach nay tot hon viec ghi ma ao vi tranh tao BCF hop schema nhung sai semantic voi Trimble.

### 4. Thuat toan ToIfcGuid dung chung

File:

```text
src\Antigravity.IssueManager\Services\BcfExporter.cs
```

Ham:

```csharp
internal static string ToIfcGuid(Guid guid)
```

Dung alphabet buildingSMART:

```text
0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$
```

Ket qua la chuoi IFC GUID 22 ky tu.

### 5. BcfZipParser giu lai mapping khi import BCF

File:

```text
src\Antigravity.IssueManager\Services\BcfZipParser.cs
```

Khi doc component:

```xml
<Component IfcGuid="..." AuthoringToolId="..." />
```

Parser luu lai:

```csharp
issue.Viewpoint.ComponentIfcGuids[authoringId.Trim()] = ifcGuid.Trim();
```

Nho vay, neu load BCF roi export lai, code khong lam mat IFC GUID goc.

## Cau truc XML BCF 2.1 can giu nguyen

### markup.bcf

```xml
<Viewpoints Guid="viewpoint-guid">
  <Viewpoint>viewpoint.bcfv</Viewpoint>
  <Snapshot>snapshot.png</Snapshot>
</Viewpoints>
```

Khong boc them `<ViewPoint>` ben trong `<Viewpoints>`.

### viewpoint.bcfv

```xml
<VisualizationInfo Guid="viewpoint-guid">
  <PerspectiveCamera>
    ...
  </PerspectiveCamera>
  <Components>
    <Selection>
      <Component IfcGuid="ifc-guid-22" AuthoringToolId="revit-element-id" />
    </Selection>
    <Visibility DefaultVisibility="false">
      <Exceptions>
        <Component IfcGuid="ifc-guid-22" AuthoringToolId="revit-element-id" />
      </Exceptions>
    </Visibility>
  </Components>
</VisualizationInfo>
```

`Guid` cua `<VisualizationInfo>` phai trung voi `Guid` cua `<Viewpoints>` trong `markup.bcf`.

## File da sua trong v1.3

```text
src\Antigravity.IssueManager\Models\ViewpointModel.cs
src\Antigravity.IssueManager\Services\RevitIssueCreator.cs
src\Antigravity.IssueManager\Services\BcfExporter.cs
src\Antigravity.IssueManager\Services\BcfZipParser.cs
```

## Build verification

### Build rieng IssueManager

Lenh:

```powershell
dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj
```

Ket qua:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

### Build solution

Lenh:

```powershell
dotnet build Antigravity.sln
```

Ket qua:

```text
Build succeeded.
25 Warning(s)
0 Error(s)
```

Warning nam o cac project khac hoac dependency cu:

- `netDxf 3.0.0` khong tim thay, resolve sang `netDxf 3.0.1`.
- Mot so warning `MSB3246` o cac project khac.

Trong luc build solution, buoc copy sang Revit Addins folder co bao mot so DLL dang bi Revit giu lock. Viec nay khong anh huong DLL debug dung cho Revit Add-In Manager.

## DLL de test bang Revit Add-In Manager

Load DLL:

```text
E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager\bin\Debug\Antigravity.IssueManager.dll
```

Command:

```text
Antigravity.IssueManager.Commands.CmdOpenIssueManager
```

## Checklist test trong Revit

1. Mo Revit 2024.
2. Mo model co element da select duoc.
3. Mo View3D.
4. Select 1 hoac nhieu element can tao issue.
5. Load `Antigravity.IssueManager.dll` bang Revit Add-In Manager.
6. Run `CmdOpenIssueManager`.
7. Bam `Create Issue`.
8. Export `.bcfzip`.
9. Import len Trimble Connect va kiem tra:
   - Snapshot hien dung.
   - Nut `View 3D` khong bi mo.
   - Component duoc highlight/isolate dung.

## Luu y bao tri

- Khong them fallback sinh `IfcGuid` ao cho component.
- Khong doi cau truc `<Viewpoints>` trong `markup.bcf`.
- Khong bo `Guid` cua `<VisualizationInfo>`.
- Khong gom `<Selection>` va `<Visibility>` thanh component phang.
- Neu sau nay them comment/tag/grid, chi them theo schema BCF 2.1, khong chen the tuy tien vao `markup.bcf` hoac `viewpoint.bcfv`.
