# Revit Learning Safeguards — Declarative Reference

Status: ACTIVE REFERENCE / NON-EXECUTABLE
Canonical runtime authority: `E:\chatgpt-local-orchestrator` Phase 9 Operational Memory + Phase 10 Skill Evolution

This file preserves the Revit-specific prevention knowledge that previously lived in the retired project-local `learning_guard.py` / `evolution_pipeline.py`. It is evidence/routing guidance only: it owns no memory store, scheduler, review runtime, candidate registry, canary runtime, promotion state, or production mutation authority.

## Recurring prevention patterns

| Pattern | Evidence terms/examples | Current prevention rule |
|---|---|---|
| `invalid_structured_output_schema` | `invalid_json_schema`, unsupported structured-output keyword, schema-format rejection | Keep structured-output contracts inside the provider-supported subset. Treat schema rejection as infrastructure/contract evidence; do not weaken required review fields merely to make a provider accept the schema. |
| `dirty_baseline_or_scope` | `OUT_OF_SCOPE`, `NO_TASK_DELTA`, `BLOCKED_SCOPE`, `BLOCKED_BASELINE` | Bind work to the approved baseline/current owner checkpoint and exact scoped paths. Never widen allowed paths or rewrite unrelated dirty work to manufacture a clean result. |
| `blind_retry_after_failure_budget` | failure budget exhausted, unchanged snapshot, repeated hypothesis | Do not rerun an unchanged hypothesis. Require new scoped evidence, a changed implementation/hypothesis, or a human decision according to the canonical `LoopContract`. |
| `unverified_background_or_recovery_claim` | no durable execution evidence, stale lease/heartbeat, prose claiming work is still running | Use Local Orchestrator durable workflow/job/execution/recovery state. Never claim background progress from agent prose, old report timestamps, or an assumed process. |
| `provider_auth_or_repair_unavailable` | provider auth missing, runtime not READY, repair provider unavailable | Fail closed with the exact infrastructure/provider status. Do not claim repair progress, bypass trust gates, or substitute a different provider without an authorized workflow decision. |
| `review_context_split` | production/test/project evidence reviewed separately so relationships are lost | Keep related source, project configuration, tests, plan/acceptance and smoke evidence together in the review package when feasible; review source truth outranks agent summaries. |
| `provider_sandbox_degraded` | sandbox/helper degradation while the provider still returns a valid bounded result | Record the diagnostic distinctly from code failure. Treat it as blocking only when it prevents trustworthy execution/review evidence; never hide the degradation. |

## Skill-evolution invariants retained from the retired local engine

- Experience/trajectory evidence never auto-edits production source or active skills.
- Build candidates against an exact baseline identity/hash.
- Evaluate candidates in shadow mode before any canary state.
- Use distinct validation/holdout evidence; do not promote from training evidence alone.
- Security/trust findings may block validation/canary/promotion.
- Candidate content/hash must remain identical between evaluation and the approval it supports.
- Any canary regression stops promotion and requires rollback handling.
- CANARY, PROMOTION and ROLLBACK are separate explicit human approvals.
- Automatic promotion remains disabled.
- Local project evidence is an input only; Local Orchestrator owns `ExperienceEpisode`, `LessonCandidate`, `SkillCandidate`, canary, registry, promotion and rollback state.

## Retired implementation boundary

The former project-local Python files and `.agent/learning`/`.agent/knowledge` substrate are not executable authorities and must not be recreated. Historical implementation details remain available only in dated Phase 0/9/10/12 reports when migration archaeology is needed.
