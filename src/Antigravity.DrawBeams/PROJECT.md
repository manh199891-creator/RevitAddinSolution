<!-- Managed-By: Initialize-ProjectMemory.ps1 -->
# Antigravity.DrawBeams

Classification: REVIT_FEATURE
Activity: ACTIVE_ROADMAP
Last reviewed: 2026-08-27

## Purpose

Owns the DrawBeams Revit production feature and its durable engineering history.

## Agent resume entrypoints

Read in this order before planning, promotion, smoke, or production edits:

1. Repository registry: `docs/projects/PROJECTS.md`
2. This file: `src/Antigravity.DrawBeams/PROJECT.md`
3. Project roadmap: `src/Antigravity.DrawBeams/docs/plans/ROADMAP.md`
4. Machine-readable resume checkpoint: `src/Antigravity.DrawBeams/PROJECT_STATE.json`
5. Active DBR-4B evidence plus the approved DBR-4C execution plan referenced by the checkpoint
6. Smoke contract: `src/Antigravity.DrawBeams/smoke-tests/smoke-manifest.json`
7. Last-known-good: `src/Antigravity.DrawBeams/smoke-tests/baseline/last-known-good.json`
8. Latest closed project report: `src/Antigravity.DrawBeams/docs/reports/DrawBeams_DBR4A_Integration_ParityHarness_20260826.md`

## Project identity

- Project file: `src/Antigravity.DrawBeams/Antigravity.DrawBeams.csproj`
- Output artifact: `Antigravity.DrawBeams.dll`
- Focused test project: `tests/Antigravity.DrawBeams.Tests/Antigravity.DrawBeams.Tests.csproj`
- Runtime owner: `E:\Antigravity\RevitAddinSolution\src\Antigravity.DrawBeams`
- Local Orchestrator isolated worktrees are transient execution locations; they do not redefine project ownership.

### Declared entrypoint

- `Antigravity.DrawBeams.CreateBeamCommand`

### Declared dependency

- `Antigravity.Core`

## Durable plan inventory

- `docs/plans/2026-08-22-drawbeams-recognition-engine-v14.md` — multi-phase recognition roadmap
- `docs/plans/2026-08-23-dbr2-candidate-evidence-scoring.md` — DBR-2 PASS / CLOSED
- `docs/plans/2026-08-23-dbr3-conflict-graph-global-resolver-centerline.md` — DBR-3 PASS / CLOSED
- `docs/plans/2026-08-26-dbr4a-production-integration-parity-harness.md` — DBR-4A PASS / CLOSED
- `docs/plans/2026-08-26-dbr4b-production-activation-preview-diagnostics.md` — DBR-4B ACTIVE / REVIEW PENDING; S4 BLOCKED
- `docs/plans/2026-08-26-dbr4b-s4-recognition-repair.md` — S4 repair lineage; latest real revalidation still FAILS pairing/centerline acceptance
- `docs/plans/2026-08-27-dbr4c-physical-beam-strip-pairing.md` — DBR-4C IMPLEMENTATION REVIEW FAILED / REPAIR REQUIRED

## Current reviewed state

- DBR-0 through DBR-3: PASS / CLOSED.
- DBR-4A: PASS / CLOSED.
- DBR-4B: ACTIVE / REVIEW PENDING / S4 BLOCKED pending real-CAD revalidation.
- DBR-4C physical beam-strip pairing: IMPLEMENTED / ACTUAL-SOURCE REVIEW PASS / READY_FOR_STAGED_S4.
- Final repair workflow `WF-f61534f7-a6c4-8296-3fff-1daa39b1b881` removes narrowest-width-as-owner behavior, adds bounded text tie evidence only after geometry edges exist, and enforces independent longitudinal and lateral semantic locality before the legacy text matcher.
- ChatGPT actual-source review confirms the executable production path remains `BeamCandidateGenerator -> BeamFaceAdjacencyGraph -> BeamStripResolver -> BeamStripSemanticMatcher -> BeamCandidate -> DBR-3`; broad normal anchor×all-segment pairing remains removed; disjoint shared-face strips survive Pipeline; overlapping local claims still conflict; unresolved physical ambiguity fails closed; and source IDs/input order do not decide the physical centerline.
- The repaired lateral semantic contract is explicit: inclusive 500 mm longitudinal endpoint margin plus a centerline-based lateral corridor `clamp(strip width, 500 mm, 1000 mm)` applied before `BeamTextMatcher` can observe the text. Same-span multi-metre remote text is rejected.
- Final workflow evidence records DrawBeams build PASS, 54/54 focused DBR-4C/conflict fixtures PASS and 247/247 owner tests PASS after supporting boundary-test additions. Direct CodexPro rerun remains blocked by the current verify-only PowerShell policy, so the workflow's observed verification is retained as the executable test evidence.
- Latest reviewed DBR-4B candidate evidence remains Local Orchestrator workflow `WF-ded08b56-05be-59c4-b83a-03683bd69a54`; Pipeline remains the staged default behind explicit Legacy `ProcessScene` rollback.
- S2 Revit load: PASS on the prior reviewed DBR-4B candidate.
- S3 command safe-cancel: PENDING for the DBR-4C staged candidate.
- Prior real S4: FAIL on the pre-DBR-4C candidate; DBR-4C current S4 status is PENDING_REVALIDATION on the same stair/core and D5-E failure regions.
- Deep-research decision remains geometry-first DBR-4C before DBR-5 support/junction topology; do not continue broad tolerance tuning.
- Beam-end non-connection remains DBR-5 topology scope unless DBR-4C pure-recognition evidence proves otherwise.
- Smoke / last-known-good: PENDING_REVALIDATION; baseline is not advanced.
- The DBR-4C DLL may now be staged only under owner-local `smoke-tests/results/WF-f61534f7-a6c4-8296-3fff-1daa39b1b881/package/` for manual S3/S4; staging is not canonical production promotion.

## Resume checklist

1. Validate `PROJECT_STATE.json`; do not infer manual S4 completion from workflow `COMPLETED` or package PASS alone.
2. Read `docs/plans/ROADMAP.md`, the approved DBR-4C plan, deep-research report, and final actual-source findings for `WF-f61534f7-a6c4-8296-3fff-1daa39b1b881`.
3. Stage the reviewed DBR-4C candidate DLL/dependencies under `smoke-tests/results/WF-f61534f7-a6c4-8296-3fff-1daa39b1b881/package/`; do not promote canonical source.
4. Run S3 safe-cancel, then real-CAD S4 against the previously failing stair/core and D5-E regions, checking wrong pair, duplicate beam, wrong BxH/Mark and lateral centerline shift.
5. If raw axes and semantics are correct but endpoints still do not meet, proceed to DBR-5 topology rather than retuning recognition.
6. Only after required S3/S4 pass, perform final promotion review and explicitly authorize canonical landing/LKG advancement.
7. Keep plan/design/acceptance/report artifacts under this add-in's `docs/` tree and smoke artifacts under `smoke-tests/`.

## Maintenance rule

Keep this landing page short. Update milestone, evidence, next action, and promotion state only when durable project knowledge changes.
