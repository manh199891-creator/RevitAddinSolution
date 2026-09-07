# Agent Operating System — Phase 11 Revit Adoption

Date: 2026-08-27
Repository: `E:\Antigravity\RevitAddinSolution`
Canonical owner: `E:\chatgpt-local-orchestrator`
Status: ADOPTED / ACCEPTED / CONSUME-ONLY

## Scope

RevitAddinSolution adopts Phase 11 by consuming the canonical Local Orchestrator `external-capability-router` service. This adoption does not add an external connector runtime to any Revit add-in and does not modify DrawBeams production source.

## Adopted routing

`.agents/capability-profile.json` marks `external-capability-router` as `ACTIVE_CONSUMED_2026-08-27` with `local-orchestrator` as owner.

`.agents/policies/capability-routing.md` routes Gmail/Slack/GitHub/MCP-backed/Composio-backed/direct-API capability requests through Local Orchestrator. The project-local policy requires exact project/principal/capability/connection authority, requires WRITE/ADMIN access to remain workflow-bound and idempotent, and routes new provider enablement through `agent-security`.

## Ownership boundaries preserved

RevitAddinSolution does not create or own:

- an external capability registry/router;
- Composio SDK/provider runtime;
- external OAuth/token/credential storage;
- a generic webhook server;
- external-provider scheduler/workflow/review state;
- a project-local `external-capability-router` skill that could be mistaken for runtime ownership.

Local Orchestrator remains the sole owner of provider registration, connection state, grants, invocation receipts, event receipts, replay/idempotency behavior and Bridge exposure.

## Trust model

External provider results and events remain `UNTRUSTED_EXTERNAL`. They are evidence/data only and cannot create user approval, workflow authorization, grants, jobs or tool execution by themselves.

A future provider adapter must pass Phase 7 security/supply-chain review before enablement. Revit does not receive or persist provider credentials.

## Verification boundary

Phase 11 real-repository acceptance is owned by Local Orchestrator and verifies this consume-only adoption against the actual Revit repository. The acceptance checks capability profile/routing/adoption state and ensures no direct Composio dependency or duplicate router skill has been introduced.

DrawBeams `src/Antigravity.DrawBeams/**` is intentionally outside this adoption change because a separate project window is actively implementing DrawBeams project-scoped smoke execution.

## Closure

Revit Phase 11 adoption is COMPLETE / ACCEPTED. Canonical Local Orchestrator acceptance passed with Phase 11 contracts 5/5, router runtime 7/7, Bridge boundary 4/4 and real Revit adoption 2/2. No production C#/XAML behavior is changed by this adoption.
