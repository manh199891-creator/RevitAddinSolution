# Implementation Plan: Dynamic BIM_X Volume Parameters

This plan replaces physical splitting for complex elements, or for any element where preserving the original model geometry and total volume is preferred. Instead of splitting elements, ZoneSplit calculates each zone intersection volume and writes the values into dynamic BIM parameters.

## Architecture Decisions

- **Preserve original geometry**: Do not physically split elements in this workflow.
- **Dynamic parameters**: Create numeric parameters named `BIM_[ZoneName]`, for example `BIM_Zone 1`.
- **Reset zombie data**: Before writing current volumes, reset existing numeric `BIM_` zone-volume parameters to `0.0` so moved elements do not keep stale quantities.

## Task List

- [x] **Task 1: Update ParameterSetupService (BIM_X)**
    - **Description**:
      - Change `PARAM_VOLUME_PREFIX` from `Vol_` to `BIM_`.
      - `EnsureDynamicZoneVolumeParameters` scans project zones and creates `BIM_[ZoneName]` parameters.
    - **Acceptance Criteria**: Dynamic numeric parameters are created for target categories.
    - **Files**: `src/Antigravity.ZoneSplit/Services/ParameterSetupService.cs`
    - **Status**: IMPLEMENTED (2026-05-16) - Dynamic volume parameters now use `BIM_[ZoneName]`.

- [x] **Task 2: Reset Zombie Data**
    - **Description**: Before writing new intersection volumes, reset all writable numeric parameters whose names start with `BIM_` to `0.0`, excluding text identity parameters such as `BIM_ZoneID`.
    - **Acceptance Criteria**: Elements moved out of an old zone no longer retain stale quantity in that old zone column.
    - **Files**: `src/Antigravity.ZoneSplit/Services/ZoneVolumeProcessor.cs`
    - **Status**: IMPLEMENTED (2026-05-16) - Numeric `BIM_` parameters on target elements are reset before current volumes are written.

- [x] **Task 3: Calculate and Write Volume m3**
    - **Description**:
      - Calculate every valid solid intersection, whether the element intersects one zone or multiple zones.
      - Convert cubic feet to cubic meters.
      - Write the m3 value into the matching `BIM_[ZoneName]` parameter.
    - **Acceptance Criteria**: Revit Properties can show values such as `BIM_Zone 1 = 15.5 m3`.
    - **Status**: IMPLEMENTED (2026-05-16) - Every valid zone intersection writes converted m3 to `BIM_[ZoneName]`.

## Checkpoint

- [x] Beams, walls, floors, and columns are not deformed or physically split by this BIM parameter workflow.
- [x] Dynamic `BIM_X` parameters are created and filled with calculated m3 values.

