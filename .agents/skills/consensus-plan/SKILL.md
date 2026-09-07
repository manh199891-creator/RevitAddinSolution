---
name: consensus-plan
description: "RevitAddinSolution adapter for canonical Phase 2 Planner -> Architect -> Critic consensus planning. Use after RequirementSpec approval for complex, cross-module, migration, architecture-sensitive, multi-version, or high-regression-risk work before finalizing the canonical implementation plan."
---

# Consensus Plan — RevitAddinSolution Adapter

Status: ACTIVE / ADOPTED 2026-08-25
Source Phase: 2 — Requirements + Consensus Planning
Canonical owner: `E:\chatgpt-local-orchestrator`

## Role

Provide the complex-planning capability for `project-workflow-governance`. Phase 12 retired the old lifecycle wrappers; consensus output now synchronizes directly into the surviving owner-local plan/governance path.

Use consensus planning when at least one applies:

- cross-module or cross-add-in change;
- material architecture boundary change;
- destructive refactor/migration;
- multi-version Revit/.NET compatibility impact;
- external integration or unfamiliar API assumption with material design impact;
- high regression/rollback risk;
- several plausible technical approaches where trade-offs matter.

Simple/local plans may be authored directly under `project-workflow-governance` without the consensus loop.

## Entry gate

Requires an explicitly approved RequirementSpec. If external technical uncertainty remains, route to `deep-research` before consensus.

## Revit consensus sequence

`Planner -> Architect -> Critic`, bounded and durable.

Load the project-local specialist roles:

- `.agents/agents/planner.md`
- `.agents/agents/architect.md`
- `.agents/agents/critic.md`

Every approved RequirementSpec item must trace to at least one implementation task. The final Architect and Critic round must explicitly cover every requirement.

## Revit planning checks

Where relevant, consensus must cover:

- Nice3point/Revit API ownership patterns;
- transaction, ExternalEvent/main-thread, UnitUtils, and reference rules;
- Revit-year / target-framework compatibility;
- owner-local artifact paths and flat `src/Antigravity.*` structure;
- unit/integration/manual Revit acceptance strategy;
- smoke-test fixture and rollback identity for destructive work;
- Side Panel / Local Orchestrator execution handoff;
- preservation of unrelated dirty work.

## Output and ownership

`ConsensusPlan` is cognitive evidence only. When consensus-ready, synchronize the accepted content into the canonical owner-local dated plan under `src/Antigravity.<Owner>/docs/plans/` and update `ROADMAP.md` when milestone state changes.

The canonical ConsensusPlan may compile to existing Local Orchestrator `WorkflowPlan v1`; it does not execute directly and does not create a second scheduler/state database.

`CONSENSUS_READY` means ready for user approval. It does not mean the plan was approved or implementation occurred.
