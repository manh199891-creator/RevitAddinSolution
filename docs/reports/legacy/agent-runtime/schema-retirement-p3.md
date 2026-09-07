# P3 Runtime Schema Retirement Audit

Date: 2026-08-29
Project: RevitAddinSolution
Status: COMPLETE

## Scope

Audit the five JSON schemas under `.agents/runtime/schemas/` after Phase 12 consolidation. The objective is to retain only schemas with a proven live or compatibility consumer and retire schemas that belong solely to the removed project-local dual-agent/review runtime.

## Method

For each schema, the audit checked:

- exact filename references across the repository;
- schema title/contract-key references;
- generic schema-loader or `jsonschema`/validator usage;
- runtime references to `TASK_SCOPE.json`, `TASK_CONTEXT.json`, `review_run.json`, `evidence_manifest.json`, and Codex review output;
- active tests and fixtures;
- historical plans/reports separately from active runtime consumers.

A schema is not treated as live merely because a historical document or lesson mentions the artifact name.

## Classification

### `codex_review_output.schema.json`

Classification: `RETIRE_ZERO_CONSUMER`
SHA-256 before retirement: `083181028fbdbac50a563709374a130bd8c2e19886e55ebf0e8c7ddfd3ed3e85`

Evidence:
- No exact filename consumer.
- Contract keys such as `REVIEWED_RUN_ID` and `REVIEWED_SNAPSHOT_HASH` occur only inside the schema.
- The retired project-local review pipeline that produced/validated this contract has already been removed.

### `evidence_manifest.schema.json`

Classification: `RETIRE_ZERO_CONSUMER`
SHA-256 before retirement: `6cc78eae2a70ddd96e8ac0db75dac46ed7c6e64a702a9e2b02481ee72a882f39`

Evidence:
- No exact filename consumer.
- Distinctive fields such as `snapshot_before` occur only inside the schema.
- No generic schema validator loads this file.
- Verification/evidence authority now belongs to Local Orchestrator.

### `review_run.schema.json`

Classification: `RETIRE_ZERO_CONSUMER`
SHA-256 before retirement: `217b301e01dc755b89494491985e9d4d25bba45f0e3b1b78691f0af2e3533769`

Evidence:
- No exact filename consumer.
- `learning_guard.py` mentions `review_run.json` only as historical prevention text; it does not load or validate this schema.
- The runtime that owned review-run manifests has already been retired.

### `task_context.schema.json`

Classification: `RETIRE_ZERO_CONSUMER`
SHA-256 before retirement: `a05f0d397672c51d672679f37fc2c06eeafd4dbc226cae98e80212216922beeb`

Evidence:
- No exact filename consumer.
- Distinctive fields such as `resume_cursor` occur only inside the schema.
- `TASK_CONTEXT.json` appears only in historical migration material, not in the active Revit runtime.
- Operational memory authority is Phase 9 Local Orchestrator / project-local durable memory, not this retired task-context contract.

### `task_scope.schema.json`

Classification: `RETIRE_ZERO_CONSUMER`
SHA-256 before retirement: `e43d0b178ee3add33b16c05a5da8d12849ad72b36c37653203edc6c7e495471d`

Evidence:
- No exact filename consumer and no generic schema loader.
- The active `test_agent_context_contract.py` reads a `TASK_SCOPE.json` fixture directly and manually validates only the bounded fields it needs; it never loads this schema.
- The schema is inconsistent with the currently accepted fixture: it requires `repository_root`, while `.agents/runtime/tests/fixtures/agent_context_v1/TASK_SCOPE.json` does not contain that field. Keeping the schema would therefore create a stale/false authority.

## Decision

Retire all five schema files from `.agents/runtime/schemas/`.

Do not replace them with a second project-local schema authority. Active workflow, review/evidence, convergence, operational-memory, and skill-evolution contracts remain owned by Local Orchestrator. The surviving Revit runtime remains limited to bounded evidence/safeguard adapters and active governance tests.

Historical references in dated plans/reports are retained as audit evidence and are not treated as live consumers.
