<!-- Managed-By: Initialize-ProjectMemory.ps1 -->
# Antigravity.CheckFloorElevation

Classification: REVIT_FEATURE
Activity: PLAN_HISTORY_PRESENT_NO_ACTIVE_DECLARED
Last reviewed: 2026-08-23

## Purpose

Owns a Revit production feature and its durable engineering history.

## Agent resume entrypoints

Read in this order before planning or production edits:

1. Repository registry: docs/projects/PROJECTS.md
2. This file: src/Antigravity.CheckFloorElevation/PROJECT.md
3. Project roadmap: src/Antigravity.CheckFloorElevation/docs/plans/ROADMAP.md
4. Active/detailed plan linked from the roadmap, if one is explicitly active
5. Smoke contract: src/Antigravity.CheckFloorElevation/smoke-tests/smoke-manifest.json when applicable
6. Last-known-good: src/Antigravity.CheckFloorElevation/smoke-tests/baseline/last-known-good.json when applicable
7. Latest project report: src/Antigravity.CheckFloorElevation/docs/reports/CheckFloorElevation_Implementation_20260606.md

## Project identity

- Project file: src/Antigravity.CheckFloorElevation/Antigravity.CheckFloorElevation.csproj
- Output artifact: Antigravity.CheckFloorElevation.dll
- Focused test project: NONE_DECLARED
- Runtime boundary: NONE_DECLARED

### Declared entrypoints

- CheckFloorElevationCommand.cs

### Declared dependencies

- No requiredModules list declared in the smoke manifest.

## Durable plan inventory

- docs/plans/Plan_FloorElevationChecker_v1.0.md

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
