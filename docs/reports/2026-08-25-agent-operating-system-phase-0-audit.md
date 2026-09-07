# Agent Operating System — Phase 0 RevitAddinSolution Audit

Date: 2026-08-25
Status: COMPLETE — read/audit/documentation only
Repository: `E:\Antigravity\RevitAddinSolution`
Master baseline: `E:\chatgpt-local-orchestrator\docs\architecture\agent-operating-system\PHASE-0-ARCHITECTURE-BASELINE.md`
Master roadmap: `E:\chatgpt-local-orchestrator\docs\plans\2026-08-25-agent-operating-system-master-roadmap.md`
Revit adoption roadmap: `docs/plans/2026-08-25-agent-operating-system-adoption-roadmap.md`

## 1. Audit scope

Fresh Phase 0 inspection covered:

- current workspace skill inventory;
- lifecycle and project governance skills;
- dual-agent research/plan/code/release pipeline;
- convergence/failure-budget behavior;
- compact operational memory / Learning Guard;
- workflow knowledge/episode storage;
- offline skill-evolution pipeline;
- repository context/build/runtime exclusions;
- gaps relative to the cross-project Agent Operating System roadmap.

No production add-in source was edited. Existing dirty working-tree changes, including ongoing DrawBeams and repository-governance work, were preserved.

## 2. Existing workspace skill authority

Current workspace skills found:

1. `1-spec`
2. `2-plan`
3. `3-code`
4. `4-ship`
5. `addin-artifact-governance`
6. `dual-agent`
7. `dual-agent-pipeline`
8. `project-workflow-governance`

Decision: keep these as the Revit lifecycle/governance authority. Do not replace them with a parallel OMH/Addy/Matt skill stack.

## 3. Lifecycle mapping

### `1-spec`

Already provides:
- design/spec phase boundary;
- durable artifact ownership;
- user approval gate before planning/coding.

Future adoption:
- enrich with reusable deep-interview RequirementSpec semantics;
- enrich with Matt-style domain modeling when useful;
- keep current approval boundary.

### `2-plan`

Already provides:
- mandatory user-discovery gate;
- durable context reuse so known facts are not asked again;
- exact project/add-in artifact ownership;
- executable task/TDD planning expectations;
- Local Orchestrator as default execution lane.

Future adoption:
- add explicit bounded Planner -> Architect -> Critic consensus;
- compile accepted consensus into the existing Local Orchestrator WorkflowPlan instead of introducing another executor.

### `3-code`

Already provides:
- Local Orchestrator / isolated workflow as default production-change lane;
- TDD RED -> GREEN -> REFACTOR;
- root-cause-first debugging;
- adversarial review;
- review between substantive tasks.

Future adoption:
- align its retry/convergence vocabulary with the future shared LoopContract;
- do not remove existing dual-agent safeguards until shared semantics are proven equivalent.

### `4-ship`

Already provides:
- verification/acceptance/closure;
- owner-local acceptance/report storage;
- rollback/release governance;
- explicit authorization boundary for commits/integration.

Future adoption:
- curate additional agent/browser/MCP/supply-chain security gates;
- preserve current release authority.

## 4. Dual-agent pipeline is already a mature RALPH/Loopy precursor

Fresh inspection of `.agents/skills/dual-agent-pipeline/SKILL.md` and runtime code shows:

- writer/reviewer timeouts terminate the complete process tree;
- expired `RUNNING` reviews reconcile to terminal `STALE`;
- plan/research snapshot identity excludes pipeline reports/caches/factory metadata;
- repeated same snapshot + stage + hypothesis + evidence becomes `DUPLICATE_ATTEMPT` without consuming failure budget;
- resume from stale/exhausted state requires changed relevant snapshot and an explicit falsifiable hypothesis;
- bounded `MaxCycles` exists;
- polling is bounded and terminal-aware;
- Anti is the single writer; Codex is independent read-only verifier;
- both agents are never allowed to write the same worktree concurrently.

Important convergence outcomes already include:

- `BLOCKED_NO_PROGRESS`
- `BLOCKED_OSCILLATION`
- `BLOCKED_NO_FIX_DELTA`
- `BLOCKED_HANDOFF`
- `BLOCKED_SCOPE`
- `BLOCKED_BASELINE`
- `BLOCKED_VERIFY`
- `STALE`
- `INFRA_FAIL`

Decision: **Loopy/RALPH is not greenfield in this repository.** The future shared Local Orchestrator LoopContract should generalize these semantics and provide adapters; Revit should not receive a second loop engine.

## 5. Existing research capability is review-only, not web acquisition

`dual-agent-pipeline` already has `research` mode, but its role is to review prepared `.agent/context/RESEARCH.md` against repository evidence/snapshot.

It does not implement the requested external web research acquisition stack:

- source routing;
- Agent-Reach acquisition;
- Defuddle page cleanup;
- YouTube transcript acquisition;
- primary-source preference;
- contradiction detection across external sources;
- citation/provenance normalization.

Decision: build `deep-research` in Local Orchestrator and let Revit consume/review the resulting ResearchBrief through the existing research review lane.

## 6. Existing operational memory is significant

### Learning Guard

`.agents/runtime/learning_guard.py` already implements compact, task-relevant operational memory.

Observed behavior includes:
- prevention-rule matching;
- compact `.agent/context/MEMORY_CONTEXT.md` generation;
- bug-memory/trajectory inputs;
- full memory not loaded by default;
- full memories loaded only when a compact rule is relevant;
- token-budget-conscious context behavior.

Representative prevention topics already include:
- correct dual-orchestrator invocation;
- supported structured-output schema usage;
- dirty baseline/scope protection;
- no blind retry after failure-budget exhaustion;
- no false background-execution claims;
- runtime doctor before auto-fix;
- keeping related source/project/test files together;
- treating sandbox helper errors proportionally.

Decision: this is a useful existing prototype for future shared operational-memory retrieval. Preserve and benchmark it.

## 7. SkillClaw-style evolution is already implemented locally

This was the largest Phase 0 discovery.

`.agents/runtime/evolution_pipeline.py` already defines an offline, gated learning/evolution system whose explicit design prevents a model from rewriting production artifacts directly.

Existing stages/capabilities:

1. Append-only task trajectory JSONL.
2. Pipeline outcome recording.
3. Human labels: success / failure / partial / regression.
4. Reproducible dataset construction.
5. Deterministic train / validation / holdout split.
6. Baseline vs candidate shadow evaluation.
7. Candidate growth limits.
8. Basic dangerous-pattern/security scanning.
9. Validation improvement threshold.
10. Holdout non-regression requirement.
11. Candidate hash/evaluation identity.
12. Fresh evidence requirement before candidate gate.
13. `APPROVED_FOR_CANARY` gate only — not production promotion.
14. Canary traffic percentage bounds.
15. Canary success/failure/regression recording.
16. Any regression -> `ROLLBACK_REQUIRED`.
17. Sufficient successful canary outcomes -> `READY_FOR_HUMAN_PROMOTION`.
18. `human_approval_required_for_promotion = True`.
19. `auto_promotion_enabled = False`.

Decision: **do not install SkillClaw as a second self-learning authority and do not implement another Revit-local skill-evolution pipeline.**

Long-term work should:

- benchmark the existing pipeline;
- normalize trajectory/episode contracts;
- improve lesson deduplication/support/counterexample logic;
- move/shared-host registry/evaluation authority in Local Orchestrator when mature;
- preserve explicit human promotion and rollback.

## 8. Existing knowledge/episode layout

`.agents/runtime/workflow_governance.py` already creates/uses operational knowledge areas including:

- `.agent/knowledge/memory/bugs`
- `.agent/knowledge/memory/decisions`
- `.agent/knowledge/learn`
- `.agent/knowledge/later`

It can write bug episodes when failure budgets are exhausted.

Decision: later Phase 9/10 should adapt these existing records into a shared `ExperienceEpisode` contract rather than deleting or ignoring them.

## 9. Genuine Revit gaps after Phase 0

### Gap A — External deep research acquisition

Missing locally. Should be consumed from Local Orchestrator.

### Gap B — Explicit Planner / Architect / Critic consensus

No substantive bounded three-role planning consensus implementation was found in the workspace skills/runtime.

This remains a real Phase 2 target.

### Gap C — Reusable RequirementSpec engine

User discovery exists, but no shared structured deep-interview/RequirementSpec engine currently owns cross-project requirements clarification.

This remains a shared Phase 2 target.

### Gap D — XAML-specific interface quality skill

UI standards already exist, but no dedicated project skill applies `make-interfaces-feel-better` concepts to WPF/XAML review.

`xaml-interface-quality` remains a justified Revit-specific future skill.

### Gap E — Persistent semantic code graph

Revit has architecture mapping support, but no selected persistent codebase graph/memory provider is established as canonical.

This remains a Phase 8 benchmark/POC target.

### Gap F — Shared cross-project skill registry/evolution authority

Revit has a strong local evolution implementation, but Local Orchestrator does not yet own a shared registry/evolution service. This is a consolidation/generalization gap, not a greenfield Revit gap.

## 10. Memory boundary for this repository

Do not use the following as canonical code-graph input by default:

- `**/bin/**`
- `**/obj/**`
- `artifacts/**`
- `TestResults/**`
- `**/__pycache__/**`
- `.pytest_cache/**`
- `.agents/factory/**`
- `.agents/context/**`
- `.agent/context/**`
- `.agent/state/**`
- `.agent/reports/**`
- `.agent/learning/**`
- `.agent/knowledge/**`
- `.ai-bridge/**`
- root `source-code/**`
- `scratch/**`
- `**/smoke-tests/results/**`
- generated architecture maps and compiled/deployment output.

Important distinction:

- `.agents/runtime/**/*.py` **is canonical source for the agent-runtime subsystem** and may be indexed as code.
- `.agent/learning/**` and `.agent/knowledge/**` are operational/skill memory, useful for learning/review but not code source truth.
- `.agent/context/**` and `.agent/state/**` may be read for a specific runtime diagnostic but must not outrank current source/project artifacts.

Full cross-project rule: `E:\chatgpt-local-orchestrator\docs\architecture\agent-operating-system\CONTEXT-AND-MEMORY-BOUNDARIES.md`.

## 11. Artifact compatibility

Future shared artifacts should map to existing Revit governance:

- `ResearchBrief` -> reviewed through existing research pipeline; material decisions can feed canonical project docs.
- `RequirementSpec` -> material approved decisions synchronize to project-local plan/design/acceptance.
- `ConsensusPlan` -> produces a Local Orchestrator WorkflowPlan; project-specific durable plan stays owner-local.
- `ExecutionReceipt` / `VerificationReport` -> reference Local Orchestrator workflow/job/execution/review evidence.
- `ExperienceEpisode` -> adapts existing Revit trajectories/bug episodes.
- `SkillCandidate` -> adapts existing Revit shadow/gate/canary implementation.

Do not introduce competing copies of WorkflowPlan, ReviewPackage or release authority.

## 12. Revised Revit roadmap implication

The initial long-horizon roadmap remains valid at the capability level, but Revit implementation scope changes:

### Do not build again
- scheduler/runtime;
- browser controller;
- recovery coordinator;
- failure-budget engine;
- no-progress/oscillation detector;
- trajectory collector;
- shadow skill evaluator;
- canary/rollback core;
- automatic self-promotion (must remain disabled).

### Add/enrich later
- deep-research consumption;
- deep-interview/RequirementSpec integration;
- Planner/Architect/Critic planning consensus;
- shared LoopContract adapter/alignment;
- `xaml-interface-quality`;
- curated security improvements;
- codebase-memory POC;
- shared Local Orchestrator memory/skill registry integration.

## 13. Phase 0 acceptance

- [x] Eight existing workspace skills inventoried.
- [x] `1-spec -> 2-plan -> 3-code -> 4-ship` authority confirmed.
- [x] Project/artifact governance confirmed.
- [x] Dual-agent convergence/failure-budget semantics audited.
- [x] Learning Guard/compact operational memory audited.
- [x] Evolution pipeline audited.
- [x] Real gaps separated from already-implemented capabilities.
- [x] Context/memory exclusions documented.
- [x] No production source edited.
- [x] No reset, clean, stash, commit, push or tag performed.

## 14. Next action

Proceed to Phase 1 in `E:\chatgpt-local-orchestrator`: establish the minimum project-owned skill foundation and implement the `deep-research` capability/ResearchBrief contract against the Phase 0 benchmark fixtures.

RevitAddinSolution should remain a consumer/reviewer of that research capability during Phase 1; no Revit production-source change is required to start it.
