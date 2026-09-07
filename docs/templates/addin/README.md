# Add-in Documentation & Smoke-Test Template

Copy this structure into `src/<Addin>/` when the add-in enters active development/refactor:

```text
docs/
├── plans/
├── design/
├── acceptance/
└── reports/
smoke-tests/
├── README.md
├── smoke-manifest.json
├── baseline/
│   ├── last-known-good.json
│   └── expected-behavior.md
├── scripts/
├── fixtures/
└── results/
```

Rules:

- `docs/plans/` is canonical for add-in-specific active plans.
- `.ai-bridge/current-plan.md` may mirror a plan but is transient.
- `smoke-tests/results/` is generated and must be ignored by Git.
- `last-known-good.json` records rollback identity, not binary payloads.
- Before destructive refactor, define at least S0/S1 smoke; Revit-facing modules should define S2/S3; production-critical paths should define S4.
