# Add-in Artifact Governance — Phase 4 Enforcement Verification

Date: 2026-08-23  
Status: PASS / CLOSED

## Scope

Verify and harden the active agent/skill/runtime governance so future add-in-specific plans, design notes, acceptance contracts, implementation/review reports and smoke evidence cannot silently drift back to repository-root or transient runtime locations.

## Enforcement review

The following active governance surfaces were inspected directly:

- `.agents/AGENTS.md`
- `.agents/policies/repository-structure.md`
- `.agents/skills/addin-artifact-governance/SKILL.md`
- `.agents/skills/1-spec/SKILL.md`
- `.agents/skills/2-plan/SKILL.md`
- `.agents/skills/3-code/SKILL.md`
- `.agents/skills/4-ship/SKILL.md`
- `.agents/skills/dual-agent/SKILL.md`
- `.agents/skills/dual-agent-pipeline/SKILL.md`
- `.agents/skills/dual-agent-pipeline/references/mode_contract.md`
- `.ai-bridge/current-plan.md`

## Findings discovered during RED verification

A new executable governance-contract test was written before the hardening edits. Its first run intentionally exposed two real enforcement gaps:

1. `2-plan` and `3-code` still described Commit as a normal automatic task step even though repository/user workflow may require reviewed uncommitted work and Approved Baseline promotion instead.
2. Governance described `.ai-bridge` as transient but did not explicitly classify `.agent/context/` and `.agents/context/` planning artifacts used by the dual-agent/runtime pipeline as transient mirrors. This left a path for generated `PLAN.md` / `TECHNICAL_DESIGN.md` / `ACCEPTANCE_CRITERIA.md` files to be mistaken for canonical project archives.

Initial test result:

```text
3 passed, 2 failed
```

The failures were governance failures, not product-source failures.

## Corrections implemented

### Canonical ownership enforcement

`addin-artifact-governance` now explicitly classifies all of these as transient runtime/handoff mirrors:

- `.ai-bridge/`
- `.agent/context/`
- `.agents/context/`

For add-in-specific work, durable plan/design/acceptance/report/smoke content must map or synchronize back to the owning project under:

```text
src/Antigravity.<Owner>/docs/
src/Antigravity.<Owner>/smoke-tests/
```

The repository-structure policy and `.agents/AGENTS.md` now state the same invariant.

### Phase skill routing

- `1-spec`: identifies owner before durable spec/design creation.
- `2-plan`: add-in plans route to owner-local `docs/plans/`; cross-solution plans alone use repository-level `docs/superpowers/plans/`.
- `3-code`: preflight loads owner-local canonical plan and smoke contract before production/refactor work.
- `4-ship`: add-in reports, acceptance and smoke/rollback evidence close inside the owner-local tree.

### Dual-agent runtime compatibility

The dual-agent skill and dual-agent-pipeline keep their generated runtime planning files for deterministic review/snapshot behavior, but those files are explicitly runtime mirrors rather than canonical project docs. Durable add-in planning/release artifacts must synchronize to project-local ownership before the phase is considered closed.

This preserves the existing runtime protocol without forcing a rewrite of `.agent/context` internals.

### Git mutation safety

The previous phase skills were also hardened so commit/push/merge/delete actions are not implied automatic behavior:

- `2-plan`: Commit step is conditional on explicit user/current execution-policy authorization.
- `3-code`: checkpoint can remain uncommitted and use reviewed baseline/worktree evidence; commit requires explicit authorization.
- `4-ship`: merge, commit, push, PR creation, branch deletion and worktree deletion require explicit authorization before the corresponding option is executed.

No unrelated dirty work was reset, cleaned, stashed, committed, pushed or tagged.

## Executable regression contract

Created:

`.agents/runtime/tests/test_artifact_governance_contract.py`

It verifies:

- authoritative governance is wired from `AGENTS.md` and repository policy;
- `1-spec` / `2-plan` / `3-code` / `4-ship` route project artifacts to owner-local paths;
- `.ai-bridge`, `.agent/context`, and `.agents/context` are transient rather than canonical;
- dual-agent runtime artifacts map back to canonical project-local ownership;
- commit/push actions are not automatic;
- final review remains actual-source/worktree based and Approved Baseline promotion remains review-gated.

Final focused result:

```text
5 passed
```

Full agent-runtime regression after all Phase 4 edits:

```text
129 passed, 6 subtests passed
```

## Residual active-skill scan

A final scan of `.agents/skills` found `docs/superpowers/plans/` only in the intended conditional rules:

- `2-plan`: cross-solution plans only;
- `3-code`: read repository-level plans only when the task is genuinely cross-solution.

Runtime `PLAN.md` references remain only where needed by the dual-agent pipeline and are now explicitly marked as transient runtime mirrors with canonical project-local synchronization requirements.

## `.ai-bridge` verification

`.ai-bridge/current-plan.md` was refreshed to point to the active cross-solution canonical rollout plan:

`docs/superpowers/plans/2026-08-23-addin-artifact-governance-rollout.md`

It now records Phase 4 as closed and points Phase 5 as the next action. This demonstrates the intended pattern: `.ai-bridge` tracks active execution state while canonical durable plans stay outside it.

## Safety conclusion

Phase 4 changes governance/agent/runtime instructions and tests only. It does not introduce feature behavior changes to Revit add-ins. Existing unrelated dirty production work remains preserved.

## Next gate

Phase 5 may now produce the project-local and cross-solution rollout reporting required for closure. DBR-2 remains paused until Phase 5 and Phase 6 finish and the final reviewed repository structure is promoted into a refreshed Approved Baseline.
