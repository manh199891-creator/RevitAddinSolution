# Antigravity.Formwork.Core Expected Behavior Baseline

Status: PENDING_CAPTURE

Phase 2 creates the ownership contract only; it does not assert runtime behavior has been revalidated.

## Invariants during governance migration

- Production source behavior is unchanged.
- Namespace, assembly name and project references are unchanged.
- Existing runtime/command/integration behavior remains the reference until a project-specific smoke baseline is captured.
- Any future destructive refactor must preserve a verified rollback identity before promotion.

## Project classification

Shared/core library

Required smoke checks are defined in ../smoke-manifest.json and must be executed according to project type rather than copied blindly from another add-in.