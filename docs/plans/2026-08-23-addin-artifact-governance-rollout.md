# RevitAddinSolution — Add-in Artifact Governance Rollout

Date: 2026-08-23  
Owner: cross-solution governance  
Status: PASS / CLOSED — Phase 1 through Phase 6 verified

## Objective

Move durable engineering artifacts to the smallest stable owner under `src/Antigravity.<Project>/`, while keeping repository-level documentation only for genuinely cross-solution architecture/build/deploy/migration concerns.

## Mandatory governance

- `.agents/AGENTS.md`
- `.agents/policies/repository-structure.md`
- `.agents/skills/addin-artifact-governance/SKILL.md`
- `docs/architecture/REPOSITORY_STRUCTURE.md`

## Closed phases

### Phase 1 — Inventory — CLOSED

Canonical evidence: `docs/reports/2026-08-23-addin-artifact-governance-inventory.md`.

### Phase 2 — Canonical skeletons — CLOSED

Canonical evidence: `docs/reports/2026-08-23-addin-artifact-governance-phase2.md`.

31 `.csproj` owners now have project-local smoke contracts; the empty `src/Antigravity.Installer/` placeholder remains deferred. Rollback states are truthful (`PENDING_CAPTURE` unless separately verified); no fake runtime PASS was introduced.

## Phase 3 — High-confidence artifact migration — PASS / CLOSED

Move only artifacts whose owner is explicit from file content/path. Preserve historical filenames where practical. Do not move ambiguous or multi-project documents.

Exact high-confidence migration set for this phase:

### Antigravity.AutoDimWalls
- `plans/autodim_walls_plan.md` -> `src/Antigravity.AutoDimWalls/docs/plans/autodim_walls_plan.md` (already moved)
- `plans/autodim_walls_implementation_result.md` -> `src/Antigravity.AutoDimWalls/docs/reports/autodim_walls_implementation_result.md`

### Antigravity.TagArranger
- `PLAN.md` -> `src/Antigravity.TagArranger/docs/plans/PLAN.md`
- `TECHNICAL_DESIGN.md` -> `src/Antigravity.TagArranger/docs/design/TECHNICAL_DESIGN.md`
- `ACCEPTANCE_CRITERIA.md` -> `src/Antigravity.TagArranger/docs/acceptance/ACCEPTANCE_CRITERIA.md`
- `docs/superpowers/plans/Plan_TagArranger_v1.0.md` -> `src/Antigravity.TagArranger/docs/plans/Plan_TagArranger_v1.0.md`

### Antigravity.CheckFloorElevation
- `docs/superpowers/plans/Plan_FloorElevationChecker_v1.0.md` -> local `docs/plans/`
- `docs/reports/CheckFloorElevation_Implementation_20260606.md` -> local `docs/reports/`

### Antigravity.WallMepClash
- `plans/wall_mep_clash_plan.md` -> local `docs/plans/`
- `docs/superpowers/plans/Plan_WallMepClash_v1.0.md` -> local `docs/plans/`

### Antigravity.Formwork
- `docs/superpowers/plans/2026-07-22-automatic-formwork-spec.md` -> local `docs/design/`
- `docs/superpowers/plans/2026-07-22-automatic-formwork-plan-correction.md` -> local `docs/plans/`

### Antigravity.ZoneSplit
- clear ZoneSplit plan files -> local `docs/plans/`
- clear ZoneSplit implementation/review/postmortem files -> local `docs/reports/`
- residual ownership review — RESOLVED in Phase 6:
  - `ZoneSplit_CodeX_Context.md` -> `src/Antigravity.ZoneSplit/docs/design/ZoneSplit_CodeX_Context.md`
  - `ZoneSplit_Core.cs` -> `src/Antigravity.ZoneSplit/docs/design/reference/ZoneSplit_Core.cs`
  - `ZoneSplit_Core.cs` is documentation/reference-only and is not compiled by the production project.

### Antigravity.Autojoin
- clear `AutoJoin_*Plan*` -> local `docs/plans/`
- `AutoJoin_Refactor_Spec.md` -> local `docs/design/`
- clear phase/implementation/full reports -> local `docs/reports/`
- `docs/plans/clash_control_plan.md` is explicitly cross-project (`DoorClearance`, `ZoneSplit`, `Main`) and remains repository-level after migration from the legacy root `plans/` location.

### Antigravity.IssueManager
Move explicit IssueManager/BCF/Navis/Trimble/camera-sync plans and reports into local `docs/plans/` or `docs/reports/`. Preserve repository-level UI-wide/build/deploy documents.

## Deferred / ambiguous

Phase 3 left these ownership decisions unresolved. They remain repository-level under the finalized tree until a later semantic review proves a smaller owner:

- `docs/plans/CODEX_RESEARCH_PLAN.md`
- `docs/plans/CODEX_RESEARCH_PLAN_CAD.md`
- `docs/plans/implementation_plan.md`
- `docs/plans/Plan_WallProfiler_IntersectionEngine_v1.0.md`
- `docs/plans/test_codex.md`
- repository-wide UI/UX plans/guidelines
- cross-project `docs/plans/clash_control_plan.md`
- cross-solution build/deploy/migration/audit reports

## Phase 3 acceptance

- migrations are rename/move operations or reference-only corrections;
- no production C# behavior changes are introduced by this phase;
- no ambiguous ownership is guessed;
- obvious references to old paths are updated after migration;
- each affected owner has a concise local migration report;
- a cross-solution rollout report lists completed/deferred ownership.

## Phase 4 — Governance enforcement verification — PASS / CLOSED

Canonical evidence: `docs/reports/2026-08-23-addin-artifact-governance-phase4.md`.

Phase 4 hardened `AGENTS.md`, repository policy, `addin-artifact-governance`, the `1-spec` / `2-plan` / `3-code` / `4-ship` lifecycle skills, and dual-agent runtime guidance. `.ai-bridge`, `.agent/context`, and `.agents/context` are now explicitly transient runtime mirrors; add-in-specific durable artifacts must synchronize to project-local ownership. Commit/push/merge/delete actions require explicit authorization. Executable governance regression now passes `5/5`; full agent-runtime regression passes `129` tests + `6` subtests.

## Phase 5 — Project-local + cross-solution reporting — PASS / CLOSED

Canonical evidence: `docs/reports/2026-08-23-addin-artifact-governance-phase5.md` and `docs/reports/2026-08-23-addin-artifact-governance-rollout.md`.

The solution registry, per-project `PROJECT.md` / `ROADMAP.md` navigation and conservative activity/LKG reporting are durable and project-owned.

## Phase 6 — Structural/build/JSON/ignore verification — PASS / CLOSED

Canonical evidence: `docs/reports/2026-08-23-addin-artifact-governance-phase6.md`.

Fresh closure verification: repository validator `32 indexed / 1 deferred / 0 errors`; agent runtime `132 tests + 6 subtests`; solution build PASS; DrawBeams focused tests `76/76`. Manual Revit S2-S4 is not claimed by this governance closure.

## Closure

The governance rollout is complete. Feature work may resume under the project-local ownership rules. DrawBeams DBR-2 still requires its reviewed continuation baseline to contain accepted DBR-0/DBR-1 plus the canonical DrawBeams-local structure before isolated implementation starts.
