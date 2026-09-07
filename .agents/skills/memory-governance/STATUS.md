# memory-governance — ACTIVE

Source Phase: 9 — Operational Memory
Status: ACTIVE / ADOPTED 2026-08-26

Canonical runtime owner: `E:\chatgpt-local-orchestrator`.

This Revit adapter governs owner-local `PROJECT_STATE.json` resume checkpoints, selective operational-memory retrieval and fail-closed freshness handling. It does not create a second workflow/review/memory runtime inside RevitAddinSolution.

The always-read authority remains `.agents/policies/context-memory-policy.md`; load `SKILL.md` only for continuation, repeated-failure, regression, decision-lookup or memory-maintenance work routed by `.agents/policies/capability-routing.md`.
