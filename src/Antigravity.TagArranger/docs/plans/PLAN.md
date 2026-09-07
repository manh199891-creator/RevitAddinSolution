# Plan: Revit Tag Arranger

## 1. Repository Map and Existing Tag Arranger Files
- `src/Antigravity.TagArranger/Core/AnnotationBoxExtractor.cs`
- `src/Antigravity.TagArranger/Core/BoundingBox2D.cs`
- `src/Antigravity.TagArranger/Services/AntiOverlapService.cs`
- `src/Antigravity.TagArranger/Services/AlignService.cs`
- `src/Antigravity.TagArranger/Services/AutoTagService.cs`
- `src/Antigravity.TagArranger/Models/AnnotationBox.cs`
- `src/Antigravity.TagArranger/Models/ArrangeResult.cs`

## 2. Confirmed Current Behavior
- Extracts bounding boxes using a mix of `get_BoundingBox(view)` and string length estimations.
- Uses a Multi-pass 2D Greedy algorithm with a Force-Directed fallback to resolve overlaps.
- Modifies the Revit document directly inside a single transaction.
- Hardcodes X/Y logic, which restricts the functionality to Plan views.

## 3. Mandatory Feasibility Questions
1. **Whether `IndependentTag.get_BoundingBox(view)` includes tag text, leader, elbow, tag head, hidden or invisible extents.**
   - [REQUIRES_PROBE] (Revit API typically includes leaders in the bounding box, which invalidates text-only collision detection).
2. **How to obtain or approximate the text-head-only bounding box.**
   - [CONFIRMED_FROM_CODE] Current implementation uses `TagHeadPosition` + `TEXT_SIZE` + text length estimation. Needs refinement for multiline.
3. **How bounding boxes behave for:**
   - tags without leaders: [CONFIRMED_FROM_API] `get_BoundingBox` is text-head only.
   - tags with leaders: [REQUIRES_PROBE] `get_BoundingBox` includes the leader.
   - rotated tags: [CONFIRMED_FROM_API] `get_BoundingBox` returns Axis-Aligned Bounding Box (AABB) which is larger than the rotated box.
   - multiline tags: [REQUIRES_PROBE] Current length estimation fails; height needs line-count multiplier.
   - Room Tags: [CONFIRMED_FROM_API] `RoomTag` is a separate class from `IndependentTag` and needs separate extraction logic.
   - Plan/Section/Elevation views: [CONFIRMED_FROM_API] Current implementation assumes Plan view (Z=up). Need View Right/Up projection.
4. **How paper millimetres are converted to collision clearance for a view.**
   - [CONFIRMED_FROM_API] `ClearanceInModel = ClearanceInPaper * (1ft / 304.8mm) * View.Scale`.
5. **How view-plane coordinates are defined using the view's right, up and normal directions.**
   - [CONFIRMED_FROM_API] `LocalX = (Point - Origin).DotProduct(RightDirection)`, `LocalY = (Point - Origin).DotProduct(UpDirection)`.

## 4. Phased Implementation Plan

### Phase 1: Geometry and Revit API feasibility probe
- **Scope**: Probe bounding box behavior for tags with leaders, rotated tags, multiline, and Room Tags. Build the ViewCoordinateSystem base.
- **Expected Files**: `Probes/TagBoundingBoxProbe.cs`, `Core/ViewCoordinateSystem.cs`
- **Tests**: `ViewCoordinateSystemTests.cs`
- **Acceptance Gate**: Deterministic confirmation of text-head bounds extraction and view projections.
- **Rollback Point**: Git commit prior to Phase 1 merge.

### Phase 2: Pure deterministic 2D arrangement engine with unit tests
- **Scope**: Build CandidateGenerator, CollisionEngine, CandidateScorer, ArrangementSolver in memory without Revit dependencies.
- **Expected Files**: `Engine/CandidateGenerator.cs`, `Engine/CollisionEngine.cs`, `Engine/CandidateScorer.cs`, `Engine/ArrangementSolver.cs`
- **Tests**: Comprehensive unit tests for engine components using mock 2D bounds.
- **Acceptance Gate**: 100% test pass rate proving determinism and bounded execution.
- **Rollback Point**: Revert Phase 2 engine code.

### Phase 3: Plan-view Revit integration
- **Scope**: Wire up AnnotationBoundsProvider, ClearancePolicy, ObstacleCollector, and RevitMutationService for Plan views.
- **Expected Files**: `Providers/AnnotationBoundsProvider.cs`, `Providers/ObstacleCollector.cs`, `Services/RevitMutationService.cs`
- **Tests**: Integration tests in Revit Plan views.
- **Acceptance Gate**: Tags in plan views arranged with single undo and no overlaps.
- **Rollback Point**: Revert Phase 3 integration files.

### Phase 4: Section and Elevation integration
- **Scope**: Extend the integration to support Section and Elevation views utilizing ViewCoordinateSystem.
- **Expected Files**: Update `AnnotationBoundsProvider.cs`, `RevitMutationService.cs`
- **Tests**: Integration tests in Section and Elevation views.
- **Acceptance Gate**: Tags arranged correctly regardless of view orientation.
- **Rollback Point**: Revert Phase 4 updates.

### Phase 5: Debug Mode, performance and hardening
- **Scope**: Implement DebugVisualizationService, structure unresolved tags report, and enforce performance limits.
- **Expected Files**: `Services/DebugVisualizationService.cs`, `Models/ArrangementReport.cs`
- **Tests**: Verify debug boundaries and execution budget limits.
- **Acceptance Gate**: Debug mode correctly draws bounds without altering arrangement.
- **Rollback Point**: Disable debug mode entry points.

## 5. Migration and Rollback Strategy
- **Migration**: Introduce new solver alongside `AntiOverlapService`, activated via feature flag or new Command. Remove old service after validation.
- **Rollback**: Disable feature flag, revert to `AntiOverlapService.cs`. Single Undo for all operations.

## 6. Risks and Mitigations
- **Risk**: Revit API `get_BoundingBox` includes leader lines, breaking tight packing.
  - **Mitigation**: Use TagHeadPosition and font metrics estimation instead of `get_BoundingBox`.
- **Risk**: Multiline tags have varying heights.
  - **Mitigation**: Probe `TagText` for newlines and multiply height estimation.
- **Risk**: Infinite loops in solver.
  - **Mitigation**: Strict bounded candidate lists and execution budget (max iterations).

## 7. Explicit Out-of-Scope Items
- Moving tag leaders or elbows (only the tag head moves).
- Auto-tagging un-tagged elements.
- Arranging 3D tags.
- Editing tag families or types.
