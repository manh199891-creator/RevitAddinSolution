# RevitAddinSolution — Logical Module Placement Map

Status: ACTIVE LOGICAL NAVIGATION MAP

This map classifies current projects for human/agent navigation and Visual Studio Solution Folders. It does **not** authorize moving production feature folders on disk. Production projects remain physically flat under `src/Antigravity.*` unless a separately reviewed semantic move is approved.

## Host

- `Antigravity.Main`

## Shared / Core

- `Antigravity.Core`
- `Antigravity.SharedParamMapper` — provisional utility classification; remains a Revit feature physically until reviewed

## Features — Modeling

- `Antigravity.ArchModeling`
- `Antigravity.AutoFoundation`
- `Antigravity.DrawBeams`
- `Antigravity.DrawColumns`
- `Antigravity.DrawFloors`
- `Antigravity.DrawWalls`
- `Antigravity.Formwork`
- `Antigravity.Formwork.Core`

## Features — Coordination

- `Antigravity.Autojoin`
- `Antigravity.CadSleevePlacer`
- `Antigravity.CadVoidPlacer`
- `Antigravity.CheckFloorElevation`
- `Antigravity.DoorClearance`
- `Antigravity.IssueManager`
- `Antigravity.WallMepClash`
- `Antigravity.ZoneSplit`

## Features — Documentation / Annotation / Data Enrichment

- `Antigravity.AutoDimWalls`
- `Antigravity.HoanThien`
- `Antigravity.LOQN1_Location` — provisional because legacy packaging/identity still needs review
- `Antigravity.TagArranger`

## Integrations — BIMLink family

- `Antigravity.BIMLink.Core`
- `Antigravity.BIMLink.Revit`
- `Antigravity.BIMLink.Etabs`

These remain physically flat for now. A future reviewed integration-family move may be justified because the projects form a real shared domain boundary, but it is not required for agent readability.

## Integrations — Hatch Patterns / AutoCAD family

- `Antigravity.HatchPatterns.Contracts`
- `Antigravity.HatchPatterns.Core`
- `Antigravity.AutoCAD.HatchBridge`

As with BIMLink, logical grouping is sufficient unless a later migration proves a physical family boundary provides concrete build/ownership benefit.

## Packaging / Installer candidates

- `Antigravity.IssueManager.Installer` — current buildable installer/support project under `src`; candidate for a future reviewed move to `packaging/`
- `Antigravity.Installer` — currently a deferred directory with no `.csproj`; do not treat as buildable until classified
- root `installer/` — existing packaging infrastructure; consolidation is a later dedicated task

## Test-only candidates

- `Antigravity.WallMepClash.Tests` — currently under `src`; candidate for a future reviewed move to repository `tests/`
- existing repository `tests/` projects remain there

## Navigation contract for agents

Logical category is secondary. Durable project ownership is always discovered through:

```text
docs/projects/PROJECTS.md
-> src/<Project>/PROJECT.md
-> src/<Project>/docs/plans/ROADMAP.md
```

An agent must not derive owner state solely from this classification file.

## Future physical-move gate

Only consider a physical move when all are true:

1. the move expresses a real semantic boundary rather than visual tidiness;
2. S0 project/reference identity is captured;
3. S1 build/test is captured;
4. all solution/project/script references are inventoried;
5. last-known-good identity is preserved;
6. production behavior, namespace, assembly and AddInId remain unchanged unless separately approved.

Logical categories can be represented by Visual Studio Solution Folders without changing disk paths.
