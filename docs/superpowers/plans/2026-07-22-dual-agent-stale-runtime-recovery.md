# Dual-Agent STALE/Timeout Recovery Plan

Date: 2026-07-22  
Status: Ready for implementation; not dual-agent reviewed because the active pipeline is STALE  
Task: `dual_agent_stale_runtime_recovery`

## Goal

Làm cho doctor, writer, reviewer, status và failure budget của dual-agent phản ánh đúng trạng thái thực, không để tiến trình timeout còn sống, manifest `RUNNING` vĩnh viễn hoặc pipeline tự làm stale snapshot của chính nó.

## Observed errors

| ID | Evidence | Error | Impact |
|---|---|---|---|
| DA-01 | Doctor trả `antigravity READY`, nhưng prompt `AGY_READY` và ba lượt writer đều timeout | Doctor chỉ chạy `agy models`, không kiểm tra inference | False-ready; orchestration gọi writer dù runtime không sinh được phản hồi |
| DA-02 | `agy` vẫn tồn tại sau khi shell timeout | Timeout không bảo đảm đóng toàn bộ process tree | Writer có thể tiếp tục ghi khi Codex bắt đầu, vi phạm single-writer |
| DA-03 | `review_run.json.status=RUNNING` từ 11:43, heartbeat không đổi, trong khi `dual_wait` trả terminal `STALE` | Manifest, report và status reader không được terminalize đồng bộ | `ReviewStatus: RUNNING`, `Status: STALE`, `PipelineStatus: BLOCKED` cùng lúc |
| DA-04 | Artifact snapshot phụ thuộc `get_deterministic_snapshot(source-code)`; dirty list chứa `.agents/context`, `.agents/runtime`, debug và test ngoài task | Artifact review hash toàn bộ dirty worktree thay vì nguồn liên quan | Pipeline/report thay đổi trong khi review có thể tự làm snapshot stale |
| DA-05 | Nhiều lần failure budget dùng cùng hypothesis `The plan artifact was ready for acceptance` và cùng fingerprint | Duplicate failure không bị khử theo snapshot/hypothesis | Budget bị đốt bởi blind retry, tạo nhiều `BLOCKED_HANDOFF` |
| DA-06 | `ROOT_CAUSE_HANDOFF.md` báo 3/3 nhưng `TASK_CONTEXT.json` hiện 2/3; manifest lại RUNNING | Nhiều nguồn trạng thái không có precedence/canonical reconciliation | Resume decision không đáng tin cậy |
| DA-07 | Writer có thể ghi artifact rồi treo ở bước trả stdout | Pipeline chỉ coi exit code là completion, không phân biệt artifact changed + process hung | Artifact mới tồn tại nhưng run vẫn INFRA_FAIL và tiến trình có thể chồng lấn |

## Scope

### IN

- Runtime probing và process cleanup cho Antigravity CLI.
- Terminalization/reconciliation cho stale Codex run.
- Artifact-review snapshot chỉ phụ thuộc HEAD, artifact và source files liên quan.
- Failure-budget deduplication và canonical status contract.
- Automated regression tests và cập nhật tài liệu pipeline.

### OUT

- Thay model/provider của Anti hoặc Codex.
- Nới lỏng scope/guardrail để ép pipeline pass.
- Reset/xóa report lịch sử của task Formwork.
- Sửa nội dung nghiệp vụ Automatic Formwork trong plan này.

## Target file map

- `.agents/runtime/dual_agent_runtime.py`: inference probe, managed process tree và writer completion evidence.
- `.agents/runtime/review_pipeline.py`: artifact snapshot boundary và stale terminalization helpers.
- `.agents/runtime/workflow_governance.py`: failure-attempt identity/deduplication và canonical task status.
- `.agents/runtime/harness.py`: doctor/run integration và terminal state persistence.
- `.agents/skills/dual-agent-pipeline/scripts/dual_status.ps1`: một effective status duy nhất.
- `.agents/skills/dual-agent-pipeline/scripts/dual_wait.ps1`: bounded wait sử dụng canonical status.
- `.agents/runtime/tests/test_dual_agent_runtime.py`: doctor/writer process lifecycle tests.
- `.agents/runtime/tests/test_review_pipeline_snapshot.py`: artifact snapshot isolation and stale-run tests.
- `.agents/runtime/tests/test_workflow_governance_failure_budget.py`: duplicate failure-budget tests.
- `.agents/runtime/tests/test_dual_status_contract.py`: cross-file status contract tests.
- `.agents/skills/dual-agent-pipeline/SKILL.md`: documented recovery semantics.
- `.agents/skills/dual-agent-pipeline/references/mode_contract.md`: terminal-state and retry contract.

### Task 1: Make doctor verify real Antigravity inference
**Files:** Modify `.agents/runtime/dual_agent_runtime.py`; Modify `.agents/runtime/harness.py`; Create/Test `.agents/runtime/tests/test_dual_agent_runtime.py`
**Steps:**
- [ ] Write failing test (RED): mock `agy models` exit 0 while a read-only `agy --print --mode plan` probe times out; assert doctor returns `INFERENCE_TIMEOUT`, `ready=false`.
- [ ] Run `python .agents/runtime/tests/test_dual_agent_runtime.py` → verify FAIL because `diagnose()` currently marks models-only success as READY.
- [ ] Write minimal implementation (GREEN): retain executable/auth discovery, then run a 30-second no-edit inference probe with exact sentinel output; classify `AUTH_REQUIRED`, `INFERENCE_TIMEOUT`, `BROKEN_OUTPUT`, or `READY`.
- [ ] Run `python .agents/runtime/tests/test_dual_agent_runtime.py` → verify PASS.
- [ ] Commit `fix(dual-agent): require inference-ready doctor probe`.

### Task 2: Kill the complete writer process tree on timeout
**Files:** Modify `.agents/runtime/dual_agent_runtime.py`; Test `.agents/runtime/tests/test_dual_agent_runtime.py`
**Steps:**
- [ ] Write failing test (RED): launch a parent test process with a long-lived child, trigger timeout, and assert both PIDs terminate and one terminal fixer report is written.
- [ ] Run `python .agents/runtime/tests/test_dual_agent_runtime.py` → verify FAIL because `subprocess.run(..., timeout=...)` does not provide the pipeline an explicit tree-cleanup contract.
- [ ] Write minimal implementation (GREEN): use `Popen`, a bounded `communicate`, Windows `taskkill /F /T /PID`, final `kill()` fallback, pipe drain, and `completed_at/reason_code` persistence in `FIXER_COMMAND_REPORT.json`.
- [ ] Run `python .agents/runtime/tests/test_dual_agent_runtime.py` → verify PASS and no orphan PID remains.
- [ ] Commit `fix(dual-agent): terminate timed-out writer trees`.

### Task 3: Terminalize stale Codex runs consistently
**Files:** Modify `.agents/runtime/review_pipeline.py`; Modify `.agents/runtime/harness.py`; Modify `.agents/skills/dual-agent-pipeline/scripts/dual_status.ps1`; Modify `.agents/skills/dual-agent-pipeline/scripts/dual_wait.ps1`; Create/Test `.agents/runtime/tests/test_dual_status_contract.py`
**Steps:**
- [ ] Write failing test (RED): create an expired RUNNING manifest plus BLOCKED report; assert status JSON must return `status=STALE`, `review_status=STALE`, `pipeline_status=STALE`, `terminal=true`, with a non-empty completion reason.
- [ ] Run `python .agents/runtime/tests/test_dual_status_contract.py` → verify FAIL on the current contradictory `STALE/RUNNING/BLOCKED` output.
- [ ] Write minimal implementation (GREEN): add one stale-reconciliation function that atomically writes manifest, task context, workflow state and dual report to terminal STALE; make status/wait consume that canonical state.
- [ ] Run `python .agents/runtime/tests/test_dual_status_contract.py` → verify PASS.
- [ ] Commit `fix(dual-agent): reconcile stale review state atomically`.

### Task 4: Isolate plan/research artifact snapshots from pipeline-owned dirt
**Files:** Modify `.agents/runtime/review_pipeline.py`; Modify/Test `.agents/runtime/tests/test_review_pipeline_snapshot.py`
**Steps:**
- [ ] Write failing test (RED): compute a plan artifact snapshot, mutate `.agents/runtime/__pycache__`, `.agents/factory`, reports and debug files, then assert the artifact snapshot is unchanged; mutate an included artifact or scoped source file and assert it changes.
- [ ] Run `python .agents/runtime/tests/test_review_pipeline_snapshot.py` → verify FAIL because `_artifact_snapshot()` currently incorporates the full dirty source hash.
- [ ] Write minimal implementation (GREEN): introduce artifact-mode source snapshot using HEAD plus explicit existing task-scope source files and approved artifact inputs; exclude pipeline state/report/cache paths without changing code/release scope evaluation.
- [ ] Run `python .agents/runtime/tests/test_review_pipeline_snapshot.py` → verify PASS.
- [ ] Commit `fix(dual-agent): stabilize artifact review snapshots`.

### Task 5: Deduplicate failure-budget attempts
**Files:** Modify `.agents/runtime/workflow_governance.py`; Create/Test `.agents/runtime/tests/test_workflow_governance_failure_budget.py`
**Steps:**
- [ ] Write failing test (RED): record the same stage, hypothesis, evidence and snapshot twice; assert `used` increments once. Then change snapshot or hypothesis and assert a new attempt is counted.
- [ ] Run `python .agents/runtime/tests/test_workflow_governance_failure_budget.py` → verify FAIL because `record_failed_attempt()` always appends.
- [ ] Write minimal implementation (GREEN): persist snapshot hash in each attempt and reject identical fingerprints for the same snapshot; return an explicit `DUPLICATE_ATTEMPT` result without altering remaining budget.
- [ ] Run `python .agents/runtime/tests/test_workflow_governance_failure_budget.py` → verify PASS.
- [ ] Commit `fix(dual-agent): preserve failure budget on duplicate retries`.

### Task 6: Integrate canonical recovery and update contracts
**Files:** Modify `.agents/skills/dual-agent-pipeline/SKILL.md`; Modify `.agents/skills/dual-agent-pipeline/references/mode_contract.md`; Test `.agents/runtime/tests/test_dual_agent_runtime.py`; Test `.agents/runtime/tests/test_review_pipeline_snapshot.py`; Test `.agents/runtime/tests/test_workflow_governance_failure_budget.py`; Test `.agents/runtime/tests/test_dual_status_contract.py`
**Steps:**
- [ ] Write failing test (RED): add an end-to-end fake-runtime case covering doctor READY → writer success → review timeout → STALE terminalization → changed snapshot + explicit hypothesis → one permitted retry.
- [ ] Run `python -m unittest discover -s .agents/runtime/tests -p "test_*.py"` → verify FAIL before integration.
- [ ] Write minimal implementation (GREEN): connect the tested helpers and document exact status precedence, writer completion contract and resume rule.
- [ ] Run `python -m unittest discover -s .agents/runtime/tests -p "test_*.py"` → verify PASS.
- [ ] Commit `test(dual-agent): gate stale recovery lifecycle`.

## Parallel detection

- Tasks 1 and 4 can run in parallel: separate files except their tests.
- Tasks 2 depends on Task 1 because both edit `dual_agent_runtime.py`.
- Task 3 can run parallel with Task 5.
- Task 6 runs only after Tasks 1–5 merge; no concurrent writer is allowed during integration.

## Acceptance gate

- Doctor cannot report READY when a sentinel inference cannot complete.
- Timeout leaves zero writer/reviewer child processes.
- No status output may combine terminal STALE with raw RUNNING/BLOCKED fields.
- Pipeline-owned report/cache changes do not stale plan/research review snapshots.
- Duplicate retry cannot consume failure budget.
- Full runtime unittest suite passes twice consecutively.
- Only after these pass may `automatic_formwork_mvp` be resumed with a changed artifact snapshot and an explicit falsifiable hypothesis.

