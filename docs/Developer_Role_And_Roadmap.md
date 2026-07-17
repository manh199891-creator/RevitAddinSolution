# Vai Trò, Trách Nhiệm và Hướng Phát Triển (Developer Context)

Tài liệu này định vị bối cảnh, vai trò, trách nhiệm hiện tại và lộ trình phát triển của người phát triển dự án (Developer / BIM Coordinator). Đây là kim chỉ nam giúp định hình mục tiêu của các Add-in tự động hóa được xây dựng trong dự án.

## 1. Vai trò hiện tại (Current Role)
Đảm nhận vị trí **BIM Coordinator (Điều phối viên BIM)** tại một Tổng thầu thi công lớn. Hiện tại, tiêu điểm công việc thực tế gắn liền với dự án nhà máy Lite-On Quảng Ninh (Giai đoạn 1).

Khác với vai trò BIM Modeler thuần túy (chỉ tập trung dựng hình), vị trí Coordinator định vị bạn là cầu nối kỹ thuật, quản lý dữ liệu và tối ưu hóa quy trình phối hợp giữa các bộ môn.

## 2. Trách nhiệm cốt lõi (Key Responsibilities)
Thông qua các bài toán thường xuyên xử lý, trách nhiệm xoay quanh 3 trụ cột chính:

- **Quản lý dữ liệu và Môi trường dữ liệu chung (CDE):** Thiết lập, duy trì cấu trúc SSoT (Single Source of Truth) trên nền tảng Trimble Connect, đảm bảo luồng thông tin đồng bộ và chính xác giữa các bên.
- **Xử lý xung đột và Phối hợp bộ môn:** Sử dụng Navisworks để kiểm tra va chạm (Clash Detection), phân loại, quản lý và xuất dữ liệu xung đột qua định dạng BCF (BIM Collaboration Format) để điều phối sửa lỗi hiệu quả.
- **Tự động hóa quy trình (Automation):** Nghiên cứu và viết các công cụ, add-in bổ trợ (bằng C#, Python, pyRevit) kết nối với API của Autodesk Revit và Navisworks nhằm tự động hóa các tác vụ lặp đi lặp lại (như xuất báo cáo va chạm, join hình học, tạo profile tường...).

## 3. Hướng phát triển sự nghiệp (Career Development Roadmap)
Lộ trình phát triển được hoạch định rõ ràng, kết hợp chặt chẽ giữa Chuyên môn quản lý, Năng lực công nghệ và Học thuật nâng cao:

### 3.1. Nâng cao năng lực Quản lý BIM (BIM Management)
- Mở rộng phạm vi từ 3D phối hợp sang các chiều kích cao hơn: 4D (Quản lý tiến độ) và 5D (Quản lý chi phí/Khối lượng - QTO).
- Làm chủ quy trình trao đổi dữ liệu liên thông, ví dụ như chuyển đổi cấu trúc dữ liệu hình học và thuộc tính từ Revit sang các phần mềm phân tích kết cấu (như ETABS) thông qua định dạng JSON.

### 3.2. Làm chủ Công nghệ & AI (Tech & AI Integration)
- Nâng cấp hạ tầng phần cứng cá nhân để tối ưu hóa hiệu suất chạy các phần mềm Autodesk.
- Tích hợp AI vào quy trình làm việc bằng cách thiết lập các mô hình ngôn ngữ lớn (LLM) cục bộ thông qua Ollama (Gemma 4), kết hợp với các công cụ hỗ trợ viết code như Anti Gravity để tăng tốc độ phát triển add-in độc quyền.

### 3.3. Phát triển Học thuật & Ngôn ngữ (Academic & Language Goals)
- **Tháng 6 - Tháng 8:** Tập trung nghiên cứu các đề tài liên quan đến Quản lý BIM và chuẩn bị đề cương nghiên cứu.
- **Tháng 9 - Tháng 10:** Chính thức nhập học chương trình Thạc sĩ tại Trường Đại học Việt Nhật (VJU). Chuẩn bị sẵn phương án cân bằng thời gian giữa công việc tại tổng thầu và lịch học vào các buổi tối trong tuần (Thứ 4, 5, 6) cùng sáng Chủ Nhật.
- **Chuẩn hóa ngôn ngữ:** Với nền tảng TOEIC 900 hiện tại, mục tiêu tiếp theo là đạt IELTS 6.5 để phục vụ tối đa cho việc nghiên cứu tài liệu quốc tế và viết luận văn thạc sĩ.

## 4. Trợ lý AI (Anti Gravity) - Nhiệm vụ định kỳ
Để hỗ trợ hành trình trên, AI sẽ đảm nhận tác vụ định kỳ sau:
- **Tần suất:** Hàng tuần.
- **Nhiệm vụ:** Tự động tìm kiếm, nghiên cứu Top 20 Trending GitHub (các ngôn ngữ và chủ đề liên quan đến C#, Python, BIM, Automation, LLM/AI).
- **Mục tiêu đầu ra:**
  - Lọc và đề xuất các thư viện, framework hoặc mã nguồn mở có khả năng tích hợp vào **Revit Addin Solution** hiện tại.
  - Đề xuất các ý tưởng App/Công cụ mới bám sát với *Trách nhiệm cốt lõi (Mục 2)* và *Lộ trình phát triển (Mục 3)*.
  - Báo cáo chi tiết về tính tương thích công nghệ, khả năng áp dụng thực tế và giá trị mang lại cho vị trí BIM Coordinator.

---
**Tầm nhìn:** Bằng việc kết hợp thực tế chiến đấu tại công trường (Tổng thầu) với tư duy tự động hóa (Lập trình API) và nền tảng lý luận chuyên sâu (Thạc sĩ VJU), hướng đi đang hướng thẳng tới chân dung một **BIM Manager / BIM Director toàn diện** trong tương lai gần.
