# Antigravity.DrawBeams Smoke Tests

Owner add-in: `Antigravity.DrawBeams`

Purpose: provide the minimum repeatable checks and rollback metadata required before DrawBeams recognition/refactor work is promoted.

## Smoke levels

- **S0 Structural:** project file, source tree, references and assembly identity are intact.
- **S1 Build/Test:** focused DrawBeams build/tests pass.
- **S2 Revit Load:** assembly and manifest load without dependency/type-load failures.
- **S3 Command:** DrawBeams command starts and safe-cancel leaves the document unchanged.
- **S4 Critical Path:** representative AutoCAD geometry is recognized and produces the expected Revit beam family/type, BxH, centerline, level/offset/justification/mark with no duplicate beam.

## Current refactor contract

DBR-0 and DBR-1 are characterization/extraction-normalization baseline phases. DBR-2+ must keep the legacy production path available until the corresponding smoke/acceptance gate passes.

## Rollback

See `baseline/last-known-good.json`. The file records identity only; rollback binaries belong in immutable release storage outside Git.

## Generated results

Put local smoke output under `results/`. This directory is generated and must not be committed.
