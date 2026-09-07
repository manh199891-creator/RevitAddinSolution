# Technical Design: Revit Tag Arranger

## 1. Data Structures
- `ViewCoordinateSystem`: Contains `Origin`, `Right`, `Up`, `Normal`, and `Scale`. Maps `XYZ` to `Vector2D`.
- `Vector2D`: Immutable 2D coordinates `(X, Y)`.
- `BoundingBox2D`: Immutable rectangle defined by `Min (Vector2D)` and `Max (Vector2D)`.
- `ArrangementCandidate`: Represents a possible position `Vector2D` for a tag, along with a `Score`.
- `ArrangementReport`: Contains a list of successfully placed tags and unresolved tags.

## 2. Module Responsibilities
- **ViewCoordinateSystem**: Converts Revit 3D XYZ geometry to deterministic 2D view coordinates and back.
- **AnnotationBoundsProvider**: Extracts text-head-only bounding boxes for tags and obstacles.
- **ClearancePolicy**: Translates UI/configuration clearance in paper mm to model units using view scale.
- **ObstacleCollector**: Queries the view for all relevant static elements (Room Tags, Dimensions, TextNotes) and builds an obstacle spatial index.
- **CandidateGenerator**: For a given tag, produces a finite, stable, ordered list of discrete candidate positions (e.g., 8-way directional grid around the origin).
- **CollisionEngine**: Performs AABB intersection checks between a candidate bounds (plus clearance) and the obstacle list.
- **CandidateScorer**: Evaluates a non-colliding candidate using deterministic tie-break rules (distance to origin, leader length, directional preference).
- **ArrangementSolver**: Orchestrates the greedy placement. Uses a bounded loop to place tags. Falls back to limited backtracking if a tag gets blocked, constrained by a strict depth budget.
- **RevitMutationService**: Commits all accepted candidate positions into the Revit document inside a single `Transaction`.
- **DebugVisualizationService**: Draws `DetailLine` or `ModelLine` representations of bounding boxes and candidates in a temporary transaction (or separate debug view) without affecting the solver's RNG/state.

## 3. View Coordinate Transformation
- **To 2D**: 
  `localX = (point - view.Origin).DotProduct(view.RightDirection)`
  `localY = (point - view.Origin).DotProduct(view.UpDirection)`
- **To 3D**:
  `point = view.Origin + localX * view.RightDirection + localY * view.UpDirection`

## 4. Bounding-Box Strategy
- Do not rely blindly on `get_BoundingBox(view)` for tags with leaders.
- Use `TagHeadPosition` as the center.
- Estimate width using `BuiltInParameter.TEXT_SIZE`, `TEXT_WIDTH_SCALE`, and string splitting (for multiline).
- Estimate height using `TEXT_SIZE * line_count`.
- For fixed obstacles (Dimensions, TextNotes), use `get_BoundingBox` as they generally represent the exact visible bounds.

## 5. Candidate Generation Algorithm
- Generate candidates in concentric rings around the initial `TagHeadPosition`.
- Use fixed angle steps (e.g., 0, 45, 90... degrees) and fixed distance increments (e.g., 2mm paper space).
- Order candidates purely by distance, then by angle (starting from 0, counter-clockwise).
- Output is a deterministic `List<Vector2D>` with a maximum size (e.g., 100 candidates).

## 6. Collision Rules
- A candidate is valid if its `BoundingBox2D` expanded by `ClearancePolicy` does not intersect any Obstacle `BoundingBox2D` or previously placed Tag `BoundingBox2D`.
- Intersection is strict 2D AABB overlap: `!(A.MaxX <= B.MinX || A.MinX >= B.MaxX || A.MaxY <= B.MinY || A.MinY >= B.MaxY)`.

## 7. Deterministic Scoring Formula
```csharp
double score = 0;
score -= DistanceToOrigin(candidate); // Penalize far displacements
score -= LeaderLength(candidate) * 0.5; // Penalize excessively long leaders
// Tie-breaker based on ElementId to ensure absolute determinism across identical runs
score -= (tag.Id.IntegerValue % 1000) * 0.000001; 
```
Sorting candidates by `score` descending ensures a stable pick.

## 8. Bounded Solver Pseudocode
```csharp
public Report Solve(List<Tag> tags, Obstacles obstacles) {
    tags.SortBy(t => t.Id.IntegerValue); // Stable order
    var placedBounds = new List<BoundingBox2D>();
    var unresolved = new List<Tag>();
    
    foreach(var tag in tags) {
        var candidates = Generator.Generate(tag);
        var bestCandidate = null;
        
        foreach(var cand in candidates) {
            if(!CollisionEngine.Collides(cand, obstacles, placedBounds)) {
                if(bestCandidate == null || Scorer.Score(cand) > Scorer.Score(bestCandidate)) {
                    bestCandidate = cand;
                }
            }
        }
        
        if(bestCandidate != null) {
            placedBounds.Add(bestCandidate.Bounds);
            tag.AssignedPosition = bestCandidate;
        } else {
            unresolved.Add(tag);
        }
    }
    return new Report(tags, unresolved);
}
```

## 9. Transaction Flow
1. Start `Transaction("Arrange Tags")`.
2. Extract models (Read-only).
3. Run Solver in memory (Read-only).
4. Iterate successfully assigned tags: set `tag.TagHeadPosition = pos`.
5. If fatal error, `Transaction.RollBack()`.
6. Else `Transaction.Commit()`.
7. Present report of `unresolved` tags to user.

## 10. Debug Mode Architecture
- Activated via config or hidden UI toggle.
- When active, the solver runs normally.
- After the main transaction commits (or inside it), `DebugVisualizationService` creates Revit `DetailLine` elements tracing the expanded bounding boxes, obstacle bounds, and rejected candidates (color-coded red/green).
- Lines are grouped into a Revit `Group` for easy manual deletion, or drawn in a temporary sub-transaction that is rolled back.

## 11. Performance Limits
- Max tags per run: 5,000.
- Max obstacles queried: Bounding box filter around the active view crop region.
- Max candidates per tag: 200.
- Time budget: 5 seconds max before forcing early exit and marking remaining tags as unresolved.
