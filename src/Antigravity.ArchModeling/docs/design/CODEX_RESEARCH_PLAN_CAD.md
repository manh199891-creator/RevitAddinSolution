# Báo cáo Nghiên cứu & Thiết kế Kiến trúc (Chief Architect) - Phương thức Dựng hình từ CAD

**Dự án**: RevitAddinSolution
**Yêu cầu**: Research các công cụ dựng hình kiến trúc (tường, trần, sàn, cửa đi và cửa sổ) dựa trên bản vẽ CAD có sẵn. Sử dụng UX/UI guideline Vilaiviet. Vị trí tại Panel "Dựng hình".

---

## 1. Phân tích Hiện trạng & Công nghệ

- **Nguồn dữ liệu CAD**: Trong Revit, bản vẽ CAD được chèn vào dưới dạng `ImportInstance` hoặc `CADLinkType`. Add-in cần cho phép người dùng chọn (pick) file CAD này.
- **Trích xuất hình học (Geometry Extraction)**:
  - Sử dụng `Element.get_Geometry(Options)` để lấy `GeometryElement`.
  - Duyệt qua các `GeometryInstance` để lấy các đối tượng `Line`, `Arc`, `PolyLine`.
  - Phân loại đường nét theo **Layer** (người dùng sẽ map layer CAD với cấu kiện tương ứng thông qua UI).
- **UX/UI (Vilaiviet Guideline)**:
  - WPF Form gọn gàng, hiện đại.
  - Hỗ trợ MVVM.
  - Sử dụng chung Panel `DỰNG HÌNH` hiện có. Các nút bấm sẽ nằm cạnh các nút "Vẽ Cột", "Vẽ Dầm", "Vẽ Vách", "Vẽ Sàn".

## 2. Giải pháp Kỹ thuật & Thuật toán (CAD to Revit)

### 2.1. Dựng Tường (Wall from CAD)
- **Thuật toán**: Lọc các đường Line thuộc Layer Tường. Tạo `Wall.Create` cho mỗi đường Line.
- **Xử lý nâng cao**: Các đường line CAD thường là 2 nét song song thể hiện bề dày tường. Thuật toán cần tìm các cặp đường song song, tính đường tâm (centerline) và khoảng cách (bề dày) để chọn `WallType` phù hợp và tạo tường tại đường tâm.

### 2.2. Dựng Sàn & Trần (Floor & Ceiling from CAD)
- **Thuật toán**: Lọc các đường Line thuộc Layer Sàn/Trần.
- Dùng thuật toán Graph (Topology) để tìm các chu trình khép kín (Closed Loops) từ các đoạn thẳng rời rạc.
- Chuyển đổi thành `IList<CurveLoop>` và gọi API `Floor.Create()` hoặc `Ceiling.Create()`.

### 2.3. Đặt Cửa Đi & Cửa Sổ (Door & Window from CAD)
- **Thuật toán**:
  - Cách 1: Đọc Block Reference từ CAD (nếu CAD được block chuẩn) -> lấy Point và Rotation.
  - Cách 2: Lọc các đoạn Line thuộc Layer Cửa nằm đè lên Layer Tường. Xác định giao điểm để tìm Host (Tường) và vị trí Center Point để đặt FamilyInstance.
- Gọi API `Document.Create.NewFamilyInstance(Point, FamilySymbol, Wall, StructuralType.NonStructural)`.

## 3. Đánh giá Rủi ro

| Rủi ro | Đánh giá | Giải pháp |
|--------|----------|-----------|
| CAD nét vẽ rác, không khép kín | Rất Cao | Cho phép dung sai (tolerance) khi nối điểm. Thông báo cho người dùng những khu vực không thể tự động khép kín profile. |
| Xử lý 2 nét tường song song | Cao | Cung cấp tùy chọn: Dựng tường theo 1 nét (chọn nét làm Core Face) hoặc tự động tìm đường tâm từ 2 nét. |

## 4. Các file dự kiến cần tạo/sửa

1. **Thư mục**: `src/Antigravity.CadToArch/` (Module mới chuyên xử lý CAD to Arch)
2. **Commands**:
   - `CadToWallCommand.cs`, `CadToFloorCommand.cs`, `CadToDoorCommand.cs`...
3. **Services/Utils**:
   - `CadGeometryExtractor.cs`: Chuyên xử lý bóc tách hình học từ ImportInstance.
   - `CurveLoopBuilder.cs`: Chuyên nối các đoạn line thành CurveLoop khép kín.
4. **UI**:
   - `CadMappingView.xaml`: UI cho phép người dùng chọn CAD Link, chọn Layer tương ứng cho Tường/Sàn/Cửa, và chọn Type của Revit.
5. **App.cs**: Đăng ký các nút bấm mới vào Panel `DỰNG HÌNH`.
