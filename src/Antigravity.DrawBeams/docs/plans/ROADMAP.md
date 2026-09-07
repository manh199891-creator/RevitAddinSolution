<!-- Managed-By: Initialize-ProjectMemory.ps1 -->
# Antigravity.DrawBeams Roadmap

Status: ACTIVE_ROADMAP  
Last reviewed: 2026-08-27

## Current

DBR-0 through DBR-3 are PASS / CLOSED. DBR-4A — production integration parity harness — is PASS / CLOSED. DBR-4B — production activation + preview diagnostics — remains ACTIVE / REVIEW PENDING and blocked pending real-S4 revalidation. DBR-4C — physical beam-strip pairing — is `IMPLEMENTED / ACTUAL-SOURCE REVIEW PASS / READY_FOR_STAGED_S4` after workflow `WF-f61534f7-a6c4-8296-3fff-1daa39b1b881`: the integrated strip path is retained, unresolved physical competition fails closed unless bounded local width evidence uniquely distinguishes an already-existing edge, narrowest-width ownership is removed, and semantic ownership is bounded both longitudinally and laterally before the legacy matcher.

The latest reviewed DBR-4B staged candidate is owned by `Antigravity.DrawBeams` and was produced through Local Orchestrator workflow `WF-ded08b56-05be-59c4-b83a-03683bd69a54`. Recognition defaults to the Pipeline route through `CadInteropService.GetCadBeamRecognition` -> `RecognizeScene` -> `BeamRecognitionRouter`, while `CadInteropService.ProcessScene(...)` remains the explicit Legacy rollback.

The latest real CAD/Revit revalidation confirmed that the repaired candidate still misidentifies physical beam face pairs in dense linework, producing wrong/duplicate beam hypotheses and laterally shifted raw centerlines in representative stair/core and D5-E regions. Deep research therefore split the remaining work into DBR-4C geometry-first physical strip pairing followed by DBR-5 structural topology/junction resolution. Beam-end non-connection is not to be repaired inside DBR-4C unless a pure-recognition RED fixture proves recognition ownership.

The staged candidate has **not yet been promoted/landed as canonical production source**. S4 must pass before production promotion. Local Orchestrator worktree paths and DLLs are transient staged execution outputs and do not redefine project ownership.

## Smoke / promotion gate

- S0 structural/build evidence: PASS for the reviewed staged lineage.
- S1 focused DrawBeams verification: PASS for DBR-4C workflow `WF-f61534f7-a6c4-8296-3fff-1daa39b1b881`; final workflow evidence records 247/247 owner tests PASS and 54/54 focused DBR-4C/conflict fixtures PASS, with DrawBeams build PASS.
- S2_REVIT_LOAD: PASS on the prior reviewed DBR-4B candidate; DBR-4C candidate S2 must be re-run after staging.
- S3_COMMAND_SAFE_CANCEL: PENDING on the DBR-4C candidate.
- S4_CAD_TO_REVIT_BEAM_CRITICAL_PATH: PENDING_REVALIDATION for DBR-4C. The prior pre-DBR-4C candidate failed wrong physical pairing / duplicate hypotheses / lateral raw-centerline acceptance; re-run the same failure regions against the reviewed DBR-4C DLL.
- Last-known-good: `PENDING_REVALIDATION`; not advanced.
- Production promotion/landing: BLOCKED until DBR-4C staged S3/S4 evidence is acceptable; DBR-5 then owns support/junction endpoint convergence if non-connection remains after raw recognition passes.

For add-in-scoped S1 verification, resolve through `src/Antigravity.DrawBeams/smoke-tests/smoke-manifest.json` and its focused test project first. Do not default to `dotnet test Antigravity.sln` unless a separate solution-wide integration/release gate is explicitly requested.

## Plan inventory

- `2026-08-22-drawbeams-recognition-engine-v14.md` — authoritative multi-phase recognition roadmap.
- `2026-08-23-dbr2-candidate-evidence-scoring.md` — DBR-2 PASS / CLOSED.
- `2026-08-23-dbr3-conflict-graph-global-resolver-centerline.md` — DBR-3 PASS / CLOSED.
- `2026-08-26-dbr4a-production-integration-parity-harness.md` — DBR-4A PASS / CLOSED.
- `2026-08-26-dbr4b-production-activation-preview-diagnostics.md` — DBR-4B ACTIVE / REVIEW PENDING; production promotion blocked by S4.
- `2026-08-26-dbr4b-s4-recognition-repair.md` — DBR-4B S4 repair lineage; latest real revalidation still FAILS physical pairing/centerline acceptance.
- `2026-08-27-dbr4c-physical-beam-strip-pairing.md` — DBR-4C `IMPLEMENTED / ACTUAL-SOURCE REVIEW PASS / READY_FOR_STAGED_S4`; final reviewed workflow `WF-f61534f7-a6c4-8296-3fff-1daa39b1b881` closes the physical-pair and bounded-semantic source blockers without entering DBR-5 topology.

## Latest evidence

- DBR-3 final technical closure: workflow `WF-db098295-4d71-bec8-4ff6-3e0e062b165c`; final closure record workflow `WF-40622ddb-bb76-f7a3-1a16-6b98fcd7cd2a`.
- DBR-4A closed evidence: `../reports/DrawBeams_DBR4A_Integration_ParityHarness_20260826.md` and `../acceptance/2026-08-26-dbr4a-production-integration-parity-harness.md`.
- DBR-4B current evidence: `../reports/DrawBeams_DBR4B_Production_Activation_20260826.md` and `../acceptance/2026-08-26-dbr4b-production-activation.md`.
- Latest reviewed DBR-4B S4-repair workflow: `WF-ded08b56-05be-59c4-b83a-03683bd69a54`.
- Final reviewed DBR-4C workflow: `WF-f61534f7-a6c4-8296-3fff-1daa39b1b881`; build PASS, 54/54 focused DBR-4C/conflict fixtures PASS and final owner evidence 247/247 PASS. ChatGPT actual-source review passes the implementation for staged S3/S4; direct CodexPro rerun was blocked by the active verify-only PowerShell policy.
- Latest real-S4/deep-research evidence: `../reports/2026-08-27-dbr4b-s4-deep-research-beam-pairing-topology.md`.
- DBR-4C approved execution plan: `2026-08-27-dbr4c-physical-beam-strip-pairing.md`.
- DBR-4B is not closed; DBR-4A remains the latest closed milestone.

## Next resume action

1. Validate `src/Antigravity.DrawBeams/PROJECT_STATE.json`, then read the approved DBR-4C plan, deep-research report, and final actual-source findings for workflow `WF-f61534f7-a6c4-8296-3fff-1daa39b1b881`.
2. Stage the reviewed DBR-4C candidate DLL plus required adjacent dependencies under `src/Antigravity.DrawBeams/smoke-tests/results/WF-f61534f7-a6c4-8296-3fff-1daa39b1b881/package/`. This is a generated smoke artifact only, not canonical production promotion.
3. Run S2 load on the DBR-4C candidate if needed, then S3 safe-cancel. The correct candidate should retain the DBR-4B Pipeline diagnostics before drawing.
4. Re-run staged S4 on the previously failing stair/core and D5-E regions. Check wrong physical pair, duplicate beam, wrong BxH/Mark and lateral raw-centerline shift separately from beam-end join/topology.
5. If raw recognition and semantics pass but endpoints still do not converge, proceed to DBR-5 structural topology/junction resolution rather than widening recognition tolerances.
6. Only after required S3/S4 pass, perform final promotion review and explicitly authorize production landing and any last-known-good advancement.

## Completed / active / deferred milestones

- DBR-0 — baseline + golden fixtures — PASS / CLOSED.
- DBR-1 — CAD extraction normalization — PASS / CLOSED.
- DBR-2 — candidate generation + evidence scoring — PASS / CLOSED.
- DBR-3 — conflict graph + global resolver + projected-overlap centerline — PASS / CLOSED.
- DBR-4A — production integration parity harness — PASS / CLOSED.
- DBR-4B — production activation + preview diagnostics — ACTIVE / REVIEW PENDING; staged candidate, promotion blocked by failed S4.
- DBR-4C — physical beam-strip pairing — IMPLEMENTED / ACTUAL-SOURCE REVIEW PASS / READY_FOR_STAGED_S4; production integration is accepted for transient smoke staging, not yet for canonical promotion.
- DBR-5 — structural support/junction topology + Revit end-join verification — PENDING after DBR-4C raw recognition is accepted.
- DBR-6 — production acceptance / promotion — PENDING.

## Roadmap maintenance

This is the stable long-term index. Detailed implementation steps belong in dated plan files; closure evidence belongs in `../reports/` and `../acceptance/`. Transient worktree paths are execution evidence only and never become the durable project owner.
