# R1 — RevitAddinSolution GitHub Normalization Plan

Date: 2026-09-07
Status: COMPLETE / AUTHORITATIVE_LOCAL_SNAPSHOT_PUBLISHED
Scope: repository-level Git/GitHub normalization only; no production behavior changes
Workspace: `E:\Antigravity\RevitAddinSolution`

## Objective

Prepare RevitAddinSolution for safe GitHub CI/monitoring without flattening the current mixed working tree into one blind snapshot. The live R1 remote audit currently confirms that the GitHub repository is public; changing repository visibility is outside this normalization step unless explicitly requested.

R1 must establish:

1. authoritative local branch/head and tracked upstream state;
2. GitHub repository identity/visibility/default branch through an authenticated read path;
3. working-tree lane ownership;
4. transient/generated exclusions;
5. explicit publish blockers;
6. a bounded landing sequence that preserves all unrelated WIP.

No R1 step may use `reset`, `clean`, `stash`, broad restore, or an unrelated feature rewrite.

## Current Git/GitHub state

Authenticated R1 remote audit confirms:

- repository: `manh199891-creator/RevitAddinSolution`;
- visibility: `public`;
- GitHub default branch: `main` at `c463f4040a0fbe0ba0833ebafa4ce86e260b1703`;
- local branch: `hotfix/agy-probe-isolation`;
- local HEAD: `a1b06f11af425853bd988d66e30a367ac3e99b00`;
- tracked upstream: `origin/hotfix/agy-probe-isolation`;
- live remote hotfix SHA: `c179d05fd15cd63bfd789065efd483fb3d2abb9f`;
- local tracking relation: ahead `1`, behind `0`.

The one local commit ahead of upstream is:

`a1b06f11af425853bd988d66e30a367ac3e99b00 fix(issue-manager): load BitmapImage safely to prevent locking when appending 2D images`

Therefore the ahead-one commit is classified as **IM — IssueManager**, not repository governance. It must not be silently included in a governance landing. The local project registry currently naming the hotfix branch as its default also differs from GitHub's actual default branch `main`; that registry drift must be resolved deliberately rather than by changing branches in a dirty worktree.

## Working-tree lanes

The current tree must be preserved as separate ownership lanes.

### G — Repository governance / durable structure

Includes:

- `.agents/AGENTS.md`, active policies, manifest, routed skills and specialist agent definitions;
- retirement of obsolete project-local runtime/skills already documented by Phase 12 closure;
- repository `docs/architecture`, `docs/plans`, `docs/projects`, `docs/reports`, `docs/standards`, `docs/templates`;
- categorized `scripts/` and project-local `PROJECT.md`, docs and smoke-test governance;
- root artifact relocation/removal where durable replacements are already present.

This lane is structurally intentional but must be landed separately from feature WIP.

### B — Build/dependency semantics

`Directory.Build.props` currently changes Nice3point/Revit extension injection from unconditional to opt-in through `UseNice3pointRevitExtensions`. This corresponds to OPT-1 in the 2026-08-23 working-tree optimization plan.

This lane requires build/test evidence and a caller/project inventory before landing.

### DB — DrawBeams active feature lane

Includes modified/new recognition, candidate evidence/scoring and CAD normalization source plus project-local state/docs/smoke artifacts.

This is active production WIP. Preserve exactly; do not absorb into generic repository cleanup.

### IM — IssueManager active feature lane

Includes Excel exporter and CreateIssueDialog/IssueManagerWindow source changes, plus migration of IssueManager plans/reports/smoke ownership.

Preserve separately from G and DB.

### ZS — ZoneSplit compile/governance lane

Includes `Antigravity.ZoneSplit.csproj` plus project-local durable docs/smoke ownership.

Preserve as its own bounded lane.

### HT — HoanThien test lane

The current tree contains modified HoanThien test project/tests and `tests/SETUP_LOG.md`. These changes are not part of the original G/DB/IM/ZS lane list and therefore must be treated as independent WIP until their owner/history is confirmed.

### L — Legacy / forensic lane

Examples include `.codex-bcf-sample/bcf.version`, root legacy probes and `lib/ClashNavigator.dll`.

`lib/ClashNavigator.dll` is a hard R1 publish blocker: the 2026-08-23 optimization plan explicitly says not to delete it from filename evidence alone and requires binary/manifest/deployment forensic review first.

## Transient/generated policy

The following are non-durable and must not be reintroduced into authoritative GitHub ownership:

- `.ai-bridge/`;
- `.agent/context/`, `.agent/state/`, `.agent/reports/`;
- `.agents/context/`;
- Python `__pycache__`, `*.pyc`, `*.pyo`;
- `**/bin/`, `**/obj/`, `TestResults/`;
- generated smoke-test results except `.gitkeep`;
- architecture renderer output listed in `.gitignore`;
- local deployment/package/log/temp artifacts.

Historical tracked deletions of these paths are expected cleanup evidence; they are not a reason to restore transient files.

## Publish gates

R1 is allowed to publish only after all of the following are true:

1. authenticated GitHub metadata confirms repository slug, visibility and default branch;
2. the intended landing branch is explicit;
3. the local ahead-one commit is identified and classified;
4. every changed path belongs to an approved lane;
5. `lib/ClashNavigator.dll` forensic status is resolved or its deletion is excluded from the landing;
6. DB, IM, ZS and HT WIP are not silently mixed into a governance commit;
7. ignored/generated paths are absent from staged durable content;
8. solution verification is green for the chosen landing set;
9. remote SHA is fetched before any push;
10. normal push is attempted first; any history replacement requires separate explicit approval and `--force-with-lease`, never plain `--force`.

## Landing sequence

Preferred order after gates are satisfied:

1. `R1-G` — governance/runtime retirement + durable repository structure;
2. `R1-B` — build dependency semantics;
3. `R1-DB` — DrawBeams feature snapshot only when its own project state is exact/verified;
4. `R1-IM` — IssueManager lane;
5. `R1-ZS` — ZoneSplit lane;
6. `R1-HT` — HoanThien test lane after ownership confirmation;
7. `R1-L` — legacy/forensic cleanup only after explicit evidence.

The sequence may be merged only if the same evidence proves the merged paths have one logical owner and rollback boundary.

## Verification baseline observed during R1

`dotnet.exe test Antigravity.sln` passed during this audit:

- DrawBeams: 45/45
- HoanThien: 6/6
- WallMepClash: 10/10
- Total: 61/61

This is repository health evidence only. It does not authorize landing mixed lanes and does not replace Revit smoke/LKG gates.

## R1 status definition

- `AUDIT_COMPLETE`: local branch, tree structure, lanes and blockers classified.
- `REMOTE_VERIFIED`: authenticated GitHub slug/visibility/default-branch evidence recorded.
- `LANDING_READY`: all publish gates satisfied for one bounded lane.
- `PUBLISHED`: bounded lane committed/pushed and remote SHA independently verified.

Current state: `AUDIT_COMPLETE / REMOTE_VERIFIED / AUTHORITATIVE_SNAPSHOT_PUBLISHED`.

After the audit, the user explicitly designated the current computer workspace as the authoritative source and authorized replacing the stale GitHub contents. R1 therefore published the complete durable local snapshot to GitHub `main`, including intentional local deletions, while excluding transient/generated/runtime content and failing closed on sensitive-path, credential-pattern, and oversized-file checks. The publish uses a temporary Git index and does not switch or clean the dirty local working branch.
