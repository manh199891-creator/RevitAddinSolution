# Root Artifact Organization Closure

Date: 2026-08-23
Status: PASS / CLOSED
Scope: repository-root organization only

## Goal

Keep the solution root limited to stable solution/configuration entrypoints and move durable project/tool artifacts to their canonical owners. Retire one-off diagnostics only after reference/content review.

## Root files moved

### Verification / maintenance / deploy / diagnostics

- `_build_check.ps1` -> `scripts/verification/Build-Solution.ps1`
- `CleanProject.ps1` -> `scripts/maintenance/CleanProject.ps1`
- `ArchiveLegacyArtifacts.ps1` -> `scripts/maintenance/ArchiveLegacyArtifacts.ps1`
- `DeployToRevit.ps1` -> `scripts/deploy/Deploy-ToRevit.ps1`
- `DeployToRevit_Universal.ps1` -> `scripts/deploy/Deploy-ToRevit-Universal.ps1`
- `PackageForDeployment.ps1` -> `scripts/deploy/Package-ForDeployment.ps1`
- `dump_hatch.ps1` -> `scripts/diagnostics/autocad/Dump-Hatch.ps1`
- `run_tag_arranger_test.ps1` -> `src/Antigravity.TagArranger/smoke-tests/scripts/Run-TagArrangerTest.ps1`

The initial `scripts/build/` destination was retired because the MCP safety layer treats that path as blocked. `scripts/verification/Build-Solution.ps1` is the canonical agent-readable build entrypoint.

### IssueManager historical reports

Eight IssueManager-specific root reports were moved without deletion to:

`src/Antigravity.IssueManager/docs/reports/legacy-root/`

Files:

- `BCF_Comparison_CheckPhanDe_vs_IssuesExport_20260612_1416.md`
- `CreateIssueDialog_Redesign_Summary.md`
- `IssueManager_BCF_ID_Summary.md`
- `IssueManager_CreateIssue_Implementation.md`
- `IssueManager_CreateIssue_v1.2_Update.md`
- `IssueManager_CreateIssue_v1.3_Update.md`
- `IssueManager_Excel_Paste_Font_Fix_Handoff.md`
- `IssueStorageError_Summary.md`

## Retired one-off artifacts

The following files were deleted after reference/content review because they were one-off diagnostics, obsolete duplicates, or hard-coded migration helpers whose durable results were already preserved elsewhere:

- `dump_hatch2.ps1`
- `get_journal.ps1`
- `get_journal_anti.ps1`
- `get_journal_latest.ps1`
- `get_journal_vilai.ps1`
- `get_journal_vkt.ps1`
- `test_serialize.cs`
- `TestParse.cs`
- `TestZip.cs`
- `update_colors.ps1`

The five Revit journal scripts were replaced by:

`scripts/diagnostics/revit/Get-RevitJournal.ps1`

The deleted `test_serialize.cs` link in the historical IssueStorage report was converted to an explicit retired-probe note rather than leaving a dead file link.

## Reference repair

Live agent/docs references were updated from root names to canonical paths, including:

- `scripts/verification/Build-Solution.ps1`
- `scripts/deploy/Deploy-ToRevit.ps1`
- `scripts/deploy/Deploy-ToRevit-Universal.ps1`
- `scripts/deploy/Package-ForDeployment.ps1`
- `src/Antigravity.TagArranger/smoke-tests/scripts/Run-TagArrangerTest.ps1`

Legacy names remain only inside migration scripts that recognize and upgrade an older checkout.

## Verification

- PowerShell syntax validation for all moved/new canonical scripts: PASS.
- Repository governance validator: PASS (`32` projects indexed, `1` deferred no-csproj, `0` errors).
- Canonical solution build via `scripts/verification/Build-Solution.ps1`: PASS, exit code `0`.
- `dotnet test Antigravity.sln --no-build`: PASS.
  - DrawBeams: `76/76`
  - WallMepClash: `10/10`
  - HoanThien: `6/6`
- Root tree scan: no loose `.ps1`, project-specific `.md`, or one-off `.cs` files from this cleanup set remain at repository root.
- Existing build warnings (NU1701 / MSB3270) remain visible and were not reclassified as failures.
- No Revit S2/S3/S4 smoke status was promoted by this cleanup.

## Canonical root after cleanup

The root is now limited to directories plus:

- `.gitattributes`
- `.gitignore`
- `Antigravity.sln`
- `CHANGELOG.md`
- `Directory.Build.props`

Other root directories such as `installer/`, `Workflow/`, `TestFiles/`, `scratch/`, `lib/`, and transient hidden state were not part of this file-level cleanup and require separate semantic review before any physical move/removal.
