"""
harness.py v2 — AI Software Factory Full Pipeline Orchestrator
Điều phối toàn bộ 8 bước workflow: status, next-step, codex, sync, commit
"""
import subprocess, sys, json, shutil, time, os
from pathlib import Path
from datetime import datetime

# Fix Windows console encoding
if sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")

FACTORY_ROOT = Path(os.environ.get("AI_SOFTWARE_FACTORY_ROOT", "E:/AI_SOFTWARE_FACTORY"))

PROJECT_ALIASES = {
    "revit": "RevitAddinSolution",
    "navis": "NavisAddinSolution",
    "trend": "TrendingUpdate",
}

SOURCE_DIR = {
    "RevitAddinSolution": Path("e:/Antigravity/RevitAddinSolution"),
    "NavisAddinSolution": Path("e:/Antigravity/NavisAddinSolution"),
    "TrendingUpdate":     Path("e:/Antigravity/TrendingUpdate"),
}

def load_profile(project_root):
    f = project_root / ".agent/project_profile.json"
    if f.exists():
        return json.loads(f.read_text(encoding="utf-8"))
    return {}

def load_project(alias_or_name):
    name = PROJECT_ALIASES.get(alias_or_name, alias_or_name)
    profile = load_profile(FACTORY_ROOT / name)
    return name, profile

STEPS = [
    ("01_context",     "Điền PROJECT_CONTEXT.md",               "context/PROJECT_CONTEXT.md"),
    ("02_plan",        "Claude Planner → PLAN.md",               "context/PLAN.md"),
    ("03_design",      "Claude Architect → TECHNICAL_DESIGN.md", "context/TECHNICAL_DESIGN.md"),
    ("04_implement",   "Gemini Implementer → code",              "reports/IMPLEMENTATION_REPORT.md"),
    ("05_qa",          "Gemini QA → QA_REPORT.md = PASS",       "reports/QA_REPORT.md"),
    ("06_codex",       "Codex review → CODEX_REVIEW.md = PASS", "reports/CODEX_REVIEW.md"),
    ("07_release",     "Release Agent → FINAL_REPORT.md",       "reports/FINAL_REPORT.md"),
    ("08_commit",      "Human review → Commit",                  None),
]

# ─── helpers ──────────────────────────────────────────────────────────────────

def ts(): return datetime.now().strftime("%H:%M:%S")
def log(msg): print(f"[{ts()}] {msg}")
def sep(): print("─" * 55)
def header(title): print(f"\n{'═'*55}\n  {title}\n{'═'*55}")

def run(cmd, cwd=None, capture=False):
    return subprocess.run(cmd, cwd=cwd, capture_output=capture,
                          text=True, encoding="utf-8", errors="replace")

from dual_agent_runtime import (
    build_handoff as build_agent_handoff,
    diagnose as diagnose_dual_runtime,
    find_codex as find_codex_runtime,
    run_antigravity_fixer,
    write_handoff as write_agent_handoff,
)
from evolution_pipeline import (
    build_dataset as build_learning_dataset,
    continuous_status as get_continuous_status,
    gate_candidate,
    label_outcome,
    record_canary_outcome,
    record_pipeline_outcome,
    shadow_evaluate,
    start_canary,
)
from learning_guard import build_learning_guard


def find_codex():
    return find_codex_runtime()

def load_state(project_root):
    f = project_root / ".agent/state/workflow_state.json"
    return json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}

def save_state(project_root, state):
    f = project_root / ".agent/state/workflow_state.json"
    f.write_text(json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8")


def save_dual_terminal_state(project_root, task_id, mode, status, next_step, reason=""):
    """Persist terminal dual status for runners that only read workflow state."""
    state = load_state(project_root)
    state["task_id"] = task_id
    state["dual_mode"] = mode
    state["dual_status"] = status.lower()
    state["next_step"] = next_step
    if reason:
        state["dual_reason"] = reason
    save_state(project_root, state)


def initialize_dual_run_state(project_root, task_id, feature, mode, next_step):
    """Reset task-facing run state so stale terminal evidence is never resumed."""
    state = load_state(project_root)
    state["task_id"] = task_id
    state["dual_mode"] = mode
    state["dual_status"] = "initialized"
    state["next_step"] = next_step
    state.pop("dual_reason", None)
    save_state(project_root, state)

    reports_dir = project_root / ".agent/reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    report = [
        "# DUAL_AGENT_REPORT.md",
        "",
        "## Status: NOT_STARTED",
        f"- task_id: {task_id}",
        f"- mode: {mode}",
        f"- feature: {feature}",
        f"- initialized_at: {datetime.now().isoformat()}",
        "- reason: Task initialized; no review has run yet.",
        "",
    ]
    (reports_dir / "DUAL_AGENT_REPORT.md").write_text(
        "\n".join(report), encoding="utf-8"
    )

from review_pipeline import (
    capture_task_baseline,
    evaluate_task_scope,
    run_codex_review,
    run_codex_artifact_review,
    ReviewStatus,
    get_artifact_snapshot,
    get_deterministic_snapshot,
    reconcile_stale_review,
)
from workflow_governance import (
    build_evidence_manifest,
    ensure_knowledge_layout,
    initialize_task_context,
    prepare_failure_budget_retry,
    record_failed_attempt,
    sha256_text,
    update_task_context,
    validate_fresh_evidence,
    write_later_finding,
)

def file_has_content(project_root, rel_path):
    f = project_root / ".agent" / rel_path
    return f.exists() and f.stat().st_size > 100

def get_status(project_root, rel_path, expected_task_id=None):
    if rel_path is None: return "─"
    if not file_has_content(project_root, rel_path): return "⬜ PENDING"
    content = (project_root / ".agent" / rel_path).read_text(encoding="utf-8", errors="replace").lower()
    if expected_task_id and rel_path.startswith("reports/"):
        task_lines = [line for line in content.splitlines() if "task_id:" in line]
        if not task_lines or not any(expected_task_id.lower() in line for line in task_lines):
            return "⚠️ STALE"
    if "status: pass" in content or "status: effective pass" in content or "status: n/a" in content: return "✅ PASS"
    if "status: fail" in content: return "❌ FAIL"
    return "📄 EXISTS"

import hashlib
def get_diff_hash(source_code):
    result = run(["git", "diff", "HEAD"], cwd=source_code, capture=True)
    diff = result.stdout.replace("\r\n", "\n")
    return hashlib.sha256(diff.encode("utf-8")).hexdigest()

def report_status_is(report_path, accepted_statuses, manifest=None):
    if not report_path.exists():
        return False
    content = report_path.read_text(encoding="utf-8", errors="replace")
    status = None
    for line in content.splitlines():
        stripped = line.strip()
        lower = stripped.lower()
        if lower.startswith("## status:") or lower.startswith("- status:") or lower.startswith("status:"):
            status = stripped.split(":", 1)[1].strip().upper()
            break
    if status not in {s.upper() for s in accepted_statuses}:
        return False
    if manifest:
        expected_task = manifest.get("task_id")
        expected_hash = manifest.get("snapshot_hash")
        if expected_task and expected_task not in content:
            return False
        if expected_hash and expected_hash not in content:
            return False
    return True

# ─── COMMAND: setup ──────────────────────────────────────────────────────────

def cmd_setup(proj_key, args):
    """Khởi tạo project mới từ PROJECT_ONBOARDING_SPEC.md"""
    header("SETUP — Khởi tạo Project Mới")
    spec_path = FACTORY_ROOT / "PROJECT_ONBOARDING_SPEC.md"
    if not spec_path.exists():
        print("  ❌ Thiếu E:\\AI_SOFTWARE_FACTORY\\PROJECT_ONBOARDING_SPEC.md")
        print("  Tạo file này và điền: Project Name, Alias, Ý tưởng")
        return

    import re
    spec = spec_path.read_text(encoding="utf-8")
    name_m  = re.search(r"Project Name.*?`(.+?)`", spec)
    alias_m = re.search(r"Alias.*?`(.+?)`", spec)
    idea_m  = re.search(r"[Yý] t[ưu][ởo]ng.*?\n>\s*`(.+?)`", spec, re.DOTALL | re.IGNORECASE)

    if not (name_m and alias_m):
        print("  ❌ Spec thiếu 'Project Name' hoặc 'Alias'. Hãy điền đủ.")
        return

    project_name = name_m.group(1).strip()
    alias        = alias_m.group(1).strip()
    idea         = idea_m.group(1).strip() if idea_m else "[Chưa có mô tả]"

    # Kiểm tra placeholder chưa được điền
    placeholders = ["Điền tên dự án", "vd:", "SmartTodoApp", "Điền từ khoá", "todo]"]
    for ph in placeholders:
        if ph.lower() in project_name.lower() or ph.lower() in alias.lower():
            print(f"  ❌ Spec vẫn còn giá trị mẫu chưa thay: '{project_name}' / '{alias}'")
            print("  Hãy mở PROJECT_ONBOARDING_SPEC.md và điền tên thực của dự án.")
            return

    print(f"  Project : {project_name}")
    print(f"  Alias   : {alias}")
    print(f"  Idea    : {idea[:100]}...")
    sep()

    project_root = FACTORY_ROOT / project_name
    for sub in [".agent/context", ".agent/reports", ".agent/state", "source-code"]:
        (project_root / sub).mkdir(parents=True, exist_ok=True)
        print(f"  ✅ {project_root / sub}")

    # project_profile.json — skeleton (Implementer sẽ điền lệnh thực tế)
    profile = {
        "project_name": project_name,
        "build_command": "echo 'TBD'",
        "test_command":  "echo 'TBD'",
        "lint_command":  "echo 'TBD'",
        "codex_review": {
            "timeout_seconds": 180,
            "max_diff_chars": 150000,
            "max_file_chars": 100000,
            "batch_max_files": 3,
            "batch_max_chars": 150000,
            "max_timeout_retries": 2
        },
        "review_routing": {
            "quick_max_files": 3,
            "quick_max_changed_lines": 50,
            "deep_min_files": 11,
            "deep_min_changed_lines": 301,
            "sensitive_paths": [
                "**/auth/**", "**/security/**", "**/permissions/**",
                "**/*secret*", "**/*credential*", "**/migrations/**"
            ]
        },
        "evidence_policy": {
            "required_for_gate": True,
            "required_stages": ["build", "test", "lint"]
        },
        "failure_budget": {"max_failed_attempts": 3},
        "dual_agents": {
            "topology": "single_writer_independent_verifier",
            "writer": "antigravity",
            "verifier": "codex",
            "fixer_provider": "antigravity",
            "auto_fix": False,
            "fixer_timeout_seconds": 900
        },
        "knowledge": {"capture_learning_on_pass": False},
        "runtime_validation": {"type": "manual", "required_for_gate": False},
        "stage_patterns": [], "allowed_scope": ["source-code"]
    }
    (project_root / ".agent/project_profile.json").write_text(
        json.dumps(profile, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"  ✅ project_profile.json (skeleton)")
    ensure_knowledge_layout(project_root)

    # PROJECT_CONTEXT.md
    ctx = (f"# PROJECT_CONTEXT.md\n\n## Task\n{idea}\n\n"
           f"## Scope\nToàn bộ project {project_name}\n\n"
           f"## Mục tiêu\nKhởi tạo và xây dựng tính năng đầu tiên theo mô tả trên.\n\n"
           f"## Files liên quan\n[AI Planner sẽ xác định]\n")
    (project_root / ".agent/context/PROJECT_CONTEXT.md").write_text(ctx, encoding="utf-8")
    print(f"  ✅ PROJECT_CONTEXT.md")

    # workflow_state.json
    state = {"project": project_name, "next_step": "01_context",
             "status": "in_progress", "retry_count": 0, "max_retry": 2}
    (project_root / ".agent/state/workflow_state.json").write_text(
        json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"  ✅ workflow_state.json")

    sep()
    print(f"  ✅ Setup '{project_name}' hoàn tất!")
    print(f"")
    print(f"  BƯỚC TIẾP THEO — Thêm alias vào harness.py dòng ~17:")
    print(f'      "{alias}": "{project_name}",  # Auto-setup')
    print(f"  Sau đó chạy: python harness.py {alias} status")
    sep()


# ─── COMMAND: status ──────────────────────────────────────────────────────────

def cmd_status(project_name, project_root):
    header(f"STATUS — {project_name}")
    state = load_state(project_root)
    current_step = state.get("next_step", "01_context")

    for i, (key, label, artifact) in enumerate(STEPS, 1):
        icon = get_status(project_root, artifact, state.get("task_id"))
        marker = " ◄ NEXT" if key == current_step else ""
        print(f"  [{i}] {icon:<14} {label}{marker}")

    sep()
    print(f"  retry_count : {state.get('retry_count', 0)} / {state.get('max_retry', 2)}")
    print(f"  next_step   : {state.get('next_step', '?')}")
    sep()

# ─── COMMAND: hash ────────────────────────────────────────────────────────────

def cmd_hash(project_name, project_root):
    source_code = project_root / "source-code"
    h, _ = get_deterministic_snapshot(source_code)
    state_dir = project_root / ".agent/state"
    state_dir.mkdir(parents=True, exist_ok=True)
    (state_dir / "current_diff_hash.txt").write_text(h, encoding="utf-8")
    print(f"Diff Hash: {h}")
    return h

# ─── COMMAND: verify ──────────────────────────────────────────────────────────

def cmd_verify(project_name, project_root):
    header(f"VERIFY (Build/Test/Lint) — {project_name}")
    _, profile = load_project(project_name)
    source_code = project_root / "source-code"
    reports_dir = project_root / ".agent/reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    scope_path = project_root / ".agent/context/TASK_SCOPE.json"
    scope = json.loads(scope_path.read_text(encoding="utf-8")) if scope_path.exists() else {}
    task_id = scope.get("task_id") or load_state(project_root).get("task_id") or "unknown"
    try:
        snapshot_before = evaluate_task_scope(
            source_code, scope_path, project_root / ".agent/state/task_baseline.json",
            expected_task_id=task_id,
        )["snapshot_hash"]
    except Exception as exc:
        snapshot_before = f"snapshot-error:{exc}"
    policy = profile.get("evidence_policy", {})
    configured_required = set(policy.get("required_stages", ["build", "test", "lint"]))
    evidence_results = []

    for stage, cmd_key, report_file in [
        ("BUILD", "build_command", "BUILD_REPORT.md"),
        ("TEST", "test_command", "TEST_REPORT.md"),
        ("LINT", "lint_command", "LINT_REPORT.md")
    ]:
        stage_key = stage.lower()
        cmd = profile.get(cmd_key)
        required = stage_key in configured_required and bool(cmd)
        started_at = datetime.now().astimezone().isoformat()
        if not cmd:
            (reports_dir / report_file).write_text(f"# {report_file}\n\n## Status: N/A\n", encoding="utf-8")
            print(f"  [{stage}] N/A (Not configured)")
            evidence_results.append({
                "stage": stage_key,
                "command": None,
                "required": False,
                "status": "N/A",
                "exit_code": None,
                "started_at": started_at,
                "completed_at": datetime.now().astimezone().isoformat(),
                "report": f".agent/reports/{report_file}",
                "output_sha256": sha256_text(""),
            })
            continue

        print(f"  [{stage}] Running: {cmd}")
        result = subprocess.run(cmd, cwd=source_code, capture_output=True, text=True, shell=True, encoding="utf-8", errors="replace")
        status = "PASS" if result.returncode == 0 else "FAIL"
        output = f"{result.stdout}\n{result.stderr}"
        report = (f"# {report_file}\n\n"
                  f"## Status: {status}\n\n"
                  f"## Command\n`{cmd}`\n\n"
                  f"## Exit Code\n{result.returncode}\n\n"
                  f"## Output\n```\n{output}\n```\n")
        (reports_dir / report_file).write_text(report, encoding="utf-8")
        evidence_results.append({
            "stage": stage_key,
            "command": cmd,
            "required": required,
            "status": status,
            "exit_code": result.returncode,
            "started_at": started_at,
            "completed_at": datetime.now().astimezone().isoformat(),
            "report": f".agent/reports/{report_file}",
            "output_sha256": sha256_text(output),
        })
        print(f"  [{stage}] {status}")
    try:
        snapshot_after = evaluate_task_scope(
            source_code, scope_path, project_root / ".agent/state/task_baseline.json",
            expected_task_id=task_id,
        )["snapshot_hash"]
    except Exception as exc:
        snapshot_after = f"snapshot-error:{exc}"
    evidence = build_evidence_manifest(
        project_root,
        task_id,
        snapshot_before,
        snapshot_after,
        evidence_results,
    )
    print(f"  [EVIDENCE] {evidence['status']} · run_id={evidence['run_id']}")
    sep()
    return evidence

# ─── COMMAND: guardrails ──────────────────────────────────────────────────────

def cmd_guardrails(project_name, project_root):
    header(f"GUARDRAILS CHECK — {project_name}")
    scope_file = project_root / ".agent/context/TASK_SCOPE.json"
    reports_dir = project_root / ".agent/reports"
    source_code = project_root / "source-code"
    
    scope = json.loads(scope_file.read_text(encoding="utf-8")) if scope_file.exists() else {}
    try:
        evaluation = evaluate_task_scope(
            source_code,
            scope_file,
            project_root / ".agent/state/task_baseline.json",
            expected_task_id=scope.get("task_id"),
            require_delta=scope.get("mode", "release") in {"code", "release"},
        )
    except Exception as exc:
        evaluation = {
            "status": ReviewStatus.INFRA_FAIL,
            "task_files": [],
            "excluded_preexisting_files": [],
            "issues": [f"SNAPSHOT_ERROR: {exc}"],
        }
    status = evaluation["status"]
    issue_lines = [f"- ❌ {issue}" for issue in evaluation["issues"]] or ["- ✅ Task delta is within scope."]
    task_lines = [f"- `{path}`" for path in evaluation["task_files"]] or ["- None"]
    excluded_lines = [f"- `{path}`" for path in evaluation["excluded_preexisting_files"]] or ["- None"]
    report = (
        f"# GUARDRAILS_REPORT.md\n\n## Status: {status}\n\n"
        "## Task Delta\n" + "\n".join(task_lines) + "\n\n"
        "## Excluded Pre-existing Dirty Files\n" + "\n".join(excluded_lines) + "\n\n"
        "## Issues\n" + "\n".join(issue_lines)
    )
    (reports_dir / "GUARDRAILS_REPORT.md").write_text(report, encoding="utf-8")
    for iss in issue_lines: print(f"  {iss}")
    for path in evaluation["excluded_preexisting_files"]:
        print(f"  ℹ️ BASELINE EXCLUDED: {path}")
    print(f"\n  → Status: {status}")
    sep()
    return evaluation

# ─── COMMAND: runtime ─────────────────────────────────────────────────────────

def cmd_runtime(project_name, project_root):
    header(f"RUNTIME VALIDATION — {project_name}")
    _, profile = load_project(project_name)
    rt = profile.get("runtime_validation", {})
    
    is_required = rt.get("required", False) or rt.get("required_for_gate", False)
    
    rt_type = rt.get("type", "manual_revit_test")
    report_rel_path = rt.get("report", ".agent/reports/RUNTIME_VALIDATION_REPORT.md")
    report_path = project_root / report_rel_path
    report_name = Path(report_rel_path).name
    
    print(f"  Type: {rt_type}")
    print(f"  Required for Gate: {is_required}")
    reports_dir = project_root / ".agent/reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    
    status = "FAIL"
    if rt_type == "manual_revit_test" or rt_type == "manual_navis_test":
        print("\n  👉 HƯỚNG DẪN TEST THỦ CÔNG:\n")
        print("  1. Load Add-in từ thư mục build")
        print("  2. Test các tính năng mới")
        ans = input("\n  Kết quả test (P=Pass, F=Fail): ").strip().lower()
        if ans == 'p': status = "PASS"
    elif rt_type == "ricaun_revit_test":
        print("  Đang chạy ricaun.RevitTest via dotnet test...")
        
        # Preflight check: Xem có InstallationLocation không để fast-fail (tránh đợi 3.5 phút)
        has_registry = False
        try:
            import winreg
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Autodesk\Revit\Autodesk Revit 2024") as key:
                install_loc, _ = winreg.QueryValueEx(key, "InstallationLocation")
                if install_loc:
                    has_registry = True
        except Exception:
            pass
            
        if not has_registry:
            print("  ⚠️ [PREFLIGHT] Không tìm thấy HKLM\\...\\InstallationLocation.")
            print("  ⚠️ Bỏ qua test để tránh treo 3-4 phút (INFRA_FAIL).")
            status = "FAIL (INFRA_FAIL)"
            output_str = "Preflight check failed: Missing InstallationLocation in HKLM registry. Please fix registry as per REVIT_TEST_SETUP.md."
        else:
            test_csproj = "source-code/tests/Antigravity.TagArranger.RevitTests/Antigravity.TagArranger.RevitTests.csproj"
            cmd = f"dotnet test {test_csproj} --arch x64 --logger \"trx;LogFileName=TestResults.trx\""
            print(f"  Chạy lệnh: {cmd}")
            try:
                result = subprocess.run(cmd, cwd=project_root, capture_output=True, text=True, shell=True, encoding="utf-8", errors="replace", timeout=600)
                if result.returncode == 0:
                    status = "PASS"
                else:
                    status = "FAIL"
                output_str = f"{result.stdout}\n{result.stderr}"
            except subprocess.TimeoutExpired as exc:
                status = "FAIL (TIMEOUT)"
                output_str = f"Lệnh test vượt quá 10 phút (600s).\n{exc}"
            
        report = f"# {report_name}\n\n## Status: {status}\n\n## Type: {rt_type}\n\n## Output\n```\n{output_str}\n```\n"
        report_path.write_text(report, encoding="utf-8")
        print(f"  → Status: {status}")
        sep()
        return

    report = f"# {report_name}\n\n## Status: {status}\n\n## Type: {rt_type}\n"
    report_path.write_text(report, encoding="utf-8")
    print(f"  → Status: {status}")
    sep()

# ─── COMMAND: gate ────────────────────────────────────────────────────────────

def cmd_gate(project_name, project_root):
    header(f"RELEASE GATE — {project_name}")
    _, profile = load_project(project_name)
    reports_dir = project_root / ".agent/reports"
    state_dir = project_root / ".agent/state"
    context_dir = project_root / ".agent/context"
    
    req = profile.get("release_requires", {})
    
    # 1. Build pass
    build_pass = False
    if req.get("build_pass"):
        build_pass = report_status_is(reports_dir / "BUILD_REPORT.md", ["PASS"]) or report_status_is(reports_dir / "IMPLEMENTATION_REPORT.md", ["PASS"])
    else:
        build_pass = True

    # 2. QA pass
    qa_pass = False
    if req.get("qa_pass"):
        qa_pass = report_status_is(reports_dir / "QA_REPORT.md", ["PASS", "EFFECTIVE PASS"])
    else:
        qa_pass = True
        
    # 3. Codex real review pass (Rigorous Checks)
    codex_pass = False
    codex_real = False
    schema_valid = False
    task_id_match = False
    hash_match = False
    gate_issues = []
    
    manifest_file = state_dir / "review_run.json"
    manifest = {}
    if manifest_file.exists():
        try:
            manifest = json.loads(manifest_file.read_text(encoding="utf-8"))
            # Check schema
            import jsonschema
            from review_pipeline import validate_scope
            schema_path = Path(__file__).parent / "schemas" / "review_run.schema.json"
            if not schema_path.exists():
                raise Exception(f"Missing schema: {schema_path}")
            schema = json.loads(schema_path.read_text(encoding="utf-8"))
            jsonschema.validate(instance=manifest, schema=schema)
            schema_valid = True
        except Exception:
            schema_valid = False
            
    # Check task scope task_id vs manifest task_id
    task_scope_file = context_dir / "TASK_SCOPE.json"
    scope = {}
    if task_scope_file.exists():
        try:
            scope = json.loads(task_scope_file.read_text(encoding="utf-8"))
            if scope.get("task_id") == manifest.get("task_id"):
                task_id_match = True
            else:
                gate_issues.append("TASK_SCOPE.json task_id does not match latest review manifest.")
        except Exception as e:
            gate_issues.append(f"TASK_SCOPE.json is invalid: {e}")
    else:
        gate_issues.append("TASK_SCOPE.json is missing.")

    # Diff hash match
    try:
        current_evaluation = evaluate_task_scope(
            project_root / "source-code",
            task_scope_file,
            project_root / ".agent/state/task_baseline.json",
            expected_task_id=scope.get("task_id"),
            require_delta=True,
        )
        current_hash = current_evaluation["snapshot_hash"]
        current_files = current_evaluation["task_files"]
    except:
        current_hash = ""
        current_files = []
        
    reviewed_hash = manifest.get("snapshot_hash", "")
    hash_match = (current_hash == reviewed_hash) and bool(current_hash)
    reviewed_files_match = current_files == manifest.get("included_files", [])

    evidence_policy = profile.get("evidence_policy", {})
    evidence_required = bool(evidence_policy.get("required_for_gate", False))
    required_evidence_stages = evidence_policy.get("required_stages", ["build", "test", "lint"])
    evidence_pass, evidence_issues, evidence_manifest = validate_fresh_evidence(
        project_root,
        scope.get("task_id") if task_scope_file.exists() and isinstance(scope, dict) else None,
        current_hash,
        required_evidence_stages,
    ) if evidence_required else (True, [], {})
    gate_issues.extend(evidence_issues)

    if req.get("codex_real_review_pass"):
        if manifest.get("status") == "PASS" and schema_valid and task_id_match and hash_match and reviewed_files_match:
            codex_pass = True
            if manifest.get("exit_code") == 0:
                codex_real = True
    else:
        codex_pass = True
        codex_real = True
        schema_valid = True
        task_id_match = True
        hash_match = True
        reviewed_files_match = True
        
    evidence_complete = not manifest.get("evidence_truncated", False)
    if not evidence_complete:
        gate_issues.append("Evidence was truncated (too large).")
        
    # 5. Runtime validation pass
    runtime_pass = False
    if req.get("runtime_validation_pass"):
        rt = profile.get("runtime_validation", {})
        is_rt_required = rt.get("required", False) or rt.get("required_for_gate", False)
        if not is_rt_required:
            runtime_pass = True
        else:
            report_rel_path = rt.get("report", ".agent/reports/RUNTIME_VALIDATION_REPORT.md")
            report_path = project_root / report_rel_path
            runtime_pass = report_status_is(report_path, ["PASS"])
    else:
        runtime_pass = True

    # 6. Guardrails
    guardrails_pass = False
    guardrails_pass = report_status_is(reports_dir / "GUARDRAILS_REPORT.md", ["PASS", "WARN"])

    batches = manifest.get("batches", [])
    
    def check_batch_pass(b):
        children = b.get("children")
        if children:
            return all(check_batch_pass(c) for c in children)
        return b.get("status") == "PASS"
        
    if batches:
        batch_complete = all(check_batch_pass(b) for b in batches)
    else:
        batch_complete = True
        
    if not batch_complete:
        gate_issues.append("One or more Codex review batches did not pass.")

    checks = [
        ("Build pass", req.get("build_pass", False), build_pass),
        ("QA pass", req.get("qa_pass", False), qa_pass),
        ("Schema & Scope Task Match", req.get("codex_real_review_pass", False), schema_valid and task_id_match),
        ("Codex real review", req.get("codex_real_review_pass", False), codex_pass and codex_real),
        ("Diff hash match", req.get("diff_hash_match", False), hash_match),
        ("Reviewed files match", req.get("codex_real_review_pass", False), reviewed_files_match),
        ("Batch reviews complete", req.get("codex_real_review_pass", False), batch_complete),
        ("Evidence complete", req.get("codex_real_review_pass", False), evidence_complete),
        ("Fresh verification evidence", evidence_required, evidence_pass),
        ("Runtime validation", req.get("runtime_validation_pass", False), runtime_pass),
        ("Guardrails pass", True, guardrails_pass),
    ]

    all_pass = all(c[2] for c in checks if c[1] or c[0] == "Guardrails pass")
    status = "ALLOW_RELEASE" if all_pass else "BLOCK_RELEASE"
    
    report = f"# RELEASE_GATE_REPORT.md\n\n## Status: {status}\n\n## Gate Checks\n\n| Check | Required | Status |\n|---|---|---|\n"
    for name, required, passed in checks:
        if not required and name != "Guardrails pass":
            st = "N/A"
        else:
            st = "PASS" if passed else "FAIL"
        report += f"| {name} | {'Yes' if required else 'No'} | {st} |\n"
        print(f"  {name:<25}: {st}")
        
    report += f"\n## Decision\n- {status}\n"
    if gate_issues:
        report += "\n## Gate Issues\n"
        print("\n  ⚠️ Các vấn đề phát hiện:")
        for issue in gate_issues:
            report += f"- {issue}\n"
            print(f"    - {issue}")
    (reports_dir / "RELEASE_GATE_REPORT.md").write_text(report, encoding="utf-8")
    
    sep()
    if status == "ALLOW_RELEASE":
        print("  ✅ ALLOW_RELEASE")
        return True
    else:
        print("  ❌ BLOCK_RELEASE — Hãy fix các lỗi trên trước khi release.")
        sys.exit(1)

# ─── COMMAND: codex ───────────────────────────────────────────────────────────

def cmd_codex(project_name, project_root, *args):
    header(f"CODEX REVIEW — {project_name}")
    source_code = project_root / "source-code"
    reports_dir = project_root / ".agent/reports"

    codex_path = find_codex()
    if not codex_path:
        print("❌ Không tìm thấy Codex CLI!")
        print("   Cài: npm install -g @openai/codex   hoặc cài từ openai.com/codex")
        return

    log(f"Codex: {codex_path}")

    task_id = None
    requested_feature = None
    review_purpose = "release"
    cycle = 1
    max_cycles = 1
    for i, a in enumerate(args):
        if a == "--task-id" and i + 1 < len(args):
            task_id = args[i+1]
        if a == "--feature" and i + 1 < len(args):
            requested_feature = args[i+1]
        if a == "--review-purpose" and i + 1 < len(args):
            review_purpose = args[i+1]
        if a == "--cycle" and i + 1 < len(args):
            cycle = int(args[i+1])
        if a == "--max-cycles" and i + 1 < len(args):
            max_cycles = int(args[i+1])
            
    if not task_id:
        state = load_state(project_root)
        task_id = state.get("task_id", "unknown_task_id")

    feature_name = requested_feature or "Autodetected Feature"
    
    ctx = project_root / ".agent/context/PROJECT_CONTEXT.md"
    if not requested_feature and ctx.exists():
        for line in ctx.read_text(encoding="utf-8").splitlines():
            if "Task" in line or "Idea" in line or "Feature" in line:
                feature_name = line.strip()
                break

    log("Chạy codex review (Strict Mode)...")
    print()
    manifest = run_codex_review(
        project_root, task_id, feature_name, codex_path,
        review_purpose=review_purpose,
        cycle=cycle,
        max_cycles=max_cycles
    )
    
    status = manifest.get("status")

    # Cập nhật state
    state = load_state(project_root)
    state["codex_status"] = status.lower() if status else "infra_fail"
    if status in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES):
        state["next_step"] = "07_release"
        state["current_agent"] = "codex_done"
        state["retry_count"] = 0
    elif status == ReviewStatus.FAIL:
        state["retry_count"] = state.get("retry_count", 0) + 1
        max_retry = state.get("max_retry", 2)
        if state["retry_count"] > max_retry:
            print(f"  ❌ Vượt quá max_retry ({state['retry_count']}/{max_retry}). Pipeline BLOCKED.")
            state["next_step"] = "blocked"
        else:
            state["next_step"] = "gemini_fixer"
    else:
        state["next_step"] = "blocked" # Wait for manual fix
        
    save_state(project_root, state)

    sep()
    if status in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES):
        print("  ✅ CODEX PASS")
        print("  → Bước tiếp: python harness.py revit next")
    elif status == ReviewStatus.FAIL:
        print("  ❌ CODEX FAIL — Issues tìm thấy")
        print("  → Xem chi tiết tại: .agent/reports/CODEX_REVIEW.md")
        if state["next_step"] == "blocked":
            print("  → Pipeline bị khóa do vượt quá max retry.")
        else:
            print("  → Chạy Gemini Fixer để sửa.")
    else:
        print(f"  ⚠️ CODEX REVIEW {status}: {manifest.get('reason')}")
        print("  → Vui lòng kiểm tra infra hoặc scope.")
    sep()

# ─── COMMAND: next ────────────────────────────────────────────────────────────

def get_changed_files_for_scope(project_root):
    try:
        _, files = get_deterministic_snapshot(project_root / "source-code")
        return files
    except Exception:
        return []

def parse_dual_args(args):
    opts = {
        "task_id": None,
        "feature": "Dual Agent Pipeline",
        "mode": "release",
        "artifacts": [],
        "max_cycles": 1,
        "skip_verify": False,
        "no_autoscope": False,
        "fix_command": None,
        "resume_hypothesis": None,
    }
    i = 0
    while i < len(args):
        arg = args[i]
        if arg == "--task-id" and i + 1 < len(args):
            opts["task_id"] = args[i + 1]
            i += 2
        elif arg == "--feature" and i + 1 < len(args):
            opts["feature"] = args[i + 1]
            i += 2
        elif arg == "--mode" and i + 1 < len(args):
            opts["mode"] = args[i + 1].lower()
            i += 2
        elif arg == "--artifacts" and i + 1 < len(args):
            opts["artifacts"].extend(split_list_arg(args[i + 1]))
            i += 2
        elif arg == "--max-cycles" and i + 1 < len(args):
            val = int(args[i + 1])
            if val < 1 or val > 3:
                raise ValueError("max_cycles must be between 1 and 3")
            opts["max_cycles"] = val
            i += 2
        elif arg == "--skip-verify":
            opts["skip_verify"] = True
            i += 1
        elif arg == "--no-autoscope":
            opts["no_autoscope"] = True
            i += 1
        elif arg == "--fix-command" and i + 1 < len(args):
            opts["fix_command"] = args[i + 1]
            i += 2
        elif arg == "--resume-hypothesis" and i + 1 < len(args):
            opts["resume_hypothesis"] = args[i + 1]
            i += 2
        else:
            i += 1
    return opts

def split_list_arg(value):
    if not value:
        return []
    return [item.strip() for item in value.replace(";", ",").split(",") if item.strip()]

def parse_dual_init_args(args):
    opts = {
        "task_id": None,
        "feature": None,
        "mode": "release",
        "artifacts": [],
        "allowed": [],
        "forbidden": [],
        "force": False,
    }
    i = 0
    while i < len(args):
        arg = args[i]
        if arg == "--task-id" and i + 1 < len(args):
            opts["task_id"] = args[i + 1]
            i += 2
        elif arg == "--feature" and i + 1 < len(args):
            opts["feature"] = args[i + 1]
            i += 2
        elif arg == "--mode" and i + 1 < len(args):
            opts["mode"] = args[i + 1].lower()
            i += 2
        elif arg == "--artifacts" and i + 1 < len(args):
            opts["artifacts"].extend(split_list_arg(args[i + 1]))
            i += 2
        elif arg == "--allowed" and i + 1 < len(args):
            opts["allowed"].extend(split_list_arg(args[i + 1]))
            i += 2
        elif arg == "--forbidden" and i + 1 < len(args):
            opts["forbidden"].extend(split_list_arg(args[i + 1]))
            i += 2
        elif arg == "--force":
            opts["force"] = True
            i += 1
        else:
            i += 1
    return opts

def write_context_file(path, content, force=False):
    if path.exists() and not force:
        return False
    path.write_text(content, encoding="utf-8")
    return True

VALID_DUAL_MODES = {"research", "plan", "code", "release"}

DEFAULT_MODE_ARTIFACTS = {
    "research": ["RESEARCH.md"],
    "plan": ["PLAN.md", "TECHNICAL_DESIGN.md", "ACCEPTANCE_CRITERIA.md"],
    "code": ["PLAN.md", "TECHNICAL_DESIGN.md", "ACCEPTANCE_CRITERIA.md"],
    "release": ["PLAN.md", "TECHNICAL_DESIGN.md", "ACCEPTANCE_CRITERIA.md"],
}

def normalize_dual_mode(mode):
    normalized = (mode or "release").lower()
    if normalized not in VALID_DUAL_MODES:
        raise ValueError(f"Unsupported mode '{mode}'. Expected one of: {', '.join(sorted(VALID_DUAL_MODES))}")
    return normalized

def build_default_task_context(project_name, task_id, feature, mode="release"):
    mode = normalize_dual_mode(mode)
    plan = f"""# PLAN.md - {task_id}

## Goal

{feature}

## Pipeline Contract

- Antigravity implements the feature only inside `TASK_SCOPE.json`.
- Codex reviews the exact snapshot.
- Pipeline mode: `{mode}`.
- Release is blocked until build/QA/Codex/gate pass when mode is `release`.

## Phases

1. Inspect current code and identify impacted files.
2. Implement the smallest scoped change.
3. Add or update tests proportional to risk.
4. Run verify, guardrails, Codex review, and release gate.
5. If Codex fails, fix only the reported issues and rerun dual.
"""
    design = f"""# TECHNICAL_DESIGN.md - {task_id}

## Feature

{feature}

## Scope

The authoritative file scope is `.agent/context/TASK_SCOPE.json`.

## Implementation Rules

- Keep changes inside `allowed_files`.
- Do not edit `forbidden` paths.
- Preserve existing project architecture and naming.
- Avoid broad refactors unless required by the feature.
- Add focused tests where the project has a test surface.

## Review Rules

Codex must verify:

- changed files match task scope;
- implementation matches this design and acceptance criteria;
- tests or validation are meaningful;
- no unrelated changes are included.
"""
    ac = f"""# ACCEPTANCE_CRITERIA.md - {task_id}

## AC-01 - Scope

All changed files must be allowed by `.agent/context/TASK_SCOPE.json`.

## AC-02 - Build/Test

Project `verify` command must pass according to `.agent/project_profile.json`.

## AC-03 - Feature Behavior

The implemented behavior must satisfy:

{feature}

## AC-04 - Codex Review

Codex review must return `PASS` for the exact run id, snapshot hash, and reviewed file list.

## AC-05 - Release Gate

Release gate must return `ALLOW_RELEASE`.
"""
    project_context = f"""# PROJECT_CONTEXT.md - {task_id}

## Project

{project_name}

## Task

{feature}

## Pipeline Mode

{mode}

## Workflow

Use:

```powershell
python E:\\AI_SOFTWARE_FACTORY\\harness.py {project_name} dual --task-id {task_id} --feature "{feature}" --mode {mode} --max-cycles 2
```
"""
    research = f"""# RESEARCH.md - {task_id}

## Question

{feature}

## Repository Evidence

Document relevant files, symbols, constraints, and current behavior with precise paths.

## External Evidence

Record sources and access dates when external research is required. Separate sourced facts from inference.

## Options And Tradeoffs

Compare viable options, risks, assumptions, and rejected alternatives.

## Recommendation

State the recommended direction, confidence, open questions, and validation needed before implementation.
"""
    files = {"PROJECT_CONTEXT.md": project_context}
    if mode == "research":
        files["RESEARCH.md"] = research
    else:
        files.update({
            "PLAN.md": plan,
            "TECHNICAL_DESIGN.md": design,
            "ACCEPTANCE_CRITERIA.md": ac,
        })
    return files

def cmd_dual_init(project_name, project_root, *args):
    header(f"DUAL INIT — {project_name}")
    _, profile = load_project(project_name)
    opts = parse_dual_init_args(list(args))
    try:
        mode = normalize_dual_mode(opts["mode"])
    except ValueError as e:
        print(f"  ERROR: {e}")
        sys.exit(1)
    if not opts["task_id"] or not opts["feature"]:
        print("  ❌ Missing required args: --task-id and --feature")
        sys.exit(1)

    context_dir = project_root / ".agent/context"
    state_dir = project_root / ".agent/state"
    context_dir.mkdir(parents=True, exist_ok=True)
    state_dir.mkdir(parents=True, exist_ok=True)

    scope_path = context_dir / "TASK_SCOPE.json"
    existing = {}
    if scope_path.exists():
        existing = json.loads(scope_path.read_text(encoding="utf-8"))
        if existing.get("task_id") != opts["task_id"] and not opts["force"]:
            print(f"  ERROR: Existing TASK_SCOPE.json belongs to {existing.get('task_id')}. Use --force to replace it.")
            sys.exit(1)

    same_task = existing.get("task_id") == opts["task_id"]
    allowed = opts["allowed"] or (existing.get("allowed_files", []) if same_task else []) or profile.get("stage_patterns", ["**/*"])
    if same_task and not opts["force"]:
        forbidden = list(existing.get("forbidden", []))
    else:
        forbidden = [".git/**", ".agent/state/**", ".agent/reports/**", "bin/**", "obj/**"]
        forbidden.extend(profile.get("dual_forbidden", []))
    forbidden.extend(item for item in opts["forbidden"] if item not in forbidden)
    forbidden = list(dict.fromkeys(forbidden))

    artifact_files = opts["artifacts"] or DEFAULT_MODE_ARTIFACTS[mode]
    scope = {
        "schema_version": 1,
        "task_id": opts["task_id"],
        "mode": mode,
        "repository_root": str(project_root / "source-code"),
        "allowed_files": allowed,
        "forbidden": forbidden,
        "artifact_files": artifact_files,
        "plan_files": [".agent/context/PLAN.md", ".agent/context/TECHNICAL_DESIGN.md"],
        "acceptance_criteria_files": [".agent/context/ACCEPTANCE_CRITERIA.md"],
        "implementation_report_files": [".agent/reports/IMPLEMENTATION_REPORT.md", ".agent/reports/FIX_REPORT.md"]
    }
    scope_path.write_text(json.dumps(scope, indent=2, ensure_ascii=False), encoding="utf-8")
    baseline_path = state_dir / "task_baseline.json"
    if opts["force"] or not same_task or not baseline_path.exists():
        capture_task_baseline(project_root, opts["task_id"])
    ensure_knowledge_layout(project_root)
    try:
        snapshot_hash, snapshot_files = get_deterministic_snapshot(project_root / "source-code")
    except Exception as exc:
        snapshot_hash, snapshot_files = f"snapshot-error:{exc}", []
    initialize_task_context(
        project_root,
        project_name,
        opts["task_id"],
        opts["feature"],
        mode,
        scope,
        snapshot_hash,
        snapshot_files,
        profile,
        force=opts["force"],
    )
    learning_guard = build_learning_guard(project_root, opts["task_id"], opts["feature"], mode)

    context_files = build_default_task_context(project_name, opts["task_id"], opts["feature"], mode)
    wrote = []
    for name, content in context_files.items():
        if write_context_file(context_dir / name, content, force=opts["force"]):
            wrote.append(name)

    next_step = {
        "research": "research_artifact",
        "plan": "plan_artifacts",
        "code": "04_implement",
        "release": "06_codex",
    }[mode]
    initialize_dual_run_state(
        project_root,
        opts["task_id"],
        opts["feature"],
        mode,
        next_step,
    )

    print(f"  ✅ TASK_SCOPE.json ready for {opts['task_id']}")
    print("  ✅ TASK_CONTEXT.json ready (resume + evidence + failure budget)")
    print(f"  ✅ allowed_files: {len(allowed)}")
    print(f"  ✅ forbidden: {len(forbidden)}")
    if wrote:
        print(f"  ✅ wrote context: {', '.join(wrote)}")
    else:
        print("  ℹ️ Context files already existed; use --force to replace.")
    sep()

def ensure_task_scope(project_root, profile, task_id, allow_autoscope=True, mode="release", artifacts=None):
    context_dir = project_root / ".agent/context"
    context_dir.mkdir(parents=True, exist_ok=True)
    scope_file = context_dir / "TASK_SCOPE.json"
    source_code = project_root / "source-code"

    if scope_file.exists():
        scope = json.loads(scope_file.read_text(encoding="utf-8"))
        if scope.get("task_id") != task_id:
            raise Exception(f"TASK_SCOPE.json task_id mismatch. Expected {task_id}, found {scope.get('task_id')}")
        return scope

    if not allow_autoscope:
        raise Exception("TASK_SCOPE.json is missing and --no-autoscope was provided.")

    changed_files = get_changed_files_for_scope(project_root)
    allowed_files = changed_files if changed_files else profile.get("stage_patterns", ["**/*"])
    scope = {
        "schema_version": 1,
        "task_id": task_id,
        "mode": mode,
        "repository_root": str(source_code),
        "allowed_files": allowed_files,
        "forbidden": [".git/**", ".agent/state/**", ".agent/reports/**", "bin/**", "obj/**"],
        "artifact_files": artifacts or DEFAULT_MODE_ARTIFACTS[mode],
        "plan_files": [".agent/context/PLAN.md", ".agent/context/TECHNICAL_DESIGN.md"],
        "acceptance_criteria_files": [".agent/context/ACCEPTANCE_CRITERIA.md"],
        "implementation_report_files": [".agent/reports/IMPLEMENTATION_REPORT.md"]
    }
    scope_file.write_text(json.dumps(scope, indent=2, ensure_ascii=False), encoding="utf-8")
    return scope

def write_dual_report(project_root, task_id, feature, status, steps, reason="", mode="release"):
    reports_dir = project_root / ".agent/reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    report = [
        "# DUAL_AGENT_REPORT.md",
        "",
        f"## Status: {status}",
        f"- task_id: {task_id}",
        f"- mode: {mode}",
        f"- feature: {feature}",
        f"- completed_at: {datetime.now().isoformat()}",
        f"- reason: {reason or status}",
        "",
        "## Steps",
        "",
        "| Step | Status | Detail |",
        "|---|---|---|",
    ]
    for step in steps:
        report.append(f"| {step['name']} | {step['status']} | {step.get('detail', '')} |")
    (reports_dir / "DUAL_AGENT_REPORT.md").write_text("\n".join(report) + "\n", encoding="utf-8")
    # P2 observability: append-only learning evidence. A broken recorder must
    # never mask the authoritative pipeline result.
    try:
        record_pipeline_outcome(
            project_root,
            task_id=task_id,
            feature=feature,
            mode=mode,
            status=status,
            steps=steps,
            reason=reason or status,
        )
    except Exception as exc:
        log(f"Learning trajectory warning: {exc}")


def _record_blocked_verify(project_root, task_id, feature, mode, steps, reason):
    """Persist one terminal BLOCKED_VERIFY result for every state reader."""
    save_dual_terminal_state(
        project_root, task_id, mode, "BLOCKED_VERIFY", "verify", reason,
    )
    write_dual_report(
        project_root, task_id, feature, "BLOCKED_VERIFY", steps, reason, mode,
    )
    update_task_context(
        project_root,
        status="blocked_verify",
        resume_cursor="verify",
        event_type="verify_failed",
        detail=reason,
    )


def _record_blocked_guardrails(
    project_root, task_id, feature, mode, steps, guardrail_status,
):
    """Persist the exact terminal guardrail result before secondary evidence."""
    reason = f"Guardrails failed: {guardrail_status}"
    save_dual_terminal_state(
        project_root, task_id, mode, guardrail_status, "guardrails", reason,
    )
    write_dual_report(
        project_root, task_id, feature, guardrail_status, steps, reason, mode,
    )
    update_task_context(
        project_root,
        status=guardrail_status.lower(),
        resume_cursor="guardrails",
        event_type="guardrails_failed",
        detail=reason,
    )

def write_fixer_handoff(project_name, project_root, task_id, feature, cycle, reason, mode="release", review_manifest=None):
    reports_dir = project_root / ".agent/reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    content = f"""# FIXER_HANDOFF.md

## Status: NEEDS_FIX
- task_id: {task_id}
- feature: {feature}
- cycle: {cycle}
- reason: {reason}

## Instructions For Antigravity Fixer

Read these files:

1. `.agent/reports/CODEX_REVIEW.md`
2. `.agent/context/MEMORY_CONTEXT.md`
3. `.agent/context/PLAN.md`
4. `.agent/context/TECHNICAL_DESIGN.md`
5. `.agent/context/ACCEPTANCE_CRITERIA.md`
6. `.agent/context/TASK_SCOPE.json`

Fix only the issues listed by Codex and only within `TASK_SCOPE.json`.

After fixing, run:

```powershell
python E:\\AI_SOFTWARE_FACTORY\\harness.py {project_name} dual --task-id {task_id} --feature "{feature}" --mode {mode}
```
"""
    (reports_dir / "FIXER_HANDOFF.md").write_text(content, encoding="utf-8")
    manifest = review_manifest or {}
    handoff = build_agent_handoff(project_root, task_id, feature, cycle, mode, manifest)
    write_agent_handoff(project_root, handoff)
    return handoff

def run_fixer_command(project_root, command):
    if not command:
        return False, "No fixer command configured"
    result = subprocess.run(command, cwd=project_root, capture_output=True, text=True, shell=True, encoding="utf-8", errors="replace")
    reports_dir = project_root / ".agent/reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    report = f"# FIXER_COMMAND_REPORT.md\n\n## Status: {'PASS' if result.returncode == 0 else 'FAIL'}\n\n## Command\n`{command}`\n\n## Output\n```\n{result.stdout}\n{result.stderr}\n```\n"
    (reports_dir / "FIXER_COMMAND_REPORT.md").write_text(report, encoding="utf-8")
    return result.returncode == 0, f"exit_code={result.returncode}"

def cmd_dual(project_name, project_root, *args):
    header(f"DUAL AGENT PIPELINE — {project_name}")
    _, profile = load_project(project_name)
    opts = parse_dual_args(list(args))
    try:
        mode = normalize_dual_mode(opts["mode"])
    except ValueError as e:
        print(f"  ERROR: {e}")
        sys.exit(1)
    state = load_state(project_root)
    task_id = opts["task_id"] or state.get("task_id") or f"{project_name}_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    feature = opts["feature"]
    fix_command = opts["fix_command"] or profile.get("fixer_command") or ""
    dual_agents = profile.get("dual_agents", {})
    antigravity_auto_fix = (
        dual_agents.get("fixer_provider") == "antigravity"
        and bool(dual_agents.get("auto_fix", False))
    )
    has_fixer = bool(fix_command) or antigravity_auto_fix
    steps = []

    state["task_id"] = task_id
    state["dual_mode"] = mode
    state["dual_status"] = "running"
    state.pop("dual_reason", None)
    save_state(project_root, state)

    try:
        scope = ensure_task_scope(
            project_root,
            profile,
            task_id,
            allow_autoscope=not opts["no_autoscope"],
            mode=mode,
            artifacts=opts["artifacts"],
        )
        scope_mode = scope.get("mode", "release")
        if scope_mode != mode:
            raise Exception(f"TASK_SCOPE.json mode mismatch. Expected {mode}, found {scope_mode}")
        artifact_files = None
        if mode in {"research", "plan"}:
            artifact_files = opts["artifacts"] or scope.get("artifact_files") or DEFAULT_MODE_ARTIFACTS[mode]
            preflight_snapshot_hash, _ = get_artifact_snapshot(project_root, mode, artifact_files)
        else:
            preflight_scope = evaluate_task_scope(
                project_root / "source-code",
                project_root / ".agent/context/TASK_SCOPE.json",
                project_root / ".agent/state/task_baseline.json",
                expected_task_id=task_id,
            )
            preflight_snapshot_hash = preflight_scope["snapshot_hash"]
        budget_ready, budget_detail = prepare_failure_budget_retry(
            project_root,
            preflight_snapshot_hash,
            opts["resume_hypothesis"],
        )
        if not budget_ready:
            steps.append({"name": "failure_budget_preflight", "status": "BLOCKED_HANDOFF", "detail": budget_detail})
            write_dual_report(project_root, task_id, feature, "BLOCKED_HANDOFF", steps, budget_detail, mode)
            save_dual_terminal_state(
                project_root, task_id, mode, "BLOCKED_HANDOFF",
                "root_cause_handoff", budget_detail,
            )
            print(f"  ❌ DUAL BLOCKED_HANDOFF: {budget_detail}")
            sys.exit(1)
        try:
            initial_hash, initial_files = get_deterministic_snapshot(project_root / "source-code")
        except Exception as exc:
            initial_hash, initial_files = f"snapshot-error:{exc}", []
        initialize_task_context(
            project_root,
            project_name,
            task_id,
            feature,
            mode,
            scope,
            initial_hash,
            initial_files,
            profile,
        )
        learning_guard = build_learning_guard(project_root, task_id, feature, mode)
        update_task_context(
            project_root,
            status="running",
            resume_cursor="scope_ready",
            event_type="pipeline_started",
            detail=f"Dual pipeline started in {mode} mode; learning guard matched {learning_guard.get('matched_rule_count', 0)} rules",
        )
        steps.append({"name": "learning_guard", "status": "PASS", "detail": f"{learning_guard.get('matched_rule_count', 0)} matched rules"})
        steps.append({"name": "scope", "status": "PASS", "detail": f"{len(scope.get('allowed_files', []))} allowed patterns/files"})
        log(f"TASK_SCOPE ready: {task_id}")
    except Exception as e:
        steps.append({"name": "scope", "status": "FAIL", "detail": str(e)})
        write_dual_report(project_root, task_id, feature, "BLOCKED", steps, str(e), mode)
        print(f"  ❌ DUAL BLOCKED: {e}")
        sys.exit(1)

    if mode in {"research", "plan"}:
        codex_path = find_codex()
        if not codex_path:
            steps.append({"name": "codex_artifact_review", "status": "INFRA_FAIL", "detail": "Codex CLI not found"})
            write_dual_report(project_root, task_id, feature, "BLOCKED", steps, "Codex CLI not found", mode)
            sys.exit(1)
        log(f"Codex {mode} artifact review")
        manifest = run_codex_artifact_review(
            project_root, task_id, feature, codex_path, mode, artifact_files
        )
        codex_status = manifest.get("status", ReviewStatus.INFRA_FAIL)
        steps.append({
            "name": f"{mode}_review",
            "status": codex_status,
            "detail": manifest.get("reason", ""),
        })
        passed = codex_status in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES)
        final_status = "PASS" if passed else "BLOCKED"
        state = load_state(project_root)
        state["dual_mode"] = mode
        state["codex_status"] = codex_status.lower()
        state["dual_status"] = final_status.lower()
        state["next_step"] = "complete" if passed else f"revise_{mode}"
        save_state(project_root, state)
        write_dual_report(project_root, task_id, feature, final_status, steps, manifest.get("reason", ""), mode)
        if passed:
            update_task_context(
                project_root,
                status="complete",
                resume_cursor="complete",
                event_type=f"{mode}_accepted",
                detail=manifest.get("reason", "PASS"),
            )
            print(f"  DUAL PIPELINE PASS ({mode})")
            return True
        _, exhausted = record_failed_attempt(
            project_root,
            stage=f"{mode}_review",
            hypothesis=f"The {mode} artifact was ready for acceptance.",
            evidence=manifest.get("reason", "Artifact review failed"),
            run_id=manifest.get("run_id"),
        )
        if exhausted:
            steps.append({"name": "failure_budget", "status": "BLOCKED_HANDOFF", "detail": "Failure budget exhausted"})
            write_dual_report(project_root, task_id, feature, "BLOCKED_HANDOFF", steps, manifest.get("reason", ""), mode)
            save_dual_terminal_state(
                project_root, task_id, mode, "BLOCKED_HANDOFF",
                "root_cause_handoff", "Failure budget exhausted",
            )
        print(f"  DUAL PIPELINE BLOCKED ({mode}) - see .agent/reports/CODEX_REVIEW.md")
        sys.exit(1)

    if not opts["skip_verify"]:
        evidence = cmd_verify(project_name, project_root)
        verify_status = evidence.get("status", "FAIL")
        steps.append({"name": "verify", "status": verify_status, "detail": f"evidence_run={evidence.get('run_id', 'unknown')}"})
        if verify_status != "PASS":
            _record_blocked_verify(
                project_root, task_id, feature, mode, steps,
                "Required verification failed",
            )
            print("  ❌ DUAL BLOCKED_VERIFY: build/test/lint evidence did not pass.")
            sys.exit(1)
    else:
        steps.append({"name": "verify", "status": "SKIPPED", "detail": "--skip-verify"})
        if mode == "release":
            _record_blocked_verify(
                project_root, task_id, feature, mode, steps,
                "Release mode cannot skip verification",
            )
            print("  ❌ DUAL BLOCKED_VERIFY: release mode cannot use --skip-verify.")
            sys.exit(1)

    guardrails = cmd_guardrails(project_name, project_root)
    guardrail_status = guardrails.get("status", "FAIL")
    gr_ok = guardrail_status in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES)
    steps.append({"name": "guardrails", "status": guardrail_status, "detail": "Shared task-delta scope decision"})
    if not gr_ok:
        _record_blocked_guardrails(
            project_root, task_id, feature, mode, steps, guardrail_status,
        )
        print("  ❌ DUAL BLOCKED: Guardrails failed.")
        sys.exit(1)

    codex_status = ReviewStatus.INFRA_FAIL
    for cycle in range(1, opts["max_cycles"] + 1):
        log(f"Codex review cycle {cycle}/{opts['max_cycles']}")
        cmd_codex(
            project_name, project_root,
            "--task-id", task_id,
            "--feature", feature,
            "--review-purpose", mode,
            "--cycle", str(cycle),
            "--max-cycles", str(opts["max_cycles"]),
        )
        manifest_file = project_root / ".agent/state/review_run.json"
        manifest = json.loads(manifest_file.read_text(encoding="utf-8")) if manifest_file.exists() else {}
        codex_status = manifest.get("status", "INFRA_FAIL")
        steps.append({"name": f"codex_cycle_{cycle}", "status": codex_status, "detail": manifest.get("reason", "")})
        if codex_status in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES):
            update_task_context(
                project_root,
                status="review_passed",
                resume_cursor="release_gate" if mode == "release" else "release_ready",
                event_type="codex_review_passed",
                detail=f"run_id={manifest.get('run_id', 'unknown')}",
            )
            break
        _, exhausted = record_failed_attempt(
            project_root,
            stage="codex_review",
            hypothesis=f"Cycle {cycle} changes satisfy scope, design, and acceptance criteria.",
            evidence=manifest.get("reason", codex_status),
            run_id=manifest.get("run_id"),
        )
        if exhausted:
            steps.append({
                "name": "failure_budget",
                "status": "BLOCKED_HANDOFF",
                "detail": "Failure budget exhausted; see ROOT_CAUSE_HANDOFF.md",
            })
            write_dual_report(
                project_root,
                task_id,
                feature,
                "BLOCKED_HANDOFF",
                steps,
                "Failure budget exhausted",
                mode,
            )
            save_dual_terminal_state(
                project_root, task_id, mode, "BLOCKED_HANDOFF",
                "root_cause_handoff", "Failure budget exhausted",
            )
            print("  ❌ FAILURE BUDGET EXHAUSTED — see .agent/reports/ROOT_CAUSE_HANDOFF.md")
            sys.exit(1)
        if cycle < opts["max_cycles"]:
            if fix_command:
                fixer_ok, fixer_detail = run_fixer_command(project_root, fix_command)
                steps.append({"name": f"fixer_cycle_{cycle}", "status": "PASS" if fixer_ok else "FAIL", "detail": fixer_detail})
                if not fixer_ok:
                    write_fixer_handoff(project_name, project_root, task_id, feature, cycle, fixer_detail, mode, manifest)
                    break
                if not opts["skip_verify"]:
                    cmd_verify(project_name, project_root)
                    steps.append({"name": f"verify_after_fix_{cycle}", "status": "DONE", "detail": "Build/test/lint reports refreshed"})
                cmd_guardrails(project_name, project_root)
                continue
            if antigravity_auto_fix:
                handoff = write_fixer_handoff(
                    project_name, project_root, task_id, feature, cycle,
                    manifest.get("reason", codex_status), mode, manifest,
                )
                fixer_ok, fixer_detail = run_antigravity_fixer(
                    project_root,
                    handoff,
                    timeout_seconds=int(dual_agents.get("fixer_timeout_seconds", 900)),
                    model=dual_agents.get("antigravity_model") or None,
                    agent=dual_agents.get("antigravity_agent") or None,
                )
                steps.append({
                    "name": f"antigravity_fixer_cycle_{cycle}",
                    "status": "PASS" if fixer_ok else "BLOCKED",
                    "detail": fixer_detail,
                })
                if not fixer_ok:
                    break
                if not opts["skip_verify"]:
                    refreshed = cmd_verify(project_name, project_root)
                    steps.append({
                        "name": f"verify_after_fix_{cycle}",
                        "status": refreshed.get("status", "FAIL"),
                        "detail": f"evidence_run={refreshed.get('run_id', 'unknown')}",
                    })
                    if refreshed.get("status") != "PASS":
                        break
                refreshed_guardrails = cmd_guardrails(project_name, project_root)
                steps.append({
                    "name": f"guardrails_after_fix_{cycle}",
                    "status": refreshed_guardrails.get("status", "FAIL"),
                    "detail": "Post-fix scope validation",
                })
                if refreshed_guardrails.get("status") not in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES):
                    break
                continue
            print("  ⚠️ Codex chưa PASS. Hãy để Anti/Fixer sửa theo CODEX_REVIEW.md rồi chạy lại dual.")
            break

    if steps and steps[-1]["name"].startswith("codex_cycle_") and steps[-1]["status"] not in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES) and opts["max_cycles"] > 1 and not has_fixer:
        write_fixer_handoff(project_name, project_root, task_id, feature, opts["max_cycles"], steps[-1].get("detail", "Codex review did not pass"), mode, manifest)
        steps.append({"name": "fixer_handoff", "status": "BLOCKED", "detail": "No fixer command configured; wrote FIXER_HANDOFF.md"})

    if mode == "code":
        code_ok = codex_status in (ReviewStatus.PASS, ReviewStatus.PASS_WITH_ADVISORIES)
        smoke_run = opts["skip_verify"]
        steps.append({
            "name": "code_gate",
            "status": "PASS" if code_ok else "FAIL",
            "detail": ("SMOKE_PASS_VERIFY_REQUIRED" if smoke_run else "READY_FOR_RELEASE") if code_ok else "CODE_REVIEW_BLOCKED",
        })
        final_status = "SMOKE_PASS" if code_ok and smoke_run else ("PASS" if code_ok else "BLOCKED")
        state = load_state(project_root)
        state["dual_mode"] = mode
        state["dual_status"] = final_status.lower()
        state["next_step"] = "verify" if code_ok and smoke_run else ("release" if code_ok else "fix_code")
        save_state(project_root, state)
        write_dual_report(project_root, task_id, feature, final_status, steps, mode=mode)
        if code_ok and not smoke_run:
            update_task_context(
                project_root,
                status="ready_for_release",
                resume_cursor="release",
                event_type="code_gate_passed",
                detail="READY_FOR_RELEASE",
            )
            print("  DUAL PIPELINE PASS (code) - READY_FOR_RELEASE")
            return True
        if code_ok:
            update_task_context(project_root, status="smoke_pass", resume_cursor="verify", event_type="code_smoke_passed", detail="Review passed; verification still required")
            print("  DUAL PIPELINE SMOKE_PASS (code) - verification required before release")
            return True
        print("  DUAL PIPELINE BLOCKED (code) - see .agent/reports/DUAL_AGENT_REPORT.md")
        sys.exit(1)

    try:
        cmd_gate(project_name, project_root)
        gate_ok = True
        gate_detail = "ALLOW_RELEASE"
    except SystemExit as e:
        gate_ok = False
        gate_detail = f"BLOCK_RELEASE ({e.code})"
    steps.append({"name": "release_gate", "status": "PASS" if gate_ok else "FAIL", "detail": gate_detail})

    final_status = "PASS" if gate_ok else "BLOCKED"
    state = load_state(project_root)
    state["dual_status"] = final_status.lower()
    save_state(project_root, state)
    write_dual_report(project_root, task_id, feature, final_status, steps, mode=mode)

    if gate_ok:
        update_task_context(
            project_root,
            status="complete",
            resume_cursor="complete",
            event_type="release_gate_passed",
            detail="ALLOW_RELEASE",
        )
        print("  ✅ DUAL PIPELINE PASS")
        return True
    print("  ❌ DUAL PIPELINE BLOCKED — xem .agent/reports/DUAL_AGENT_REPORT.md")
    sys.exit(1)

AGENT_PROMPTS = {
    "02_plan": ("Claude Sonnet 4.6 (Antigravity)", """Bạn là Claude Planner trong AI Software Factory.

Đọc:
1. {root}\\AGENTS.md
2. {root}\\.agent\\context\\PROJECT_CONTEXT.md

Tạo file: {root}\\.agent\\context\\PLAN.md
(Phân tích codebase, liệt kê bugs, đề xuất features mới, chia phase)
QUY TẮC: KHÔNG sửa code."""),

    "03_design": ("Claude Sonnet 4.6 (Antigravity)", """Bạn là Claude Architect trong AI Software Factory.

Đọc:
1. {root}\\AGENTS.md
2. {root}\\.agent\\context\\PLAN.md
3. {root}\\.agent\\context\\PHASES.md
4. {root}\\.agent\\context\\RISK_ANALYSIS.md
5. {root}\\source-code\\src\\

Tạo: {root}\\.agent\\context\\TECHNICAL_DESIGN.md
(Liệt kê chính xác file nào sửa, dòng nào thay đổi)
QUY TẮC: KHÔNG sửa source code. Chỉ thiết kế."""),

    "04_implement": ("Claude Sonnet 4.6 (Antigravity)", """Bạn là Implementer trong AI Software Factory.

Đọc:
1. {root}\\AGENTS.md
2. {root}\\GEMINI.md
3. {root}\\.agent\\context\\TECHNICAL_DESIGN.md
4. {root}\\.agent\\context\\ACCEPTANCE_CRITERIA.md
5. {root}\\source-code\\src\\

Nhiệm vụ: Triển khai theo TECHNICAL_DESIGN.md, build, ghi IMPLEMENTATION_REPORT.md
QUY TẮC: KHÔNG báo done khi build chưa pass."""),

    "05_qa": ("Claude Sonnet 4.6 (Antigravity)", """Bạn là QA Agent trong AI Software Factory.

Đọc:
1. {root}\\AGENTS.md
2. {root}\\.agent\\context\\ACCEPTANCE_CRITERIA.md
3. {root}\\.agent\\reports\\IMPLEMENTATION_REPORT.md
4. {root}\\source-code\\src\\

Build lại, kiểm tra từng AC, ghi QA_REPORT.md"""),

    "07_release": ("Claude Sonnet 4.6 (Antigravity)", """Bạn là Release Agent trong AI Software Factory.

Đọc:
1. {root}\\.agent\\reports\\QA_REPORT.md
2. {root}\\.agent\\reports\\CODEX_REVIEW.md
3. {root}\\.agent\\context\\PLAN.md
4. {root}\\.agent\\reports\\IMPLEMENTATION_REPORT.md

Tạo: RELEASE_NOTES.md, PR_DESCRIPTION.md, FINAL_REPORT.md
QUY TẮC: KHÔNG push lên main."""),

    "gemini_fixer": ("Claude Sonnet 4.6 (Antigravity)", """Bạn là Fix Agent trong AI Software Factory.

Đọc:
1. {root}\\.agent\\reports\\CODEX_REVIEW.md  ← đây là issues cần fix
2. {root}\\.agent\\reports\\QA_REPORT.md
3. {root}\\.agent\\context\\ACCEPTANCE_CRITERIA.md
4. {root}\\source-code\\src\\  ← đọc file liên quan

Fix ĐÚNG issue được liệt kê, build lại, ghi FIX_REPORT.md
QUY TẮC: KHÔNG sửa file ngoài phạm vi issue."""),
}

def cmd_next(project_name, project_root):
    header(f"NEXT STEP — {project_name}")
    state = load_state(project_root)
    next_step = state.get("next_step", "02_plan")

    if next_step == "08_commit":
        cmd_commit(project_name, project_root)
        return

    if next_step == "06_codex":
        print("  → Chạy: python harness.py revit codex")
        return

    info = AGENT_PROMPTS.get(next_step)
    if not info:
        print(f"  ⚠️  Không có prompt cho step: {next_step}")
        return

    agent, prompt = info
    prompt = prompt.replace("{root}", str(project_root))

    sep()
    print(f"  Agent cần mở: {agent}")
    sep()
    print("\n  📋 PROMPT (copy toàn bộ bên dưới):\n")
    print("  " + "─"*50)
    print(prompt)
    print("  " + "─"*50)
    alias = next((k for k, v in PROJECT_ALIASES.items() if v == project_name), project_name)
    print(f"\n  Sau khi agent xong → chạy lại: python harness.py {alias} status")

# ─── COMMAND: done ────────────────────────────────────────────────────────────

STEP_ORDER = ["02_plan","03_design","04_implement","05_qa","06_codex","07_release","08_commit"]

def cmd_done(project_name, project_root, step_key=None):
    """Đánh dấu 1 bước đã xong, tự động advance next_step."""
    state = load_state(project_root)
    current = state.get("next_step", "02_plan")

    # Task P1-E: Lịch sử QA
    if current == "05_qa":
        qa_report = project_root / ".agent/reports/QA_REPORT.md"
        qa_initial = project_root / ".agent/reports/QA_REPORT_INITIAL.md"
        fix_report = project_root / ".agent/reports/FIX_REPORT.md"
        qa_after = project_root / ".agent/reports/QA_REPORT_AFTER_FIX.md"
        if qa_report.exists():
            if fix_report.exists():
                shutil.copy(qa_report, qa_after)
                log("Đã copy QA_REPORT.md → QA_REPORT_AFTER_FIX.md (QA run 2)")
            elif not qa_initial.exists():
                shutil.copy(qa_report, qa_initial)
                log("Đã copy QA_REPORT.md → QA_REPORT_INITIAL.md (QA run 1)")

    if current == "gemini_fixer":
        state["next_step"] = "05_qa"
    else:
        idx = STEP_ORDER.index(current) if current in STEP_ORDER else 0
        if idx + 1 < len(STEP_ORDER):
            state["next_step"] = STEP_ORDER[idx + 1]
            
    state["status"] = "in_progress"
    save_state(project_root, state)
    log(f"Step '{current}' đánh dấu xong → next: {state['next_step']}")
    cmd_status(project_name, project_root)

# ─── COMMAND: commit ──────────────────────────────────────────────────────────

def cmd_promote(project_name, project_root):
    header(f"PROMOTION GATE — {project_name}")
    
    # Deploy Lock (Check Review Status)
    manifest_file = project_root / ".agent/state/review_run.json"
    if manifest_file.exists():
        manifest = json.loads(manifest_file.read_text(encoding="utf-8"))
        if manifest.get("status") == ReviewStatus.RUNNING:
            print("  ❌ BLOCK_RELEASE: Codex đang review (RUNNING). Không được phép promote/deploy.")
            return

    gate_report = project_root / ".agent/reports/RELEASE_GATE_REPORT.md"
    if not gate_report.exists() or "Status: ALLOW_RELEASE" not in gate_report.read_text(encoding="utf-8"):
        print("  ❌ BLOCK_RELEASE: Release Gate chưa PASS. Chạy `python harness.py [project] gate` để kiểm tra.")
        return
        
    source = project_root / "source-code"
    target = SOURCE_DIR.get(project_name)
    
    print(f"  Bắt đầu promote code từ sandbox về {target}...\n")
    src_cs = source / "src"
    if src_cs.exists():
        print(f'  xcopy /E /I /Y "{src_cs}\\*" "{target}\\src\\"')
    else:
        print(f'  xcopy /E /I /Y "{source}\\*" "{target}\\"')
        
    print(f"""
  [2] Kiểm tra diff:
  cd "{target}"
  git diff --stat

  [3] Commit:
  git add -A
  git commit -m "feat/fix: update from AI Software Factory"
  """)
    sep()

# ─── COMMAND: reset ───────────────────────────────────────────────────────────

def cmd_reset(project_name, project_root):
    state = load_state(project_root)
    state["next_step"] = "02_plan"
    state["retry_count"] = 0
    state["status"] = "initialized"
    save_state(project_root, state)
    log("Workflow reset về bước 2 (Plan). PROJECT_CONTEXT.md giữ nguyên.")

# ─── MAIN ─────────────────────────────────────────────────────────────────────

def _project_path(project_root, value):
    path = Path(value)
    return path if path.is_absolute() else project_root / path


def cmd_doctor(project_name, project_root):
    """Diagnose the real Antigravity/Codex runtimes and auth boundary."""
    header(f"DUAL AGENT DOCTOR — {project_name}")
    profile = load_profile(project_root)
    stale_after = int(profile.get("codex_review", {}).get("timeout_seconds", 180)) + 60
    reconciled = reconcile_stale_review(project_root, stale_after_seconds=stale_after)
    result = diagnose_dual_runtime(project_root)
    result["stale_reconciliation"] = reconciled
    report_path = project_root / ".agent/reports/DUAL_AGENT_DOCTOR.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for check in result["checks"]:
        print(f"  {check['name']:<14} {check['status']:<20} {check.get('executable') or ''}")
        if check["status"] != "READY":
            lines = [line.strip() for line in check.get("detail", "").splitlines() if line.strip()]
            useful = next((line for line in lines if any(term in line.lower() for term in (
                "not logged", "please sign in", "access is denied", "sandbox helper", "failed"
            ))), lines[-1] if lines else "")
            print(f"    {useful}")
    print(f"  Report: {report_path}")
    return result


def cmd_reconcile_stale(project_root):
    profile = load_profile(project_root)
    stale_after = int(profile.get("codex_review", {}).get("timeout_seconds", 180)) + 60
    result = reconcile_stale_review(project_root, stale_after_seconds=stale_after)
    print(json.dumps(result or {"status": "UNCHANGED"}, ensure_ascii=False))
    return result


def cmd_learning(project_name, project_root, command, args):
    """P2-P5 learning/evolution commands. None auto-promote production."""
    if command == "learn-label":
        if len(args) < 2:
            raise ValueError("learn-label requires <task-id> <success|failure|partial|regression> [note]")
        path = label_outcome(project_root, args[0], args[1], " ".join(args[2:]))
        result = {"status": "RECORDED", "path": str(path)}
    elif command == "learn-dataset":
        result = build_learning_dataset(project_root, args[0] if args else "operations")
    elif command == "shadow":
        if len(args) < 3:
            raise ValueError("shadow requires <baseline> <candidate> <dataset>")
        result = shadow_evaluate(
            project_root,
            _project_path(project_root, args[0]),
            _project_path(project_root, args[1]),
            _project_path(project_root, args[2]),
        )
    elif command == "evolution-gate":
        if not args:
            raise ValueError("evolution-gate requires <shadow-run.json>")
        result = gate_candidate(project_root, _project_path(project_root, args[0]))
    elif command == "canary":
        if not args:
            raise ValueError("canary requires <candidate-gate.json> [percent]")
        result = start_canary(
            project_root,
            _project_path(project_root, args[0]),
            int(args[1]) if len(args) > 1 else 10,
        )
    elif command == "canary-outcome":
        if len(args) < 2:
            raise ValueError("canary-outcome requires <canary.json> <success|failure|regression>")
        result = record_canary_outcome(_project_path(project_root, args[0]), args[1])
    elif command == "learning-status":
        result = get_continuous_status(project_root)
    else:
        raise ValueError(f"Unsupported learning command: {command}")
    print(json.dumps(result, indent=2, ensure_ascii=False))
    return result


HELP = """
Cú pháp:  python harness.py [project] [command]

Projects:  revit | navis | trend

Commands:
  status    Xem tiến độ toàn bộ pipeline
  next      Xem bước tiếp theo + prompt cần paste
  done      Đánh dấu bước hiện tại xong, advance sang bước tiếp
  hash      Tính diff hash hiện tại
  verify    Chạy build/test/lint theo project_profile.json
  guardrails Kiểm tra file thay đổi so với TASK_SCOPE.json
  codex     Chạy Codex review tự động
  runtime   Chạy Project-Specific Runtime Validation
  gate      Chạy Release Gate kiểm tra tất cả điều kiện
  promote   Promote code về project gốc (thay cho commit)
  reset     Reset pipeline về bước 2 (giữ nguyên PROJECT_CONTEXT)
  setup     Khởi tạo project mới từ PROJECT_ONBOARDING_SPEC.md (/hermes)

Ví dụ:
  python harness.py revit status
  python harness.py revit next
  python harness.py revit done
  python harness.py revit codex
  python harness.py revit commit
"""

def main():
    if len(sys.argv) < 3:
        print(HELP)
        sys.exit(0)

    proj_key = sys.argv[1]
    command  = sys.argv[2]

    project_name, profile = load_project(proj_key)
    project_root = FACTORY_ROOT / project_name

    # setup là lệnh đặc biệt — tạo project từ đầu, không cần project tồn tại trước
    if command == "setup":
        cmd_setup(proj_key, sys.argv[3:])
        return

    if not project_root.exists() or not (project_root / ".agent/project_profile.json").exists():
        print(f"❌ Project không hợp lệ hoặc thiếu project_profile.json: {project_name}")
        sys.exit(1)

    if command == "status":    cmd_status(project_name, project_root)
    elif command == "next":    cmd_next(project_name, project_root)
    elif command == "done":    cmd_done(project_name, project_root)
    elif command == "hash":    cmd_hash(project_name, project_root)
    elif command == "verify":  cmd_verify(project_name, project_root)
    elif command == "codex":   cmd_codex(project_name, project_root, *sys.argv[3:])
    elif command == "dual":    cmd_dual(project_name, project_root, *sys.argv[3:])
    elif command == "dual-init": cmd_dual_init(project_name, project_root, *sys.argv[3:])
    elif command == "guardrails": cmd_guardrails(project_name, project_root)
    elif command == "runtime": cmd_runtime(project_name, project_root)
    elif command == "gate":    cmd_gate(project_name, project_root)
    elif command == "promote": cmd_promote(project_name, project_root)
    elif command == "reset":   cmd_reset(project_name, project_root)
    elif command == "doctor":  cmd_doctor(project_name, project_root)
    elif command == "reconcile-stale": cmd_reconcile_stale(project_root)
    elif command in {
        "learn-label", "learn-dataset", "shadow", "evolution-gate",
        "canary", "canary-outcome", "learning-status",
    }:
        try:
            cmd_learning(project_name, project_root, command, sys.argv[3:])
        except (ValueError, OSError, json.JSONDecodeError) as exc:
            print(f"ERROR: {exc}")
            sys.exit(1)
    elif command == "setup":   cmd_setup(proj_key, sys.argv[3:])
    else:
        print(f"❌ Command không hợp lệ: {command}")
        print(HELP)

if __name__ == "__main__":
    main()
