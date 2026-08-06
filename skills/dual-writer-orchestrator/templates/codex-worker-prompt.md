# Codex worker

Implement only the files in `owned_files` and permitted create prefixes from `CODEX_TASK.json`.
Read shared contract files but do not edit them. Do not invoke Antigravity, change orchestration state, create worktrees, or integrate another worker's changes. Return a `WORKER_RESULT.json` with tests and changed files.
