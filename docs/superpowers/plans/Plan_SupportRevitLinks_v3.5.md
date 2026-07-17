# 🚨 PLAN v3.5: Hỗ trợ tìm Element trong Revit Links
**Mục tiêu:** Quét tìm IFC GUID trong cả Host Document và các Linked Documents. Áp dụng Section Box chính xác cho phần tử nằm trong file Link.

---

## 1. Vấn đề của file Link

- Hàm `FindElementByStringIdentifier` hiện tại chỉ quét các element nằm trong `doc` (Host Model).
- Nếu phần tử (như cửa, lỗ mở) nằm trong file Kiến Trúc (được Link vào file MEP), hàm này sẽ không tìm thấy.
- Revit API không cho phép dùng `uidoc.Selection.SetElementIds` hoặc `view3d.IsolateElementsTemporary` với ElementID của file Link.
- **Giải pháp:** Chúng ta sẽ tạo một class trung gian `MatchedElement` để lưu trữ thông tin Element và LinkInstance của nó. Sau đó, tính toán BoundingBox bằng cách nhân với `Transform` của file Link để ra tọa độ thực trong Host, từ đó đặt Section Box. Không cần Isolate/Select phần tử Link.

---

## 2. Các thay đổi cụ thể

### 2.1. Thêm class `MatchedElement`
Thêm class này vào cuối file `RevitCameraSync.cs` hoặc đặt làm class con bên trong:
```csharp
public class MatchedElement
{
    public Element Element { get; set; }
    public RevitLinkInstance LinkInstance { get; set; }
    public bool IsLinked => LinkInstance != null;

    public BoundingBoxXYZ GetTransformedBoundingBox()
    {
        BoundingBoxXYZ bbox = Element.get_BoundingBox(null);
        if (bbox == null) return null;

        if (IsLinked)
        {
            Transform linkTransform = LinkInstance.GetTotalTransform();
            
            // Lấy 8 đỉnh của BoundingBox trong file link
            XYZ[] corners = new XYZ[]
            {
                new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
                new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
                new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
                new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
            };

            // Transform sang Host Coordinates
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (XYZ corner in corners)
            {
                XYZ transformedCorner = linkTransform.OfPoint(corner);
                minX = Math.Min(minX, transformedCorner.X);
                minY = Math.Min(minY, transformedCorner.Y);
                minZ = Math.Min(minZ, transformedCorner.Z);
                maxX = Math.Max(maxX, transformedCorner.X);
                maxY = Math.Max(maxY, transformedCorner.Y);
                maxZ = Math.Max(maxZ, transformedCorner.Z);
            }

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ),
                Transform = Transform.Identity
            };
        }
        return bbox;
    }
}
```

### 2.2. Viết lại hàm `FindElementByStringIdentifier`
Tách logic tìm kiếm hiện tại thành `FindElementInDoc`, và để `FindElementByStringIdentifier` quét qua các Link:
```csharp
private static MatchedElement FindElementByStringIdentifier(Document hostDoc, string identifier, IDictionary<IFCGuidKey, ElementId> ifcGuidMap)
{
    // 1. Tìm trong Host Document
    Element hostElem = FindElementInDoc(hostDoc, identifier, ifcGuidMap);
    if (hostElem != null)
    {
        return new MatchedElement { Element = hostElem, LinkInstance = null };
    }

    // 2. Tìm trong Linked Documents
    FilteredElementCollector linkCollector = new FilteredElementCollector(hostDoc).OfClass(typeof(RevitLinkInstance));
    foreach (RevitLinkInstance linkInstance in linkCollector)
    {
        Document linkDoc = linkInstance.GetLinkDocument();
        if (linkDoc == null) continue;

        Element linkElem = FindElementInDoc(linkDoc, identifier, null); // Có thể tối ưu Map sau
        if (linkElem != null)
        {
            return new MatchedElement { Element = linkElem, LinkInstance = linkInstance };
        }
    }
    return null;
}

private static Element FindElementInDoc(Document doc, string value, IDictionary<IFCGuidKey, ElementId> ifcGuidMap)
{
    // BÊ NGUYÊN TOÀN BỘ LOGIC CỦA FindElementByStringIdentifier CŨ VÀO ĐÂY
    // - UniqueId match
    // - Parameter "IFC GUID" match
    // - ExportUtils.GetExportId match
    // ...
    // Trả về Element
}
```

### 2.3. Cập nhật `SyncCamera`
Quản lý riêng `hostIdsToSelect` (để Isolate/Select) và `matchedElements` (để tính Section Box).
```csharp
// Thay vì list ElementId, ta dùng:
List<MatchedElement> matchedElements = new List<MatchedElement>();
List<ElementId> hostIdsToSelect = new List<ElementId>();
List<string> unmatchedIds = new List<string>();

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
        System.Diagnostics.Debug.WriteLine($"BCF matched: {idStr} -> {(match.IsLinked ? "Link" : "Host")} Element {match.Element.Id}");
    }
    else
    {
        unmatchedIds.Add(idStr);
    }
}
```
Cập nhật việc Isolate & Selection:
- Chỉ isolate `hostIdsToSelect` nếu danh sách này `> 0`.
- Nếu có Element nằm trong Link, chúng ta không isolate chúng (vì Revit không cho phép), nhưng **nhờ Section Box**, ta vẫn sẽ quan sát được chúng rõ ràng.

### 2.4. Tính BoundingBox từ danh sách `MatchedElement`
Sửa lại các hàm tạo SectionBox để nhận `IList<MatchedElement>` thay vì `IList<ElementId>`:
```csharp
private static BoundingBoxXYZ CreateMergedElementBoundingBox(IList<MatchedElement> matchedElements)
{
    if (matchedElements == null || matchedElements.Count == 0) return null;

    bool hasBox = false;
    double minX = 0, minY = 0, minZ = 0;
    double maxX = 0, maxY = 0, maxZ = 0;

    foreach (var match in matchedElements)
    {
        BoundingBoxXYZ box = match.GetTransformedBoundingBox();
        if (box == null) continue;

        if (!hasBox)
        {
            minX = box.Min.X; minY = box.Min.Y; minZ = box.Min.Z;
            maxX = box.Max.X; maxY = box.Max.Y; maxZ = box.Max.Z;
            hasBox = true;
            continue;
        }

        minX = Math.Min(minX, box.Min.X);
        minY = Math.Min(minY, box.Min.Y);
        minZ = Math.Min(minZ, box.Min.Z);
        maxX = Math.Max(maxX, box.Max.X);
        maxY = Math.Max(maxY, box.Max.Y);
        maxZ = Math.Max(maxZ, box.Max.Z);
    }

    if (!hasBox) return null;
    return new BoundingBoxXYZ { Min = new XYZ(minX, minY, minZ), Max = new XYZ(maxX, maxY, maxZ), Transform = Transform.Identity };
}
```

---
**Chuyển plan này sang IDE ngay bằng lệnh: `/code`**
Đảm bảo IDE làm đúng hướng dẫn. Sau đó build lại và test.
