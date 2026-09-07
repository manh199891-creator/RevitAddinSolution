# DrawBeams Smoke Scripts

Place repeatable DrawBeams-only smoke automation here. Shared smoke infrastructure belongs under repository-level test/build tooling.

## Verified package publisher

`Publish-VerifiedPackage.ps1` is the project-scoped publisher used by the Local MCP maintenance command `publish-drawbeams-smoke-package`.

It reads the transient request at `../results/.publish-request.json`, verifies the referenced Local Orchestrator workflow/task is completed, its review package and tests are PASS, checks the source package contains exactly the approved six DLLs, verifies every requested SHA-256, and then publishes to `../results/<workflow-id>/package`.

Safety rules:

- source must be the exact runtime worktree recorded by the workflow job;
- destination overwrite is forbidden when any byte/hash differs;
- an identical existing package is treated as idempotent `ALREADY_PUBLISHED`;
- copy is verified in a temporary directory before the package directory is moved into place;
- `publish-receipt.json` is written beside the package and records `productionAdvanced=false` and `lkgAdvanced=false`;
- the publisher never edits production source or `baseline/last-known-good.json`.

The request file is generated/transient under ignored `smoke-tests/results/`; do not archive workflow-specific publish requests in tracked source.
