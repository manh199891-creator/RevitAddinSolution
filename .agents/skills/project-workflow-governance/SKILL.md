---
name: project-workflow-governance
description: "Project-wide execution and planning governance for RevitAddinSolution. Prefer Side Panel / ChatGPT Local Orchestrator for implementation and require user discovery before finalizing plans."
---

# Project Workflow Governance

**Scope:** mandatory across `RevitAddinSolution` for ChatGPT, CodexPro, Codex, Antigravity and supporting agents.

## 1. Default implementation lane — Side Panel / ChatGPT Local Orchestrator

For feature implementation, refactor, bug fixing, migration, or other production-source changes, always prefer the repository's Side Panel / ChatGPT Local Orchestrator workflow as the default execution lane.

Default flow:

`ChatGPT discussion -> canonical plan -> Side Panel / Local Orchestrator -> isolated workflow/worktree -> CODEX/ANTIGRAVITY implementation -> result returned to ChatGPT -> actual source/diff verification -> acceptance/promotion`

Rules:

- Do not silently bypass the Side Panel by directly editing production source from ChatGPT/CodexPro when the Local Orchestrator lane is available.
- Use the Local Orchestrator's isolated worktree / Approved Baseline mechanism when supported.
- Agent reports are evidence only; final review must inspect the actual changed source/diff and rerun relevant verification.
- Do not auto-land, commit, push, tag, reset, clean or stash unless the current workflow contract or user explicitly authorizes it.
- Preserve unrelated dirty work.
- Direct CodexPro implementation is a fallback, not the default. Use it only when the user explicitly requests direct implementation or when the Side Panel / Local Orchestrator cannot perform the task and the user authorizes the fallback.
- Planning, inspection, review, diagnostics and read-only verification may still be performed directly when appropriate; this rule governs the default lane for source-changing implementation.

## 2. Mandatory user-discovery gate before finalizing a plan

Whenever creating a new implementation plan or materially revising an existing plan, consult the user before treating the plan as canonical/final.

The user is the product/domain authority for this repository. Do not optimize a plan solely from code assumptions when user intent can materially change behavior or acceptance.

Before finalizing the plan, ask focused questions or run a short requirements survey covering the relevant items, such as:

- desired end-user workflow and expected behavior;
- current pain points / failure cases;
- must-have vs optional behavior;
- UI/UX expectations when applicable;
- constraints on compatibility, Revit versions, AutoCAD input, performance or deployment;
- representative real-world examples / fixtures;
- acceptable trade-offs;
- explicit acceptance criteria and definition of done;
- anything that must remain unchanged.

Questioning rules:

- Ask only questions that can materially improve the plan; do not create ceremonial questionnaires.
- Prefer a compact, structured set of high-value questions over a long interview.
- Use existing durable project context first so the user is not asked to repeat facts already known from `PROJECT.md`, `ROADMAP.md`, active plans, reports or acceptance artifacts.
- If the user already supplied enough answers in the current discussion, summarize the interpreted requirements and ask only about genuine gaps.
- Do not write the canonical plan file until this discovery gate is satisfied, unless the user explicitly says to skip consultation and proceed with best judgment.
- Record material user decisions in the canonical owner-local plan/design/acceptance artifacts rather than leaving them only in chat history.

## 3. Consolidated lifecycle gates

The old `1-spec -> 2-plan -> 3-code -> 4-ship` wrappers are retired by Phase 12. Their unique safeguards live here and route to accepted specialist capabilities instead of maintaining a second lifecycle framework.

### Requirements / spec

- Read durable owner context before asking the user to repeat known facts.
- If material requirements remain unclear, route to `deep-interview`; if uncertainty depends on external Revit/API/domain facts, use `deep-research` first.
- RequirementSpec or equivalent durable owner requirement material stops at `READY_FOR_APPROVAL`; only explicit user approval makes it approved.
- Do not implement production code while material requirements/approval are unresolved.
- For UI/UX work, route interface-specific quality review to `xaml-interface-quality` while preserving the existing design system and behavior.

### Planning

- Simple/local work may be planned directly under owner-local artifact governance.
- Complex/cross-module/migration/destructive/high-regression work routes through `consensus-plan` after requirements are approved.
- Every implementation plan identifies exact files/responsibilities, explicit IN/OUT scope, test strategy, rollback/acceptance and must-remain-unchanged behavior.
- Prefer RED -> GREEN -> REFACTOR task slices when behavior is testable; do not add ceremonial placeholders/TODO-only tasks.
- A plan becomes canonical only after the user-discovery/approval gate required by this skill.

### Implementation / debug / repair

- Production-source implementation uses Local Orchestrator / Side Panel as the normal execution authority.
- Default engineering discipline is RED -> GREEN -> REFACTOR where a meaningful automated regression is possible.
- For defects, establish root cause/hypothesis and evidence before broad fixes; do not repeatedly mutate source without a falsifiable reason.
- If a second evidence-backed implementation/repair attempt is required, route to `verified-execution`; `LoopContract`/`ConvergenceRuntime` are the only active retry/convergence authority.
- Stop at `NO_PROGRESS`, `OSCILLATING`, `BLOCKED_INFRA`, `BUDGET_EXHAUSTED` or `NEEDS_HUMAN`; do not hide these states by increasing retry/timeout budgets.
- Agent success prose is not review evidence. Inspect actual changed source/diff and run the required project-scoped verification.

### Acceptance / ship / rollback

- Do not claim completion while required verification is failing. Record real command output and distinguish code failure from infrastructure/live-fixture exceptions.
- Security-sensitive release/deploy/model-write/dependency/MCP/credential changes route through `agent-security`; security review does not grant release authority.
- Apply `addin-artifact-governance` for owner-local report/acceptance/smoke/rollback evidence and durable `PROJECT.md`/`ROADMAP.md` state.
- Commit, merge, push, PR creation, branch/worktree deletion, deployment and release remain explicit user/current-workflow actions; none are automatic consequences of PASS.
- Rollback must preserve exact accepted predecessor/baseline identity and required evidence.

## 4. Interaction with project artifact governance

This skill complements, and does not replace:

- `.agents/skills/addin-artifact-governance/SKILL.md`
- `.agents/policies/repository-structure.md`
- `docs/architecture/REPOSITORY_STRUCTURE.md`

Artifact ownership remains project-local. For one add-in, durable plans/design/acceptance/reports must remain under `src/Antigravity.<Owner>/docs/...`.

## 5. Preflight checklist

Before finalizing a plan:

- Owner project identified? YES/NO
- Durable project context read? YES/NO
- User discovery completed or explicitly waived by user? YES/NO
- Material user decisions reflected in acceptance/plan? YES/NO

Before production-source implementation:

- Canonical plan approved? YES/NO
- Side Panel / Local Orchestrator chosen as default execution lane? YES/NO
- If not, explicit user-authorized fallback recorded? YES/NO
- Isolated/approved baseline mechanism established when applicable? YES/NO
- Unrelated dirty work protected? YES/NO

If a required answer is NO, resolve it before destructive or production-changing implementation.
