---
name: dual-agent-pipeline
description: Run the AI Software Factory dual-agent workflow for research, implementation planning, code review, or release gating. Use when a user asks to research with independent Codex validation, review a plan, run Antigravity plus Codex on code, validate a task for release, or invokes dual-agent-pipeline, dual-init, dual review, or release gate.
---

# Dual Agent Pipeline

Run the deterministic wrappers in `scripts/`. Treat reports as the source of truth; never claim Codex, a fixer, or a background process ran without a real invocation and report.

## Runtime recovery contract

- `doctor` is READY only when Antigravity returns the exact read-only inference sentinel; listing models is not sufficient.
- Writer and reviewer timeouts terminate the complete process tree before another agent may start.
- An expired `RUNNING` review is atomically reconciled to terminal `STALE` in the manifest, task context, workflow state, report, and `pipeline_status.json`.
- Plan/research snapshot identity is HEAD plus explicitly scoped source files plus approved artifacts; pipeline reports, caches, and local factory metadata are excluded.
- Repeating the same snapshot, stage, hypothesis, and evidence returns `DUPLICATE_ATTEMPT` and does not consume failure budget.
- Resume from STALE requires infrastructure recovery, a changed relevant snapshot, and one explicit falsifiable hypothesis.

## Shortcut compatibility

If another orchestrator or Antigravity slash command invokes `research-test-and-fix` or
`test-and-fix`, treat it as a thin alias to this pipeline, not as an independent workflow.

- `research-test-and-fix`: run `research -> plan -> code -> release`, stopping early only when
  a phase fails or the user explicitly limited the scope.
- `test-and-fix`: run `code -> release`, stopping early when code review is not
  `READY_FOR_RELEASE`.
- Every alias path must still run `dual_init.ps1` / `dual_run.ps1`, refresh and read
  `.agent/context/MEMORY_CONTEXT.md`, enforce `TASK_SCOPE.json`, and require Codex report
  evidence before claiming review or release.

## Required inputs

- `Project`: registered project alias.
- `TaskId`: stable lowercase machine identifier.
- `Feature`: concise human-readable objective.
- `Mode`: `research`, `plan`, `code`, or `release`.

Optional inputs: `Allowed`, `Forbidden`, `Artifacts`, `MaxCycles` (default `2`), `SkipVerify`, `FixCommand`, and `ResumeHypothesis`.

## Select a mode

- `research`: Review `.agent/context/RESEARCH.md` against the repository snapshot. Require repository evidence, source traceability, alternatives, risks, assumptions, and a recommendation. Do not run build or release gate.
- `plan`: Review `PLAN.md`, `TECHNICAL_DESIGN.md`, and `ACCEPTANCE_CRITERIA.md` against the repository. Require executable scope, dependencies, tests, rollback, decisions, and acceptance criteria. Do not run build or release gate.
- `code`: Run verify, scope guardrails, and Codex task-delta review. A verified pass means `READY_FOR_RELEASE`; it is not a release approval. With `SkipVerify`, treat a successful run only as `SMOKE_PASS` and require verification next.
- `release`: Run the complete code-review workflow and the strict release gate. A pass means `ALLOW_RELEASE`.

Omitting `Mode` preserves legacy behavior and defaults to `release`.

## Execute

1. Initialize or replace the task context:

```powershell
& "scripts\dual_init.ps1" -Project <Project> -TaskId <TaskId> -Feature <Feature> -Mode <Mode> [-Allowed <Allowed>] [-Artifacts <Artifacts>] [-Forbidden <Forbidden>] [-Force]
```

2. For `research`, complete `RESEARCH.md`. For `plan`, complete the three planning artifacts. For `code` or `release`, implement only inside `TASK_SCOPE.json`.

   `dual-init` also creates `TASK_CONTEXT.json` and an immutable task baseline. Treat its `resume_cursor`, evidence references, failure budget, and baseline as durable execution state; do not reconstruct or recapture them from chat history. Dirty files that existed at initialization are excluded only while their index/worktree fingerprint remains unchanged.

   It also refreshes Learning Guard outputs:

   - `.agent/context/MEMORY_CONTEXT.md`: compact memory for Anti and Codex to read by default.
   - `.agent/reports/LEARNING_GUARD.md`
   - `.agent/reports/LEARNING_GUARD.json`

   Read `MEMORY_CONTEXT.md` before coding, fixing, or reviewing. Do not load full `.agent/knowledge/**` by default; open full memories only when a listed prevention rule matches the active issue.

3. Run the selected mode:

```powershell
& "scripts\dual_run.ps1" -Project <Project> -TaskId <TaskId> -Feature <Feature> -Mode <Mode> [-Artifacts <Artifacts>] [-MaxCycles 2] [-SkipVerify] [-FixCommand <FixCommand>]
```

4. Inspect evidence:

```powershell
& "scripts\dual_status.ps1" -Project <Project>
& "scripts\dual_read_reports.ps1" -Project <Project>
& "scripts\dual_wait.ps1" -Project <Project> [-TimeoutSeconds 300]
```

Before enabling the Antigravity auto-fixer, diagnose both real runtimes:

```powershell
& "scripts\dual_doctor.ps1" -Project <Project>
```

The supported topology is single-writer/independent-verifier: Antigravity edits
the live worktree, while Codex reviews a read-only snapshot. A failed Codex run
creates both `FIXER_HANDOFF.md` and schema-bound `FIXER_HANDOFF.json`. Never let
both agents write the same worktree concurrently.

If the doctor reports `antigravity` as `AUTH_REQUIRED`, do not run or claim an
automatic fixer. Continue only with manual IDE edits or after sign-in. If Codex
reports `READY`, read-only review may still proceed.

Codex structured-output schemas must stay within the supported subset accepted by
the Codex/OpenAI CLI. Do not use unsupported JSON Schema keywords such as
`uniqueItems`; exact reviewed-file matching is enforced by the pipeline parser
and release gate.

## Interpret results

- Research/plan `PASS`: reviewed artifact is accepted for the next phase, not for release.
- Code `PASS` or `PASS_WITH_ADVISORIES`: `READY_FOR_RELEASE`; run `release` separately.
- Code `SMOKE_PASS`: Codex review passed without verification; verify before release.
- Release `PASS` or `PASS_WITH_ADVISORIES`: `ALLOW_RELEASE`.
- `FAIL`: revise the reviewed artifact or code using `CODEX_REVIEW.md`.
- `INFRA_FAIL` or `STALE`: fix infrastructure or rerun against a stable snapshot.
- `BLOCKED_SCOPE`: task-created changes are outside Allowed/inside Forbidden. Revert only task-owned changes or correct the original task boundary; never widen scope merely to pass.
- `BLOCKED_NO_DELTA`: no source change was created after task initialization.
- `BLOCKED_BASELINE`: a pre-existing dirty file changed or was cleaned during the task. Stop and request an explicit ownership decision.
- `BLOCKED_VERIFY`: a required build/test/lint stage failed, or release was invoked with `SkipVerify`. Codex review and release must not continue.
- `BLOCKED_HANDOFF`: the failure budget is exhausted. Read `ROOT_CAUSE_HANDOFF.md` and resume only with a new testable hypothesis or new evidence.
- `BLOCKED_NO_PROGRESS`: Hai vòng lặp liên tiếp không có tiến triển giảm số lượng lỗi blocking.
- `BLOCKED_OSCILLATION`: Cảnh báo hội tụ lỗi xoay vòng (oscillating).
- `BLOCKED_NO_FIX_DELTA`: Fixer chạy nhưng không thay đổi gì trong scope. Cần xem lại code logic.
- When the project enables `evidence_policy.required_for_gate`, `EVIDENCE_MANIFEST.json` must match the current repository snapshot and every configured required stage must pass.

If no `fixer_command` exists, stop at the handoff report and state that no fixer is running.

Never poll `review_run.json` with a custom unbounded loop. Use `dual_wait.ps1`; it exits immediately for every terminal status and returns `WAIT_TIMEOUT` after its bounded deadline. After `BLOCKED_HANDOFF`, change scoped code first, then rerun with `-ResumeHypothesis "<new falsifiable hypothesis>"`. The pipeline rejects a missing hypothesis or an unchanged task snapshot.

Read [references/mode_contract.md](references/mode_contract.md) when integrating another orchestrator. Read [references/project_profile_contract.md](references/project_profile_contract.md) when onboarding a project. Read [references/task_scope_examples.md](references/task_scope_examples.md) when translating user intent into file scope.
