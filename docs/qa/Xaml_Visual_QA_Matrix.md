# XAML Visual QA Matrix

Ngày tạo: 2026-07-17

## Automated verification

| Gate | Kết quả |
|---|---|
| Parse XAML | PASS — 24 window, 30 total XAML |
| Shared theme/resources | PASS |
| StaticResource graph | PASS — 0 unresolved keys |
| Canonical BrandHeader/BrandSignature | PASS — normal windows migrated; overlay exception retained |
| Captured layout regressions | PASS — Clearance landscape, HoanThien star body/footer, ArchModeling full-width mapping |
| Root background/font/min-size contract | PASS |
| `@manhns` contract | PASS |
| Button wiring | PASS — 131/131 button có action, handler tồn tại và không rỗng |
| Full solution build | PASS — 0 error, 3 warning ngoài UI |
| Unit tests | PASS — 16/16 |

## Manual Revit verification

Chạy trên một model test, không dùng model production. Mỗi module UI phải được kiểm
tra ở default size và minimum size.

| Nhóm | 100% | 125% | 150% | 200% | Keyboard | Long text/data | Revit action |
|---|---|---|---|---|---|---|---|
| ArchModeling | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Draw Walls/Floors/Columns/Beams | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| CAD Sleeve/Void | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| AutoJoin/AutoDim/ZoneSplit | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Door Clearance | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Floor/Wall-MEP clash | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Issue Manager + dialogs | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Tag Arranger/HoanThien | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Password/Markup/Overlay | Pending | Pending | Pending | Pending | Pending | Pending | Pending |

## Mandatory regression scenarios from Revit 2024 capture

- Open every command that previously failed around XAML lines 139, 141 or 152; no `StaticResourceExtension` dialog.
- ClearanceBox: all Scope/Categories/Geometry/Options labels remain readable on white at default and minimum size.
- Clearance clash table: columns fill the workbench; footer actions and `@manhns` do not overlap.
- HoanThien: TabControl fills the body; selected tab remains visible; no unused lower white region.
- ArchModeling Door/Window mode: when the secondary pane is collapsed, the primary mapping DataGrid spans the full result pane.
- At 200% scaling, header subtitle wraps, body scrolls internally and footer remains reachable.

## Pass criteria

- Không có text, button, input hoặc footer bị che/cắt.
- Header, subtitle và `@manhns` đọc được trên nền trắng.
- Tab/Shift+Tab theo thứ tự thao tác; Enter/Escape không kích hoạt hành động nguy hiểm.
- DataGrid/ListView hiển thị đúng khi rỗng, một dòng, nhiều dòng và chuỗi dài.
- Overlay vẫn trong suốt, topmost, vẽ/undo/clear/done/cancel đúng.
- Mọi thao tác ghi Revit chạy trên main thread/ExternalEvent và transaction hiện có.
