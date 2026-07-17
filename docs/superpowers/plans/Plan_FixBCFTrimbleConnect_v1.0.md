# Kế hoạch khắc phục lỗi hiển thị BCF trên Trimble Connect

Dựa trên các thông tin bạn vừa cung cấp, nguyên nhân gây ra lỗi view trắng tinh và relink không thành công đã được xác định rõ ràng. Dưới đây là phân tích kỹ thuật và giải pháp chi tiết.

## Phân tích nguyên nhân cốt lõi

### 1. View trắng tinh (Lỗi hệ tọa độ Camera)
Plugin `AutoIssueManager` của bạn hiện tại đang được code để xuất tọa độ Camera theo **Shared Coordinates** (tọa độ chia sẻ).
*(Đoạn code trong plugin của bạn gọi hàm `doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero)` và tịnh tiến tọa độ qua `ToBcfPoint`).*

Tuy nhiên, khi bạn xuất file từ Revit sang `.nwc` (Navisworks), mặc định trình xuất NWC thường sử dụng tọa độ **Project Internal** (tọa độ gốc của project).
=> **Hệ quả:** File mô hình trên Trimble Connect nằm ở gốc tọa độ 0,0,0 của Internal, nhưng camera trong file BCF lại bay đến tọa độ Shared (thường cách xa hàng ngàn mét). Do đó, camera bay ra ngoài khoảng không, dẫn đến màn hình trắng tinh.

### 2. Không nhận diện được cấu kiện (Lỗi GUID Mismatch)
File BCF được plugin tạo ra đang lưu ID của cấu kiện dưới dạng Revit UniqueId hoặc IFC GUID. Tuy nhiên, định dạng `.nwc` sử dụng hệ thống ID riêng của Navisworks.
=> **Hệ quả:** Trimble Connect không thể ánh xạ (map) được ID cấu kiện trong BCF với ID cấu kiện trong file NWC. Do đó thao tác cắt Section Box hay tô màu đỏ cấu kiện bị lỗi sẽ không hoạt động.

---

## Các phương án khắc phục (Solutions)

Bạn có thể chọn một trong hai phương án sau tùy thuộc vào workflow của dự án:

### Phương án 1: Đổi định dạng file up lên Trimble Connect thành `.ifc` (Khuyên dùng - Best Practice)
Trimble Connect và chuẩn BCF sinh ra là để tối ưu hóa cho định dạng IFC. Plugin của bạn cũng đã có sẵn hàm `ToIfcGuid` để convert ID rất chuẩn.
*   **Bước 1:** Từ Revit, thay vì xuất ra NWC, hãy **xuất mô hình ra file `.ifc`** (Sử dụng chuẩn IFC2x3 hoặc IFC4).
*   **Bước 2:** Khi xuất IFC, nhớ tick chọn hệ tọa độ là **Shared Coordinates**.
*   **Bước 3:** Upload file `.ifc` lên Trimble Connect.
*   **Kết quả:** Hệ tọa độ của mô hình và Camera BCF sẽ khớp nhau 100%. Các ID cấu kiện (IFC GUID) cũng sẽ khớp nhau 100%. Lỗi view trắng tinh và lỗi relink sẽ hoàn toàn biến mất.

### Phương án 2: Giữ nguyên file `.nwc` nhưng đổi thiết lập lúc xuất
Nếu dự án bắt buộc phải dùng file `.nwc` trên Trimble Connect, bạn phải đồng bộ hệ tọa độ.
*   **Cách 1 (Sửa thiết lập xuất NWC):** Khi xuất NWC từ Revit, ấn vào nút **Navisworks settings...** > Mở rộng mục **Coordinates** > Đổi từ *Project Internal* sang **Shared**. (Khi đó NWC sẽ mang tọa độ Shared giống với BCF của bạn).
*   **Cách 2 (Sửa code Plugin BCF):** Nếu file NWC up lên mạng bắt buộc phải là Project Internal, bạn cần sửa lại code trong file `RevitIssueCreator.cs` của plugin `AutoIssueManager`. Trong hàm `ToBcfPoint`, hãy bỏ việc cộng thêm `projectPosition.EastWest`, `NorthSouth`... (tức là không dịch chuyển sang hệ tọa độ Shared nữa) mà chỉ convert thẳng Internal Point (feet) sang Meters.

> [!TIP]
> Mình khuyên bạn nên sử dụng **Phương án 1 (Dùng IFC)**. Vì cho dù Phương án 2 có sửa được lỗi trắng màn hình, file `.nwc` trên Trimble Connect vẫn sẽ không hiểu được ID cấu kiện từ BCF, dẫn đến việc không thể tự động highlight (làm sáng) hay focus chính xác vào cấu kiện đang bị lỗi.
