# Automatic Formwork Add-in — Research and Specification

Date: 2026-07-22  
Status: Awaiting approval  
Phase: `/1.spec`

## Goal

Xây dựng Revit add-in tự động lập phương án cốp pha theo catalog thiết bị cho cấu kiện bê tông đổ tại chỗ, bắt đầu bằng tường và cột, đồng thời giữ cho kỹ sư quyền xem trước, khóa, sửa và thống kê từng cấu kiện cốp pha.

## Research findings from BIM² channel

Kênh BIM² hiện có 82 video và thể hiện BIM²form như một hệ thống lập phương án, không chỉ là công cụ tạo một lớp hình học bao quanh bê tông. Các workflow nổi bật:

1. Tự động hóa theo chu kỳ đổ:
   - Forming toàn bộ cycles trong một lần.
   - Forming từng cycle để người dùng kiểm soát tuần tự.
   - Kết nối giữa các cycle và tô màu/hiển thị theo phase.

2. Cốp pha đứng:
   - Nhận dạng toàn bộ giao tường, góc, nút chữ T/Y và cột.
   - Xử lý tường thay đổi chiều dày, tường nghiêng, tường cong.
   - Điều khiển hướng xếp theo thứ tự click, độ nhô và chiều sâu ăn khớp.
   - Bù chiều dài, filler gỗ, lying extension và stop-end.

3. Phụ kiện và khả năng sửa thủ công:
   - Tie rod, lock, alignment rail, brace, scaffolding bracket.
   - Đặt một cấu kiện hoặc phụ kiện thủ công theo điểm.
   - Cho phép chỉnh kết quả tự động thay vì coi kết quả là bất biến.

4. Cốp pha sàn và hệ chống:
   - Nhận diện biên sàn.
   - Panel/deck system, shoring tower, head, extension và điều kiện đỡ đặc biệt.
   - Đây là subsystem khác đáng kể so với tường/cột.

5. Catalog và đầu ra:
   - Hỗ trợ nhiều hãng/hệ cốp pha.
   - Content/family browser và thư mục content.
   - Quản lý phần tử, thống kê và xuất Excel tùy chỉnh.

### Product lesson

Giá trị cốt lõi cần sao chép ở cấp độ workflow là: `Concrete host -> topology -> zones/cycles -> system-specific layout -> validation -> native Revit instances -> manual correction -> quantities`. Không sao chép family, dữ liệu catalog hay logic độc quyền của BIM².

## Primary users

- Kỹ sư biện pháp/cốp pha lập phương án thi công.
- BIM structural modeler cần mô hình, shop drawing và khối lượng.
- Quản lý thiết bị cần BOM theo mã hàng, cycle và tầng.

## Scope — IN for MVP

1. Revit host input
   - Structural Wall và Structural Column bê tông đổ tại chỗ trong document hiện hành.
   - Chọn thủ công, theo view hoặc theo level.
   - Kiểm tra host không hợp lệ trước khi sinh phương án.

2. Geometry and topology
   - Trích xuất các mặt cần cốp pha, loại mặt tiếp xúc với bê tông khác theo rule cấu hình.
   - Phân đoạn cạnh thẳng; nhận dạng end, corner, T-junction và cross-junction.
   - Chuẩn hóa đơn vị và tolerance; tạo mô hình hình học thuần .NET để có thể unit test ngoài Revit.

3. Cycle management
   - Gán cycle thủ công hoặc theo level/zone parameter.
   - Form từng cycle hoặc toàn bộ cycles.
   - Không cho hai cycle sinh trùng cùng một mặt nếu không có rule kết nối.

4. One configurable wall-formwork system
   - Catalog nội bộ gồm panel, inside/outside corner, filler, stop-end, tie và alignment rail.
   - Mỗi item có mã hàng, kích thước, orientation hợp lệ, family/type mapping và giới hạn sử dụng.
   - Layout ưu tiên panel tiêu chuẩn, sau đó tối thiểu hóa phần bù theo rule có thể cấu hình.

5. Preview, validation and commit
   - Preview phương án trước khi tạo phần tử.
   - Báo khoảng hở, chồng lấn, item thiếu family/type, panel vượt host và vị trí không có tie hợp lệ.
   - Commit bằng native loadable FamilyInstance để schedule, tag, filter và chỉnh thủ công được.
   - Ghi metadata: HostUniqueId, FaceKey, CycleId, SystemId, CatalogItemId, RunId và trạng thái Locked/Manual.

6. Update workflow
   - Detect thay đổi host/cycle/catalog.
   - Rebuild chỉ phần tử generated chưa khóa; giữ phần tử Locked/Manual và báo conflict.
   - Xóa an toàn theo RunId hoặc phạm vi người dùng xác nhận.

7. Output
   - Schedule/BOM theo item, level, cycle và host.
   - Tổng diện tích cốp pha, số lượng panel/phụ kiện và danh sách phần bù.
   - Export CSV trong MVP; Excel tùy chỉnh là phase sau.

## Scope — OUT for MVP

- Cốp pha sàn, beam soffit, shoring tower và scaffolding đầy đủ.
- Tường cong, tường nghiêng, tường thay đổi chiều dày theo chiều cao.
- Tối ưu tồn kho nhiều công trường, logistics, chi phí và lịch luân chuyển 4D.
- Tự động thiết kế theo tải/áp lực bê tông hoặc chứng minh khả năng chịu lực.
- Hỗ trợ nhiều hãng trong lần phát hành đầu tiên.
- Cloud catalog, licensing thương mại và collaboration server.
- Sao chép dữ liệu family/catalog hoặc thuật toán độc quyền từ BIM²form.

## Proposed architecture

### 1. Revit integration layer

Project mới `src/Antigravity.Formwork` theo `net48`, Revit API và WPF conventions hiện có. Layer này chỉ đảm nhiệm selection, geometry extraction, transaction, family placement, parameter/storage và ribbon command.

### 2. Testable domain engine

Project mới `src/Antigravity.Formwork.Core` không tham chiếu Revit API, gồm:

- Concrete surface DTOs and topology graph.
- Catalog model and compatibility rules.
- Cycle/zone model.
- Panelization solver.
- Collision/gap/coverage validators.
- Deterministic plan diff for update/regeneration.

Engine nhận dữ liệu theo millimeter và trả về placement plan bất biến. Cùng input, catalog và config phải cho cùng output.

### 3. Catalog adapter

Catalog versioned được lưu ngoài code dưới JSON, nhưng family/type mapping được validate trong Revit trước khi chạy. Solver làm việc bằng `CatalogItemId`, không phụ thuộc tên family hiển thị.

### 4. Persistence

- Shared parameters cho dữ liệu cần schedule/filter.
- Extensible Storage hoặc DataStorage cho RunId, catalog version, solver settings và dấu vết update.
- Không suy ngược ownership chỉ từ tên family.

### 5. UI/UX

Một workbench WPF dạng landscape theo design system VilaiViet hiện có:

- Trái: nguồn host, level, cycle và system.
- Giữa: danh sách run/host cùng trạng thái coverage và lỗi.
- Phải: rule panelization, preview summary và conflict inspector.
- Footer: Preview, Generate/Update, Export và Close.

UI không chứa thuật toán; mọi thao tác Revit chạy qua ExternalEvent hoặc external command phù hợp.

## Recommended delivery slices after approval

1. Foundation spike: geometry DTO, planar wall faces, deterministic segment tests.
2. Vertical MVP: straight isolated walls, one panel system, preview and native placement.
3. Junction engine: ends, L/T/X intersections, corners and stop-ends.
4. Complete wall run: fillers, ties, rails, cycle connections and update/lock behavior.
5. Columns: rectangular columns using the same catalog/rule engine.
6. Production output: BOM, CSV, diagnostics, performance and Revit integration tests.
7. Separate future product track: slab/deck/shoring.

The exact task/file/TDD implementation plan is intentionally deferred to `/2.plan` until this spec is approved.

## Acceptance criteria for MVP

- A straight-wall benchmark set produces deterministic layout results independent of selection order.
- Supported wall faces reach at least 98% planned coverage; every remaining gap is classified and visible.
- No generated panel overlaps another generated panel beyond configured assembly tolerance.
- Every generated instance has valid catalog, host, face, cycle and run metadata.
- Re-running unchanged input creates no duplicate instances and produces an empty diff.
- Updating one host changes only its affected unlocked placements.
- Locked/manual placements are preserved and conflicts are reported.
- BOM quantities reconcile exactly with generated instances.
- Domain unit tests run without Revit; Revit integration tests cover placement, update and deletion by RunId.
- A benchmark of 100 straight wall segments completes preview within the performance budget agreed before `/2.plan`.

## Risks

- Catalog quality is the dominant dependency: without exact panel dimensions, connectors and family insertion/origin rules, automation cannot be production-correct.
- Revit face references are not durable enough alone; stable host/run metadata and geometric face keys are required.
- Junction combinatorics grow quickly; the solver must use bounded rule sets and diagnostics rather than silent fallback.
- Native families are editable and schedulable but heavier than DirectShape; preview and committed representations should be separated.
- “Automatic formwork” must not imply structural certification unless engineering checks are explicitly implemented and validated.

## Open questions / approval decisions

1. Hệ cốp pha đầu tiên là catalog nào: PERI, DOKA, MEVA, RINGER hay bộ family nội bộ?
2. Revit target là 2024 בלבד hay 2024–2026?
3. MVP có chấp nhận giới hạn `tường thẳng + cột chữ nhật` như đề xuất không?
4. Cycle lấy từ parameter hiện có, người dùng vẽ zone, hay add-in tự chia theo giới hạn chiều dài/khối lượng?
5. Tie/rail/brace trong MVP chỉ bố trí hình học theo catalog hay phải kiểm tra tải và khoảng cách kỹ thuật?
6. Đầu ra ưu tiên là mô hình 3D, shop drawing, BOM/Excel hay cả ba theo một thứ tự cụ thể?
7. Có bộ family và project mẫu được phép dùng để benchmark/kiểm thử không?
8. Performance budget mong muốn cho 100 đoạn tường là bao nhiêu?

## Approval gate

Đề xuất approve spec với mặc định: Revit 2024, one internal catalog, tường thẳng + cột chữ nhật, cycle thủ công/theo parameter, không structural certification trong MVP, đầu ra 3D + BOM CSV. Sau approval mới chạy `/2.plan` để map exact files và TDD tasks.
