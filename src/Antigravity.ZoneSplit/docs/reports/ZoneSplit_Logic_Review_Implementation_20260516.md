# ZoneSplit Logic Review & Implementation - 2026-05-16

## Scope

Reviewed and updated the current `Antigravity.ZoneSplit` implementation around:

- zone volume processing,
- shared parameter setup,
- command/report handling,
- test project wiring.

## Findings

### 1. `Comments` was still used as output storage

`ZoneVolumeProcessor` wrote zone and volume text into `BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS`.

This conflicted with the newer cleanup direction in `ZoneSplit_PhysicalSplit_Plan.md`, where `BIM_ZoneID` and `BIM_ZoneName` should be the source of truth.

### 2. Multi-zone counter could overcount

`MultiZoneCount` was incremented every time a better zone replaced a previous one.

That counted replacement events, not the number of elements that actually intersected more than one zone.

### 3. Length was calculated for every category

Projected length was computed even for floors, walls, and foundations.

This is only meaningful for:

- `OST_StructuralFraming`,
- `OST_StructuralColumns`.

### 4. Shared parameter setup was too coarse

`ParameterSetupService` returned early when both parameters already existed anywhere in the document.

That could miss newly selected categories that were not included in the existing binding.

### 5. Report opening could fail the whole command

`Process.Start(reportPath)` could throw depending on shell association or environment.

The command should still succeed once processing and report writing have completed.

### 6. Test project was wired to an old project path

`ZoneSplit.Tests.csproj` referenced:

```text
..\src\ZoneSplit.addin.csproj
```

That project does not exist in the current repository layout.

Some tests also referenced old classes that are no longer present in the active implementation.

## Implementation

### `Services/ZoneVolumeProcessor.cs`

Updated processor logic to:

- write only `BIM_ZoneID` and `BIM_ZoneName`,
- stop writing to `Comments`,
- calculate `MultiZoneCount` from unique zone intersections per element,
- calculate projected length only for beams and columns,
- skip missing zone source elements safely,
- validate string parameter storage before writing.

### `Services/ParameterSetupService.cs`

Updated parameter setup to:

- use selected categories, falling back to the default structural categories,
- create or reuse `BIM_ZoneID` and `BIM_ZoneName`,
- insert or reinsert bindings so existing parameters can be bound to the current category set,
- always restore the original `SharedParametersFilename`.

### `Commands/ZoneProcessCommand.cs`

Updated command behavior to:

- keep processing result successful even when opening the Markdown report fails,
- open reports through `ProcessStartInfo` with `UseShellExecute = true`,
- log report-opening failures through `Debug.WriteLine`.

### `tests/ZoneSplit.Tests.csproj`

Updated test project to:

- reference `..\Antigravity.ZoneSplit.csproj`,
- use `Antigravity.ZoneSplit.Tests` as root namespace,
- exclude obsolete tests for removed architecture classes:
  - `ZoneAssignerServiceTests.cs`,
  - `GridXStrategyTests.cs`,
  - `GridYStrategyTests.cs`.

## Verification

Build:

```powershell
dotnet build src\Antigravity.ZoneSplit\Antigravity.ZoneSplit.csproj
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Tests:

```powershell
dotnet test src\Antigravity.ZoneSplit\tests\ZoneSplit.Tests.csproj
```

Result:

```text
Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13
```

## Notes

The workspace folder used during this change did not contain a `.git` repository, so no git diff/status summary was available from `git status`.

