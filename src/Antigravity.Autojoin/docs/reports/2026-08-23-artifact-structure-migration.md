# Antigravity.Autojoin — Artifact Structure Migration

Date: 2026-08-23  
Status: PASS / PHASE 3

- Classification: Revit production feature add-in.
- Migrated `AutoJoin_Dropdown_Foundation_Plan.md` to local `docs/plans/`.
- Migrated `AutoJoin_Refactor_Spec.md` to local `docs/design/`.
- Migrated Phase 1, Phase 2/3, Phase 4 and full refactor reports to local `docs/reports/`.
- `docs/plans/clash_control_plan.md` was inspected and deliberately retained at repository level because it spans DoorClearance, ZoneSplit and Antigravity.Main rather than belonging only to Autojoin; it was migrated from the legacy root `plans/` location.
- Smoke profile: S0/S1/S2/S3/S4; last-known-good remains `PENDING_CAPTURE`.
- Production source behavior was not changed by these ownership moves.
