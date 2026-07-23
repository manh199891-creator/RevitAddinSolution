# Automatic Formwork Plan Correction

Date: 2026-07-22  
Status: Blocked until dual-agent runtime recovery passes  
Depends on: `2026-07-22-dual-agent-stale-runtime-recovery.md`

## Goal

Sửa ba artifact plan để bám đúng Automatic Formwork spec đã approve và đủ chi tiết cho `/3.code`, sau đó chạy đúng một Codex plan review trên snapshot ổn định.

## Errors in the current Formwork plan

| ID | Current plan | Required correction |
|---|---|---|
| FW-01 | Biến sản phẩm thành grid panel 1000×2000 đơn giản | Khôi phục versioned catalog và system-specific placement rules |
| FW-02 | Không có cycle, preview, conflict validation, lock/manual update diff hoặc BOM CSV | Thiết kế toàn bộ lifecycle đã approve |
| FW-03 | `PLAN.md` chỉ có ba phase, không có Task/RED/GREEN/Commit | Viết lại theo format TDD bắt buộc của `/2.plan` |
| FW-04 | Plan sửa `verify.ps1`, nhưng file này không nằm trong `TASK_SCOPE.json` | Chỉ dùng project/test files được allowed; không mở rộng scope để pass |
| FW-05 | RTF path/template được ghi là “via install path or standard team directory” | Loại placeholder; dùng test seam thuần .NET và exact Revit test harness hiện có hoặc tách runtime QA có đường dẫn xác thực |
| FW-06 | Xóa panel bằng FamilyName + HostUniqueId | Dùng metadata đầy đủ `RunId/HostUniqueId/FaceKey/CycleId/SystemId/CatalogItemId/Locked/Manual` và deterministic diff |
| FW-07 | Shared parameter chỉ có `FormworkRunId` | Map rõ shared parameters cần schedule/filter và Extensible Storage/DataStorage cho run/config/catalog version |
| FW-08 | Acceptance dựa vào số panel cố định 12/8 | Kiểm tra deterministic layout, coverage ≥98%, overlap tolerance, classified gaps, metadata, idempotency và BOM reconciliation |
| FW-09 | Binary `FormworkPanel.rfa` được coi là một panel duy nhất embedded | Catalog phải map nhiều family/type theo stable ID và validate origin/orientation/parameter contract trước preview/commit |
| FW-10 | Không có UI/workbench hoặc transaction boundary preview→commit | Tách pure planning khỏi Revit mutation; preview không viết model, commit/update là transaction riêng có rollback |

## Artifact/file map

- `.agents/factory/RevitAddinSolution/.agent/context/PLAN.md`: implementation tasks and exact paths.
- `.agents/factory/RevitAddinSolution/.agent/context/TECHNICAL_DESIGN.md`: domain/Revit/catalog/update architecture.
- `.agents/factory/RevitAddinSolution/.agent/context/ACCEPTANCE_CRITERIA.md`: objective traceable gates.
- `docs/superpowers/plans/2026-07-22-automatic-formwork-implementation.md`: approved repository copy after Codex PASS.
- `.agents/runtime/tests/test_plan_artifact_contract.py`: machine contract preventing scope regression and placeholder plans.

### Task 1: Add an artifact contract test
**Files:** Create/Test `.agents/runtime/tests/test_plan_artifact_contract.py`; Read `.agents/factory/RevitAddinSolution/.agent/context/TASK_SCOPE.json`; Read `docs/superpowers/plans/2026-07-22-automatic-formwork-spec.md`
**Steps:**
- [ ] Write failing test (RED): assert plan has numbered Tasks with Files/RED/GREEN/PASS/Commit, every mentioned path is allowed, and approved capabilities `catalog`, `cycle`, `preview`, `Locked`, `Manual`, `BOM CSV`, deterministic update and coverage exist across the artifacts.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → verify FAIL against the current three artifacts.
- [ ] Write minimal implementation (GREEN): implement only the artifact validator and machine-readable failure messages; do not modify production Formwork code.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → keep FAIL until Tasks 2–4 correct the artifacts.
- [ ] Commit `test(plan): codify automatic formwork artifact contract`.

### Task 2: Rewrite PLAN.md into executable TDD slices
**Files:** Modify `.agents/factory/RevitAddinSolution/.agent/context/PLAN.md`; Test `.agents/runtime/tests/test_plan_artifact_contract.py`
**Steps:**
- [ ] Write failing test (RED): use Task 1 validator to report missing exact tasks and out-of-scope `verify.ps1`.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → verify FAIL with FW-01 through FW-05.
- [ ] Write minimal implementation (GREEN): define exact file paths and TDD tasks for project setup, immutable DTOs, catalog parser/validator, face/topology extraction, deterministic panel solver, junction rules, cycle service, validators, placement plan, preview UI, persistence/update diff, native placement, CSV and integration tests.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → verify PLAN section PASS while design/acceptance checks remain explicit.
- [ ] Commit `docs(plan): restore approved formwork implementation scope`.

### Task 3: Rewrite TECHNICAL_DESIGN.md
**Files:** Modify `.agents/factory/RevitAddinSolution/.agent/context/TECHNICAL_DESIGN.md`; Test `.agents/runtime/tests/test_plan_artifact_contract.py`
**Steps:**
- [ ] Write failing test (RED): assert immutable millimeter DTOs, stable FaceKey, JSON schema/version, solver objective/tie-breaks, L/T/X/stop-end rules, metadata/update diff, transaction rollback, CSV escaping and 100-wall budget are defined.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → verify FAIL against the current single-panel technical design.
- [ ] Write minimal implementation (GREEN): specify data contracts and algorithms without introducing curved/sloped walls, slab/shoring or structural certification.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → verify technical-design checks PASS.
- [ ] Commit `docs(design): define deterministic formwork architecture`.

### Task 4: Rewrite and trace acceptance criteria
**Files:** Modify `.agents/factory/RevitAddinSolution/.agent/context/ACCEPTANCE_CRITERIA.md`; Test `.agents/runtime/tests/test_plan_artifact_contract.py`
**Steps:**
- [ ] Write failing test (RED): require every criterion to reference a PLAN task and concrete automated/manual evidence.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → verify FAIL on current fixed-count-only criteria.
- [ ] Write minimal implementation (GREEN): add deterministic selection-order, ≥98% supported-face coverage, classified gaps, no out-of-tolerance overlap, metadata completeness, unchanged rerun empty diff, locked/manual preservation, exact BOM reconciliation, rollback and preview performance gates.
- [ ] Run `python .agents/runtime/tests/test_plan_artifact_contract.py` → verify all artifact-contract checks PASS.
- [ ] Commit `docs(acceptance): trace formwork gates to tests`.

### Task 5: Run one fresh Codex plan review
**Files:** Review `.agents/factory/RevitAddinSolution/.agent/context/PLAN.md`; Review `.agents/factory/RevitAddinSolution/.agent/context/TECHNICAL_DESIGN.md`; Review `.agents/factory/RevitAddinSolution/.agent/context/ACCEPTANCE_CRITERIA.md`; Create `docs/superpowers/plans/2026-07-22-automatic-formwork-implementation.md` only after PASS
**Steps:**
- [ ] Write failing test (RED): run `python .agents/runtime/tests/test_plan_artifact_contract.py` and refuse pipeline execution unless it passes and the snapshot differs from run `13a54373-745f-4ea9-b257-0d9d400b5e1b`.
- [ ] Run the contract test → verify PASS locally; verify current Codex gate is not yet PASS.
- [ ] Write minimal implementation (GREEN): invoke dual plan mode once with `-ResumeHypothesis "Restored approved catalog-cycle-preview-update-BOM scope and added machine artifact contract"`; do not use `-Force` or widen Allowed.
- [ ] Run `dual_wait.ps1 -Project RevitAddinSolution -TimeoutSeconds 300` → verify terminal `PASS`; if FAIL/STALE/INFRA_FAIL, stop and use the exact new evidence rather than retrying.
- [ ] Commit the repository plan copy only after Codex PASS.

## Execution order

Tasks 1 → 2 → 3 → 4 → 5 are sequential because each consumes the same three artifacts and Task 5 must review a frozen snapshot. No parallel writer is allowed for this plan.

## Gate

The Automatic Formwork implementation phase remains blocked until:

1. Dual-agent runtime recovery tests pass.
2. Artifact contract passes.
3. A fresh `review_run.json` and `CODEX_REVIEW.md` agree on task, run ID, snapshot hash, reviewed files and verdict `PASS`.

