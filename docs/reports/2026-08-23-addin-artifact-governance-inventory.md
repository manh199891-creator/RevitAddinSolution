# Add-in Artifact Governance — Phase 1 Inventory

Date: 2026-08-23
Status: INVENTORY COMPLETE / MIGRATION IN PROGRESS

## Scope

Inventory all project directories and `.csproj` files under `src/`, classify runtime type, and identify governance/smoke requirements before any documentation migration. Production C# behavior is out of scope.

## Project classification

| Project | Type | Minimum smoke profile | Notes |
|---|---|---|---|
| Antigravity.ArchModeling | Revit production feature add-in | S0/S1/S2/S3/S4 before promotion | Multiple Revit command classes under `Commands/` |
| Antigravity.AutoCAD.HatchBridge | Integration/adapter with AutoCAD runtime boundary | S0/S1 + AutoCAD integration smoke when executable | Not a Revit command contract |
| Antigravity.AutoDimWalls | Revit production feature add-in | S0/S1/S2/S3/S4 | `AutoDimCommand` |
| Antigravity.AutoFoundation | Revit production feature add-in | S0/S1/S2/S3/S4 | `AutoFoundationCommand` |
| Antigravity.Autojoin | Revit production feature add-in | S0/S1/S2/S3/S4 | `AutoJoinCommand`; integration test command exists |
| Antigravity.BIMLink.Core | Shared/core library | S0/S1 | Pure contracts/models/services |
| Antigravity.BIMLink.Etabs | Integration/adapter | S0/S1 + ETABS integration smoke when executable | Adapter is currently not a standalone command |
| Antigravity.BIMLink.Revit | Integration/adapter | S0/S1 + Revit integration smoke when meaningful | Extractor boundary, not a standalone command |
| Antigravity.CadSleevePlacer | Revit production feature add-in | S0/S1/S2/S3/S4 | `AppCommand` |
| Antigravity.CadVoidPlacer | Revit production feature add-in | S0/S1/S2/S3/S4 | `AppCommand` |
| Antigravity.CheckFloorElevation | Revit production feature add-in | S0/S1/S2/S3/S4 | `CheckFloorElevationCommand` |
| Antigravity.Core | Shared/core library | S0/S1 | Shared Revit/UI/services library; no fake command smoke |
| Antigravity.DoorClearance | Revit production feature add-in | S0/S1/S2/S3/S4 | `App` plus clearance/clash commands |
| Antigravity.DrawBeams | Revit production feature add-in | S0/S1/S2/S3/S4 | Reference implementation; governance structure already present |
| Antigravity.DrawColumns | Revit production feature add-in | S0/S1/S2/S3/S4 | `CreateColumnCommand` |
| Antigravity.DrawFloors | Revit production feature add-in | S0/S1/S2/S3/S4 | `CreateFloorCommand` |
| Antigravity.DrawWalls | Revit production feature add-in | S0/S1/S2/S3/S4 | `CreateWallCommand` |
| Antigravity.Formwork | Revit production feature add-in | S0/S1/S2/S3/S4 | `AutoFormworkCommand` |
| Antigravity.Formwork.Core | Shared/core library | S0/S1 | Pure model/solver layer |
| Antigravity.HatchPatterns.Contracts | Shared/contracts library | S0/S1 | No runtime command |
| Antigravity.HatchPatterns.Core | Shared/core library | S0/S1 | No runtime command |
| Antigravity.HoanThien | Revit production feature add-in | S0/S1/S2/S3/S4 | `HoanThienCommand` |
| Antigravity.Installer | Special support placeholder | DEFERRED | Directory is currently empty; no `.csproj` found |
| Antigravity.IssueManager | Revit production feature add-in | S0/S1/S2/S3/S4 | `App` + `CmdOpenIssueManager` |
| Antigravity.IssueManager.Installer | Installer/support executable | S0/S1 + installer package smoke | `Program.cs` process entrypoint; no Revit command smoke |
| Antigravity.LOQN1_Location | Revit production feature add-in / legacy packaging | S0/S1/S2/S3/S4 | `App` + `Command`; legacy DLL/manifest naming requires later review |
| Antigravity.Main | Revit host/composition root | S0/S1/S2/S3 | `App` host; ribbon/load is critical smoke |
| Antigravity.SharedParamMapper | Revit production feature add-in | S0/S1/S2/S3/S4 | `ParamMapperCommand` |
| Antigravity.TagArranger | Revit production feature add-in | S0/S1/S2/S3/S4 | `TagArrangeCommand`; root plan/design/acceptance are high-confidence owner artifacts |
| Antigravity.WallMepClash | Revit production feature add-in | S0/S1/S2/S3/S4 | `WallMepClashCommand` |
| Antigravity.WallMepClash.Tests | Test-only project | S0 + focused test execution | No Revit command smoke |
| Antigravity.ZoneSplit | Revit production feature add-in | S0/S1/S2/S3/S4 | `App`, `ZoneProcessCommand`, `ExportCommand`; owns nested `ZoneSplit.Tests.csproj` |

## High-confidence repository-level artifact ownership identified

The following families are safe to route by owner after reference review:

- TagArranger: root `PLAN.md`, `TECHNICAL_DESIGN.md`, `ACCEPTANCE_CRITERIA.md`, `docs/superpowers/plans/Plan_TagArranger_v1.0.md`.
- CheckFloorElevation: `docs/superpowers/plans/Plan_FloorElevationChecker_v1.0.md`, `docs/reports/CheckFloorElevation_Implementation_20260606.md`.
- WallMepClash: `plans/wall_mep_clash_plan.md`, `docs/superpowers/plans/Plan_WallMepClash_v1.0.md`.
- AutoDimWalls: `plans/autodim_walls_plan.md`, `plans/autodim_walls_implementation_result.md`.
- Formwork: `docs/superpowers/plans/2026-07-22-automatic-formwork-spec.md`, `docs/superpowers/plans/2026-07-22-automatic-formwork-plan-correction.md`.
- ZoneSplit: repository docs whose names start with `ZoneSplit_` and `docs/superpowers/plans/Zone4_MissingVolume_PostMortem.md`.
- Autojoin: repository docs whose names start with `AutoJoin_` and `plans/clash_control_plan.md` only if content confirms Autojoin ownership.
- IssueManager: `IssueManager_*`, `CreateIssueDialog_*`, BCF/Navis issue documents and most `Plan_*` documents concerning BCF, camera sync, issue storage or Trimble workflows.

## Ambiguous/deferred families

Do not move automatically until content/ownership is reviewed:

- `docs/superpowers/plans/CODEX_RESEARCH_PLAN.md`
- `docs/superpowers/plans/CODEX_RESEARCH_PLAN_CAD.md`
- `docs/superpowers/plans/implementation_plan.md`
- `docs/superpowers/plans/Plan_WallProfiler_IntersectionEngine_v1.0.md`
- `docs/superpowers/plans/test_codex.md`
- repository-wide UI/UX standardization plans and UI guidelines
- build/deploy/migration/audit reports that span multiple add-ins

## Safety conclusion

This inventory does not modify production C# behavior, namespace, assembly names, project references, Revit commands or runtime behavior. High-confidence migration is allowed only after canonical local structures exist; ambiguous artifacts remain in place and are surfaced in the rollout report.
