# Antigravity.DrawBeams — Add-in-local artifact structure migration

Date: 2026-08-23  
Status: PASS / ACTIVE GOVERNANCE

## Objective

Make `Antigravity.DrawBeams` the canonical owner of its own plans, design notes, acceptance contracts, implementation reports and smoke-test artifacts before DBR-2 begins.

## Canonical structure

```text
src/Antigravity.DrawBeams/
├── Models/
├── Services/
├── UI/
├── docs/
│   ├── plans/
│   ├── design/
│   ├── acceptance/
│   └── reports/
└── smoke-tests/
    ├── README.md
    ├── smoke-manifest.json
    ├── baseline/
    │   ├── last-known-good.json
    │   └── expected-behavior.md
    ├── scripts/
    ├── fixtures/
    └── results/          # generated / Git ignored
```

## Migration completed

- canonical roadmap remains at `docs/plans/2026-08-22-drawbeams-recognition-engine-v14.md`;
- DBR-0 report moved from repository-level `docs/reports/` to this add-in's `docs/reports/`;
- DBR-1 report moved from repository-level `docs/reports/` to this add-in's `docs/reports/`;
- `docs/design/`, `docs/acceptance/` and `docs/reports/` are established as module-local ownership zones;
- `smoke-tests/` contains baseline identity/expected behavior, manifest, scripts/fixtures placeholders and a persistent generated `results/` directory with local ignore rules;
- `.ai-bridge/current-plan.md` is execution-handoff state only and points to the canonical add-in-local plan rather than acting as the archive.

## Repository-wide rule

For future work, an artifact belongs to the smallest stable owner:

- one add-in only -> `src/Antigravity.<Addin>/docs/...`;
- cross-add-in/solution architecture, build, installer or migration -> repository-level `docs/...`;
- generated smoke output -> owner add-in `smoke-tests/results/` and never committed;
- `.ai-bridge` -> transient coordination only.

Existing legacy artifacts for other add-ins do not need a bulk blind move. They should be inventoried and migrated to their owner add-in when that add-in enters active work, so references and historical context can be preserved deliberately.

## DBR-2 gate

DBR-2 may start only after the Local Orchestrator approved baseline is refreshed to include this governance/migration state and the new DBR-2 worktree proves that the canonical DBR-0/DBR-1 source, tests, local docs and smoke contract are present.
