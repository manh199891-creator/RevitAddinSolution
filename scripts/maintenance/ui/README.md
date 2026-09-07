# UI Maintenance Utilities

This directory contains explicit, opt-in repository maintenance scripts for historical XAML migrations and UI consistency repair.

These scripts are **not** part of normal build, test, deploy, or agent startup flows. Run them only from an approved UI migration plan and review the resulting XAML diff before accepting changes.

Current utilities:

- `Migrate-XamlStrictV3.ps1` — historical strict-v3 header/signature migration.
- `Repair-XamlThemeMerge.ps1` — repairs known malformed shared-theme merge cases.
- `Standardize-XamlShell.ps1` — applies shared shell/theme/minimum-size conventions across window XAML.
- `Translate-XamlUiToEnglish.ps1` — historical translation map for UI labels.

All scripts resolve the repository root from `scripts/maintenance/ui/`; pass `-RepositoryRoot` explicitly when running against another checkout.

Do not use these utilities as a substitute for the current UI spec, owner-local plan, acceptance criteria, or visual/Revit validation.
