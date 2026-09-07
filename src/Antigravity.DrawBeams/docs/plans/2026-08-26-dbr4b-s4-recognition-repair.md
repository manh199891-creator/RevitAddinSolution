# DBR-4B S4 Recognition Repair Plan

Status: PENDING_REVALIDATION
Last durable update: 2026-08-27

## Goal

Repair deterministic recognition of legitimate adjacent CAD beam spans without changing DBR-4B routing or Revit creation semantics, then re-run the real Revit/CAD S4 gate before production promotion.

## Observed S4 failure

The first DBR-4B S4 smoke reached Revit creation but visually exposed:

- missing beam despite CAD beam geometry;
- shifted/incorrect centerline or span;
- duplicate/double beam for one physical beam region;
- beam-end non-connection, currently unproven as recognition ownership and likely DBR-5/Revit topology unless revalidation shows a recognition-stage defect.

## Repair scope

In scope: pure `CadScene` regressions for adjacent bays, partial spans, columns/openings, parallel annotation lines, repeated sizes/marks, shared alignment, candidate diagnostics, conflict resolution, fragmentation, overlapping/contained/exact-duplicate face fragments, selected-text-layer isolation, and support-gap classification.

Out of scope: Revit joins/topology, `RevitBeamBuilder`, coordinate transforms, routing architecture, Legacy rollback semantics, and LKG promotion.

## Completed automated repair lineage

- `WF-477a6953-85b0-6bb8-459a-25dcb059c95a` — first real-CAD defect repair.
- `WF-960a8be1-e508-508e-0f2d-a19ffb522d9f` — overlap/fragment safety hardening.
- `WF-ded08b56-05be-59c4-b83a-03683bd69a54` — selected-text isolation + support-gap classification hardening; latest reviewed candidate.

Implemented/verified recognition rules include:

- zero-gap spans remain separate support/bay boundaries;
- two faces with an aligned small support gap remain separate even with identical mark/size;
- true single-face drafting fragmentation against a continuous covering partner can reconstruct one beam;
- overlapping/contained fragments merge only with credible covering parallel-partner evidence;
- selected text layers are filtered before preprocessing;
- unrelated text outside the combined local span cannot veto fragmentation merge;
- adjacent same-axis beams remain distinct;
- input reorder remains deterministic;
- DBR-3 projected-overlap centerline and DBR-4A parity behavior remain intact.

## Current verification state

- Latest focused supporting verification: `Antigravity.DrawBeams.Tests` **191/191 PASS**.
- S2_REVIT_LOAD: PASS on staged candidate.
- S3_COMMAND_SAFE_CANCEL: PENDING.
- S4_CAD_TO_REVIT_BEAM_CRITICAL_PATH: PENDING_REVALIDATION.
- DBR-4B: ACTIVE / REVIEW PENDING.
- LKG: PENDING_REVALIDATION.

## Next action

Re-run S3 and S4 against the latest reviewed staged candidate, focusing first on the previously marked `THIẾU`, `LỆCH`, and `DOUBLE` regions. Treat beam-end non-connection separately unless recognition-stage evidence reproduces it.

Do not promote/land DBR-4B production source into canonical `E:\Antigravity\RevitAddinSolution` until S4 passes and final review explicitly authorizes promotion.
