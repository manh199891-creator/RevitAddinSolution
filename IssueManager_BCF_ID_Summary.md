# Tong hop xu ly loi BCF khong tim thay ID

## Boi canh

Du an duoc kiem tra:

```text
E:\Antigravity\RevitAddinSolution\src\Antigravity.IssueManager
```

Van de ban dau: khi load file BCF trong addin Issue Manager thi khong tim thay ID va cham de chon/isolate element trong Revit.

Thu muc file BCF mau:

```text
E:\GMD\BCF\26-05-19
```

Trong thu muc nay co cac file nhu:

```text
260519.bcf
260519.bcfzip
260520-1.bcfzip
260520-2.bcfzip
260520.bcfzip
260522.bcfzip
```

## Ket qua kiem tra file BCF mau

File `260519.bcfzip` co cau truc BCF hop le:

```text
bcf.version
project.bcfp
<issue-guid>/markup.bcf
<issue-guid>/snapshot.png
<issue-guid>/viewpoint.bcfv
```

Noi dung `viewpoint.bcfv` trong file mau chi co `IfcGuid`, vi du:

```xml
<VisualizationInfo>
  <Components>
    <Component IfcGuid="2cIUWpeSrBtQOU7f8BJJ90">
    </Component>
  </Components>
</VisualizationInfo>
```

Khong co:

```xml
AuthoringToolId
```

Va cung khong co Revit `ElementId` truc tiep.

Vi vay loi khong tim thay ID xay ra vi addin ban dau chi xu ly tot khi BCF co Revit ElementId/UniqueId, trong khi file BCF thuc te chi co IFC GUID.

## Nguyen nhan ky thuat

Parser cu trong `BcfZipParser.cs`:

- Gia dinh viewpoint file ten co dinh la `viewpoint.bcfv`.
- Doc XML bang XPath thong thuong, co the loi khi XML co namespace.
- Lay `AuthoringToolId` neu co, fallback sang `IfcGuid`.

Sync cu trong `RevitCameraSync.cs`:

- Neu ID la so thi tao `ElementId`.
- Neu khong phai so thi thu `doc.GetElement(idStr)` nhu Revit UniqueId.
- Khong co map chinh quy tu `IfcGuid` sang Revit `ElementId`.

Voi file BCF mau, gia tri duy nhat de map element la IFC GUID nen can dung co che map IFC GUID.

## Cac sua doi da thuc hien

### 1. `BcfZipParser.cs`

File:

```text
src\Antigravity.IssueManager\Services\BcfZipParser.cs
```

Da sua:

- Doc XML bang `local-name()` de khong phu thuoc namespace.
- Doc viewpoint/snapshot tu `markup.bcf`.
- Neu khong co khai bao viewpoint, tu tim file `*.bcfv`.
- Ho tro parse `AuthoringToolId`.
- Neu khong co `AuthoringToolId`, fallback sang `IfcGuid`.
- Parse toa do camera bang `CultureInfo.InvariantCulture`.

### 2. `RevitCameraSync.cs`

File:

```text
src\Antigravity.IssueManager\Services\RevitCameraSync.cs
```

Da sua:

- Dung `long.TryParse()` va `new ElementId(long)` phu hop Revit 2024.
- Thu tim element bang Revit `UniqueId`.
- Them fallback do cac shared parameter pho bien:

```text
IFC GUID
IfcGUID
IfcGuid
IfcGlobalId
GlobalId
```

- Them huong chinh quy bang Revit IFC API:

```csharp
IFCHybridImport.CreateMap(doc, candidateIds)
```

API nay tao map:

```text
IFCGuidKey -> ElementId
```

Nen co the map truc tiep `IfcGuid` trong BCF sang element trong Revit neu model hien tai co quan he IFC GUID phu hop.

### 3. `Antigravity.IssueManager.csproj`

File:

```text
src\Antigravity.IssueManager\Antigravity.IssueManager.csproj
```

Da them reference:

```xml
<Reference Include="RevitAPIIFC">
  <HintPath>C:\Program Files\Autodesk\Revit 2024\RevitAPIIFC.dll</HintPath>
  <Private>False</Private>
</Reference>
```

### 4. `IssueManagerWindow.xaml.cs`

File:

```text
src\Antigravity.IssueManager\UI\IssueManagerWindow.xaml.cs
```

Da sua filter chon file BCF:

```csharp
dlg.Filter = "BCF files (*.bcf;*.bcfzip)|*.bcf;*.bcfzip";
```

Ly do: file `260519.bcf` trong thu muc mau thuc chat cung la BCF zip hop le.

## Kiem tra build

Da chay:

```powershell
dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj
```

Ket qua:

```text
Build succeeded.
0 Warning(s), 0 Error(s)
```

## Ket luan

Huong xu ly dung cho file BCF cua ban la map `IfcGuid` sang Revit `ElementId` bang `RevitAPIIFC`.

Addin hien da co cac tang xu ly:

1. Revit ElementId dang so.
2. Revit UniqueId.
3. IFC GUID map bang `IFCHybridImport.CreateMap()`.
4. Fallback do shared parameter IFC GUID.

## Luu y khi test trong Revit

Can mo dung model Revit hoac link/model co lien quan voi file BCF.

Neu BCF duoc tao tu IFC/model khac version, hoac model Revit hien tai khong giu quan he IFC GUID tuong ung, Revit API van co the khong map duoc element.

Trong truong hop do, cac huong tiep theo la:

- Xuat lai BCF tu dung model/link dang mo trong Revit.
- Dam bao workflow export/import IFC giu IFC GUID.
- Bo sung shared parameter IFC GUID vao element trong model Revit.
- Neu BCF co the xuat duoc `AuthoringToolId` la Revit ElementId/UniqueId thi day la huong on dinh nhat.
