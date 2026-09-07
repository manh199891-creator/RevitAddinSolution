# Acceptance Criteria: Revit Tag Arranger

## 1. Tag-Tag Clearance
- **Scenario**: Two tags are placed near each other.
- **Criteria**: The closest edges of their text-head bounding boxes must be separated by exactly or more than the configured clearance.

## 2. Tag-Obstacle Clearance
- **Scenario**: A tag is placed near a Room Tag, Dimension, or TextNote.
- **Criteria**: The tag's text-head must not overlap the bounding box of the obstacle, maintaining the configured minimum clearance.

## 3. Paper Millimetre Conversion
- **Scenario**: View scale is changed (e.g., 1:100 to 1:50) and arrangement is rerun.
- **Criteria**: The physical distance on the printed sheet between elements matches the configured paper millimetres, mathematically verified by `Clearance * View.Scale`.

## 4. Plan / Section / Elevation
- **Scenario**: Tags are arranged in a Plan view, a Section view, and an Elevation view.
- **Criteria**: The arrangement resolves overlaps correctly on the local 2D screen plane in all three view types without shifting tags out of the view plane (Z-depth remains unchanged).

## 5. Deterministic Output
- **Scenario**: The Arrange command is run multiple times on the same view state.
- **Criteria**: Every run produces the exact same final positions for all tags. No random jitter or alternative placements occur.

## 6. Single Undo
- **Scenario**: The user executes the arrangement, then presses Ctrl+Z (Undo).
- **Criteria**: All arranged tags return to their exact original positions in a single Undo step. The undo history shows only one transaction named "Arrange Tags".

## 7. Rollback
- **Scenario**: An unhandled exception occurs during the assignment of the 50th tag out of 100.
- **Criteria**: The transaction rolls back cleanly. No tags are partially moved, and the document state is identical to before the command was run.

## 8. Unresolved Tags
- **Scenario**: A tag is completely surrounded by obstacles and no valid candidate exists within the maximum radius.
- **Criteria**: The tag remains in its original position. The system generates a structured report listing this tag's ElementId and warning message.

## 9. Debug Mode
- **Scenario**: Debug Mode is enabled and Arrange is executed.
- **Criteria**: Bounding boxes of obstacles and clearance radii are visibly drawn in the view. The final positions of the tags perfectly match a non-debug run.

## 10. Performance
- **Scenario**: 1,000 tags are selected for arrangement in a heavily populated view.
- **Criteria**: The arrangement engine completes calculation and Revit mutation within 5.0 seconds.

## 11. Regression Safety
- **Scenario**: The new deterministic Tag Arranger is deployed.
- **Criteria**: Existing `AntiOverlapService` usages elsewhere (if any) are not broken. Unit tests for the new `CandidateGenerator` and `CollisionEngine` pass 100% on the CI pipeline.
