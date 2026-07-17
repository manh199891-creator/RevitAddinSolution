# Changelog

## [2026-07-17]
### Added
- **Shared UI system:** Added common design tokens, typography, control styles, branded headers, and the `@manhns` signature for Revit add-in windows.
- **UI contract validation:** Added automated XAML checks covering 24 windows, 131 buttons, theme resources, readability, minimum sizing, signatures, and click-handler wiring.

### Changed
- **UI/UX standardization:** Standardized white window backgrounds, header colors, Segoe UI typography, readable font sizing, resize behavior, and scroll-safe layouts across the add-in suite.

### Fixed
- **Revit 2024 deployment:** Corrected framework-output selection so `ZoneSplit` deploys the real `net48` assembly instead of a small intermediate DLL.
- **ArchModeling deployment:** Added ExcelDataReader, Newtonsoft.Json, JetBrains annotations, and Nice3point dependencies required by its ribbon commands.

## [2026-05-18]
### Added
- **Monorepo Revit 2024 Upgrade:** Upgraded all project files (`.csproj`) to target the Revit 2024 SDK (via NuGet references or direct assembly paths) to secure compatibility for current and future Revit versions.
- **Granular Type Selection:** Added interactive ListBox controls to `ClearanceBoxWindow.xaml` to display family types dynamically for Doors, Windows, Curtain Panels, and Curtain Walls.
- **Clash Control (Kiểm Soát Xung Đột):** Implemented a high-performance 3D structural collision pre-check system to query Columns, Beams, Walls, and Floors intersecting door clearance boxes (excluding self and host walls to avoid false positives).
- **VILAIVIET Tab Consolidation:** Deleted the separate, redundant "BIM Tools" Ribbon tab from both `DoorClearanceBox` and `ZoneSplit` add-ins. Consolidated all features under the corporate **VILAIVIET** ribbon tab, introducing a dedicated **KIỂM SOÁT XUNG ĐỘT** panel.

### Changed
- **DX Post-Build Resilience:** Enhanced post-build events to never fail the compilation when Revit holds file locks.

### Changed
- **Obsolete API Cleanup:** Replaced all obsolete `ElementId.IntegerValue` calls with `ElementId.Value` properties (type `long`) across all modules (Beams, DoorClearance, Autojoin), avoiding overflow issues on extremely large BIM models.

### Fixed
- **InvalidCastException Protection:** Audited all `.Cast<T>()` operations and confirmed type safety via `.OfClass(typeof(T))` guards to prevent runtime crashes.
- **AutoJoin Deployment:** Deployed clean assemblies directly into the target Autodesk Revit 2024 Add-in directory.

## [2026-05-14]
### Added
- **AutoJoin Reporting Panel:** New side panel in AutoJoin UI showing intersection statistics (Total, Success, Already Joined, Failed).
- **Failed Joins Tracking:** Detailed list showing Element IDs and specific error reasons for joins that couldn't be processed.
- **Flex-Scroll Layout:** Implementation of scrollable rule lists in AutoJoin to preserve button visibility.

### Changed
- **UI Standardization:** 
    - Hardcoded Dark Mode background (`#2D2D8A`) for all ComboBoxes to ensure visibility across system themes.
    - Enabled `CanResize` mode for all Add-in modules (Walls, Floors, Beams, Columns, AutoJoin).
    - Unified bottom margin (10px) for all status bars to prevent layout clipping.
- **UI Guidelines:** Updated `VilaiViet_UI_Guidelines.md` with new standards for Dark Mode, Resizing, and Flex layouts.

### Fixed
- **ComboBox Visibility:** Fixed issue where ComboBoxes would turn light gray with white text on certain Windows themes.
- **AutoJoin Layout:** Fixed issue where adding multiple rules would push action buttons off-screen.
