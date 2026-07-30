# Mode Contract

## Canonical status precedence

`pipeline_status.json` is the current-status authority when its run ID matches `review_run.json`. An expired `RUNNING` review is terminalized as `STALE` before status is displayed. Status readers must never emit `STALE` together with raw `RUNNING` or `BLOCKED` fields.

Terminal recovery states are `STALE`, `INFRA_FAIL`, `NO_PROGRESS`, and `BLOCKED_HANDOFF`. `NEEDS_FIX` remains actionable and non-terminal. STALE/INFRA failures do not consume content failure budget. A duplicate attempt on an unchanged semantic snapshot does not consume budget.

Plan and research modes hash only repository HEAD, explicitly allowed source paths, and the named artifacts. Code and release modes retain the full task-delta scope contract.

| Mode | Primary evidence | Required checks | Success meaning |
|---|---|---|---|
| `research` | `RESEARCH.md` plus repository snapshot | Independent artifact review; snapshot stability | Research accepted for planning |
| `plan` | `PLAN.md`, `TECHNICAL_DESIGN.md`, `ACCEPTANCE_CRITERIA.md` plus repository snapshot | Independent artifact review; snapshot stability | Plan accepted for implementation |
| `code` | Git task delta created after initialization | Verify, guardrails, Codex diff review | `READY_FOR_RELEASE`; `SMOKE_PASS` only when verify is explicitly skipped |
| `release` | Same code evidence plus QA/build reports | Verify, guardrails, Codex diff review, release gate | `ALLOW_RELEASE` |

## Invariants

- Persist `mode` and `artifact_files` in `TASK_SCOPE.json`.
- Reject a run when the requested mode differs from the initialized scope.
- Bind every Codex verdict to task ID, run ID, snapshot hash, and exact reviewed files.
- Capture the dirty worktree at initialization. Exclude unchanged pre-existing dirt from the task delta, but block if that baseline dirt changes or disappears.
- Use one shared task-delta evaluator for guardrails, Codex review, evidence freshness, and release hash/file checks.
- Fail before Codex when required verification fails. Never allow `SkipVerify` in release mode.
- Mark the review `STALE` when repository or artifact content changes during review.
- Never treat research or plan approval as a release approval.
- Keep default mode `release` for compatibility with existing callers.
- Persist a resumable `.agent/context/TASK_CONTEXT.json` for every initialized task.
- When `evidence_policy.required_for_gate` is enabled, bind build/test/lint evidence to the current repository snapshot and reject stale output.
- Stop automated retries when the configured failure budget is exhausted; emit a root-cause handoff instead of continuing blind fixes.
- Check the failure budget before verify or Codex. Reopen it only when the task snapshot changed and the caller supplied an explicit resume hypothesis; retain previous attempts in history.
- Treat `PASS`, `SMOKE_PASS`, `FAIL`, every `BLOCKED_*`, `INFRA_FAIL`, `STALE`, `CANCELLED`, and `NOT_STARTED` as terminal for polling.
- Route code reviews by risk (`QUICK`, `STANDARD`, `DEEP`); sensitive paths always use `DEEP`.
