"""Host-side safety contracts for the dual-agent pipeline.

This module is deliberately independent of either agent.  The host creates and
validates the durable contracts; models never get to widen them.
"""
from __future__ import annotations

import hashlib
import json
import shlex
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path

MAX_AUTOMATIC_FIXES = 1
MAX_CODEX_REVIEWS = 2
TERMINAL_STATUSES = {"COMPLETED", "HUMAN_DECISION", "INFRA_FAILURE", "STALE", "BLOCKED_SELF_MODIFICATION", "BLOCKED_INCOMPLETE_FIX", "BLOCKED_SCOPE_CONFIGURATION", "BLOCKED_PLAN_CHANGED"}
RESUMABLE_STATUSES = {"RETRY_AUTHORIZED", "VERIFIED", "VERIFICATION_FAILED"}
PROTECTED_PATHS = (".agents", ".agent", ".github", "AGENTS.md", ".gitignore")


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _artifact_hash(root: Path, relative: str) -> str:
    path = root / relative
    return sha256_file(path) if path.is_file() else "MISSING"


def immutable_scope_projection(scope: dict) -> dict:
    """Return only task-boundary fields; runtime mode/artifacts are mutable."""
    return {
        "task_id": scope.get("task_id"),
        "allowed_files": sorted(scope.get("allowed_files", scope.get("included_files", []))),
        "forbidden": sorted(scope.get("forbidden", [])),
        "repository_root": scope.get("repository_root"),
    }


def scope_projection_hash(scope: dict) -> str:
    projection = immutable_scope_projection(scope)
    return hashlib.sha256(json.dumps(projection, sort_keys=True, ensure_ascii=False).encode("utf-8")).hexdigest()


def plan_lock_path(project_root: Path) -> Path:
    return project_root / ".agent" / "state" / "PLAN_LOCK.json"


def create_plan_lock(project_root: Path, task_id: str, approved_plan_run_id: str,
                     baseline_commit: str | None = None,
                     approved_plan_snapshot_hash: str = "") -> dict:
    context = project_root / ".agent" / "context"
    scope = json.loads((context / "TASK_SCOPE.json").read_text(encoding="utf-8"))
    plan_files = {"plan": "PLAN.md", "technical_design": "TECHNICAL_DESIGN.md", "acceptance_criteria": "ACCEPTANCE_CRITERIA.md"}
    payload = {
        "schema_version": 1, "task_id": task_id,
        "approved_plan_run_id": approved_plan_run_id,
        "approved_plan_snapshot_hash": approved_plan_snapshot_hash or scope.get("approved_plan_snapshot_hash", ""),
        "plan_sha256": _artifact_hash(context, plan_files["plan"]),
        "technical_design_sha256": _artifact_hash(context, plan_files["technical_design"]),
        "acceptance_criteria_sha256": _artifact_hash(context, plan_files["acceptance_criteria"]),
        "task_scope_sha256": scope_projection_hash(scope),
        "baseline_commit": baseline_commit or _head(project_root / "source-code"),
        "approved_at": datetime.now(timezone.utc).isoformat(),
    }
    path = plan_lock_path(project_root)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        old = json.loads(path.read_text(encoding="utf-8"))
        if old != payload:
            raise RuntimeError("BLOCKED_PLAN_CHANGED: PLAN_LOCK.json already exists")
    else:
        path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return payload


def _head(repo: Path) -> str:
    result = subprocess.run(["git", "rev-parse", "HEAD"], cwd=repo, capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else "UNKNOWN"


def validate_plan_lock(project_root: Path, task_id: str) -> tuple[bool, str]:
    path = plan_lock_path(project_root)
    if not path.exists():
        return False, "BLOCKED_PLAN_CHANGED: PLAN_LOCK.json is missing"
    try:
        lock = json.loads(path.read_text(encoding="utf-8"))
        context = project_root / ".agent" / "context"
        scope = json.loads((context / "TASK_SCOPE.json").read_text(encoding="utf-8"))
        expected = {
            "task_id": task_id,
            "plan_sha256": _artifact_hash(context, "PLAN.md"),
            "technical_design_sha256": _artifact_hash(context, "TECHNICAL_DESIGN.md"),
            "acceptance_criteria_sha256": _artifact_hash(context, "ACCEPTANCE_CRITERIA.md"),
            "task_scope_sha256": scope_projection_hash(scope),
        }
        if any(lock.get(key) != value for key, value in expected.items()):
            return False, "BLOCKED_PLAN_CHANGED: plan, design, acceptance, scope, or task id changed"
    except (OSError, ValueError, KeyError) as exc:
        return False, f"BLOCKED_PLAN_CHANGED: invalid PLAN_LOCK.json ({exc})"
    return True, "PLAN_LOCK_VALID"


def protected_path(path: str) -> bool:
    normalized = path.replace("\\", "/")
    while normalized.startswith("./"):
        normalized = normalized[2:]
    return any(normalized == item or normalized.startswith(item + "/") for item in PROTECTED_PATHS)


def canonical_finding_id(finding: dict) -> str:
    def normalize(value: object) -> str:
        return re.sub(r"[^a-z0-9]+", " ", str(value or "").lower()).strip()
    problem = finding.get("problem", finding.get("body", finding.get("title", "")))
    value = "\0".join(normalize(finding.get(key, "")) for key in (
        "category", "rule_id", "file", "symbol", "acceptance_criterion"
    )) + "\0" + normalize(problem)
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:20]


def normalize_findings(findings: list[dict]) -> list[dict]:
    result = []
    for finding in findings:
        item = dict(finding)
        item["model_finding_id"] = item.get("finding_id")
        item["canonical_finding_id"] = canonical_finding_id(item)
        result.append(item)
    return result


def validate_max_cycles(value: int) -> int:
    if value < 1 or value > MAX_CODEX_REVIEWS:
        raise ValueError(f"max_cycles must be between 1 and {MAX_CODEX_REVIEWS}; values above the bound are rejected")
    return value


def parse_safe_command(command: str | list[str]) -> list[str]:
    if isinstance(command, list):
        if not all(isinstance(item, str) and item for item in command):
            raise ValueError("fix command arguments must be non-empty strings")
        return command
    if not isinstance(command, str) or not command.strip():
        raise ValueError("fix command is empty")
    if any(token in command for token in ("&", "|", ";", ">", "<", "`", "$(")):
        raise ValueError("untrusted shell syntax is not accepted")
    return shlex.split(command, posix=False)


def build_fix_contract(project_root: Path, task_id: str, review_run_id: str, findings: list[dict], plan_lock: dict) -> dict:
    blocking = [f for f in normalize_findings(findings) if f.get("severity") in {"P0", "P1", "P2"}]
    contract = {"schema_version": 1, "task_id": task_id, "review_run_id": review_run_id,
                "plan_lock_sha256": hashlib.sha256(json.dumps(plan_lock, sort_keys=True).encode()).hexdigest(),
                "findings": [{"canonical_finding_id": f["canonical_finding_id"], "model_finding_id": f.get("model_finding_id"),
                              "severity": f.get("severity"), "file": f.get("file"), "line": f.get("line"),
                              "problem": f.get("problem", f.get("body", f.get("title", ""))), "required_evidence": "tests and diff"} for f in blocking]}
    if blocking:
        path = project_root / ".agent" / "state" / "FIX_CONTRACT.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(contract, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return contract


def validate_fix_result(result: dict, contract: dict) -> tuple[bool, str]:
    required = {f["canonical_finding_id"] for f in contract.get("findings", [])}
    entries = result.get("findings", [])
    actual = {f.get("canonical_finding_id") for f in entries}
    if result.get("task_id") != contract.get("task_id") or result.get("review_run_id") != contract.get("review_run_id"):
        return False, "BLOCKED_INCOMPLETE_FIX: task or review run mismatch"
    if result.get("fix_round") != 1:
        return False, "BLOCKED_INCOMPLETE_FIX: fix_round must be 1"
    if not result.get("previous_snapshot") or not result.get("result_snapshot"):
        return False, "BLOCKED_INCOMPLETE_FIX: previous and result snapshots are required"
    if result.get("plan_lock_sha256") != contract.get("plan_lock_sha256"):
        return False, "BLOCKED_INCOMPLETE_FIX: plan lock hash mismatch"
    if result.get("protected_files_changed"):
        return False, "BLOCKED_SELF_MODIFICATION: protected files changed"
    if not required:
        return True, "FIX_RESULT_NOT_REQUIRED: no blocking findings"
    if required != actual:
        return False, "BLOCKED_INCOMPLETE_FIX: one result is required for every blocking finding"
    if any(f.get("status") not in {"FIXED", "BLOCKED", "NOT_REPRODUCED"} for f in entries):
        return False, "BLOCKED_INCOMPLETE_FIX: invalid finding result status"
    for finding in entries:
        if not str(finding.get("evidence", "")).strip():
            return False, "BLOCKED_INCOMPLETE_FIX: evidence is required for every finding"
        tests = finding.get("tests", finding.get("test_references", []))
        if not tests or not all(isinstance(item, str) and item.strip() for item in tests):
            return False, "BLOCKED_INCOMPLETE_FIX: required test references are missing"
    return True, "FIX_RESULT_VALID"
