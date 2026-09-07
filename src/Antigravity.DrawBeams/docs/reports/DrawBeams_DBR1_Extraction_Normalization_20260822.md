# DBR-1 AutoCAD Extraction Boundary & Pure CadScene Normalization Report

**Date:** 2026-08-22  
**Task ID:** dbr1-supporting-normalization  
**Status:** PASS / CLOSED  
**Owner add-in:** `Antigravity.DrawBeams`  
**Canonical location:** `src/Antigravity.DrawBeams/docs/reports/`

## 1. Executive Summary

DBR-1 establishes a strict boundary between AutoCAD COM interop and pure domain logic. COM calls, dynamic dispatch, block-reference traversal and hatch boundary extraction stay inside a fail-soft extraction boundary that produces a decoupled `CadScene` DTO. `CadSceneNormalizer` then provides deterministic endpoint orientation, endpoint snapping, zero-length handling and deterministic scene sorting without losing metadata/provenance.

## 2. COM Extraction Boundary Responsibilities

`CadInteropService` is responsible for:

- closed COM lifecycle and selection-set cleanup;
- fail-soft reads for entity properties so one unreadable property/entity does not abort the scene;
- DTO isolation: no COM object/dynamic pointer leaks into pure downstream processing.

### 2.1 Supported source kinds

- `AcDbLine`: single 2D segments transformed into world space.
- `AcDbPolyline` / `AcDb2dPolyline`: split into segments while preserving parent identity, segment index and width metadata.
- `AcDbText` / `AcDbMText`: insertion point, rotation and height transformed into world space with metadata preserved.
- `AcDbHatch`: boundary segments extracted safely with parent provenance.
- `AcDbBlockReference`: recursively traversed for supported nested entities.

### 2.2 Nested-block recursion & cycle limits

- hard maximum recursion depth: `16`;
- `definitionStack` prevents definition cycles along the current traversal branch;
- unsupported/unreadable block content is skipped safely rather than aborting extraction.

### 2.3 Transformation behavior

- `CadTransform2D` models translation, rotation and X/Y scaling.
- Block placement subtracts block-definition `Origin` before applying reference scale/rotation/insertion.
- Composition follows `parent.Compose(child)` deterministically.
- Nested text insertion point, rotation direction and height are transformed through the composed block transform.
- Polyline width is scaled in the segment-normal direction.
- Nested child IDs retain the full block-instance path so repeated definitions under different instances do not collide.

## 3. Pure `CadSceneNormalizer` contract

`CadSceneNormalizer` is pure C# code outside COM loops.

1. Canonical endpoint orientation using lexicographic X/Y ordering.
2. Bounded endpoint snapping with explicit `SnapTolerance` (default 1 mm).
3. Zero/very-short segments are preserved by default rather than silently discarded.
4. Deterministic segment/text ordering.
5. Metadata and `CadEntityProvenance` preservation.
6. Idempotency: normalizing an already normalized scene produces the same result.
7. No candidate recognition, scoring, span merge or Revit placement.

## 4. Legacy `ProcessScene` comparison path

The legacy `ProcessScene` path remains operational for baseline comparison. `MainWindow`, `GetCadBeams`, `ProcessScene` and `RevitBeamBuilder` remain on the legacy production recognition/placement path after DBR-1.

## 5. Deferred DBR-2+ scope

- DBR-2: candidate generation and explicit evidence scoring.
- DBR-3: conflict graph, global resolver and projected-overlap centerline.
- DBR-4: production integration/preview diagnostics.
- DBR-5: structural support topology.
- DBR-6: full production acceptance.

## 6. Independent review findings and repair

The first workflow result reported 54/54, but ChatGPT/CodexPro review found that the authoritative DBR-0 golden characterization file had not been carried into the isolated worktree. Review also found four extraction-boundary defects:

1. AutoCAD polyline `Coordinate(index)` was read with the wrong reflection shape.
2. Block-definition base points were ignored.
3. Nested entity IDs could collide across repeated block instances.
4. Nested text rotation/height and polyline width were not transformed through composed block scale/rotation.

These issues were repaired in the same DBR-1 worktree. The 11 authoritative DBR-0 golden fixtures were restored unchanged and focused extraction tests were expanded.

## 7. Final verification

Isolated reviewed worktree:

```text
Failed:   0
Passed:  69
Skipped: 0
Total:   69
```

Coverage breakdown: 45 pre-existing DrawBeams tests + 11 DBR-0 golden characterization tests + 13 DBR-1 extraction/normalization tests.

Real `E:\Antigravity\RevitAddinSolution` working copy after reviewed landing:

```text
Failed:   0
Passed:  75
Skipped: 0
Total:   75
```

Approved Local Orchestrator baseline after final review:

- workflow: `WF-2df1573f-0eea-f27f-f892-beebf93836b4`
- verified repository HEAD: `a1b06f11af425853bd988d66e30a367ac3e99b00`
- approved snapshot: `912a14a7b52b04366d8d338aec87b07966569bcd`
- internal ref: `refs/local-orchestrator/approved/revit-addin-solution`

No DBR-2 production behavior was introduced in DBR-1.
