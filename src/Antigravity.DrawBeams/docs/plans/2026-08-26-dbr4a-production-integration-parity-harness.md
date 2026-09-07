# DBR-4A Production Integration Parity Harness

Status: PASS / CLOSED

Final authority: ChatGPT/CodexPro actual-source inspection in workflow `WF-e42bc8e2-437b-5bfa-e1b0-0ba90893ffcb` plus Local Orchestrator authoritative verification. Prior hardening evidence lineage: workflow `WF-663581e7-4c58-ef64-7df4-3c3d2307b72f`.

## Goal

Compose DBR-1 through DBR-3 pure recognition into deterministic non-production `CadBeamData` output and durable legacy-versus-new parity evidence without changing production recognition.

## Scope

In scope: a pure `BeamRecognitionPipeline`, diagnostics retaining resolved evidence/provenance/rejections, deterministic mapping, parity comparison, characterization tests, and the future activation/rollback gate.

Out of scope: switching `GetCadBeams`, `MainWindow`, or `RevitBeamBuilder`; removing `ProcessScene`; changing user-facing preview UI; smoke last-known-good promotion.

## File plan

### Task 1: Pure composition contract

**Files:** Create `Models/BeamRecognitionResult.cs`, `Services/BeamRecognitionPipeline.cs`, `tests/Antigravity.DrawBeams.Tests/BeamRecognitionPipelineTests.cs`.

**Steps:**

- [x] Add RED coverage for empty scene, paired output, finite mapping, input reorder, and diagnostic retention.
- [x] Compose existing generator and resolver without duplicating either implementation.
- [x] Map only accepted finite valid results; retain rejected/preview diagnostics separately.
- [x] Run focused pipeline tests.

### Task 2: Deterministic parity evidence

**Files:** Create `Models/BeamRecognitionParityResult.cs`, `Services/BeamRecognitionParityComparer.cs`, `tests/Antigravity.DrawBeams.Tests/BeamRecognitionParityHarnessTests.cs`.

**Steps:**

- [x] Add RED coverage for equivalent, geometry/dimension/metadata deltas, legacy/new-only, and duplicate-safe matching.
- [x] Implement canonical order-independent one-to-one comparison with bounded explanations.
- [x] Harden parity for multi-layer selection, polynomial maximum-cardinality/minimum-cost matching, spatial identity pairing, invariant durable keys, and explicit non-finite accounting.
- [x] Add legacy `ProcessScene` versus pipeline characterization fixture without blind-equality assertion.
- [x] Run focused parity tests.

### Task 3: Review handoff and DBR-4B gate

**Files:** Create this plan and `docs/design/2026-08-26-dbr4a-recognition-integration-boundary.md`; modify `docs/plans/ROADMAP.md`.

**Steps:**

- [x] Record production/preview/rollback decisions.
- [x] Define DBR-4B gate: no unexplained high-risk curated-baseline delta, documented intentional deltas, green build/tests, and explicit rollback route.
- [x] Local Orchestrator authoritative verification and ChatGPT/CodexPro final actual-source review completed; activation remains separate.

## DBR-4B activation gate

DBR-4B is NEXT / READY FOR PLANNING. It requires explained parity over the curated baseline, documentation of every intentional delta, green build/tests, and a tested explicit rollback to legacy `CadInteropService.ProcessScene(...)`. Only a separate authorized workflow may wire the pipeline into production.

## Hardened parity contract

`BeamRecognitionLayerSelection` accepts normalized `IReadOnlyList<string>` beam and text layers (while retaining the single-string constructor). Beam layers select anchor eligibility only; all scene segments remain potential partners. Text layers select text eligibility only. Empty selection means all applicable content, and matching trims whitespace and ignores case.

The comparer uses a canonical successive-shortest-augmenting-path bipartite matcher, not recursive assignment enumeration. It reaches maximum one-to-one cardinality and then minimum endpoint cost in `O(V*E*F)` with at most `min(legacy,new)` augmentations; canonical node/edge order breaks exact-cost ties deterministically. The 40-by-40 dense compatibility regression asserts full cardinality and permutation-stable output without treating a timeout as correctness.

Exact `Equivalent` endpoints use a 0.01mm tolerance. Logical pairing requires compatible dimensions, parallel direction, <=50mm lateral separation, >=200mm and >=25% projected overlap, and—away from close endpoints—strong span/midpoint continuity (>=80% overlap) plus matching metadata or close midpoint evidence. Contradictory mark/text evidence prevents distant span pairing. This retains legitimate DBR-3 unequal-overlap `GeometryChanged` comparisons while preventing adjacent/partially-overlapping same-line beams with coincidental dimensions from cross-pairing. Metadata-only changes still pair when geometry is clearly identical.

Invalid/null or non-finite rows are excluded from matching but are no longer silent: `InvalidLegacyCount`, `InvalidNewCount`, and canonical bounded per-side explanations are returned separately from the six parity categories. Durable numeric keys use invariant round-trip formatting and canonical endpoint direction.
