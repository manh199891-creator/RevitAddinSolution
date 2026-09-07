# Revit Engineering Guidelines

Status: ACTIVE
Scope: cross-solution engineering rules for Revit-facing Antigravity projects

This document preserves the durable Revit engineering guidance formerly scattered under the legacy `Workflow/` directory. Current execution/planning/release governance lives under `.agents/skills/` and wins if any historical workflow text conflicts with this standard.

## 1. Revit API execution boundary

- Do not call the Revit API from arbitrary background threads, `Task.Run`, worker threads, or asynchronous callbacks that are outside a valid Revit API context.
- A modeless UI that needs to modify/read the Revit document outside the original command context must marshal work through the appropriate Revit mechanism, normally `ExternalEvent` / `IExternalEventHandler`.
- A modal dialog that completes work while the original external-command context is still valid does not automatically need an `ExternalEvent`; inspect the actual lifecycle instead of applying the pattern mechanically.
- Treat user cancellation (`OperationCanceledException`) as a normal cancelled path, not an application error.

## 2. Transaction safety

- Every model mutation must occur in a valid Revit transaction context.
- Prefer scoped `Transaction`, `SubTransaction`, and `TransactionGroup` lifetimes so exceptional paths cannot leave ambiguous transaction ownership.
- Check transaction results where failure matters and return a truthful `Result.Succeeded`, `Result.Cancelled`, or `Result.Failed`.
- Do not open nested transactions where Revit does not permit them.
- Do not blanket-swallow Revit failures or warnings. A failure preprocessor may suppress only explicitly identified, understood, and acceptance-tested warnings.

## 3. Element lifetime and document safety

- Null-check retrieved elements and re-resolve `ElementId` values when work crosses transactions/events rather than keeping long-lived mutable `Element` references without need.
- Use `IsValidObject` when an object may have crossed a document/transaction lifecycle boundary.
- Do not cache `Document`/`UIDocument` instances beyond the lifecycle where their validity is guaranteed.
- Close any document opened programmatically when ownership belongs to the add-in.

## 4. Collector and performance rules

- Apply `FilteredElementCollector` quick filters (`OfClass`, `OfCategory`, element-type filters) before in-memory LINQ predicates where practical.
- Avoid repeated full-model scans when one collection can be reused safely.
- Avoid `doc.Regenerate()` inside tight loops unless a verified API requirement demands it; regenerate at the smallest justified boundary.
- DMU / updater execution must be kept small and deterministic because it may fire frequently. Use early returns and narrow triggers.
- `DocumentChanged` is an observation boundary; do not start a transaction from the event merely to react synchronously. Queue work into an appropriate later Revit execution context when mutation is required.

## 5. Units, parameters, and IDs

- Use Revit unit APIs (`UnitUtils` plus the API types appropriate for the project's supported Revit version) instead of hard-coded conversion factors such as `304.8`.
- Prefer stable built-in parameter identifiers where they express the required semantic; avoid localized display-name strings as durable identifiers.
- Do not copy a version-specific API example across projects without checking the project's declared Revit version and compile target.
- Treat `ElementId` numeric representation as version-sensitive. Use the API supported by the project's actual Revit target rather than a repository-wide assumption.

## 6. ExternalEvent, DocumentChanged, DMU, and Extensible Storage

Use advanced Revit mechanisms only when their lifecycle matches the feature:

- `ExternalEvent`: bridge modeless/external UI intent into a valid Revit API execution context.
- `DocumentChanged`: observe committed model changes; keep handlers lightweight and read-oriented.
- DMU / `IUpdater`: deterministic automatic reactions to narrowly scoped model changes; register stable updater identity and narrow triggers.
- Extensible Storage: persist add-in-owned data when the schema identity, versioning, and migration strategy are intentionally defined.

Do not introduce any of these simply because an old template mentioned them.

## 7. Logging and user-facing errors

- Log structured context sufficient to reproduce a failure: command/operation, relevant element IDs, parameter values, and exception details.
- Keep user-facing messages actionable and concise; do not display raw stack traces as the primary UI error.
- Treat cancellation and recoverable skips differently from true errors.
- Runtime log location/retention is an implementation decision owned by the current logging subsystem; do not duplicate a historical path convention without checking current source/configuration.
- Revit journals are useful diagnostic evidence for host crashes and command sequencing, but journal interpretation is evidence, not proof of root cause by itself.

## 8. Testing model

Repository smoke governance defines the authoritative levels:

- **S0** — structural/project identity and contract checks.
- **S1** — focused build/unit/automated tests that can run outside Revit where applicable.
- **S2** — actual Revit assembly/manifest load evidence where applicable.
- **S3** — representative command startup and safe-cancel evidence.
- **S4** — feature critical-path acceptance before production promotion.

Additional rules:

- Keep pure business/geometry/parsing logic separable from Revit host calls where practical so it can be tested deterministically.
- Revit-hosted integration evidence requires a real supported Revit execution environment; do not convert a build or mocked test into a fake S2/S3/S4 PASS.
- Test fixtures belong under the repository/project fixture contract, not an undocumented personal path.
- Do not use production customer models as ordinary test fixtures without an explicit data/permission policy.

## 9. Compatibility and build targeting

- The project's own `.csproj`, smoke manifest, build scripts, and approved compatibility plan are the source of truth for supported Revit/.NET versions.
- Verify Autodesk API compatibility before introducing a version-specific API call or package upgrade.
- Revit/Nice3point dependencies are project semantics, not a repository-wide implicit dependency.
- Keep multi-version/framework migration separate from folder cleanup, assembly renaming, and behavior refactors.

## 10. Release governance

Do not use the retired `Workflow/` release text as an execution contract.

Current release/acceptance behavior is governed by:

- `.agents/skills/project-workflow-governance/SKILL.md`
- `.agents/skills/addin-artifact-governance/SKILL.md`
- `.agents/skills/agent-security/SKILL.md` when release work is security-sensitive
- project-local smoke contracts and last-known-good metadata
- repository packaging/deploy scripts
- explicit user/workflow approval for commit, push, tag, baseline promotion, or production deployment

No build result alone authorizes S2/S3/S4 promotion or production release.

## 11. Review checklist

Before accepting a Revit-facing source change, check at minimum:

- transaction ownership and exceptional paths;
- valid Revit API execution context / thread boundary;
- element/document lifetime;
- collector/performance behavior;
- units/parameter identifiers/version-sensitive API usage;
- cancellation and user-facing error behavior;
- relevant S0/S1 automated evidence;
- required S2/S3/S4 real-host evidence for promotion;
- no unrelated working-tree lane was overwritten.
