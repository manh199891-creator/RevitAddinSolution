# Implementation Plan: AutoDim Architectural Walls (AutoDim Tường Kiến Trúc)

## Overview
Develop a high-performance, intelligent **AutoDim Architectural Walls** Revit add-in under the new `BimTools` project. This tool will automate the tedious process of dimensioning architectural walls by generating precise multi-tier dimension lines (Overall Length, Wall Openings, Intersecting Walls, and Grid Lines) in the active view based on user-defined scope and dimension settings, conforming to the Vilai Viet corporate dark blue WPF design guidelines.

---

## Technical Approach & Key Decisions

### 1. Revit API Dimensioning Architecture
Revit dimensions are view-specific elements requiring:
1. **View:** An active 2D view (typically `ViewPlan` or `ViewSection`).
2. **Dimension Line:** A 3D `Line` indicating where the dimension ticks are placed.
3. **ReferenceArray:** A collection of stable `Reference` objects representing faces, ends, grid lines, or family reference planes.
4. **API Entrypoint:** `Document.Create.NewDimension(View view, Line line, ReferenceArray references)`.

### 2. Reference Parsing Strategy (The Hard Part in Revit API)
- **Wall Ends:** Query the Wall's geometry at `ViewDetailLevel.Fine` and find the end faces perpendicular to the wall's orientation. Retrieve their `Reference` using `HostObjectUtils.GetSideFaces` or parsing solid faces.
- **Openings (Doors/Windows):** Query family instances hosted by the wall. Retrieve the family's internal reference planes (typically named "Left", "Right", or "Center L/R") by parsing the family geometry or querying references via `SpecialFamilyInstanceUtils`.
- **Intersecting Walls:** Use `ElementIntersectsSolidFilter` along the wall's bounding volume to find intersecting structural or architectural walls, extracting their intersecting faces.
- **Grids:** Find grid lines that intersect the wall's location line curve using intersection math, and retrieve the Grid's reference.

### 3. Coordinate System & Offsetting (Interactive Placement)
- The dimension line orientation is parallel to the wall location curve and offset along the wall's normal vector.
- **Interactive Mouse Placement (Pick Point):** The add-in supports interactive positioning. The user can click a "Chọn Điểm Đặt" button, prompting `uiDoc.Selection.PickPoint("Chọn vị trí đặt đường Dim")`. The clicked 3D point is projected onto the wall's normal vector to determine the custom offset distance dynamically.
- **Custom Dimension Style Selection:** The UI contains a ComboBox listing all visible `DimensionType` elements in the active Revit project, allowing the user to select their desired dimension style dynamically rather than using hardcoded defaults.
- **Geometry Type:** Phase 1 (V1) will focus exclusively on straight walls (`Line` location curves) to ensure maximum mathematical stability. Curved walls will be skipped with safety guards.

---

## Dependency Graph
```
BimTools.csproj (Project Setup & Add-in Manifest)
   │
   ├── Core Geometry Utilities (Wall & Face Reference parsers)
   │       │
   │       ├── AutoDim Engine (Reference gathering & Dimension builder)
   │       │       │
   │       │       └── WPF UI Interface (Vilai Viet dark blue styles)
   │       │               │
   │       │               └── BimToolsCommands (Revit Command Integration)
```

---

## Task List

### Phase 1: Project Setup & Bootstrapping
Configure `BimTools` to compile cleanly and register as a Revit add-in.

#### Task 1.1: Project Environment & Assembly Setup
- **Description:** Prepare `BimTools.csproj` for compilation. Add post-build copy scripts to deploy output files to Revit's active Addins directory. Create `BimTools.addin` manifest.
- **Acceptance criteria:**
  - [ ] `BimTools.csproj` compiles successfully targeting .NET Framework 4.8.
  - [ ] `BimTools.addin` manifest is correctly formatted with add-in GUID.
  - [ ] Post-build script deploys assemblies to `%APPDATA%\Autodesk\Revit\Addins\2024\`.
- **Verification:**
  - [ ] Build succeeds: `dotnet build BimTools.csproj /p:Configuration=Debug`
- **Dependencies:** None
- **Files likely touched:**
  - `BimTools.csproj` (Modify)
  - `BimTools.addin` (Create)
- **Estimated scope:** Small

#### Task 1.2: Add-in UI/Command Registry
- **Description:** Create the core command entry point and setup serilog logging. Register the main VILAIVIET Ribbon panel hook.
- **Acceptance criteria:**
  - [ ] `BimToolsCommands.cs` implements `IExternalCommand` correctly.
  - [ ] Pressing the button launches a dummy dialog confirming setup.
- **Verification:**
  - [ ] Build succeeds.
- **Dependencies:** Task 1.1
- **Files likely touched:**
  - `BimToolsCommands.cs` (Create)
- **Estimated scope:** Small

---

### Phase 2: Core Geometry & Reference Retrieval Engine
Build the high-performance reference resolver, which is the foundational mathematical backbone.

#### Task 2.1: Wall End Faces Reference Resolver
- **Description:** Implement utility functions to extract stable `Reference` objects representing the start and end cap faces of a given Wall.
- **Acceptance criteria:**
  - [ ] Extracts valid perpendicular end faces for straight walls.
  - [ ] Correctly falls back to structural end-joints.
- **Verification:**
  - [ ] Core math unit test/verification passes.
- **Dependencies:** Task 1.2
- **Files likely touched:**
  - `Core/WallGeometryUtils.cs` (Create)
- **Estimated scope:** Medium

#### Task 2.2: Opening Reference Resolver (Tim/Mép Cửa)
- **Description:** Parse hosted Doors and Windows inside the wall. Retrieve Reference objects of their center plane (tim cửa) or outer jamb faces (mép cửa) based on user configuration.
- **Acceptance criteria:**
  - [ ] Retrieves center reference for standard Doors/Windows.
  - [ ] Retrieves left/right jamb references for structural openings.
- **Verification:**
  - [ ] Correctly compiles and logs parsed opening references.
- **Dependencies:** Task 2.1
- **Files likely touched:**
  - `Core/OpeningReferenceResolver.cs` (Create)
- **Estimated scope:** Large

#### Task 2.3: Intersecting Wall & Grid Reference Resolver
- **Description:** Find intersecting walls and grid lines that cross the main wall's location curve, and extract their target face/plane references.
- **Acceptance criteria:**
  - [ ] Detects intersecting walls cleanly.
  - [ ] Extracts intersection references for visible Grid lines.
- **Verification:**
  - [ ] Intersections verified via log counts.
- **Dependencies:** Task 2.2
- **Files likely touched:**
  - `Core/IntersectionResolver.cs` (Create)
- **Estimated scope:** Medium

---

### Phase 3: AutoDim Execution & Transaction Logic
Implement the active dimension generator inside Revit.

#### Task 3.1: Dimension Generator Engine
- **Description:** Write the core `AutoDimEngine` class that takes a Wall, a set of options (Overall, Openings, Grids), an offset, and generates the actual Revit `Dimension` elements.
- **Acceptance criteria:**
  - [ ] Successfully places Overall dimensions (Tier 1).
  - [ ] Successfully places detail/opening dimensions (Tier 2).
  - [ ] Safely wraps creation in a transaction rollback.
- **Verification:**
  - [ ] Visual verification of dimension placement in Revit active view.
- **Dependencies:** Phase 2
- **Files likely touched:**
  - `Services/AutoDimEngine.cs` (Create)
- **Estimated scope:** Large

---

### Phase 4: UI Design & Integration
Develop the premium WPF user control and link it with the engine.

#### Task 4.1: AutoDimWindow UI
- **Description:** Design a stunning, user-friendly WPF dialog matching Vilai Viet guidelines.
- **Acceptance criteria:**
  - [ ] Beautiful dark blue corporate styling (`#1A1A5E` / `#2D2D8A`).
  - [ ] Selection toggles for: Scope (Active View vs Selection), Dimension Types (Overall, Openings, Intersections, Grids), Opening Mode (Center vs Jamb).
  - [ ] Slider or text box to define dimension offset distance.
- **Verification:**
  - [ ] WPF window loads with flawless rendering.
- **Dependencies:** Task 3.1
- **Files likely touched:**
  - `UI/AutoDimWindow.xaml` (Create)
  - `UI/AutoDimWindow.xaml.cs` (Create)
- **Estimated scope:** Medium

#### Task 4.2: Command Wiring & Verification
- **Description:** Wire the WPF actions to call the `AutoDimEngine` inside an external command execution.
- **Acceptance criteria:**
  - [ ] Triggering "QUÉT & DIM TỰ ĐỘNG" executes the tool on selected or visible walls.
  - [ ] "XÓA DIM" clears dimensions created by the tool.
- **Verification:**
  - [ ] End-to-end integration verification in Revit 2024.
- **Dependencies:** Task 4.1
- **Files likely touched:**
  - `BimToolsCommands.cs` (Modify)
- **Estimated scope:** Small

---

## Checkpoint & Quality Gates

### Checkpoint 1: Foundations (End of Phase 2)
- [ ] Core Reference parsing utilities compile with 0 warnings.
- [ ] Bounding box math and location vector projection methods verified.

### Checkpoint 2: UI & Core Integration (End of Phase 4)
- [ ] End-to-end dimensioning flow verified in architectural samples.
- [ ] Custom WPF layout complies with the brand guidelines.
- [ ] Excel/CSV exporting of dim counts or logs works if required.

---

## Risks & Mitigations

| Risk | Impact | Mitigation Strategy |
|------|--------|---------------------|
| In-place or Curved Wall Reference Failures | Medium | Catch exceptions on geometry extraction, fall back to overall length or skip problematic faces without crashing the session. |
| Complex/Multi-nested Family Reference Names | High | Support both English/Vietnamese reference plane names ("Tim", "Center", "Left", "Right") and fall back to family bounding box bounds if references are missing. |
| Document Locking/Modifying in Active View | High | Wrap all dimension creations inside unified, transactional blocks using Revit's safe transaction wrapper. |

---

## Confirmed Architectural Decisions
1. **Dimension Style (Kiểu Dim):** The UI will query and list all available `DimensionType` elements from the active document in a dropdown, letting the user select their custom dimension style.
2. **Interactive Offset (Pick Point):** Support interactive placement where the user clicks a point on-screen to position the dimension line (`UIDocument.Selection.PickPoint`), projecting the point to determine the offset.
3. **Geometry Target:** V1 will focus exclusively on straight walls (`Line` location curves) to ensure extreme performance and math reliability. Curved walls will be ignored with a skip logger.
