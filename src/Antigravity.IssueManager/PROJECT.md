<!-- Managed-By: Initialize-ProjectMemory.ps1 -->
# Antigravity.IssueManager

Classification: REVIT_FEATURE
Activity: PLAN_HISTORY_PRESENT_NO_ACTIVE_DECLARED
Last reviewed: 2026-08-23

## Purpose

Owns a Revit production feature and its durable engineering history.

## Agent resume entrypoints

Read in this order before planning or production edits:

1. Repository registry: docs/projects/PROJECTS.md
2. This file: src/Antigravity.IssueManager/PROJECT.md
3. Project roadmap: src/Antigravity.IssueManager/docs/plans/ROADMAP.md
4. Active/detailed plan linked from the roadmap, if one is explicitly active
5. Smoke contract: src/Antigravity.IssueManager/smoke-tests/smoke-manifest.json when applicable
6. Last-known-good: src/Antigravity.IssueManager/smoke-tests/baseline/last-known-good.json when applicable
7. Latest project report: src/Antigravity.IssueManager/docs/reports/IssueManager_TrimbleBCF_ShowInModel_Summary_20260528.md

## Project identity

- Project file: src/Antigravity.IssueManager/Antigravity.IssueManager.csproj
- Output artifact: Antigravity.IssueManager.dll
- Focused test project: NONE_DECLARED
- Runtime boundary: NONE_DECLARED

### Declared entrypoints

- App.cs
- Commands/CmdOpenIssueManager.cs

### Declared dependencies

- No requiredModules list declared in the smoke manifest.

## Durable plan inventory

- docs/plans/NavisIssueManager_Migration_Plan.md
- docs/plans/Plan_CreateIssueDialog_Redesign_v1.0.md
- docs/plans/Plan_DataStorage_v1.0.md
- docs/plans/Plan_DiagnosticFix_v3.6.md
- docs/plans/Plan_ExcelFix_PenMarking_v1.0.md
- docs/plans/Plan_FixBCFTrimbleConnect_v1.0.md
- docs/plans/Plan_FixCoordinateAndDirection_v3.2.md
- docs/plans/Plan_FixNavisworksXML_v1.0.md
- docs/plans/Plan_ProductionReady_v3.7.md
- docs/plans/Plan_RefactorCameraSync_v3.0.md
- docs/plans/Plan_RefactorCameraSync_v3.1.md
- docs/plans/Plan_RevitToTrimbleConnectClipPlanes_v2.2.md
- docs/plans/Plan_SectionBox_Implementation.md
- docs/plans/Plan_SupportRevitLinks_v3.5.md
- docs/plans/Plan_URGENT_ElementFirst_v3.4.md
- docs/plans/Plan_URGENT_Fix4Bugs_v3.3.md

## Current verification state

- Smoke / last-known-good status: PENDING_CAPTURE
- A PENDING_* state is not a PASS claim.
- Manual Revit/integration acceptance requires actual evidence before promotion.

## Resume checklist

1. Read docs/plans/ROADMAP.md; do not infer current work from chat history alone.
2. Read the latest relevant plan/design/report before touching source.
3. Inspect the current worktree and protect unrelated dirty work.
4. Revalidate S0/S1 before destructive/refactor work; run higher smoke levels when required.
5. Preserve prior last-known-good identity until the replacement is actually verified.
6. Keep durable project artifacts local; runtime mirrors remain transient.

## Maintenance rule

Keep this landing page short. Update Activity, roadmap links and major constraints when durable project state changes.
