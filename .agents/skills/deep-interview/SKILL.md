---
name: deep-interview
description: "RevitAddinSolution adapter for canonical Phase 2 requirements discovery. Use when a Revit/add-in task still has material uncertainty after durable project context is loaded, before finalizing the project spec or materially revising requirements."
---

# Deep Interview — RevitAddinSolution Adapter

Status: ACTIVE / ADOPTED 2026-08-25
Source Phase: 2 — Requirements + Consensus Planning
Canonical owner: `E:\chatgpt-local-orchestrator`

## Role

Provide the focused requirement-discovery capability for `project-workflow-governance`. Phase 12 retired the old `1-spec` wrapper; this skill now feeds the surviving project governance directly and does not create a second project archive.

## Before asking the user

Read in order:

1. `.agents/AGENTS.md` and project governance;
2. `docs/projects/PROJECTS.md` to identify the owner add-in;
3. owner `PROJECT.md`;
4. owner `docs/plans/ROADMAP.md`;
5. only the relevant active plan/design/acceptance/report;
6. accepted `ResearchBrief` evidence when external uncertainty was researched.

Treat those durable facts as known context. Ask only questions whose answers can materially change scope, workflow, compatibility, failure behavior, UX/performance, fixtures, acceptance, or must-remain-unchanged behavior.

## Revit-focused coverage

In addition to generic RequirementSpec fields, pay attention when relevant to:

- Revit year and target framework;
- host/document/model assumptions;
- linked/imported CAD/model behavior;
- transaction and main-thread constraints;
- units and coordinate systems;
- existing add-in workflow/UX that must remain stable;
- smoke fixture/model and last-known-good rollback identity;
- manual Revit integration acceptance that cannot run in CI.

## Approval gate

Complete coverage produces `READY_FOR_APPROVAL`, not approval. An explicitly recorded user decision is required before RequirementSpec becomes `APPROVED` and before complex consensus planning proceeds.

## Artifact ownership

RequirementSpec is a structured handoff. Render/synchronize material accepted decisions into the owner-local canonical artifacts under `src/Antigravity.<Owner>/docs/design/`, `docs/plans/`, or `docs/acceptance/` according to `addin-artifact-governance`.

Transient `.agent/context/`, `.agents/context/`, or `.ai-bridge/` mirrors are never the sole durable copy.

## Canonical implementation

Use canonical `RequirementSpec v1` and `DeepInterviewRuntime` semantics from `E:\chatgpt-local-orchestrator`. Do not fork a second interview state engine in this repository.
