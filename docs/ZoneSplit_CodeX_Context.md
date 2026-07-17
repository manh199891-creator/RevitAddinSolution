# Context Dự Án: ZoneSplit BIM (Revit Add-in)

## 🎯 Mục tiêu
Module hỗ trợ phân chia vùng (Zone) cho các cấu kiện kết cấu (Cột, Dầm, Sàn, Tường, Móng) để kiểm soát khối lượng (Volume QTO) theo khu vực thi công.

## 🛠️ Quy trình hoạt động (Workflow)
1. **Input:** Người dùng tạo các khối Generic Model đại diện cho Volume của từng Zone. Mỗi khối có Parameter `BIM_ZoneID` (VD: Zone-1).
2. **Xử lý:**
   - **Module Phân loại:** Duyệt qua các cấu kiện kết cấu, xác định nó thuộc về Zone nào dựa trên nguyên tắc **Majority Volume** (Giao cắt với Zone nào nhiều nhất thì thuộc về Zone đó).
   - **Module Tính toán:** Tính chính xác thể tích (m3) và chiều dài (m) phần giao cắt của cấu kiện bên trong mỗi Zone.
3. **Output:** 
   - Ghi mã `ZoneID` và thông tin khối lượng vào tham số **Comments** của cấu kiện trong Revit.
   - Xuất file báo cáo tổng hợp dạng **Markdown (.md)** chứa bảng thống kê khối lượng theo Zone.

## 🧬 Thuật toán & Kỹ thuật cốt lõi (Dành cho CodeX)

### 1. Trích xuất Hình học (Solid Extraction)
Sử dụng `GeometryElement` để lấy Solid lớn nhất của cấu kiện. Hỗ trợ cả `GeometryInstance` cho Family và `DirectShape`.
- **Tối ưu:** Union các Solid nếu là Slab nhiều lớp hoặc Tường phức hợp.

### 2. Kiểm tra Giao cắt (Intersection Logic)
- **Fast Pass:** Sử dụng `ElementIntersectsElementFilter` (Robust hơn `ElementIntersectsSolidFilter` để tránh lỗi Revit khi các mặt tiếp xúc khít nhau).
- **BoundingBox Pre-check:** Luôn kiểm tra BBox overlap trước khi thực hiện các phép toán Boolean (`BooleanOperationsUtils.ExecuteBooleanOperation`) để tiết kiệm tài nguyên.

### 3. Ghi dữ liệu (Parameter Writing)
- Sử dụng `BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS` để lưu trữ thông tin ZoneID.
- Định dạng chuỗi: `Zone: [ZoneID] | Vol: [Value] m3`.

### 4. Báo cáo (Reporting)
- Tổng hợp dữ liệu bằng `Dictionary<string, ZoneSummary>` (GroupBy ZoneID).
- Xuất bảng Markdown để dễ dàng copy vào tài liệu dự án.

## 📜 Mã nguồn Cốt lõi (Snippet quan trọng)

### Hàm tính thể tích giao cắt (Intersection Volume)
```csharp
public static double GetIntersectionVolume(Solid elementSolid, Solid zoneSolid)
{
    if (elementSolid == null || zoneSolid == null) return 0;
    
    // BBox check trước khi Boolean
    if (!BBoxOverlap(elementSolid.GetBoundingBox(), zoneSolid.GetBoundingBox())) return 0;
    
    try {
        var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
            elementSolid, zoneSolid, BooleanOperationsType.Intersect);
        return intersection?.Volume ?? 0;
    } catch { return 0; }
}
```

### Hàm ghi vào Comments
```csharp
public static void WriteToComments(Element elem, string zoneId, double volume)
{
    Parameter p = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
    if (p != null && !p.IsReadOnly)
    {
        string data = $"Zone: {zoneId} | Vol: {Math.Round(volume, 3)} m3";
        p.Set(data);
    }
}
```

---
*Tài liệu này phục vụ cho việc triển khai logic ZoneSplit vào monorepo Antigravity.*
