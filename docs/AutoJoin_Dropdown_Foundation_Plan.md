# Implementation Plan: AutoJoin UI Dropdowns & Foundation Support

## 🎯 Mục tiêu
- Chuyển đổi các nhãn cấu kiện tĩnh (Dầm, Cột, Sàn...) sang **ComboBox** để người dùng tự chọn cặp join trực tiếp trên UI.
- Bổ sung cấu kiện **Structural Foundation** (Móng) vào hệ thống.
- Thêm các quy tắc mặc định cho Móng (Cột → Móng, Vách → Móng).

---

## 📋 Danh sách Task

### Phase 1: Cấu trúc & Dữ liệu (Foundation Support)
- [x] **Task 1: Cập nhật Model `JoinRule.cs`**
    - Thêm `OST_StructuralFoundation` vào danh sách `SupportedCategories`.
    - Cập nhật hàm `FriendlyName` để trả về "Móng" cho `OST_StructuralFoundation`.
- [ ] **Task 2: Cập nhật `JoinConfigService.cs`**
    - Thêm các cặp rule mặc định mới:
        - `Order 7`: Cột → Móng
        - `Order 8`: Vách → Móng

### Phase 2: Nâng cấp Giao diện (UI Dropdowns)
- [ ] **Task 3: Refactor `MainWindow.xaml`**
    - Thay thế các `TextBlock` hiển thị tên cấu kiện (NameA, NameB) bằng `ComboBox`.
    - Binding `ItemsSource` của ComboBox với danh sách `SupportedCategories`.
    - Tạo `CategoryNameConverter` để hiển thị tên tiếng Việt (Dầm, Cột...) thay vì code `OST_...`.
- [ ] **Task 4: Cập nhật Style cho ComboBox**
    - Đảm bảo ComboBox có style đồng nhất với thiết kế Vilai Viet (Nền tối, chữ trắng, viền xanh tím).

### Phase 3: Logic & Lưu trữ
- [ ] **Task 5: Xử lý sự kiện thay đổi ComboBox**
    - Đảm bảo khi người dùng chọn cấu kiện mới trong ComboBox, dữ liệu được cập nhật vào List `_rules`.
- [ ] **Task 6: Tích hợp Monorepo (DoorClearanceBox)**
    - Di chuyển dự án DoorClearanceBox vào `src/Antigravity.DoorClearanceBox`.
    - Cập nhật namespaces và NuGet.

---

## ⚠️ Rủi ro & Giải pháp
| Rủi ro | Tác động | Giải pháp |
|:---:|:---:|:---|
| Trùng lặp cặp Join | Trung | Thêm logic kiểm tra nếu cặp A-B đã tồn tại thì cảnh báo người dùng. |
| Mất cấu hình cũ | Thấp | Luôn backup file `rules.json` trước khi ghi đè cấu trúc mới. |

---

## 📅 Tổng kết
Mọi thay đổi sẽ tuân thủ nghiêm ngặt **Vilai Viet Design Guidelines**.
