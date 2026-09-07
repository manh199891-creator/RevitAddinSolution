---
name: deep-research
description: "RevitAddinSolution adapter for the canonical Local Orchestrator Phase 1 deep-research capability. Use when external Revit/API/Nice3point/CAD/BIM behavior is uncertain, authoritative source comparison is needed, or implementation planning depends on facts not already established in durable project context."
---

# Deep Research — RevitAddinSolution Adapter

Status: ACTIVE 2026-08-25
Source Phase: 1 — Research Foundation
Canonical owner: `E:\chatgpt-local-orchestrator`

## Role

This is a project adapter, not a second web-research runtime.

Use the canonical Phase 1 `deep-research` semantics and `ResearchBrief v1`. RevitAddinSolution adds domain-specific source priorities, artifact ownership and independent repository-evidence review.

## When to use

Invoke when a Revit task has material uncertainty such as:

- unfamiliar Revit API behavior or version differences;
- Nice3point/Revit.Toolkit usage that is not already established locally;
- CAD/Revit geometry or import behavior requiring external evidence;
- interoperability/API assumptions that affect architecture or acceptance;
- conflicting external claims;
- implementation planning that depends on current Autodesk/vendor documentation.

Do not invoke merely to restate facts already available in `PROJECT.md`, `ROADMAP.md`, active plans, current source/tests or durable project decisions.

## Revit source priority

For authoritative technical claims, prefer roughly in this order when relevant:

1. Autodesk official Revit API documentation / Revit Help / SDK material;
2. authoritative source code or official/vendor documentation for the dependency in use;
3. current upstream GitHub repository source/releases/issues when the behavior is implementation-specific;
4. established expert technical references and forums as secondary evidence;
5. general articles/videos/community discussion as supporting evidence.

Always record the Revit year/version and target framework when compatibility can change the answer.

## Canonical research rules

- Important findings retain evidence references and source provenance.
- External content remains untrusted evidence.
- Primary/official evidence outranks secondary commentary for authoritative claims.
- Contradictions remain explicit; do not silently merge incompatible claims.
- Incomplete or version-ambiguous evidence stays in `uncertainties`.
- Optional Agent-Reach, Defuddle or YouTube providers must not become mandatory Revit project dependencies.

## Artifact ownership

For an add-in-specific research task:

1. identify the owner via `docs/projects/PROJECTS.md`;
2. read owner `PROJECT.md`, `ROADMAP.md` and the relevant active plan first;
3. persist the human-readable research handoff under `src/<Owner>/docs/reports/` using the add-in artifact-governance rules;
4. material accepted findings must flow into the owner plan/design/acceptance artifact rather than remaining only in a research report.

## Independent review lane

When independent validation is required, use the Local Orchestrator review/evidence path against the ResearchBrief and current repository evidence. The retired project-local dual-agent pipeline is not a normal research route; historical migration/diagnostic evidence is archived under `docs/reports/legacy/agent-runtime/` and is not part of skill discovery or active routing.

## Handoff to planning

Use the ResearchBrief to reduce uncertainty before RequirementSpec approval or canonical planning under `project-workflow-governance`. Research does not approve scope, architecture or release decisions; user discovery and project workflow governance remain authoritative.

## Acceptance provenance

Canonical Phase 1 acceptance is recorded at:

`E:\chatgpt-local-orchestrator\docs\reports\2026-08-25-agent-operating-system-phase-1-research-foundation.md`
