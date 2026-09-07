# Master Prompt — Repository-wide Add-in Artifact Governance Rollout

Use this prompt when standardizing all remaining projects/add-ins in `E:\Antigravity\RevitAddinSolution`.

```text
Workspace:
E:\Antigravity\RevitAddinSolution

Objective:
Standardize durable engineering-artifact ownership for every actively maintained production project/add-in under `src\Antigravity.*` using `Antigravity.DrawBeams` as the reference implementation. Plans, design notes, acceptance contracts, implementation/review reports, smoke contracts and rollback metadata must live under the smallest stable owning project instead of accumulating at repository root or `.ai-bridge`.

Mandatory governance to load before editing:
1. `.agents/AGENTS.md`
2. `.agents/policies/repository-structure.md`
3. `.agents/skills/addin-artifact-governance/SKILL.md`
4. `docs/architecture/REPOSITORY_STRUCTURE.md`

Safety constraints:
- Preserve all unrelated dirty work.
- Do NOT reset, clean, stash, commit, push or tag.
- Do NOT modify production C# behavior, namespaces, assembly names, project references, Revit commands, UI or runtime configuration as part of this governance migration.
- Do NOT perform physical Host/Shared/Features/Integrations relocation in this task.
- Do NOT blindly bulk-move documents by filename alone.
- Do NOT invent a last-known-good PASS identity where no verified baseline exists.
- `Antigravity.DrawBeams` is the reference project; validate it but do not rewrite its already-established structure unless a concrete governance defect is found.

Target project-local structure:

src\Antigravity.<Project>\
├── <existing production source folders>
├── docs\
│   ├── plans\
│   │   └── YYYY-MM-DD-<feature>.md
│   ├── design\
│   ├── acceptance\
│   └── reports\
└── smoke-tests\
    ├── README.md
    ├── smoke-manifest.json
    ├── baseline\
    │   ├── last-known-good.json
    │   └── expected-behavior.md
    ├── scripts\
    ├── fixtures\
    └── results\          # generated / Git ignored

Owner classification:
A. Revit executable / production feature add-in:
   - establish full docs tree;
   - smoke profile S0-S4 where meaningful.
B. Shared library / core / adapter under `src\Antigravity.*`:
   - establish the same docs ownership tree;
   - smoke profile must include S0 Structural + S1 focused build/tests;
   - add higher smoke levels only when a meaningful runtime/integration boundary exists;
   - do not invent fake Revit command smoke for a pure library.
C. Test-only projects, generated projects, installers or special support projects:
   - classify explicitly before changing anything;
   - apply only the portions of this policy that have a real owner/runtime meaning;
   - record exclusions and rationale in the repository rollout report.

Execution sequence:

PHASE 1 — INVENTORY ONLY
1. Enumerate all project directories and `.csproj` files under `src\`.
2. Classify each project as executable add-in, shared/core library, adapter/integration, installer/support, or test-only.
3. Inventory existing project-specific plans/reports/specs at repository root, `docs\`, `docs\superpowers\plans\`, `.ai-bridge\`, and inside project folders.
4. Build a migration map:
   - source artifact path;
   - inferred owner;
   - target canonical path;
   - confidence: HIGH / AMBIGUOUS;
   - references that must be updated.
5. Do not move AMBIGUOUS artifacts. Leave them in place and record them for manual review.

PHASE 2 — CREATE CANONICAL SKELETONS
For each actively maintained production project:
1. Ensure `docs\plans`, `docs\design`, `docs\acceptance`, `docs\reports` exist.
2. Ensure `smoke-tests\README.md`, `smoke-manifest.json`, `baseline\last-known-good.json`, `baseline\expected-behavior.md`, `scripts`, `fixtures`, `results` exist.
3. Put a local `.gitignore` in `smoke-tests\results\` or otherwise verify the repository ignore rule covers it.
4. Make README/manifest content project-specific; do not copy DrawBeams entrypoints or test paths into unrelated projects.
5. For projects without verified rollback identity, record an explicit state such as `PENDING_CAPTURE` / `PENDING_REVALIDATION`; do not claim PASS.

PHASE 3 — MIGRATE HIGH-CONFIDENCE ARTIFACTS
1. Move only clearly project-owned artifacts to the matching project-local docs folder.
2. Preserve historical filename/date when practical.
3. Update obvious links/references that point at the old path.
4. After references are verified, remove the obsolete duplicate root copy rather than keeping two canonical copies.
5. Keep genuinely cross-solution architecture/build/deploy/migration docs at repository-level `docs\`.
6. Keep `.ai-bridge` transient; where it contains an active add-in plan, reduce it to a pointer/mirror to the canonical project-local plan.

PHASE 4 — GOVERNANCE ENFORCEMENT
1. Verify `.agents/skills/addin-artifact-governance/SKILL.md` exists and is referenced by `.agents/AGENTS.md`.
2. Verify the active lifecycle capabilities route add-in-specific artifacts to the owning project rather than hardcoding repository-root plan/report paths: `deep-interview` + `project-workflow-governance` for requirements, `consensus-plan` + `project-workflow-governance` for planning, Local Orchestrator + `project-workflow-governance` + `verified-execution` when needed for implementation/repair, and Local Orchestrator review/evidence + `project-workflow-governance` + `addin-artifact-governance` (plus `agent-security` when security-sensitive) for acceptance/release.
3. Do not duplicate the same rule in many inconsistent forms; the governance skill + repository-structure policy are authoritative.

PHASE 5 — REPORTING
For every migrated project, create:
`src\Antigravity.<Project>\docs\reports\YYYY-MM-DD-artifact-structure-migration.md`

Each report must include:
- project classification;
- structure created;
- artifacts migrated;
- artifacts intentionally left at repository level;
- ambiguous artifacts deferred;
- smoke profile selected;
- last-known-good status;
- verification performed;
- confirmation that production source behavior was not changed.

Create one cross-solution summary report:
`docs\reports\YYYY-MM-DD-addin-artifact-governance-rollout.md`

The summary must include a table for all projects:
Project | Type | Local docs | Smoke contract | Migrated artifacts | Ambiguous/deferred | Status

PHASE 6 — VERIFICATION
1. Use source/diff inspection to prove no production source file was changed by this governance-only rollout, unless an unavoidable reference-path fix is explicitly documented.
2. Validate JSON smoke manifests and last-known-good files.
3. Verify generated `smoke-tests/results/` is ignored.
4. Run focused structural/build verification appropriate to each project type; do not pretend an unavailable/manual Revit smoke passed.
5. Re-scan repository-level plan/report locations and report remaining add-in-specific artifacts that still need manual ownership decisions.

Acceptance criteria:
- Every actively maintained `src\Antigravity.*` project has a clear canonical local artifact owner structure appropriate to its type.
- Add-in-specific new plans/reports no longer default to repository root.
- Shared/cross-solution docs remain centralized rather than duplicated into every add-in.
- Smoke contracts are project-specific and truthful.
- No fake PASS rollback identity is created.
- `.ai-bridge` remains transient.
- No production behavior changes are introduced.
- All ambiguous migrations are reported, not guessed.
- `Antigravity.DrawBeams` remains valid as the reference structure.

Final handoff:
Return a concise rollout summary with:
1. projects completed;
2. projects partially completed/deferred and why;
3. exact files moved/created by project;
4. remaining root-level ambiguous artifacts;
5. verification results;
6. any blocker requiring ChatGPT/user review.
```
