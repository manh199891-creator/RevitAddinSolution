# R1 — RevitAddinSolution Bounded Landing Manifest

Date: 2026-09-07
Status: ACTIVE GATE / NO BROAD LANDING
Repository: `manh199891-creator/RevitAddinSolution`
GitHub default branch: `main`
Current local branch: `hotfix/agy-probe-isolation`

## Purpose

This manifest prevents the current mixed working tree from being committed as one snapshot. Every durable changed path must belong to one bounded lane before landing. Paths not covered by an approved lane remain excluded.

## Global exclusions from all ordinary feature/governance landings

Never stage generated/transient content merely to make Git status quiet:

- `.ai-bridge/**`
- `.agent/context/**`
- `.agent/state/**`
- `.agent/reports/**` as content; historically tracked deletions may be handled only as transient-retirement cleanup
- `.agents/context/**` as runtime content
- `**/__pycache__/**`
- `**/*.pyc`, `**/*.pyo`
- `**/bin/**`, `**/obj/**`
- `**/TestResults/**`
- generated smoke `results/**` except required `.gitkeep`
- local deploy/package/log/temp outputs

## R1-G — governance / durable repository structure

Candidate scope only after path-by-path review:

- `.agents/AGENTS.md`
- `.agents/agents/**`
- `.agents/capability-profile.json`
- `.agents/skill-manifest.json`
- `.agents/policies/**`
- active `.agents/skills/**`
- retirement of obsolete `.agents/runtime/**` authority as documented by Phase 12 closure
- `.gitignore`
- repository `docs/architecture/**`, `docs/projects/**`, cross-solution governance plans/reports, `docs/standards/**`, `docs/templates/**`
- categorized governance/architecture/deploy/maintenance/verification scripts
- add-in-local `PROJECT.md`, `docs/**`, and smoke governance skeletons where no production feature source is included

R1-G explicitly excludes:

- `Directory.Build.props` -> R1-B
- all DrawBeams production/state paths -> R1-DB
- all IssueManager production paths and the ahead-one commit -> R1-IM
- ZoneSplit csproj -> R1-ZS
- HoanThien test modifications -> R1-HT
- legacy/forensic paths -> R1-L
- loose root/document deletions whose replacement pairing has not been proven -> REVIEW

## R1-B — build/dependency semantics

Allowed scope:

- `Directory.Build.props`
- any project file that must explicitly opt in to `UseNice3pointRevitExtensions`, but only after caller inventory proves it belongs to this same dependency change

Acceptance:

- solution restore/build/test PASS;
- pure contracts/core projects do not inherit Revit-only dependency accidentally;
- no unrelated source change staged.

## R1-DB — DrawBeams

Owner scope:

- `src/Antigravity.DrawBeams/**`
- directly owned `tests/Antigravity.DrawBeams.Tests/**` when required by the same DrawBeams phase

Must use DrawBeams durable `PROJECT_STATE.json`, ROADMAP, smoke/LKG and active phase evidence. No generic repository cleanup may absorb this lane.

## R1-IM — IssueManager

Owner scope:

- `src/Antigravity.IssueManager/**`
- IssueManager-owned migrated plans/reports/smoke artifacts
- local ahead commit `a1b06f11af425853bd988d66e30a367ac3e99b00`

The ahead commit subject is:

`fix(issue-manager): load BitmapImage safely to prevent locking when appending 2D images`

This commit is not governance history and must remain an IssueManager decision/landing.

## R1-ZS — ZoneSplit

Owner scope:

- `src/Antigravity.ZoneSplit/**`

The current csproj modification and project-local durable artifacts must be reviewed together only if they share one acceptance boundary.

## R1-HT — HoanThien status/index normalization

Top-level Git status reports modifications under `tests/Antigravity.HoanThien.Tests/**` and `tests/SETUP_LOG.md`, but scoped `git_diff`/`show_changes` checks return no content diff for those paths.

Therefore R1 does not treat HT as proven feature content. These paths remain excluded from bounded commits until the status/index discrepancy is normalized or explained; they must not be staged merely because top-level status reports `M`.

## R1-L — legacy / forensic

Includes at minimum:

- `.brain/**`
- `.codex-bcf-sample/**`
- `lib/**`
- `Workflow/**`
- `scratch/**`
- `source-code/**`
- root one-off probes/binaries/legacy scripts not yet paired to a durable replacement

Forensic result for this R1 pass:

- repository-wide text search finds no consumer of `ClashNavigator` outside governance/audit documents;
- deployment scripts contain no `lib/` reference;
- project-file `HintPath` inventory contains no `ClashNavigator` reference.

This is still insufficient to prove the binary semantically unused. R1 therefore resolves the immediate landing gate by marking `lib/ClashNavigator.dll` **EXCLUDED FROM R1-G/R1-B LANDING**. Its current working-tree deletion must not be staged with those lanes. Final archive/removal remains a separate R1-L forensic decision.

## REVIEW — not automatically classified

Loose root deletions, historical docs/spec changes, fixture changes and any path whose replacement/owner is not proven remain REVIEW. They may not be included by convenience just because a replacement-looking file exists elsewhere.

## Branch gate

GitHub default branch is `main`, while this dirty workspace is on `hotfix/agy-probe-isolation`. R1 must not switch branches in-place, because that risks mixing or losing the current WIP.

Any future landing must explicitly choose one of these safe models:

1. land the owning hotfix/feature lane to its existing remote branch, then merge through normal review; or
2. create/use an isolated clean worktree/clone based on `main` and selectively reproduce only the verified bounded lane.

The second model is preferred for R1-G/R1-B because it avoids changing branches under the mixed working tree.

## Visibility gate

GitHub currently reports this repository as `public`.

Public visibility is acceptable for read-only Monitor operation, but a privileged local self-hosted runner must not be attached under the same trust assumptions used for the private Local Orchestrator repository. Visibility/isolation must be decided before R2 runner rollout.

## Current decision

`R1 AUDIT_COMPLETE / REMOTE_VERIFIED / LANDING_MANIFEST_CREATED / NO BROAD COMMIT OR PUSH`
