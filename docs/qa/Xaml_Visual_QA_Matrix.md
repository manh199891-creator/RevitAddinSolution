# XAML Visual QA Matrix

Created: 2026-07-17

## Automated verification

| Gate | Result |
|---|---|
| Parse XAML | PASS — 24 windows, 30 total XAML files |
| Shared theme/resources | PASS |
| StaticResource graph | PASS — 0 unresolved keys |
| Canonical BrandHeader | PASS — header owns `@manhns`; overlay exception retained |
| Layout regressions | PASS — Zone two-column, Tag three-column landscape, Floor light DataGrid |
| Root background/font/minimum size | PASS |
| English visible XAML and Ribbon copy | PASS |
| Button wiring | PASS — 131/131 buttons have a valid action and implemented handler |
| Full solution build | PASS — 0 errors |

## Manual Revit verification

Run on a test model, never a production model. Verify each module at its default and minimum size.

| Group | 100% | 125% | 150% | 200% | Keyboard | Long text/data | Revit action |
|---|---|---|---|---|---|---|---|
| ArchModeling | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Draw Walls/Floors/Columns/Beams | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| CAD Sleeve/Void | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| AutoJoin/AutoDim/Zone Split | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Door Clearance | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Floor/Wall-MEP clash | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Issue Manager + dialogs | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Tag Arranger/Room Finishes | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| Password/Markup/Overlay | Pending | Pending | Pending | Pending | Pending | Pending | Pending |

## Mandatory Revit 2024 regression scenarios

- Open every command that previously failed around XAML lines 139, 141, or 152; no `StaticResourceExtension` dialog may appear.
- Every normal header shows `@manhns` immediately after the module title; no signature appears in a footer or result panel.
- Zone Split uses two balanced columns and keeps both footer actions visible at minimum size.
- Tag Arranger opens as a landscape, three-column workspace; Auto Tag scrolls internally without moving the footer.
- Check Floor Elevation uses white/light ComboBox and DataGrid surfaces with readable empty, selected, error, and no-match states.
- All Ribbon panels, commands, windows, labels, buttons, tooltips, validation messages, and status text are English.
- At 200% scaling, the header wraps, the body scrolls internally, and footer actions remain reachable.

## Pass criteria

- No text, button, input, DataGrid column, or footer is clipped or obscured.
- Header, subtitle, and `@manhns` remain readable on white.
- Tab/Shift+Tab follows visual order; Enter/Escape does not trigger destructive actions.
- DataGrid/ListView renders correctly with empty, single-row, long-row, and high-volume data.
- The overlay remains transparent and topmost; draw/undo/clear/done/cancel continue to work.
- Every Revit write operation remains on the existing main-thread/ExternalEvent and transaction path.
