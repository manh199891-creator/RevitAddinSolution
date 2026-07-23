# Project Profile Contract

Each project integrated with the Dual Agent Pipeline must have a `project_profile.json` at `.agent/project_profile.json` containing standardized configuration.

## Required Fields

```json
{
  "project_id": "example",
  "source_path": "source-code",
  "stage_patterns": ["src/**", "tests/**"],
  "dual_forbidden": ["bin/**", "obj/**", ".agent/**"],
  "fixer_command": "",
  "evidence_policy": {
    "required_for_gate": true,
    "required_stages": ["build", "test", "lint"]
  },
  "failure_budget": {
    "max_failed_attempts": 3
  },
  "review_routing": {
    "quick_max_files": 3,
    "quick_max_changed_lines": 50,
    "deep_min_files": 11,
    "deep_min_changed_lines": 301,
    "sensitive_paths": ["**/auth/**", "**/security/**", "**/migrations/**"]
  },
  "release_requires": {
    "build_pass": true,
    "qa_pass": true,
    "codex_real_review_pass": true,
    "diff_hash_match": true,
    "runtime_validation_pass": false
  }
}
```

## Field Descriptions

- **`project_id`**: The stable machine name for the project (e.g., `revit`).
- **`source_path`**: The root directory for the source code relative to the project root.
- **`stage_patterns`**: The default allow-list of globs for file scoping. Used if the user omits an explicit `--allowed` boundary during `dual-init`.
- **`dual_forbidden`**: A list of globally forbidden globs (e.g., build artifacts, agent state files) that the agent must NEVER modify.
- **`fixer_command`**: The background command used to invoke an automated fixer. If left empty, the pipeline will halt and output `NEEDS_FIXER` (without claiming a fixer is running).
- **`evidence_policy`**: Enables the fresh-evidence gate and lists verification stages that must pass for the current repository snapshot. Omit it to preserve legacy project behavior.
- **`failure_budget`**: Stops repeated blind fix cycles. The default is three failed attempts, followed by `ROOT_CAUSE_HANDOFF.md` and a bug episode.
- **`review_routing`**: Routes changes to `QUICK`, `STANDARD`, or `DEEP`. Any sensitive-path match forces `DEEP`.
- **`release_requires`**: Boolean flags used by the `harness.py gate` command to enforce which checks must pass before a release is allowed. `runtime_validation_pass` can be set to false for projects lacking runtime tests.
