# Vilai Viet Revit Add-in UI Design System v3 — Strict

Effective date: 2026-07-17
Canonical theme: Light
Enforcement: `tests/ui/Assert-XamlContracts.ps1`

This document is a release contract. A rule marked **MUST** is not optional and must be enforced by an automated gate or an explicit, documented exception.

## 1. Canonical ownership

- `Antigravity.Core/UI/Themes/DesignTokens.xaml` is the only shared palette and spacing source.
- `Typography.xaml`, `Controls.xaml`, and `DataControls.xaml` are the only shared style sources.
- Shared keys **MUST** start with `Vv`. Local keys **MUST** use a module prefix and must not shadow a `Vv` key.
- Every normal Window **MUST** merge the four dictionaries exactly once and use `BrandHeader`.
- Copying logo paths, brand Runs, or raw `TextBlock Text="@manhns"` into a Window is prohibited.

## 2. Resource safety

- Every `StaticResource` key **MUST** exist locally or in one of the four canonical dictionaries.
- A local `StaticResource` **MUST** be declared before first use. WPF resolves it while loading XAML and does not support forward references.
- Use `DynamicResource` for shared color tokens that legitimately need deferred lookup. Do not use it to hide an unknown key.
- Missing-resource count must be zero before build. Runtime lines 139/141/152 are not acceptable test points; the contract must catch the key first.

Reference: https://learn.microsoft.com/dotnet/desktop/wpf/systems/xaml-resources-overview

## 3. Window shell

Normal windows use three semantic areas:

1. Header: `Auto`, shared `BrandHeader`.
2. Body: `*`, owns remaining space; long content scrolls internally.
3. Footer: `Auto`, remains visible at minimum size and contains actions and status only.

- Do not use blank `*` rows, oversized fixed spacers, or negative margins to position content.
- DataGrid/ListView/TreeView **MUST** live in a star-sized row or column and stretch in both directions.
- A collapsed secondary pane must not reserve half the result area. The primary pane spans released columns.
- Footer actions must never overlap status text or scroll with the body.

## 4. Approved profiles

| Profile | Default size | Minimum size | Use |
|---|---:|---:|---|
| Compact | 360–520 × 220–420 | content-derived | Password, confirmation, short naming dialog |
| Standard Landscape | 720–900 × 520–650 | 680 × 480 | Forms and configuration workflows |
| Workbench | 900–1100 × 600–720 | 760 × 520 | DataGrid, preview, issue and modeling workflows |
| Tall Exception | 480–600 × 640–720 | 440 × 520 | Only when two-column reflow would damage the workflow |

- New normal workflows default to Standard Landscape.
- Feature-dense tools with four or more action groups **MUST** use a two- or three-column landscape workspace. A single tall column is prohibited.
- A Tall Exception requires a comment in XAML and an entry in the QA matrix.
- `PenOverlayWindow` is the only transparent/topmost shell exception currently approved.

## 5. Color and contrast

- Window/surface: `#FFFFFF`; subtle surface: `#F5F6F8`.
- Border: `#D1D5DB`; hover: `#E5F1FB`.
- Primary text: `#111827`; secondary: `#4B5563`; muted/signature: `#6B7280`.
- Action: `#007ACC`; danger/brand red: `#D8262C`; brand navy: `#1B3679`.
- Normal text contrast **MUST** be at least 4.5:1; large text and non-text boundaries at least 3:1.
- White foreground is only allowed on accent/danger surfaces, never on white or subtle surfaces.
- `#9BA3AF` and `#AAAAAA` are prohibited for readable content on white.
- Disabled state remains readable and must not be communicated by opacity alone.
- DataGrid, ListView, ComboBox, and input surfaces **MUST** use the shared light styles. Module-local dark grids or dark combo boxes are prohibited.

Reference: https://www.w3.org/TR/WCAG22/#contrast-minimum

## 6. Typography and control sizes

- Root font: Segoe UI 12 DIP.
- Header: 16 DIP SemiBold; section: 14 DIP SemiBold; supporting copy: 11 DIP minimum with wrapping.
- Business text below 11 DIP is prohibited.
- Text button: minimum 88 × 32 DIP. Input: minimum height 30 DIP.
- CheckBox/RadioButton/pointer target: minimum 24 × 24 DIP or equivalent spacing.
- Icon-only controls require a tooltip and accessible name.

Reference: https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum

## 7. Branding

- Header background is white with the canonical navy/red vector mark and divider.
- `BrandHeader.TitleText` contains the module/action name; `SubtitleText` contains a short instruction or runtime summary.
- `@manhns` is owned by `BrandHeader` and appears immediately after the module title at 12 DIP SemiBold in `#6B7280`.
- Normal windows **MUST NOT** place `BrandSignature`, raw `@manhns`, or any signature in the footer/body. The approved drawing overlay is the only local-signature exception.

## 8. Interaction preservation

- UI refactoring must not rename `x:Name`, handlers, commands, bindings, ExternalEvent requests, or transaction boundaries without an explicit behavior change.
- Every Button needs `Click`, `Command`, `IsDefault`, or `IsCancel`; stub handlers are prohibited.
- Focus must be visible. Tab order follows visual order. Escape must not trigger destructive actions.
- Errors use text plus visual state; color alone is insufficient.

## 9. Required verification

Before deployment:

1. Run the strict XAML contract and require PASS for 24 windows and 131 buttons.
2. Build the full solution with deployment disabled and require zero errors.
3. Run the full unit-test suite.
4. Test 100%, 125%, 150%, and 200% scaling in Revit.
5. Test minimum size, empty data, long data, keyboard navigation, and every command that opens a Window.
6. Deploy only with Revit closed; checksum every module DLL.

No visual change is complete from XAML parsing alone. Revit runtime QA remains a separate mandatory release step.

## 10. UI language

- All user-facing text **MUST** be English: window titles, headers, labels, buttons, tooltips, status text, validation messages, TaskDialogs, and Ribbon panel/button text.
- Internal comments, diagnostic logs, model data, imported names, and contractual export schemas are not UI copy and may retain their source language.
- UTF-8 is mandatory for XAML and C# source. Mojibake sequences are release-blocking defects.
- New visible strings must be included in the strict UI contract or an equivalent automated language gate.

## 11. Approved exception

`Antigravity.CheckFloorElevation/UI/PenOverlayWindow.xaml` may remain transparent, borderless and topmost. It must preserve its safe-area signature, keyboard escape path and drawing hit-testing behavior.
