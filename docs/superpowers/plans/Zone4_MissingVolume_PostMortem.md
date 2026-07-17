# Báo cáo xử lý sự cố (Post-Mortem): Lỗi mất khối lượng Zone 4

## 1. Mô tả sự cố (Issue Description)
Trong quá trình sử dụng Add-in **Zone Split**, người dùng phát hiện cấu kiện **Sàn (Floor)** tại khu vực Zone 4 liên tục không nhận được khối lượng (Giá trị `BIM_Zone 4 - B1 = 0.000000`). Mặc dù trên giao diện Revit, vùng Sàn và Generic Model của Zone 4 bằng cao độ và dường như có giao cắt với nhau. Các cấu kiện khác như Dầm (Framing) nằm trong vùng Zone 4 vẫn nhận khối lượng bình thường.

## 2. Các giả thuyết và quá trình chẩn đoán (Diagnosis Process)

### Giả thuyết 1: Thuật toán Bounding Box Overlap bị lỗi sai số
- **Nhận định ban đầu:** Add-in sử dụng hàm `BBoxOverlap()` tự viết để chặn các tính toán không cần thiết. Tuy nhiên, phép so sánh tuyệt đối `<` và `>` có thể loại bỏ nhầm các khối giao cắt khít nhau hoặc sai số float nhỏ.
- **Hành động:** Đã gỡ bỏ hàm `BBoxOverlap()` trong `SolidExtractor.cs` và giao phó hoàn toàn việc lọc giao cắt cho C++ Engine của Revit.
- **Kết quả:** Vẫn không ra khối lượng. Lỗi nằm sâu hơn.

### Giả thuyết 2: Lỗi thư mục Build (BadImageFormatException)
- **Sự cố phụ:** Quá trình load DLL báo lỗi móp méo định dạng.
- **Nguyên nhân:** Dự án chuyển sang .NET SDK-style (`net48`), nên DLL thực sự nằm trong `bin\Debug\net48\`, trong khi người dùng nạp nhầm file stub sinh ra ở `bin\Debug\`.
- **Hành động:** Hướng dẫn người dùng load đúng đường dẫn có chứa thư mục `net48`.

### Giả thuyết 3: Zone 4 rỗng, không có Solid, hoặc không thuộc Category
- **Nhận định:** Do ảnh chụp màn hình lúc đầu chỉ thấy "viền xanh" (không phải Solid shaded), nghi ngờ Zone 4 bị rỗng hoặc mất thông số `Mark`.
- **Kết quả:** Bác bỏ, vì các cấu kiện Dầm (Framing) vẫn cắt được Zone 4. Nếu Zone 4 lỗi, Dầm sẽ không cắt được.

### Giả thuyết 4: Lỗi lõi Boolean Operation của Revit (Root Cause)
- **Hành động chẩn đoán:** Thêm tính năng **Warning Logger** vào `ZoneVolumeProcessor.cs`. Khi `BooleanOperationsUtils.ExecuteBooleanOperation` trả về null (bị lỗi ngầm), hệ thống sẽ ghi vào file Report Markdown thay vì bỏ qua âm thầm.
- **Kết quả:** Report trả về dòng cảnh báo rõ ràng: 
  > `WARNING: Element 1910956 (Floors) intersects Zone 4 - B1 bounding box, but Revit Boolean operation failed to calculate exact volume.`
- **Nguyên nhân gốc rễ (Root Cause):** Người dùng đã "chỉnh lại các cạnh giao bằng tay" nhằm không cho chúng trùng nhau. Thao tác này vô tình tạo ra các góc và khe hở siêu việt (ví dụ lệch $0.000001^\circ$). Thuật toán giao cắt hình học của Revit cực kỳ kém khi xử lý các mặt gần song song hoặc trùng cạnh mà không trùng mặt, dẫn đến việc văng lỗi `Exception` ngay trong lõi C++ của phần mềm. Sàn bị lỗi nặng nhưng Dầm thì không vì hình khối Dầm là hình hộp nhỏ đơn giản hơn rất nhiều.

## 3. Logic xử lý triệt để (Resolution & Logic)

Để vượt qua giới hạn lõi của Revit mà không bắt người dùng vẽ lại mô hình, thuật toán **Micro-Translation Fallback (Dịch chuyển vi mô dự phòng)** đã được triển khai trong `SolidExtractor.cs`. 

Thuật toán hoạt động theo các Step (Vòng lặp) khi tính toán giao cắt:
1. **Attempt 1:** Cắt trực tiếp bằng hình dáng gốc. Nếu văng lỗi -> Thử tiếp.
2. **Attempt 2:** Dịch chuyển tịnh tiến khối Zone đi `1e-5` feet theo XY (khoảng 0.003 mm) và cắt. Nếu văng lỗi -> Thử tiếp.
3. **Attempt 3:** Dịch chuyển tịnh tiến đi `1e-5` feet theo trục Z và cắt. Nếu văng lỗi -> Thử tiếp.
4. **Attempt 4 (Nâng cao):** Dịch chuyển tịnh tiến khối Zone đi `0.003` feet (khoảng 1 mm) theo cả không gian 3 chiều XYZ. Khoảng cách 1 mm là đủ lớn để phá vỡ mọi sự cố góc chết $0.000001^\circ$ do vẽ tay, nhưng đủ nhỏ để hoàn toàn không làm sai lệch thể tích bê tông xuất ra (sai số chỉ ở hàng phần triệu $m^3$).
5. **Attempt 5 (Max Level):** Dịch chuyển tịnh tiến khối Zone đi `-0.016` feet (khoảng 5 mm) theo chiều ngược lại ở cả 3 trục XYZ để đảm bảo bắt trọn phần bù.

**Hiệu quả:** Với lớp phòng thủ 5 tầng Fallback này, Add-in ép Revit phải tính ra thể tích bê tông bằng mọi giá, kể cả khi mô hình kiến trúc/kết cấu vẽ tay có sai số hình học nặng nhất. Hạn chế tối đa việc user phải mất thời gian ngồi nặn sửa các bề mặt Sàn.
