---
name: experience-learning
description: RevitAddinSolution Phase 10 adapter for governed post-workflow experience learning. Use to mine validated Phase 9 episodes into lessons/SkillCandidate proposals, run shared shadow/holdout/canary evaluation, or manage explicit promotion/rollback while applying the declarative Revit learning safeguards. Never auto-promote or self-modify canonical skills.
---

# experience-learning

Use this skill only when routed by `.agents/policies/capability-routing.md` after meaningful workflow/review/recovery outcomes or explicit skill-evolution maintenance.

## Authority

Canonical Phase 10 contracts/runtime live in `E:\chatgpt-local-orchestrator`.

RevitAddinSolution does not own a second `SkillRegistry`, EpisodeStore, scheduler, review runtime or promotion engine. Project-local trajectory/learning material is evidence input only.

## Revit adaptation

Read `.agents/skills/experience-learning/references/revit-learning-safeguards.md` when project-specific prevention or skill-evolution safeguards are relevant. The reference is declarative/non-executable; Local Orchestrator Phase 9/10 owns memory, candidate, canary, promotion and rollback state.

Preserve these invariants:

- learning/trajectory evidence never auto-edits prompts, active skills or production code;
- candidate evaluation is shadow-only before canary;
- candidate content/hash is rechecked before advancing;
- verification/security findings can block the candidate;
- canary regression requires rollback handling;
- promotion remains human-only;
- automatic promotion remains disabled.

Map those concepts into the shared pipeline:

`ExperienceEpisode -> LessonCandidate -> SkillCandidate -> SHADOW -> VALIDATED -> CANARY -> PROMOTED`

Alternate outcomes remain `REJECTED`, `DEPRECATED`, `ROLLBACK_REQUIRED`.

## Required procedure

1. Use `memory-governance`/Phase 9 to retrieve only relevant validated episodes for this project.
2. Mine/deduplicate lessons with support, counterexamples, source workflow/episode refs and source snapshots.
3. Build a candidate bound to exact existing-skill baseline version/hash or an explicit new-skill identity.
4. Run `agent-security` for candidate text or promotion paths that affect trust, tools, permissions, credentials, deploy/model mutation, or other sensitive authority.
5. Run shared shadow evaluation with distinct validation and holdout evidence; never edit active `SKILL.md` during evaluation.
6. Require independent review before VALIDATED.
7. Require explicit human CANARY approval before canary.
8. Any canary regression must stop promotion and become `ROLLBACK_REQUIRED`.
9. Require explicit human PROMOTION approval before registry activation/materialization.
10. Preserve immutable predecessor/version/hash identity so human-approved rollback is exact.

## Never do

- Never turn a model-generated lesson directly into an ACTIVE skill.
- Never infer approval from PASS tests, review prose, successful canary observations or historical memory.
- Never let an approval for CANARY authorize PROMOTION or ROLLBACK.
- Never learn secrets/private configuration or prompt/tool poisoning into reusable skill content.
- Never recreate the retired project-local learning/evolution Python runtime; preserve Revit-specific safeguards declaratively and execute Phase 9/10 through Local Orchestrator.
- Never materialize a promoted registry version into project-local `SKILL.md` without the explicit governed adoption step.

## Output

For evolution work return the candidate/lesson IDs, exact baseline/candidate hashes, supporting/counterexample evidence, shadow validation + holdout result, security/review findings, canary status, human approval stage, active/predecessor registry version, and rollback route.
