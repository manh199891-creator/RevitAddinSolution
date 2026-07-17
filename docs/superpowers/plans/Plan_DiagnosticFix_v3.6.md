# 🚨 PLAN v3.6: Sửa triệt để dựa trên kết quả chẩn đoán thực tế
**Ưu tiên:** TỐI CAO — Lỗi đã được xác định chính xác từ diagnostic log

---

## Tóm tắt 3 lỗi gốc rễ đã xác nhận

### Lỗi 1 (🔴): `ExportUtils.GetExportId()` tạo GUID khác hoàn toàn so với Trimble
- BCF tìm: `1l0o6ehCP0ouDtA2Q74nTs`
- Revit tính: `3Zu5Bv0LOHrPC10026FoUi`
- **Format GUID hoàn toàn không khớp** → Quét 892,706 element mà không match được
- **Nguyên nhân**: Trimble Connect tạo IFC GUID riêng khi import NWC, không dùng ExportUtils của Revit

### Lỗi 2 (🔴): `ProjectLocation.GetTransform()` trả về tọa độ sai
- Origin(m): -1989594, -1299375 — **KHÔNG phải** tọa độ VN-2000
- Camera→Internal(m): -510526, 4406693 — **Lệch 500km** so với model
- Section Box bị đặt ở tọa độ -1,674,955 feet → View trống

### Lỗi 3 (🟡): 1 trong 3 Revit Link chưa loaded
- `LC1.1-IBST-ZZ-17_RO-M3-S-0001-TE.rvt`: NOT LOADED
- Phần tử cần tìm có thể nằm trong file này

---

## GIẢI PHÁP

### Fix 1: Bỏ hoàn toàn ExportUtils matching — Dùng BCF Component tìm bằng cách khác

Vì `ExportUtils.GetExportId()` sinh GUID khác với Trimble, ta cần bỏ cách này.
Thay vào đó, khi không tìm được element bằng IFC GUID, **KHÔNG cố tạo Section Box từ tọa độ BCF** (vì tọa độ cũng sai).

**Sửa trong `FindElementInDoc`:**
- XÓA toàn bộ block `if (IsIfcGuid(value))` chứa vòng lặp `ExportUtils.GetExportId()` (quét 892K element nhưng vô ích, tốn 10-30 giây)

```csharp
// XÓA ĐOẠN NÀY (dòng ~990-1010):
if (IsIfcGuid(value))
{
    foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
    {
        try
        {
            Guid exportGuid = ExportUtils.GetExportId(doc, element.Id);
            string computedIfcGuid = BcfExporter.ToIfcGuid(exportGuid);
            // ... matching logic ...
        }
        catch { }
    }
}
```

### Fix 2: Khi không match được Element — Bỏ qua BCF coords, hiện model bình thường

Khi IFC GUID fail → ĐỪNG cố dùng BCF coordinates (đã chứng minh sai 500km).
Thay vào đó: **Reset isolation, tắt section box, hiện model bình thường + thông báo**.

**Sửa block "3. Section Box" trong `SyncCamera`:**
```csharp
// 3. Section Box
if (uidoc.ActiveView is View3D view3d)
{
    try
    {
        if (matchedElements.Count > 0)
        {
            // Có element → Tạo section box quanh element
            if (ApplySectionBoxAroundElements(view3d, matchedElements) && ZoomToSectionBox(uidoc, view3d))
            {
                return; // Thành công!
            }
            if (hostIdsToSelect.Count > 0) uidoc.ShowElements(hostIdsToSelect);
            return;
        }

        // KHÔNG có element matched → ĐỪNG cố dùng BCF coordinates
        // Chỉ tắt section box và hiện thông báo
        DisableSectionBox(doc, view3d);

        // Kiểm tra link chưa loaded
        var unloadedLinks = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .Where(l => l.GetLinkDocument() == null)
            .Select(l => l.Name)
            .ToList();

        string msg = $"Không tìm thấy phần tử BCF trong mô hình.\n\n";
        msg += $"IFC GUID: {string.Join(", ", viewpoint.ElementIds ?? new List<string>())}\n\n";

        if (unloadedLinks.Count > 0)
        {
            msg += "⚠ Các Revit Link sau CHƯA ĐƯỢC LOAD:\n";
            foreach (string name in unloadedLinks)
            {
                msg += $"  • {name}\n";
            }
            msg += "\nHãy load tất cả link rồi thử lại.\n\n";
        }

        msg += "Lưu ý: IFC GUID từ Trimble Connect không khớp với Revit ExportUtils.\n";
        msg += "Phần tử vẫn hiện trên model, hãy tìm thủ công theo ảnh BCF.";
        TaskDialog.Show("BCF Import", msg);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("Camera Sync error: " + ex.Message);
    }
}
```

### Fix 3: Xóa tất cả diagnostic TaskDialog (chỉ giữ Debug.WriteLine)

Sau khi fix xong, XÓA:
1. TaskDialog trong `FindElementByStringIdentifier` (popup "BCF Debug - Element Search")
2. TaskDialog `ShowDiagLog` (popup "BCF Sync Log")
3. Chỉ giữ `System.Diagnostics.Debug.WriteLine` cho developer debug

### Fix 4: Xóa tham số tracking khỏi `FindElementInDoc`

Trả hàm `FindElementInDoc` về signature đơn giản:
```csharp
private static Element FindElementInDoc(
    Document doc,
    string identifier,
    IDictionary<IFCGuidKey, ElementId> ifcGuidMap)
```
- XÓA tham số `ref int scannedCount` và `List<string> sampleGuids`
- XÓA logic thu thập sample GUIDs
- Cập nhật lại `FindElementByStringIdentifier` tương ứng (bỏ biến tracking)

---

## Tóm tắt thay đổi

| # | File | Thay đổi | Lý do |
|---|------|----------|-------|
| 1 | RevitCameraSync.cs | Xóa ExportUtils GUID matching loop | Quét 892K elem vô ích, tốn 30s |
| 2 | RevitCameraSync.cs | Bỏ BCF coordinate fallback khi không match | Tọa độ sai 500km |
| 3 | RevitCameraSync.cs | Thêm cảnh báo link chưa loaded | User cần biết |
| 4 | RevitCameraSync.cs | Xóa diagnostic TaskDialogs | Chỉ dùng khi debug |
| 5 | RevitCameraSync.cs | Đơn giản hóa FindElementInDoc | Bỏ tracking params |

## Build & Test
```powershell
dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj
```

## Kết quả mong đợi
Khi bấm "Show in Model" với BCF từ Trimble:
1. Add-in tìm element bằng IFC GUID (qua UniqueId + Parameter lookup) — nếu match → zoom vào
2. Nếu KHÔNG match → **model hiện bình thường** (không trống, không section box sai chỗ)
3. Hiện thông báo rõ ràng: link nào chưa load, IFC GUID nào không tìm thấy
4. User tự tìm phần tử theo ảnh BCF trên model đang hiện
