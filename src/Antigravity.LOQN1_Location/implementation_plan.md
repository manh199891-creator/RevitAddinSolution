# Goal Description
Thêm UI/UX cho phép chọn Category, Family Type và tùy chọn "Apply to: All instances / Selected elements only" vào form chính, thay thế cho các hộp thoại `TaskDialog` rời rạc.

## Proposed Changes

### 1. `LevelMappingForm.cs` (Đổi thành `MainForm.cs` hoặc giữ file update UI)
- Thêm Panel chứa `cmbCategory` và `cmbFamilyType`.
- Thêm RadioButtons `rbAllInstances` và `rbSelected`.
- Load danh sách Model Categories vào `cmbCategory`.
- Khi chọn Category, load danh sách Family Types tương ứng vào `cmbFamilyType`.
- Cung cấp các thuộc tính public để `Command.cs` lấy cấu hình: `SelectedCategoryId`, `SelectedFamilyTypeId`, `UseAllInstances`.

### 2. `Command.cs`
- Loại bỏ `TaskDialog` hỏi "Dùng đối tượng hiện tại hay chọn mới" ở đầu.
- Truyền `Document` và `preSelectedElements.Count > 0` vào Form.
- Sau khi Form đóng (OK):
  - Nếu `UseAllInstances == true`: Dùng `FilteredElementCollector` lấy tất cả elements theo Category/Type.
  - Nếu `UseAllInstances == false`:
    - Nếu đã có pre-selection: Lọc pre-selection theo Category/Type.
    - Nếu chưa có pre-selection: Cho user `PickObjects` trên màn hình rồi lọc theo Category/Type.
- Tiến hành ghi tham số như cũ.

## Verification Plan
- Build lại Add-in.
- Mở Revit, chạy lệnh.
- Form hiện ra với đầy đủ Category/Type, chọn và test "All instances" vs "Selected elements only".
