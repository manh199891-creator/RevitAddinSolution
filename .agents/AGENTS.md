# RevitAddinSolution — Agent Entry Point

Mandatory project entry point for CodexPro and authorized coding agents. Read this file before planning, implementation, review, recovery, or resume.

This repository is a downstream consumer of the Agent Operating System coordinated from `E:\chatgpt-local-orchestrator`. Ordinary project work must use this repository's local `.agents/` bootstrap, manifests and router instead of loading the full Phase 0–12 roadmap.

---

## Project Context

- Framework: Nice3point.Revit.Toolkit
- Runtime:
  - Revit 2025+: C# / .NET 8
  - Revit <=2024: C# / .NET Framework 4.8
- Tests: xUnit + RevitTestFramework
- Logging: Serilog -> `%LOCALAPPDATA%\Antigravity\Logs\`
- CI: GitHub Actions for unit tests; Revit integration remains local/manual
- Versioning: SemVer + `-r{RevitYear}`
- Model inspection MCP: `rvt-mcp`, `zfenix-revit`; prefer MCP for model queries/inspection when available

---

## Always-Read Bootstrap

Before project work, read:

1. `.agents/capability-profile.json`
2. `.agents/skill-manifest.json`
3. `.agents/policies/repository-structure.md`
4. `.agents/policies/capability-routing.md`
5. `.agents/policies/trust-and-security.md`
6. `.agents/policies/context-memory-policy.md`
7. `.agents/policies/consolidation-governance.md`
8. `.agents/skills/project-workflow-governance/SKILL.md`

Do not load every optional skill or specialist agent by default. Use the capability router and manifest state.

---

## Non-Negotiable Workflow Governance

- Local Orchestrator / Side Panel is the single normal production workflow authority.
- Phase 12 retires `1-spec`, `2-plan`, `3-code`, `4-ship`, `dual-agent`, and `dual-agent-pipeline` as active project skills. Do not reactivate their old execution entrypoints; use the active capability router and Local Orchestrator authority instead.
- Direct ChatGPT/CodexPro production editing is fallback only under `.agents/skills/project-workflow-governance/SKILL.md`.
- Preserve unrelated dirty work.
- Do not reset, clean, stash, commit, push, tag or land changes unless the current workflow/user explicitly authorizes it.
- Durable engineering knowledge belongs to the canonical owner-local project artifacts, not only chat or runtime mirrors.
- Agent reports are evidence only; closure requires inspection of actual changed source/diff and relevant verification.

---

## Mandatory Resume / Production Preflight

Before production work:

1. Read `docs/projects/PROJECTS.md` and identify the owning add-in/project.
2. Read `src/<Addin>/PROJECT.md`.
3. Read `src/<Addin>/docs/plans/ROADMAP.md`.
4. If `src/<Addin>/PROJECT_STATE.json` exists, validate it through the Phase 9 resume contract before treating historical state as exact; only `EXACT` permits exact continuation. For workflow-backed closure/synchronization, never edit this file directly: use Local Orchestrator `POST /api/workflows/:workflowId/session-closure` so source identity and verification snapshot are bound from authoritative WorkflowState.
5. Read only the active/relevant dated plan and evidence referenced by the validated owner state/roadmap.
6. Apply `.agents/skills/addin-artifact-governance/SKILL.md` for durable plan/design/acceptance/report ownership.
7. For destructive/refactor/migration work, verify `src/<Addin>/smoke-tests/` and preserve last-known-good identity.
8. Inspect actual source and relevant tests before finalizing implementation decisions.

`.ai-bridge/`, `.agent/context/`, and `.agents/context/` are transient handoff/runtime mirrors, never the sole canonical home of durable knowledge.

---

## Revit Production Invariants

- Plugin entry points follow the project's Nice3point plugin pattern: `Autodesk.Revit.Api.Plugins.AddInPlugin`, `[PluginAttribute]`, and `[AddInPlugin(AddInLocation.AddIn)]` where applicable.
- Revit/API DLL references: Copy Local = False where required by the project configuration.
- Wrap command `Execute` boundaries in appropriate exception handling.
- Transaction safety: every Revit model write must occur inside a valid `Transaction`; no model write outside transaction scope.
- Thread safety: do not call Revit API from background threads; use the Revit main thread / `ExternalEvent` / `IExternalEventHandler` as appropriate.
- Unit conversion: use `UnitUtils`; do not hardcode display/internal-unit conversions.
- Namespace pattern: `Antigravity.<Module>`.

---

## Capability Routing

`.agents/policies/capability-routing.md` is the routing authority.

- Only capabilities marked `ACTIVE` in `.agents/skill-manifest.json` may be invoked as project skills.
- Specialist-agent state is governed by `.agents/capability-profile.json`; `PLANNED` agent stubs remain target architecture only.
- Specialist agents are loaded only when an ACTIVE routed workflow requires them.
- Autopilot is ACTIVE but on-demand only: load it for explicit end-to-end autonomous coordination, never as the default path for ordinary project work.
- `xaml-interface-quality` is ACTIVE for WPF/XAML interface review/polish; it must preserve product behavior, bindings/events, Revit host semantics and the existing design system.
- `agent-security` is ACTIVE for security-sensitive MCP/tool, credential, dependency, installer/deploy, model-mutation, provenance and memory/skill-promotion review; it never grants new execution authority.
- `memory-governance` is ACTIVE for exact resume/freshness validation, repeated-failure/regression/decision retrieval and session-closure maintenance; it consumes Local Orchestrator Phase 9 and never creates a second runtime truth store.
- `experience-learning` is ACTIVE for governed post-workflow lesson mining, SkillCandidate shadow/holdout/canary evaluation, explicit promotion and rollback; it consumes Local Orchestrator Phase 10 and never auto-promotes or self-modifies canonical skills.
- `consolidation` is ACTIVE for evidence-backed duplicate/legacy cleanup only after replacement equivalence and caller/routing inventory are proven; it must preserve Revit domain safeguards and active project state.
- Shared-runtime capabilities such as workflow/scheduler/job/execution state, review/evidence, LoopContract convergence, recovery, Mission Control, code intelligence, operational-memory services, skill-registry/evolution services and external capability routing remain owned by Local Orchestrator.

---

## Agent Authority

- CODEX and ANTIGRAVITY may implement only within the assigned workflow role/scope.
- Supporting coder is not independent reviewer authority.
- Reviewer/verification roles require evidence independent from the implementation claim.
- Human-only approval/release/promotion gates remain human-only.

---

## Operational Entry Points

- Deploy: `scripts/deploy/Deploy-ToRevit.ps1`
- Plugin template: `.agents/examples/StandardAddInPlugin.cs`
- Project registry: `docs/projects/PROJECTS.md`
- Architecture graph: `scripts/architecture/Generate-ArchitectureGraph.ps1`
- Repository structure: `docs/architecture/REPOSITORY_STRUCTURE.md`
- Logical module grouping: `docs/architecture/MODULE_PLACEMENT.md`
