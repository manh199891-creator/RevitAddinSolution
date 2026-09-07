# Implementation Plan: BCF ClippingPlanes to Revit Section Box

## Overview
When the user clicks "Show in Model" for a BCF issue, we need to apply the saved `ClippingPlanes` to the active 3D View as a Section Box. Because the clipping planes in BCF may be arbitrarily rotated (due to Revit's True North or manual rotation in Trimble Connect), we must mathematically reconstruct a Revit `BoundingBoxXYZ` from the 6 planes.

## Architecture Decisions
- **Math for Bounding Box Reconstruction:** We cannot assume the planes are aligned to the global X/Y/Z axes. We will:
  1. Convert all plane `Location`s (meters to feet) and `Direction`s to Revit Internal Coordinates using `ActiveProjectLocation.GetTransform().Inverse`.
  2. Identify 3 orthogonal basis vectors from the plane directions to form a local coordinate system (`Transform`).
  3. Project the plane locations into this local system. Since the planes are now axis-aligned in this local space, we can easily extract `Min` and `Max`.
  4. Create a `BoundingBoxXYZ` with the computed `Min`, `Max`, and `Transform`, and apply it using `View3D.SetSectionBox()`.
- **Handling Incomplete Planes:** If the BCF issue has fewer than 6 planes, or they do not form a closed box, we will fall back to using them to set a partial BoundingBox if possible, or skip the section box if it's too complex. Usually, Trimble Connect / BCF exports exactly 6 planes for a box.

## Task List

### Phase 1: Foundation
- [ ] **Task 1: Math Utility for Plane Intersection/Bounding Box**
  - **Description:** Create a helper method in `RevitCameraSync` (e.g., `CreateSectionBoxFromClippingPlanes`) that takes a list of `ClippingPlaneModel` and the `Document`.
  - **Acceptance criteria:**
    - Correctly converts coordinates and vectors to Internal Coordinates.
    - Groups planes by parallel normals to find the 3 axes.
    - Constructs the `BoundingBoxXYZ` `Transform`, `Min`, and `Max`.
    - Returns the `BoundingBoxXYZ`.
  - **Files likely touched:** `src/Antigravity.IssueManager/Services/RevitCameraSync.cs`

- [ ] **Task 2: Apply Section Box to View3D**
  - **Description:** Update `RevitCameraSync.SyncCamera` to apply the Section Box if the issue has `ClippingPlanes` and the user checked the "Include Section Box" option.
  - **Acceptance criteria:**
    - Checks if `viewpoint.ClippingPlanes` has elements.
    - Calls `CreateSectionBoxFromClippingPlanes`.
    - Uses `Transaction` to apply `view3d.SetSectionBox(box)`.
  - **Files likely touched:** `src/Antigravity.IssueManager/Services/RevitCameraSync.cs`

### Checkpoint: Complete
- [ ] Code compiles without errors.
- [ ] BCF issues with Section Box from Trimble Connect successfully trigger a cropped view in Revit.

## Risks and Mitigations
| Risk | Impact | Mitigation |
|------|--------|------------|
| Planes don't form a perfect box | Medium | Add a tolerance check (e.g., `IsAlmostEqualTo`) when finding orthogonal vectors. If they don't form an orthogonal box, ignore the section box or apply a default bounding box around the clash point. |
| BCF has arbitrary planes (not a box) | Low | Same as above. Revit only supports Box section boxes. We will only apply it if we can find 3 orthogonal axes. |

## Open Questions
- If a BCF viewpoint contains only 1 or 2 clipping planes (e.g., just a floor cut), how should we handle the other boundaries of the Section Box? (Recommendation: Set the missing boundaries to a very large number or to the Extents of the project).
