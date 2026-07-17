# Implementation Plan: UI/UX recovery and standardization

Ngày: 2026-07-17  
Spec: `docs/superpowers/plans/2026-07-16-ui-ux-standardization-spec.md`  
Execution: Inline, checkpoint theo từng task

## Scope check

Công việc được tách thành năm task. Mỗi task tạo ra trạng thái build/test được xác minh độc lập. Không sửa thuật toán nghiệp vụ Revit hoặc đổi tên event handler/binding.

### Task 1: Khôi phục tính hợp lệ của XAML

**Files:**

- Create: `tests/ui/Assert-XamlContracts.ps1`
- Modify: 21 XAML đang lỗi dưới `src`
- Test: `tests/ui/Assert-XamlContracts.ps1`

**Steps:**

- [ ] Write failing test (RED): parse toàn bộ XAML, phát hiện `Window.Resources` lặp, ResourceDictionary không cân bằng, duplicate attribute và pack URI trỏ file không tồn tại.
- [ ] Run test → verify FAIL trên trạng thái hiện tại.
- [ ] Write minimal implementation (GREEN): xóa block chèn lặp ở `PenOverlayWindow`, đóng ResourceDictionary đúng chỗ, sửa duplicate `Style` của ZoneSplit.
- [ ] Run test → verify phần XML structure PASS; giữ failure theme-file cho Task 2.
- [ ] Build solution để xác nhận không còn lỗi parser XAML; ghi nhận lỗi kế tiếp nếu có.
- [ ] Commit checkpoint (không thể commit nếu `.git` tiếp tục không có metadata).

### Task 2: Tạo shared design system khả dụng

**Files:**

- Create: `src/Antigravity.Core/UI/Themes/DesignTokens.xaml`
- Create: `src/Antigravity.Core/UI/Themes/Typography.xaml`
- Create: `src/Antigravity.Core/UI/Themes/Controls.xaml`
- Create: `src/Antigravity.Core/UI/Themes/DataControls.xaml`
- Modify: `src/Antigravity.Core/Antigravity.Core.csproj` nếu SDK không tự include Page resources
- Test: `tests/ui/Assert-XamlContracts.ps1`

**Steps:**

- [ ] Write failing test (RED): yêu cầu bốn dictionary tồn tại, parse được và chứa token/style contract bắt buộc.
- [ ] Run test → verify FAIL vì theme files chưa tồn tại.
- [ ] Write minimal implementation (GREEN): thêm palette nền trắng, typography, semantic button/input styles, focus/disabled states và data-control baseline; tránh implicit style có blast radius ngoài ý muốn.
- [ ] Run test → verify PASS.
- [ ] Build `Antigravity.Core`, sau đó full solution.
- [ ] Commit checkpoint nếu Git khả dụng.

### Task 3: Chuẩn hóa layout shell và readability

**Files:**

- Modify: toàn bộ 24 XAML dưới `src`
- Modify: `VilaiViet_UI_Guidelines.md`
- Test: `tests/ui/Assert-XamlContracts.ps1`

**Steps:**

- [ ] Write failing test (RED): kiểm tra root font/background, min size cho resizable window, font nội dung tối thiểu, signature `@manhns`, target button tối thiểu và exception rõ cho overlay.
- [ ] Run test → verify FAIL trên các window chưa chuẩn.
- [ ] Write minimal implementation (GREEN): chuẩn hóa root properties, white header/divider, text palette, font, min size, scroll/wrap và signature; không đổi `x:Name`, handler hoặc binding.
- [ ] Run test → verify PASS.
- [ ] Build solution; sửa chỉ lỗi layout/resource phát sinh từ task này.
- [ ] Commit checkpoint nếu Git khả dụng.

### Task 4: Khóa interaction/button contract

**Files:**

- Modify: `tests/ui/Assert-XamlContracts.ps1`
- Modify: XAML/code-behind chỉ khi test chứng minh handler thiếu hoặc button không có hành vi
- Test: `tests/ui/Assert-XamlContracts.ps1`

**Steps:**

- [ ] Write failing test (RED): kiểm kê mọi Button, yêu cầu `Click`, `Command`, dialog role hoặc code reference; xác minh mọi Click handler tồn tại trong code-behind và không có thân rỗng/TODO.
- [ ] Run test → xác nhận contract hiện tại; nếu test không fail vì implementation đã đủ, lưu đây là characterization gate thay vì tạo thay đổi giả.
- [ ] Write minimal implementation (GREEN) chỉ cho button thực sự thiếu wiring.
- [ ] Run test → verify 100% button contract PASS.
- [ ] Đối chiếu ribbon `PushButtonData` với command classes và assembly names.
- [ ] Commit checkpoint nếu Git khả dụng.

### Task 5: Verification và handoff QA

**Files:**

- Create: `docs/qa/Xaml_Visual_QA_Matrix.md`
- Modify: `docs/superpowers/plans/2026-07-17-ui-ux-recovery-and-standardization.md` để ghi kết quả thực tế
- Test: full solution + UI contract

**Steps:**

- [ ] Write failing acceptance gate (RED): QA matrix chưa có status và automated commands chưa xanh.
- [ ] Run `tests/ui/Assert-XamlContracts.ps1`.
- [ ] Run `dotnet build Antigravity.sln --no-restore -p:DeployToRevitAddins=false`.
- [ ] Run các test project hiện có phù hợp, không deploy vào Revit.
- [ ] Tạo QA matrix cho DPI 100/125/150/200%, minimum/default size, keyboard, long text và Revit-only actions.
- [ ] Ghi rõ automated PASS và những mục bắt buộc kiểm tra trực tiếp trong Revit; không tuyên bố runtime PASS nếu chưa chạy trong host.
- [ ] Commit checkpoint nếu Git khả dụng.

## File map

- `src/Antigravity.Core/UI/Themes/*`: single source of truth cho palette, typography và control states.
- 24 XAML dưới `src`: consumer của shared theme; giữ resource nghiệp vụ cục bộ.
- `tests/ui/Assert-XamlContracts.ps1`: regression gate không cần Revit process hoặc package mới.
- `VilaiViet_UI_Guidelines.md`: canonical governance cho light theme.
- `docs/qa/Xaml_Visual_QA_Matrix.md`: manual acceptance trong Revit/multi-DPI.

## Parallel detection

Task 3 có thể chia theo module sau khi Task 1–2 hoàn tất, nhưng execution hiện tại chạy inline do không được phép tự phát sinh subagent. Task 4 có thể chạy song song về mặt logic nhưng được đặt sau Task 3 để snapshot interaction phản ánh XAML cuối.

## Execution results — 2026-07-17

- Task 1: Complete. Khôi phục 21 XAML lỗi; loại resource injection lặp và duplicate attribute.
- Task 2: Complete. Thêm bốn theme dictionary và hai reusable brand controls trong Core.
- Task 3: Complete for automated scope. 23 normal windows đạt light-theme/root/min-size/signature contract; overlay giữ exception.
- Task 4: Complete. 131/131 button có action; handler tồn tại, parse được và không phải stub.
- Task 5 automated gates: Complete. Contract PASS, build 0 error, 16/16 unit tests PASS.
- Task 5 visual/Revit host gates: Pending manual theo `docs/qa/Xaml_Visual_QA_Matrix.md`.
- Git checkpoint: không thực hiện được vì thư mục `.git` không có metadata repository.
