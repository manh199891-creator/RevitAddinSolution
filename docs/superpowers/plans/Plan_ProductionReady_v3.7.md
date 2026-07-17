# Plan v3.7: Production-ready BCF Import
**Trạng thái:** Các lỗi nghiêm trọng đã sửa (view trống, section box sai). Giờ cần dọn code + cải thiện UX.

---

## Tình trạng hiện tại (đã xác nhận qua diagnostic)

| Tính năng | Trạng thái |
|-----------|-----------|
| Model hiển thị đầy đủ | ✅ Đã sửa |
| Isolation reset khi không match | ✅ Đã sửa |
| Không đặt section box sai chỗ | ✅ Đã sửa |
| Cảnh báo link chưa loaded | ✅ Đã có |
| Tự động zoom vào element | ⚠ Chưa khả dụng (IFC GUID mismatch) |

---

## Việc cần làm

### 1. Dọn dẹp Diagnostic Code (Ưu tiên cao)
**File:** `RevitCameraSync.cs`

Xóa TẤT CẢ TaskDialog diagnostic, chỉ giữ Debug.WriteLine:

**a) Xóa hàm `ShowDiagLog` và tất cả lệnh gọi nó**
- Xóa hàm `ShowDiagLog(List<string> log)` (khoảng cuối phần SyncCamera)
- Xóa tất cả `ShowDiagLog(log)` trong SyncCamera
- Xóa tất cả biến `List<string> log` và các dòng `log.Add(...)` trong SyncCamera

**b) Xóa TaskDialog trong `FindElementByStringIdentifier`**
- Xóa toàn bộ block `// Diagnostic: Show what happened during search` đến `TaskDialog.Show("BCF Debug - Element Search", diag);`
- Xóa các biến diagnostic: `hostElementsScanned`, `linkDocsScanned`, `linkElementsScanned`, `sampleGuids`

**c) Đơn giản hóa `FindElementInDoc` signature**
Từ:
```csharp
private static Element FindElementInDoc(Document doc, string identifier, 
    IDictionary<IFCGuidKey, ElementId> ifcGuidMap, ref int scannedCount, List<string> sampleGuids)
```
Về:
```csharp
private static Element FindElementInDoc(Document doc, string identifier, 
    IDictionary<IFCGuidKey, ElementId> ifcGuidMap)
```
- Xóa tham số `ref int scannedCount` và `List<string> sampleGuids`
- Xóa logic `scannedCount++` và `sampleGuids.Add(...)` trong hàm
- Cập nhật lệnh gọi trong `FindElementByStringIdentifier` cho khớp

### 2. Xóa vòng lặp ExportUtils GUID (Ưu tiên cao)
**File:** `RevitCameraSync.cs`, trong hàm `FindElementInDoc`

**XÓA toàn bộ block:**
```csharp
if (IsIfcGuid(value))
{
    foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
    {
        try
        {
            Guid exportGuid = ExportUtils.GetExportId(doc, element.Id);
            string computedIfcGuid = BcfExporter.ToIfcGuid(exportGuid);
            // ... sample GUID collection ...
            if (string.Equals(computedIfcGuid, value, StringComparison.Ordinal))
            {
                return element;
            }
        }
        catch { }
    }
}
```
**Lý do:** Diagnostic đã chứng minh ExportUtils GUID (`3Zu5Bv0LOH...`) hoàn toàn khác IFC GUID từ Trimble (`1l0o6ehCP0...`). Vòng lặp này quét 892,706 element, tốn 10-30 giây, mà KHÔNG BAO GIỜ match được.

### 3. Đơn giản hóa SyncCamera (Ưu tiên cao)
**File:** `RevitCameraSync.cs`

Viết lại SyncCamera sạch sẽ, không có log diagnostic:

```csharp
public static void SyncCamera(UIApplication uiApp, ViewpointModel viewpoint)
{
    if (viewpoint == null) return;

    UIDocument uidoc = uiApp.ActiveUIDocument;
    Document doc = uidoc.Document;
    IDictionary<IFCGuidKey, ElementId> ifcGuidMap = CreateIfcGuidMap(doc);

    // 1. Match elements
    List<MatchedElement> matchedElements = new List<MatchedElement>();
    List<ElementId> hostIdsToSelect = new List<ElementId>();
    foreach (string idStr in viewpoint.ElementIds ?? Enumerable.Empty<string>())
    {
        MatchedElement match = FindElementByStringIdentifier(doc, idStr, ifcGuidMap);
        if (match != null)
        {
            matchedElements.Add(match);
            if (!match.IsLinked)
            {
                hostIdsToSelect.Add(match.Element.Id);
            }
        }
    }

    if (hostIdsToSelect.Count > 0)
    {
        uidoc.Selection.SetElementIds(hostIdsToSelect);
    }

    // 2. Get or Create BCF View
    View3D targetView = GetOrCreateBcfView(doc);
    if (targetView == null) return;

    if (uidoc.ActiveView.Id != targetView.Id)
    {
        uidoc.ActiveView = targetView;
    }

    // 3. Isolate or Reset
    bool hasLinkedMatches = matchedElements.Any(e => e.IsLinked);
    using (Transaction t = new Transaction(doc, "BCF Element Visibility"))
    {
        t.Start();
        if (targetView.IsTemporaryHideIsolateActive())
        {
            targetView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
        }
        if (hostIdsToSelect.Count > 0 && !hasLinkedMatches)
        {
            targetView.IsolateElementsTemporary(hostIdsToSelect);
        }
        t.Commit();
    }

    // 4. Section Box
    if (!(uidoc.ActiveView is View3D view3d)) return;

    if (matchedElements.Count > 0)
    {
        // Element matched → Section box quanh element
        if (ApplySectionBoxAroundElements(view3d, matchedElements) && ZoomToSectionBox(uidoc, view3d))
        {
            return;
        }
        if (hostIdsToSelect.Count > 0) uidoc.ShowElements(hostIdsToSelect);
        return;
    }

    // Không match được → Tắt section box, hiện thông báo
    DisableSectionBox(doc, view3d);
    ShowBcfImportMessage(doc, viewpoint);
}
```

### 4. Tách hàm thông báo riêng
```csharp
private static void ShowBcfImportMessage(Document doc, ViewpointModel viewpoint)
{
    var unloadedLinks = new FilteredElementCollector(doc)
        .OfClass(typeof(RevitLinkInstance))
        .Cast<RevitLinkInstance>()
        .Where(l => l.GetLinkDocument() == null)
        .Select(l => l.Name)
        .ToList();

    string msg = "Không tìm thấy phần tử BCF trong mô hình.\n\n";
    msg += "IFC GUID:\n";
    foreach (string id in viewpoint.ElementIds ?? new List<string>())
    {
        msg += $"  • {id}\n";
    }

    if (unloadedLinks.Count > 0)
    {
        msg += "\n⚠ Revit Link chưa loaded:\n";
        foreach (string name in unloadedLinks)
        {
            msg += $"  • {name}\n";
        }
        msg += "\nHãy load tất cả Link rồi thử lại.";
    }
    else
    {
        msg += "\nIFC GUID từ Trimble không khớp với Revit.";
        msg += "\nHãy tìm vị trí theo ảnh BCF trên mô hình.";
    }

    TaskDialog.Show("BCF Import", msg);
}
```

### 5. Xóa code không dùng
- Xóa hàm `IsIfcGuid` (trong RevitCameraSync.cs) — không còn dùng sau khi bỏ ExportUtils loop
- Xóa constant `IfcGuidAlphabet` nếu không còn reference nào

---

## Checklist IDE
- [ ] Xóa ShowDiagLog + tất cả log.Add
- [ ] Xóa TaskDialog diagnostic trong FindElementByStringIdentifier
- [ ] Đơn giản FindElementInDoc (bỏ tracking params)
- [ ] Xóa ExportUtils GUID matching loop
- [ ] Viết lại SyncCamera sạch
- [ ] Tách ShowBcfImportMessage
- [ ] Xóa IsIfcGuid, IfcGuidAlphabet nếu không dùng
- [ ] Build: `dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj`
- [ ] Kiểm tra không có warning/error
