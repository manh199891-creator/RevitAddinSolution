# RevitAddinSolution pipeline contract

- Application source is under `src/`; the dual-agent pipeline must not modify it unless the task scope explicitly authorizes application work.
- `TASK_SCOPE.json` is the narrow writer boundary. Missing scope is `BLOCKED_SCOPE_CONFIGURATION`; never widen it to `**/*`.
- An accepted plan creates `.agent/state/PLAN_LOCK.json`. Code, fix, and release preflight must recompute and compare every lock hash; any mismatch is `BLOCKED_PLAN_CHANGED`.
- `.agents/**`, `.agent/**`, `.github/**`, this file, `.gitignore`, and pipeline orchestration/configuration are protected from normal application writers.
- The host owns lifecycle decisions, budgets, finding identity, scope checks, and result verification. Agents do not invoke other agents or alter workflow state.
- Automatic flow is at most one fix and two Codex reviews. A focused re-review cannot trigger another automatic fix.
- Required validation is `python -m py_compile` for changed Python, full runtime pytest, unittest discovery, and PowerShell parsing for changed scripts. Reports must state blocked conditions and exact evidence.
- Done means the plan lock, scope, protected-path snapshot, fix contract/result, finding coverage, tests, and source snapshot all pass. Never reset, restore, or delete user changes.
