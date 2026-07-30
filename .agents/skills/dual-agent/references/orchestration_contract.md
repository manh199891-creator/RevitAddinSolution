# Orchestration Contract

## Phase routing

| User intent | Mode | Anti output | Pipeline success permits |
|---|---|---|---|
| Research, compare, assess feasibility | `research` | `RESEARCH.md` | Start planning |
| Plan, design, specify | `plan` | Planning artifacts | Start implementation |
| Implement, fix, debug, refactor, review code | `code` | Scoped diff and tests | Start release validation |
| Release, deploy, promote, final acceptance | `release` | Build, QA, runtime, and review evidence | Release |

## Multi-phase rules

1. Reuse one `TaskId` across phases for traceability.
2. Initialize every new mode before producing or reviewing its evidence.
3. Do not use `-Force` for a normal phase transition.
4. Preserve the existing `allowed_files` and `forbidden` lists across phase transitions.
5. Stop immediately when a phase is blocked; do not manufacture later-phase reports.
6. Never convert a research/plan pass into a release pass.

## Input derivation

- Generate `TaskId` in lowercase snake case from the objective.
- Resolve `Project` from the registered AI Software Factory project alias, not merely the repository folder name.
- Inspect the repository before selecting `Allowed`; verify every existing path/glob matches the intended files and mark intentional new-file boundaries explicitly.
- Make `Allowed` as narrow as practical and include proportional tests.
- Omit `Allowed` only when the project profile is intentionally the task boundary.
- Keep `MaxCycles` at `2` unless the user explicitly chooses another value.
- Do not pass `SkipVerify` for research or plan; those modes already omit build verification by contract.
- Use `SkipVerify` for code only when the user explicitly requests a smoke test. Such a run cannot produce `READY_FOR_RELEASE`.
- Reject `SkipVerify` for release.

## Evidence requirements

Accept Codex participation only when the latest manifest and report agree on:

- task ID;
- selected mode;
- run ID;
- snapshot hash;
- reviewed files;
- final status.

For `release`, additionally require `RELEASE_GATE_REPORT.md` with `ALLOW_RELEASE`.

## Failure routing

- `FAIL`: Anti fixes the exact findings, tests, then reruns the same mode.
- `STALE`: stabilize the repository/artifact and rerun; do not fix code based on stale findings.
- `INFRA_FAIL`: repair Codex CLI, timeout, schema, or evidence collection; do not treat it as a code failure.
- `AUTH_REQUIRED`: the writer CLI is not authenticated. Do not claim an automatic fixer is running. Use manual Antigravity work in the IDE or sign in before invoking a fixer command.
- Scope failure: isolate unrelated work or correct the original boundary; never broaden scope only to pass.
- Missing fixer command: create/read `FIXER_HANDOFF.md` and state that automatic fixing did not run.

## Entrypoint rules

- The bridge must always be called with `-Action` and `-Project`.
- `init` and `run` additionally require `-TaskId` and `-Feature`.
- A command such as `dual_orchestrate.ps1 -Mode release` is invalid and must be treated as `INFRA_FAIL`, not as a running background task.
- `doctor` is the first diagnostic action when either model is suspected to be unavailable.

## Dirty worktree policy

When an out-of-scope dirty file is reported:

1. Inspect `git status`, guardrail evidence, current scope, and the latest manifest.
2. Never widen `Allowed` merely to include the unrelated file.
3. Never use `-Force` or `SkipVerify` as a recovery mechanism.
4. Never alter, stage, commit, reset, restore, delete, or move unrelated user work.
5. If the pipeline has a bound baseline and proves the file is unchanged pre-existing work, allow the pipeline to exclude it and record that exclusion.
6. If no bound baseline exists, return `BLOCKED_SCOPE` and use an isolated worktree only with user authorization.
7. If the unrelated file changed after task initialization, keep the task blocked.

Required blocked report:

```text
CODEX_STATUS: NOT_STARTED | RUNNING | FAIL | INFRA_FAIL | STALE
PIPELINE_STATUS: BLOCKED_SCOPE | BLOCKED_CODEX | BLOCKED_VERIFY | INFRA_FAIL | STALE
READY_FOR_RELEASE: NO
```

Writing code can be reported separately as implementation progress, but it is not pipeline completion.
