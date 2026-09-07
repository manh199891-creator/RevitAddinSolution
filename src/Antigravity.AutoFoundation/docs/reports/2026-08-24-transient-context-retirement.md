# AutoFoundation Transient Context Retirement

Date: 2026-08-24
Owner: `src/Antigravity.AutoFoundation/`
Status: CLOSED — transient mirror retired

## Reason

The repository-level runtime mirror under `.agents/context/` still described an older AutoFoundation implementation and is not authoritative durable project knowledge.

The stale mirror referenced production paths under `src/Antigravity.Core/...`, described a modeless WPF + `ExternalEvent` architecture, and mixed historical AutoColumn cleanup notes into the AutoFoundation scope.

## Source-of-truth check

Current source inspection shows:

- command: `src/Antigravity.AutoFoundation/Commands/AutoFoundationCommand.cs`;
- UI: `src/Antigravity.AutoFoundation/UI/`;
- services: `src/Antigravity.AutoFoundation/Services/`;
- `AutoFoundationCommand` currently opens `AutoFoundationWindow` with `ShowDialog()`;
- no `ExternalEvent` usage is present under `src/Antigravity.AutoFoundation/` at this closure point.

Therefore the old transient PLAN / TECHNICAL_DESIGN / ACCEPTANCE files must not be promoted verbatim into project-local design or acceptance documents.

## Durable resume path

Use the normal project memory ladder instead:

1. `docs/projects/PROJECTS.md`
2. `src/Antigravity.AutoFoundation/PROJECT.md`
3. `src/Antigravity.AutoFoundation/docs/plans/ROADMAP.md`
4. relevant dated project-local artifacts
5. `src/Antigravity.AutoFoundation/smoke-tests/`
6. current production source

## Retirement decision

`.agents/context/PLAN.md`, `.agents/context/TECHNICAL_DESIGN.md`, and `.agents/context/ACCEPTANCE_CRITERIA.md` are classified as generated/transient runtime mirrors and may be deleted. Future runtime planning may recreate `.agents/context/`, but durable AutoFoundation knowledge must remain owner-local.

No production behavior is changed by this retirement.
