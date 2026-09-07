---
name: xaml-interface-quality
description: RevitAddinSolution Phase 6 adapter for canonical interface-quality. Use for WPF/XAML UI review, polish, consistency, clipping/DPI/layout issues, shared ResourceDictionary/style work, keyboard/focus states, and evidence-backed UI changes while preserving Revit host behavior, bindings, events, and the existing design system.
---

# XAML Interface Quality — Revit Adapter

Use this skill for Revit/WPF interface work after project/add-in context and the relevant product behavior are understood.

Canonical semantics come from Local Orchestrator `interface-quality`. This adapter adds Revit/WPF-specific evidence and invariants; it is not a second generic UI authority.

## Before review or implementation

1. Read `.agents/AGENTS.md`, `.agents/skill-manifest.json`, and `.agents/policies/capability-routing.md`.
2. Identify the owning add-in from `docs/projects/PROJECTS.md` and read its `PROJECT.md`, `docs/plans/ROADMAP.md`, and active plan.
3. Inspect the actual XAML, code-behind, referenced ResourceDictionaries, project target frameworks, and relevant tests.
4. Read `docs/specs/Spec_Xaml_UX_UI_Consistency_v1.0.md` as existing detailed UI audit evidence. It remains subject to its own approval/plan status; do not silently treat unresolved decisions in that document as approved product requirements.
5. Preserve the project's existing UI/design resources. Do not introduce another UI framework or parallel design-token system.

## Functional preservation contract

UI polish must preserve unless an approved requirement explicitly changes them:

- `x:Class` and constructor behavior;
- every `x:Name` used by code-behind;
- event attributes and command bindings;
- binding paths, modes, update triggers, converters, element-name relationships, and DataGrid column bindings;
- `IsDefault` / `IsCancel` behavior unless explicitly reviewed;
- modal/modeless ownership and Revit window owner behavior;
- `ExternalEvent` / `IExternalEventHandler` lifecycle;
- Revit API main-thread and Transaction boundaries;
- multi-target compatibility for supported Revit years.

Do not perform a broad MVVM rewrite during interface-quality work.

## Review modes

- `quick`: highest-traffic user path; HIGH/MEDIUM only; maximum 5 findings.
- `full`: requested window/scope across all categories; HIGH/MEDIUM/LOW; maximum 15 findings.
- Default is `full`.

## Required WPF categories

1. **Typography & text containment**
   - readable recurring text;
   - `TextWrapping` for dynamic/descriptive text;
   - intentional `TextTrimming` plus ToolTip for single-line constrained identifiers;
   - avoid relying on emoji/font fallback for critical icon geometry.

2. **Layout & DPI**
   - prefer `Grid`, `Auto`, `*`, sensible `MinWidth/MinHeight`, and bounded scrolling over fixed-size expansion;
   - resizable normal windows require usable minimum dimensions;
   - long forms keep header/actions reachable while body content scrolls;
   - preserve special overlay/canvas exemptions;
   - test in the Revit host because process DPI behavior is host-owned.

3. **Controls & interaction**
   - normal text buttons should have a practical minimum hit area (project baseline target: about 32 DIP high unless a documented compact exception exists);
   - custom templates must preserve hover, pressed, disabled, focus, and keyboard operation;
   - icon-only controls require ToolTip and `AutomationProperties.Name`;
   - validation/error meaning cannot depend on color alone.

4. **Shared resources**
   - prefer keyed semantic styles/tokens over repeated local semantic keys;
   - use referenced-assembly ResourceDictionary/pack URI patterns when shared resources are approved;
   - do not mutate `Application.Current.Resources` globally without a proven, reviewed need because Revit owns the host Application;
   - feature-specific canvas, converter, trigger, and domain visuals remain feature-owned.

5. **Action hierarchy**
   - primary action is visually clear and stable;
   - destructive actions are semantically distinct;
   - status/utility actions do not compete with primary execution;
   - footer/watermark layout must not overlap controls.

6. **Evidence & performance**
   - preserve interaction contracts before/after XAML edits;
   - avoid animation or visual effects that harm Revit responsiveness;
   - build/test evidence does not prove visual quality.

## Verification ladder

For implementation work use the strongest available evidence:

1. XML/XAML parse + source contract inspection;
2. build/BAML compilation using the project-approved build command;
3. static interaction/layout contract tests where available;
4. add-in smoke tests relevant to the owner module;
5. real Revit visual QA for affected windows, including keyboard path and supported DPI/window sizes.

Do not mark visual acceptance PASS solely from `dotnet build`.

## Visual QA baseline

For material window/layout changes, record what was actually checked. The existing project audit proposes testing 100%, 125%, 150%, 175%, and 200% DPI, default/minimum sizes, long/empty data, keyboard states, DataGrid/list extremes, and multi-monitor DPI when the environment permits. Use the active approved plan to decide the required subset and record unobserved cases honestly.

## Change strategy

- Prefer small batches by window/archetype, not a blind whole-solution XAML rewrite.
- Prototype shared ResourceDictionary/component changes on a bounded window before rollout.
- Keep special surfaces such as transparent overlays/canvas editors explicitly exempt where normal dialog-shell rules would break their purpose.
- When a UI change needs code-behind changes, list the exact behavior reason and verify it separately.

## Review output

Return:

- mode, add-in/window scope, Revit target(s), and evidence inspected;
- coverage for typography, layout/DPI, interaction, resources, action hierarchy, and evidence/performance;
- prioritized findings with exact `path:line`, before, after, reason, and verification;
- preserved interaction surface (`x:Name`, events, bindings, commands) for implementation work;
- explicit visual/manual items still unobserved;
- verdict: `PASS`, `PASS_WITH_FOLLOWUP`, or `NEEDS_CHANGES`.
