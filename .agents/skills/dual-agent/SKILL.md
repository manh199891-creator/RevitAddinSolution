---
name: dual-agent
description: "Orchestrate Antigravity as the lead agent and OpenAI Codex as an independent reviewer through the executable dual-agent-pipeline for research, planning, coding, debugging, review, and release. Use whenever the user requests dual-agent, Anti plus Codex, independent double-checking, research or plan validation, implementation review, release approval, or asks whether Codex actually participated."
---

# Dual Agent Orchestrator

Coordinate the work and invoke `dual-agent-pipeline` for every substantive phase. Do not replace the pipeline with a simulated review or an Antigravity subagent.

## Assign roles

- Let Anti clarify, inspect, research, plan, implement, fix, and run initial validation.
- Let Codex independently challenge the exact artifact or diff through the pipeline.
- Never let both agents write concurrently to the same worktree.

## Route the request

- Route investigation, feasibility, comparison, or architectural research to `research`.
- Route specifications, implementation plans, technical designs, or acceptance criteria to `plan`.
- Route implementation, bug fixing, debugging, refactoring, or code review to `code`.
- Route shipping, deployment approval, promotion, or final acceptance to `release`.
- For lifecycle requests, run the required sequence: `research -> plan -> code -> release`. Stop at the requested phase unless the user authorized later phases.
- Treat an ambiguous change request as `code`, never as `release`.

Read [references/orchestration_contract.md](references/orchestration_contract.md) before running a multi-phase task or transitioning between modes.

## Execute each phase

1. Inspect the registered project and repository, then select `Project`, a stable `TaskId`, a concise `Feature`, the narrowest safe `Allowed` scope, and `Mode`. Verify every proposed scope path exists or is an intentional new-file boundary; never invent a glob from naming assumptions.
2. Initialize through the bundled bridge:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\dual_orchestrate.ps1" -Action init -Project <Project> -TaskId <TaskId> -Feature <Feature> -Mode <Mode> [-Allowed <Allowed>] [-Artifacts <Artifacts>]
```

3. Produce the primary evidence:
   - `research`: complete `.agent/context/RESEARCH.md` with repository evidence and sourced external facts.
   - `plan`: complete `PLAN.md`, `TECHNICAL_DESIGN.md`, and `ACCEPTANCE_CRITERIA.md`.
   - `code`: implement and test only inside `TASK_SCOPE.json`.
   - `release`: preserve the accepted code scope and refresh required verification evidence.
4. Run independent review:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\dual_orchestrate.ps1" -Action run -Project <Project> -TaskId <TaskId> -Feature <Feature> -Mode <Mode> [-MaxCycles 2]
```

5. Read status and reports:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\dual_orchestrate.ps1" -Action status -Project <Project>
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\dual_orchestrate.ps1" -Action reports -Project <Project>
```

6. If blocked, let Anti resolve the recorded findings without widening scope, rerun relevant checks, and invoke the same mode again. Do not claim a fixer is running unless a real fixer command was invoked.

## Handle dirty worktrees and scope failures

When guardrails report an unrelated dirty file:

1. Read `git status`, `GUARDRAILS_REPORT.md`, `TASK_SCOPE.json`, and the latest pipeline report.
2. Do not add the unrelated file to `Allowed`, do not use `-Force`, and do not use `SkipVerify` to bypass the failure.
3. Do not stage, commit, reset, restore, delete, or edit unrelated user work.
4. If the current pipeline cannot prove the file predates the task, stop with `BLOCKED_SCOPE`. Offer an isolated clean worktree or wait for explicit user direction.
5. Continue only after the unrelated change is safely isolated by an authorized action or the pipeline has verified baseline exclusion.

An explicit smoke test may skip verification only when the user requested a smoke test. Report `SMOKE_PASS` at most; never report `READY_FOR_RELEASE` or `ALLOW_RELEASE` from skipped verification.

## Enforce phase gates

- Research `PASS` permits planning only.
- Plan `PASS` permits implementation only.
- Code `PASS` means `READY_FOR_RELEASE`, not approval to release.
- Release `PASS` must contain `ALLOW_RELEASE`.
- `FAIL`, `INFRA_FAIL`, `STALE`, or a scope violation blocks advancement.

When changing mode for the same `TaskId`, initialize the new mode without `-Force`. The pipeline preserves the existing allowed/forbidden scope and completed context. Use `-Force` only to intentionally replace a task after verifying the target.

## Identity and evidence gate

Before saying Codex participated, require a completed pipeline invocation plus both `.agent/state/review_run.json` and `.agent/reports/CODEX_REVIEW.md`. They must agree on the real run ID, task ID, mode, snapshot hash, reviewed files, and verdict.

If no real Codex execution exists:

- Set `CODEX_STATUS: NOT_STARTED`.
- Say "Codex has not participated yet."
- Never label Anti/Gemini output as Codex output.

Never say work is running in the background without an active process or state showing `RUNNING`. Never say dual-agent is complete while Codex is `NOT_STARTED`, `RUNNING`, `INFRA_FAIL`, `STALE`, or `FAIL`.

## Report

State separately:

- `Mode` and `Pipeline status`.
- `Anti`: artifact or code produced and tests run.
- `Codex`: run evidence, findings, and verdict.
- `Gate`: next phase allowed, `READY_FOR_RELEASE`, or `ALLOW_RELEASE`.
- `Remaining`: unresolved risks, unavailable checks, and user decisions.

When blocked, include these exact fields:

```text
CODEX_STATUS: <actual status>
PIPELINE_STATUS: <actual status>
READY_FOR_RELEASE: YES|NO
```
