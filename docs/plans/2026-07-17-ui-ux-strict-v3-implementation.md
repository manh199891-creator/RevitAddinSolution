# UI/UX Strict v3 — Implementation Plan

Execution mode: Inline, sequential checkpoints
Approved spec: `docs/plans/2026-07-17-ui-ux-strict-v3-spec.md`

### Task 1: Turn observed failures into release-gate tests
**Files:** Modify `tests/ui/Assert-XamlContracts.ps1`; Test all XAML under `src`
**Steps:**
- [ ] Write failing checks for unresolved/forward `StaticResource`, white-on-light styles, negative signature positioning, duplicate/manual brand markup, and invalid Header/Body/Footer grid ownership (RED).
- [ ] Run `tests/ui/Assert-XamlContracts.ps1` and verify it fails on the current 29 missing resources and photographed layout violations.
- [ ] Keep existing XML, theme, signature count, button-handler and stub-handler checks intact.
- [ ] Commit the failing contract checkpoint.

### Task 2: Repair P0 XAML resource failures
**Files:** Modify `src/Antigravity.AutoDimWalls/UI/AutoDimWindow.xaml`; `src/Antigravity.Autojoin/UI/MainWindow.xaml`; `src/Antigravity.TagArranger/UI/ArrangerWindow.xaml`; `src/Antigravity.WallMepClash/UI/WallMepClashDialog.xaml`; `src/Antigravity.ZoneSplit/UI/MainWindow.xaml`; `src/Antigravity.Core/UI/Themes/DesignTokens.xaml`; `src/Antigravity.Core/UI/Themes/Typography.xaml`; `src/Antigravity.Core/UI/Themes/Controls.xaml`; `src/Antigravity.Core/UI/Themes/DataControls.xaml`; Test `tests/ui/Assert-XamlContracts.ps1`
**Steps:**
- [ ] Use the failing resource graph as the RED baseline.
- [ ] Replace orphaned local keys with canonical `Vv*` resources or define module-prefixed local resources before first use (GREEN).
- [ ] Use `DynamicResource` only for token lookups that legitimately require deferred resolution; keep stable style inheritance as resolved `StaticResource`.
- [ ] Run the contract and verify zero unresolved resources.
- [ ] Build the five affected projects with deployment disabled.
- [ ] Commit the P0 runtime repair.

### Task 3: Enforce one canonical shell and branding implementation
**Files:** Modify `src/Antigravity.Core/UI/Controls/BrandHeader.xaml`; `src/Antigravity.Core/UI/Controls/BrandHeader.xaml.cs`; `src/Antigravity.Core/UI/Controls/BrandSignature.xaml`; `src/Antigravity.Core/UI/Controls/BrandSignature.xaml.cs`; `src/Antigravity.Core/UI/Themes/DesignTokens.xaml`; `src/Antigravity.Core/UI/Themes/Typography.xaml`; `src/Antigravity.Core/UI/Themes/Controls.xaml`; Modify the 24 Window XAML files listed in Task 5; Test `tests/ui/Assert-XamlContracts.ps1`
**Steps:**
- [ ] Add failing contract checks that reject duplicated manual logo/header and raw `TextBlock Text="@manhns"` in normal windows (RED).
- [ ] Make `BrandHeader` and `BrandSignature` the only normal-window branding implementation.
- [ ] Standardize root as `Header(Auto) / Body(*) / Footer(Auto)`, with body scrolling internally and signature in a dedicated footer column.
- [ ] Run contract and verify canonical shell/branding passes (GREEN).
- [ ] Commit the shared shell checkpoint.

### Task 4: Fix photographed layout/readability regressions
**Files:** Modify `src/Antigravity.DoorClearance/UI/ClearanceBoxWindow.xaml`; `src/Antigravity.DoorClearance/UI/ClashControlWindow.xaml`; `src/Antigravity.HoanThien/UI/HoanThienWindow.xaml`; `src/Antigravity.ArchModeling/UI/ArchModelingWindow.xaml`; Test `tests/ui/Assert-XamlContracts.ps1`
**Steps:**
- [ ] Add failing assertions for retired dark foreground styles, unused star rows, fixed result columns, overlapping footer content and negative margins (RED).
- [ ] Refactor ClearanceBox to landscape responsive sections with readable `Vv*` light-theme styles.
- [ ] Keep ClashControl workbench body and DataGrid in a star-sized region; place signature in footer.
- [ ] Move HoanThien TabControl into the star body and actions/signature into the actual footer.
- [ ] Change ArchModeling result pane/table to star sizing and remove dead whitespace.
- [ ] Run strict contract and verify the four photographed cases pass (GREEN).
- [ ] Commit the photographed-regression fixes.

### Task 5: Synchronize all remaining Windows
**Files:** Modify `src/Antigravity.Core/UI/PasswordWindow.xaml`; `src/Antigravity.CadSleevePlacer/UI/SleevePlacerWindow.xaml`; `src/Antigravity.CadVoidPlacer/UI/MainWindow.xaml`; `src/Antigravity.CheckFloorElevation/UI/FloorCheckerDialog.xaml`; `src/Antigravity.CheckFloorElevation/UI/PenOverlayWindow.xaml`; `src/Antigravity.DrawBeams/UI/MainWindow.xaml`; `src/Antigravity.DrawColumns/UI/MainWindow.xaml`; `src/Antigravity.DrawFloors/UI/HatchMappingWindow.xaml`; `src/Antigravity.DrawFloors/UI/MainWindow.xaml`; `src/Antigravity.DrawWalls/UI/MainWindow.xaml`; `src/Antigravity.IssueManager/UI/CreateIssueDialog.xaml`; `src/Antigravity.IssueManager/UI/ExportIssueSelectionDialog.xaml`; `src/Antigravity.IssueManager/UI/FolderNameDialog.xaml`; `src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml`; `src/Antigravity.IssueManager/UI/MarkupEditorWindow.xaml`; plus the nine normal windows modified in Tasks 2–4; Test `tests/ui/Assert-XamlContracts.ps1`
**Steps:**
- [ ] Run strict contract and record every remaining window violation (RED).
- [ ] Apply Standard Landscape or Workbench sizing; keep Compact only for password/folder dialogs and allowlist PenOverlay.
- [ ] Replace hard-coded common palette/style values with canonical tokens and controls.
- [ ] Verify long text wraps, controls have minimum sizes, body owns the star row, and footer remains visible at MinWidth/MinHeight.
- [ ] Run strict contract for all 24 windows and verify PASS (GREEN).
- [ ] Commit the complete synchronization checkpoint.

### Task 6: Publish guideline v3 and regression checklist
**Files:** Create `docs/ui/VilaiViet_UI_Guidelines_v3.md`; Modify `docs/ui/VilaiViet_UI_Guidelines_v2.md`; Modify `docs/qa/Xaml_Visual_QA_Matrix.md`; Modify `CHANGELOG.md`; Test `tests/ui/Assert-XamlContracts.ps1`
**Steps:**
- [ ] Add documentation consistency checks/version reference checks (RED).
- [ ] Document strict tokens, allowed profiles, resource rules, grid contract, contrast, scaling, exceptions and review checklist (GREEN).
- [ ] Add Revit QA cases for 100/125/150/200%, empty/long data, keyboard focus and every window-load command.
- [ ] Run strict contract and verify guideline/release-gate consistency.
- [ ] Commit the guideline v3 checkpoint.

### Task 7: Full verification, deployment and checksum audit
**Files:** Modify `scripts/deploy/Deploy-ToRevit.ps1` only if verification exposes packaging gaps; Test `Antigravity.sln`, `tests/ui/Assert-XamlContracts.ps1`, deployed Revit 2024 Addins folder
**Steps:**
- [ ] Run strict XAML contract.
- [ ] Run `dotnet build Antigravity.sln --no-restore -p:DeployToRevitAddins=false -v:minimal` and require zero errors.
- [ ] Run `dotnet test Antigravity.sln --no-build -p:DeployToRevitAddins=false -v:minimal` and require all tests pass.
- [ ] Run dependency vulnerability scan and focused sensitive-data/transaction audit.
- [ ] Ensure Revit is closed, deploy with `scripts/deploy/Deploy-ToRevit.ps1 -NoPause`, and checksum all module DLLs.
- [ ] Commit any packaging-only correction, then report actual Revit manual QA items still requiring user interaction.
