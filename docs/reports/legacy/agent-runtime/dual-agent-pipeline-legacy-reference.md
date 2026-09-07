# dual-agent-pipeline — Archived Legacy Reference

Status: HISTORICAL / NOT ROUTABLE

This file preserves the original project-local `dual-agent-pipeline` legacy reference and contracts after Phase 12 consolidation and P1 post-consolidation cleanup.

## Original path: `.agents/skills/dual-agent-pipeline/LEGACY.md`

# dual-agent-pipeline — Legacy Reference

Status: LEGACY_FALLBACK / NOT_ROUTABLE
Date: 2026-08-25
Canonical workflow owner: `E:\chatgpt-local-orchestrator`

This directory is retained as historical reference for Phase 3 migration and later consolidation. It is not part of normal project capability routing.

The retained runtime/scripts remain useful as migration evidence for convergence safeguards already proven in this repository, including stale-run reconciliation, duplicate-attempt detection, bounded failure budgets, changed-hypothesis resume, no-progress detection, oscillation detection, scope/baseline guards, and evidence identity.

Phase 3 must extract/generalize those semantics into the canonical Local Orchestrator LoopContract / verified-execution capability rather than keeping a second project-local workflow authority.

Durable project artifacts remain project-local and runtime context mirrors remain transient.

---

## Original path: `.agents/skills/dual-agent-pipeline/references/mode_contract.md`

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
- Treat `.agent/context/` planning/research artifacts as transient runtime mirrors. For add-in-specific work, durable plan/design/acceptance/report/smoke artifacts must map back to the canonical project-local `src/Antigravity.<Owner>/docs/` and `smoke-tests/` tree before the corresponding phase is considered closed.
- When `evidence_policy.required_for_gate` is enabled, bind build/test/lint evidence to the current repository snapshot and reject stale output.
- Stop automated retries when the configured failure budget is exhausted; emit a root-cause handoff instead of continuing blind fixes.
- Check the failure budget before verify or Codex. Reopen it only when the task snapshot changed and the caller supplied an explicit resume hypothesis; retain previous attempts in history.
- Treat `PASS`, `SMOKE_PASS`, `FAIL`, every `BLOCKED_*`, `INFRA_FAIL`, `STALE`, `CANCELLED`, and `NOT_STARTED` as terminal for polling.
- Route code reviews by risk (`QUICK`, `STANDARD`, `DEEP`); sensitive paths always use `DEEP`.

---

## Original path: `.agents/skills/dual-agent-pipeline/references/project_profile_contract.md`

# Project Profile Contract

Each project integrated with the Dual Agent Pipeline must have a `project_profile.json` at `.agent/project_profile.json` containing standardized configuration.

## Required Fields

```json
{
  "project_id": "example",
  "source_path": "source-code",
  "stage_patterns": ["src/**", "tests/**"],
  "dual_forbidden": ["bin/**", "obj/**", ".agent/**"],
  "fixer_command": "",
  "evidence_policy": {
    "required_for_gate": true,
    "required_stages": ["build", "test", "lint"]
  },
  "failure_budget": {
    "max_failed_attempts": 3
  },
  "review_routing": {
    "quick_max_files": 3,
    "quick_max_changed_lines": 50,
    "deep_min_files": 11,
    "deep_min_changed_lines": 301,
    "sensitive_paths": ["**/auth/**", "**/security/**", "**/migrations/**"]
  },
  "release_requires": {
    "build_pass": true,
    "qa_pass": true,
    "codex_real_review_pass": true,
    "diff_hash_match": true,
    "runtime_validation_pass": false
  }
}
```

## Field Descriptions

- **`project_id`**: The stable machine name for the project (e.g., `revit`).
- **`source_path`**: The root directory for the source code relative to the project root.
- **`stage_patterns`**: The default allow-list of globs for file scoping. Used if the user omits an explicit `--allowed` boundary during `dual-init`.
- **`dual_forbidden`**: A list of globally forbidden globs (e.g., build artifacts, agent state files) that the agent must NEVER modify.
- **`fixer_command`**: The background command used to invoke an automated fixer. If left empty, the pipeline will halt and output `NEEDS_FIXER` (without claiming a fixer is running).
- **`evidence_policy`**: Enables the fresh-evidence gate and lists verification stages that must pass for the current repository snapshot. Omit it to preserve legacy project behavior.
- **`failure_budget`**: Stops repeated blind fix cycles. The default is three failed attempts, followed by `ROOT_CAUSE_HANDOFF.md` and a bug episode.
- **`review_routing`**: Routes changes to `QUICK`, `STANDARD`, or `DEEP`. Any sensitive-path match forces `DEEP`.
- **`release_requires`**: Boolean flags used by the `harness.py gate` command to enforce which checks must pass before a release is allowed. `runtime_validation_pass` can be set to false for projects lacking runtime tests.

---

## Original path: `.agents/skills/dual-agent-pipeline/references/task_scope_examples.md`

# Task Scope Examples

When mapping human-readable intents into proper scope boundaries (`--allowed` parameter) during the initialization step (`dual-init`), use the following guidelines:

## Example 1: Specific Feature in a Subdirectory

**Intent:** "Fix the BCF export issue metadata in Navisworks."
**Command Translation:**
```text
Project: navis
Task: bcf_export_fix
Allowed: src/Navisworks/BCFExport/**, tests/Navisworks/BCFExport/**
```

## Example 2: Broad Refactoring (Using Default)

**Intent:** "Refactor the entire codebase to use the new logging pattern."
**Command Translation:**
```text
Project: revit
Task: logging_refactor
Allowed: (Omitted - let it fall back to project_profile.json/stage_patterns)
```

## Example 3: Narrow File-Specific Scope

**Intent:** "Update the configuration schema to support MaxCycles."
**Command Translation:**
```text
Project: test_project
Task: schema_update
Allowed: src/config/schema.json, src/config/parser.py, tests/test_parser.py
```

## Guidelines
- **Always be as narrow as possible** to prevent scope creep.
- **Include tests** inside the allowed boundary so TDD can be enforced.
- **Do not include build artifacts** or `.agent` directories in allowed scopes.
