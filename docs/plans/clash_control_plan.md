# Plan: Consolidated Clash Control & VILAIVIET Tab Cleanups
Created: 2026-05-18T17:05:00+07:00
Status: 🟡 In Progress

## Overview
Consolidate the entire Monorepo ribbon UI under a single unified **VILAIVIET** ribbon tab. Delete the redundant **BIM Tools** tab registration from both `DoorClearanceBox` and `ZoneSplit` add-ins. Create a dedicated **KIỂM SOÁT XUNG ĐỘT** (Clash Control) panel on the **VILAIVIET** tab containing a new **ClashControlCommand** button. This command will launch a premium WPF interface to scan, visualize, zoom-to, and report structural collisions (Columns, Beams, Walls, Floors) intersecting with door clearance volumes.

## Tech Stack
- Frontend: WPF (C# / XAML) conforming to Vilai Viet corporate dark blue design guidelines (`#2D2D8A`).
- Backend: Revit API 2024 (64-bit safe `ElementId.Value`) utilizing high-performance `ElementIntersectsSolidFilter`.

## Implementation Phases

| Phase | Name | Status | Progress |
|-------|------|--------|----------|
| 01 | Clean up & Delete "BIM Tools" Tab | ⬜ Pending | 0% |
| 02 | Add "KIỂM SOÁT XUNG ĐỘT" Ribbon Panel | ⬜ Pending | 0% |
| 03 | Implement Clash Detection Core Logic | ⬜ Pending | 0% |
| 04 | Build ClashControlWindow UI | ⬜ Pending | 0% |
| 05 | Connect Logic & UI (Zoom, Show, Export) | ⬜ Pending | 0% |
| 06 | Build & Deploy Testing | ⬜ Pending | 0% |

## Implementation Steps

### Phase 1: Clean up & Delete "BIM Tools" Tab
- Modify `src/Antigravity.DoorClearance/App.cs`:
  - Completely remove the `BuildRibbon` method.
  - Keep the `RegisterUpdater` call to ensure the DoorChangeUpdater (DMU) runs in the background.
- Modify `src/Antigravity.ZoneSplit/App.cs`:
  - Make `OnStartup` return `Result.Succeeded` instantly without creating any ribbon tabs, panels, or buttons.

### Phase 2: Add "KIỂM SOÁT XUNG ĐỘT" Ribbon Panel
- Edit `src/Antigravity.Main/App.cs`:
  - Create a new ribbon panel named `"KIỂM SOÁT XUNG ĐỘT"`.
  - Add a new `PushButton` called **"Kiểm Soát Xung Đột"** pointing to `DoorClearanceBox.Commands.ClashControlCommand`.
  - Wire up tooltips and assign the corporate logo image.

### Phase 3: Implement Clash Detection Core Logic
- Create `src/Antigravity.DoorClearance/Core/ClashChecker.cs`:
  - Query all active clearance DirectShapes via `ReserveSpaceGeometry.GetAll(doc)`.
  - For each shape, retrieve its 3D solid and parse its parent element's ID from the `ALL_MODEL_MARK` parameter.
  - Instantiate `ElementIntersectsSolidFilter` using the clearance solid.
  - Filter clashing elements belonging to structural categories: `OST_StructuralColumns`, `OST_StructuralFraming`, `OST_Walls`, `OST_Floors`.
  - Protect against false positives by excluding the clashing element if it is the parent element's host wall.

### Phase 4: Build ClashControlWindow UI
- Create `src/Antigravity.DoorClearance/UI/ClashControlWindow.xaml`:
  - Set `CanResize="True"`, borderless or clean chrome styled.
  - Hardcode corporate dark blue `#2D2D8A` backgrounds for buttons/grids.
  - Add a styled `DataGrid` displaying: Door Mark, Door Family, Clashing Category, Clashing Element ID, Collision Volume (m³).
- Create `src/Antigravity.DoorClearance/UI/ClashControlWindow.xaml.cs`:
  - Implement dynamic list binding.

### Phase 5: Connect Logic & UI (Zoom, Show, Export)
- Implement `Show` click event: Zoom/select the selected clashing element in Revit using `ActiveUIDocument.ShowElements` and `ActiveUIDocument.Selection.SetElementIds`.
- Implement `Export` click event: Export clean CSV reports of all collisions that can be opened directly in Microsoft Excel.

### Phase 6: Build & Deploy Testing
- Run `dotnet build Antigravity.sln /p:Configuration=Debug` with Revit closed to copy assemblies.
- Open Revit 2024 to verify tab cleanups, new ribbon panel, and test the Clash Control interface.
