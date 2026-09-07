# Implementation Plan: ZoneSplit Physical Splitting & Parameter Cleanup

Kế hoạch này nhằm nâng cấp công cụ ZoneSplit để thực hiện cắt vật lý (Physical Split) các cấu kiện khi chúng giao cắt với nhiều Zone, đồng thời loại bỏ việc ghi dữ liệu vào trường `Comments` để chuẩn hóa theo Parameter riêng.

## Architecture Decisions
- **Parameter Source of Truth**: Loại bỏ hoàn toàn việc sử dụng `BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS`. Chỉ sử dụng `BIM_ZoneID` và `BIM_ZoneName`.
- **Splitting Strategy**: 
  - **Walls**: Sử dụng `WallUtils.SplitWall` hoặc tạo mới 2 đoạn tường từ tọa độ giao điểm.
  - **Structural Framing (Beams)**: Tạo mới 2 dầm dựa trên đoạn Curve bị cắt và xóa dầm cũ.
  - **Floors**: Hiện tại rất phức tạp (đòi hỏi tính toán Polygon Clipping). Sẽ ưu tiên thông báo hoặc xử lý sau nếu yêu cầu bắt buộc.
- **Majority Rule vs. Split Rule**: 
  - Nếu cấu kiện nằm trọn trong 1 Zone -> Chỉ gán Parameter.
  - Nếu cấu kiện giao cắt biên giới Zone -> Thực hiện Split vật lý.

## Task List

### Phase 1: Parameter Cleanup & Refactoring
- [x] **Task 1: Remove Comment Logic**
    - **Description**: Xóa đoạn code ghi dữ liệu vào trường `Comments` trong `ZoneVolumeProcessor.cs`.
    - **Acceptance Criteria**: Trường `Comments` không bị ghi đè sau khi chạy lệnh.
    - **Files**: `src/Antigravity.ZoneSplit/Services/ZoneVolumeProcessor.cs`
    - **Status**: DONE (2026-05-16)

### Phase 2: Physical Splitting Logic
- [x] **Task 2: Implement Wall Splitting**
    - **Description**: Khi một bức tường đi qua 2 Zone, thực hiện lệnh Split tại vị trí biên giới Zone.
    - **Acceptance Criteria**: 1 bức tường cũ biến thành 2 bức tường mới, mỗi cái nhận Parameter của Zone tương ứng.
    - **Files**: `src/Antigravity.ZoneSplit/Services/PhysicalSplitService.cs` (New)
    - **Status**: IMPLEMENTED (2026-05-16) - Straight `LocationCurve` walls only; walls with hosted inserts are skipped with warning.

- [x] **Task 3: Implement Beam Splitting**
    - **Description**: Tính toán điểm giao giữa dầm (Curve) và mặt (Face) của khối Zone. Chia Curve thành các đoạn nhỏ và re-create dầm.
    - **Acceptance Criteria**: Dầm được chia nhỏ chính xác theo biên Zone.
    - **Files**: `src/Antigravity.ZoneSplit/Services/PhysicalSplitService.cs`
    - **Status**: IMPLEMENTED (2026-05-16) - Straight structural framing only; non-linear or incomplete zone coverage falls back to majority assignment.

### Phase 3: Integration & Testing
- [x] **Task 4: Update Processor Flow**
    - **Description**: Tích hợp `PhysicalSplitService` vào luồng xử lý chính của `ZoneVolumeProcessor`.
    - **Files**: `src/Antigravity.ZoneSplit/Services/ZoneVolumeProcessor.cs`
    - **Status**: IMPLEMENTED (2026-05-16) - Multi-zone linear walls/beams attempt physical split before majority assignment.

### Phase 4: Parameter-based Volume Tracking (BIM_X = xxx m3)

**Kiến trúc mới**: Không sử dụng cắt vật lý để tránh làm thay đổi/mất Volume tổng nguyên bản của các cấu kiện. Thay vào đó, khối lượng giao cắt sẽ được tự động tính và điền vào các Parameter động tương ứng với số Zone mà cấu kiện đó chạm vào.

- [ ] **Task 5: Implement Dynamic BIM_X Parameters**
    - **Description**: Quét tất cả các Zone (Generic Models có Mark) giao cắt trong dự án. Tương ứng với mỗi Zone (gọi tên là X), tự động tạo một Shared Parameter kiểu Number/Volume với tên là `BIM_X`. (Ví dụ Zone tên là "Zone 1", Parameter sẽ là `BIM_Zone 1`).
    - **Acceptance Criteria**: Hàm setup parameter chạy thành công và sinh ra đủ số lượng cột `BIM_X` bằng với số lượng Zone thực tế. Xóa bỏ/không sử dụng tiền tố `Vol_`.
    - **Files**: `src/Antigravity.ZoneSplit/Services/ParameterSetupService.cs`

- [ ] **Task 6: Write Volume to BIM_X and Reset Old Data**
    - **Description**: Trong quá trình xử lý, khi cấu kiện giao cắt với bao nhiêu Zone, hệ thống sẽ tính khối lượng phần giao (m3) và điền trực tiếp vào cột `BIM_X` tương ứng của cấu kiện đó. Để tránh dữ liệu rác (zombie data) khi Zone bị dịch chuyển, hệ thống phải tự động **Reset** toàn bộ các tham số `BIM_X` của cấu kiện về `0.0` trước khi điền số liệu mới.
    - **Acceptance Criteria**: Cấu kiện giữ nguyên hình dáng và Volume tổng. Các giá trị khối lượng giao cắt được điền chính xác vào các cột `BIM_[ZoneName]`. Cấu kiện không còn nằm trong Zone sẽ tự động bị reset về 0.
    - **Files**: `src/Antigravity.ZoneSplit/Services/ZoneVolumeProcessor.cs`

## Checkpoint: Phase 4
- [ ] Mọi cấu kiện đều bảo toàn Volume tổng gốc.
- [ ] Bảng Properties xuất hiện các dòng `BIM_Zone 1 = 15.5 m3`, `BIM_Zone 2 = 10.2 m3`, v.v...

## Risks and Mitigations
| Risk | Impact | Mitigation |
|------|--------|------------|
| Parameter Clutter | Low | Sinh ra nhiều cột Parameter nếu dự án có hàng chục Zone. Giải pháp: Có thể gom nhóm lại hoặc yêu cầu user tạo bảng schedule để filter. |
| Database ID changes | High | Khi split tường/dầm, ID cấu kiện sẽ thay đổi, cần lưu ý nếu có link dữ liệu bên ngoài. |
