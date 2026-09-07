# Plan: Tag Arranger - Smart Stack & Orthogonal Leader (v1.0)

## Yêu cầu tổng quan
Triển khai tính năng **Smart Stack** và bẻ góc **Orthogonal Leader** cho dự án `Antigravity.TagArranger`. Tính năng phải tái tạo lại chính xác hành vi từ các ảnh mẫu:
1. Dóng Tag (Align) chỉ với 1 thao tác.
2. Sắp xếp chồng Tag (Smart Stack) với khoảng cách chữ sát nhau.
3. Leader route thành 2 đoạn gấp khúc chuẩn:
   - Đoạn 1 nằm ngang hoàn toàn (từ TagHead đến Elbow).
   - Đoạn 2 đâm vào cấu kiện (chéo đối với cấu kiện điểm, thẳng đứng 90 độ đối với ống/ống gió ngang).

## Chi tiết Implement (Dành cho IDE)

### 1. Tạo mới `SmartStackService.cs` trong `Services`
- Tạo hàm `ExecuteSmartStack(Document doc, View view, IList<ElementId> tagIds, double horizontalOffsetFeet, bool alignRight = true)`.
- **Logic Stacking**:
  1. Lấy thông tin BoundingBox của các Tag để tính toán chiều cao (Height).
  2. Tính toán khoảng cách (Spacing) tự động để các tag không bị đè lên nhau (Smart Stack).
  3. Sắp xếp danh sách Tag theo trục Y để tránh các leader cắt chéo nhau.
  4. Đặt `TagHeadPosition` của tất cả Tag về cùng 1 tọa độ X, và xếp dọc Y tương ứng (`Y_i = StartY - i * Spacing`).
- **Logic Orthogonal Leader**:
  1. Tính `Elbow = new XYZ(TagHead.X ± horizontalOffsetFeet, TagHead.Y, 0)`.
  2. Đánh giá phần tử được tag (Pipe/Duct/CableTray). Nếu là tuyến tính và nằm ngang, hãy thử đặt `LeaderEnd.X = Elbow.X` để tạo ra đoạn leader thả thẳng đứng 90 độ.
  3. Nếu không thể ép vuông góc, giữ nguyên vị trí bám của `LeaderEnd` và để nó nối chéo tự nhiên.

### 2. Sửa đổi `LeaderService.cs`
- Thêm hàm hỗ trợ `FormatOrthogonalLeaders(Document doc, View view, IList<ElementId> tagIds, double horizontalOffsetFeet)`.
- Chỉ cập nhật lại `Elbow` và `TagHeadPosition` sao cho đoạn đầu của Leader luôn nằm ngang (khoảng cách `horizontalOffsetFeet`), giữ nguyên điểm cắm hiện tại.

### 3. Sửa đổi `ArrangerWindow.xaml` và `ArrangerWindow.xaml.cs`
- Bổ sung nhóm tính năng **Smart Stack**:
  - Giao diện (XAML): Có TextBox nhập `Horizontal Offset` (mặc định 200mm). 
  - Nút bấm (Button): **Stack Left** và **Stack Right**.
- Map các Event của nút bấm gọi đến `SmartStackService.ExecuteSmartStack` và tính toán chuyển đổi mm sang feet cho Revit.

---
**IDE Instruction**: Hãy dựa vào bản thiết kế trên để thay đổi và thêm mới code vào dự án. Sau khi hoàn tất, hãy build và báo cáo lại.
