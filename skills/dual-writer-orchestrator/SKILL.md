---
name: dual-writer-orchestrator
description: Review a ChatGPT-authored PLAN_PACKAGE.json and deterministically produce dual-writer contract artifacts without launching workers or changing lifecycle state.
---

# Dual-writer orchestrator

## Purpose

Review a plan for a collaborative Codex/Antigravity implementation. Produce contracts that the host Python runtime can validate and own. This skill is an artifact authoring and review boundary, not a worker launcher or state authority.

## Required inputs

- `PLAN_PACKAGE.json`, including a 40-character `base_commit`.
- Explicit acceptance criteria with stable IDs and testable text.
- Repository-relative scope and any protected or Codex-only paths.
- Existing contract schemas in `schemas/`.

## Plan review process

Validate the plan package first. Reject missing base commits, missing acceptance criteria, ambiguous scope, invalid paths, and any request to change application source or the existing dual-agent pipeline. Record findings in `PLAN_REVIEW.json`; do not silently repair intent.

## Contract-seed rules

Create `CONTRACT_SEED.json` only after the plan is structurally reviewable. Preserve the plan ID, base commit, acceptance-criterion IDs, protected paths, and approval status. Shared contract files are read-only after contract-seed approval. Only the host may approve, persist, or transition state.

## Work-splitting rules

Create disjoint `WORK_SPLIT.json` ownership sets. Validate exact and parent/child overlaps case-insensitively, reject Windows or `./` aliases, absolute paths, broad ownership such as `**/*`, and all protected paths. Never assign a Codex-only integration file to Antigravity. `shared_read_only` is readable only and is not an owned path.

## Deterministic outputs

Emit, in order, `PLAN_REVIEW.json`, `CONTRACT_SEED.json`, `WORK_SPLIT.json`, `CODEX_TASK.json`, and `AGY_TASK.json`. Use stable IDs, repository-relative POSIX paths, sorted ownership arrays, and no timestamps unless supplied by the host. Render worker prompts from the templates. The skill may produce contracts but must not launch Codex or Antigravity, create worktrees, invoke either worker, push branches, or mutate lifecycle state.

## Blocked conditions

Block on invalid schema, missing base commit or acceptance criteria, ownership overlap, protected-path assignment, broad scope, worker cross-invocation, lifecycle mutation, missing ChatGPT integration authorization, or any request to modify orchestration state. Integration remains blocked until the host records explicit ChatGPT authorization.

## Final output checklist

- All seven schemas validate the five generated artifacts and worker results.
- Plan review findings are explicit and deterministic.
- Contract seed approval is represented without changing host state.
- Worker ownership is disjoint and protected paths are excluded.
- Codex and Antigravity tasks are separate and cannot invoke each other.
- No worktree or worker launcher was created.
- Integration authorization is absent unless explicitly supplied by ChatGPT.
