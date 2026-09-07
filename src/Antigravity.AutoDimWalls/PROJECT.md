<!-- Managed-By: Initialize-ProjectMemory.ps1 -->
# Antigravity.AutoDimWalls

Classification: REVIT_FEATURE
Activity: PLAN_HISTORY_PRESENT_NO_ACTIVE_DECLARED
Last reviewed: 2026-08-23

## Purpose

Owns a Revit production feature and its durable engineering history.

## Agent resume entrypoints

Read in this order before planning or production edits:

1. Repository registry: docs/projects/PROJECTS.md
2. This file: src/Antigravity.AutoDimWalls/PROJECT.md
3. Project roadmap: src/Antigravity.AutoDimWalls/docs/plans/ROADMAP.md
4. Active/detailed plan linked from the roadmap, if one is explicitly active
5. Smoke contract: src/Antigravity.AutoDimWalls/smoke-tests/smoke-manifest.json when applicable
6. Last-known-good: src/Antigravity.AutoDimWalls/smoke-tests/baseline/last-known-good.json when applicable
7. Latest project report: src/Antigravity.AutoDimWalls/docs/reports/autodim_walls_implementation_result.md

## Project identity

- Project file: src/Antigravity.AutoDimWalls/Antigravity.AutoDimWalls.csproj
- Output artifact: Antigravity.AutoDimWalls.dll
- Focused test project: NONE_DECLARED
- Runtime boundary: NONE_DECLARED

### Declared entrypoints

- AutoDimCommand.cs

### Declared dependencies

- No requiredModules list declared in the smoke manifest.

## Durable plan inventory

- docs/plans/autodim_walls_plan.md

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
