# xaml-interface-quality — ACTIVE

Source Phase: 6 — Interface Quality
Status: ACTIVE / ADOPTED 2026-08-25

This folder is the Revit/WPF adapter for the canonical Local Orchestrator `interface-quality` capability.

The adapter preserves the existing Revit design system and functional XAML/code-behind contract, adds WPF/Revit-specific DPI, ResourceDictionary, focus/keyboard, binding/event, ExternalEvent and host-verification rules, and does not create a second UI framework or generic interface-quality authority.

Visual acceptance still requires observed Revit-host evidence for the affected window/scope; build/test success alone is not visual sign-off.
