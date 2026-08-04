"""Durable task context, verification evidence, and operational knowledge.

The markdown reports remain human-facing.  The JSON files written here are the
machine-readable contract that lets a later process resume and audit a task.
"""

from __future__ import annotations

import hashlib
import json
import re
import uuid
from datetime import datetime, timezone
from pathlib import Path


TASK_CONTEXT_SCHEMA_VERSION = 1
EVIDENCE_SCHEMA_VERSION = 1


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _slug(value: str) -> str:
    normalized = re.sub(r"[^a-zA-Z0-9_-]+", "-", value.strip()).strip("-")
    return normalized.lower() or "task"


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8", errors="replace")).hexdigest()


def task_context_path(project_root: Path) -> Path:
    return project_root / ".agent/context/TASK_CONTEXT.json"


def evidence_manifest_path(project_root: Path) -> Path:
    return project_root / ".agent/state/EVIDENCE_MANIFEST.json"


def load_task_context(project_root: Path) -> dict:
    path = task_context_path(project_root)
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def initialize_task_context(
    project_root: Path,
    project_name: str,
    task_id: str,
    feature: str,
    mode: str,
    scope: dict,
    snapshot_hash: str,
    snapshot_files: list[str],
    profile: dict,
    *,
    force: bool = False,
) -> dict:
    path = task_context_path(project_root)
    existing = load_task_context(project_root)
    if existing and existing.get("task_id") == task_id and not force:
        now = utc_now()
        existing["feature"] = feature
        existing["mode"] = mode
        existing["repository_snapshot"] = {
            "hash": snapshot_hash,
            "files": snapshot_files,
        }
        existing["scope"] = {
            "allowed_files": scope.get("allowed_files", []),
            "forbidden": scope.get("forbidden", []),
            "artifact_files": scope.get("artifact_files", []),
        }
        existing["relevant_files"] = list(snapshot_files)
        existing["verification_commands"] = {
            stage: profile.get(f"{stage}_command") or None
            for stage in ("build", "test", "lint")
        }
        existing["updated_at"] = now
        existing.setdefault("events", []).append(
            {"at": now, "type": "context_refreshed", "detail": f"Mode set to {mode}"}
        )
        _write_json(path, existing)
        return existing

    budget = profile.get("failure_budget", {})
    limit = max(1, int(budget.get("max_failed_attempts", 3)))
    verification_commands = {
        stage: profile.get(f"{stage}_command") or None
        for stage in ("build", "test", "lint")
    }
    now = utc_now()
    payload = {
        "schema_version": TASK_CONTEXT_SCHEMA_VERSION,
        "task_id": task_id,
        "project": project_name,
        "feature": feature,
        "mode": mode,
        "status": "initialized",
        "resume_cursor": "scope_ready",
        "created_at": now,
        "updated_at": now,
        "repository_snapshot": {
            "hash": snapshot_hash,
            "files": snapshot_files,
        },
        "scope": {
            "allowed_files": scope.get("allowed_files", []),
            "forbidden": scope.get("forbidden", []),
            "artifact_files": scope.get("artifact_files", []),
        },
        "relevant_files": list(snapshot_files),
        "constraints": [
            "Changes must remain inside TASK_SCOPE.json.",
            "Completion claims require fresh verification evidence when policy enables it.",
        ],
        "decisions": [],
        "verification_commands": verification_commands,
        "evidence": [],
        "failure_budget": {
            "limit": limit,
            "used": 0,
            "remaining": limit,
            "failed_attempts": [],
        },
        "events": [
            {"at": now, "type": "initialized", "detail": "Task context created"}
        ],
    }
    _write_json(path, payload)
    return payload


def update_task_context(
    project_root: Path,
    *,
    status: str | None = None,
    resume_cursor: str | None = None,
    event_type: str | None = None,
    detail: str = "",
    evidence_ref: str | None = None,
) -> dict:
    context = load_task_context(project_root)
    if not context:
        return {}
    now = utc_now()
    if status:
        context["status"] = status
    if resume_cursor:
        context["resume_cursor"] = resume_cursor
    if evidence_ref and evidence_ref not in context.setdefault("evidence", []):
        context["evidence"].append(evidence_ref)
    if event_type:
        context.setdefault("events", []).append(
            {"at": now, "type": event_type, "detail": detail}
        )
    context["updated_at"] = now
    _write_json(task_context_path(project_root), context)
    return context


def record_failed_attempt(
    project_root: Path,
    *,
    stage: str,
    hypothesis: str,
    evidence: str,
    run_id: str | None = None,
    snapshot_hash: str | None = None,
) -> tuple[dict, bool]:
    context = load_task_context(project_root)
    if not context:
        return {}, False
    failure = context.setdefault(
        "failure_budget",
        {"limit": 3, "used": 0, "remaining": 3, "failed_attempts": []},
    )
    manifest_path = project_root / ".agent/state/review_run.json"
    blocking_finding_ids = []
    if manifest_path.exists():
        try:
            manifest_data = json.loads(manifest_path.read_text(encoding="utf-8"))
            if not snapshot_hash:
                snapshot_hash = manifest_data.get("snapshot_hash")
            findings = manifest_data.get("findings", [])
            for f in findings:
                if f.get("severity") in {"P0", "P1", "P2"} and f.get("status", "OPEN") not in {"RESOLVED", "ADVISORY", "DEFERRED"}:
                    if "finding_id" in f:
                        blocking_finding_ids.append(f["finding_id"])
        except (OSError, ValueError):
            pass

    task_id = context.get("task_id", "unknown")
    blocking_finding_ids.sort()
    fingerprint_source = f"{task_id}\0{stage.lower()}\0{snapshot_hash or 'unknown'}\0{','.join(blocking_finding_ids)}"
    fingerprint = sha256_text(fingerprint_source)[:16]
    attempts = failure.setdefault("failed_attempts", [])
    if any(item.get("fingerprint") == fingerprint for item in attempts):
        context["last_attempt_result"] = "DUPLICATE_ATTEMPT"
        context["updated_at"] = utc_now()
        _write_json(task_context_path(project_root), context)
        return context, failure.get("remaining", 1) == 0
    attempt = {
        "at": utc_now(),
        "stage": stage,
        "hypothesis": hypothesis,
        "evidence": evidence,
        "run_id": run_id,
        "snapshot_hash": snapshot_hash,
        "fingerprint": fingerprint,
    }
    attempts.append(attempt)
    failure["used"] = int(failure.get("used", 0)) + 1
    failure["remaining"] = max(0, int(failure.get("limit", 3)) - failure["used"])
    exhausted = failure["remaining"] == 0
    context["status"] = "blocked_handoff" if exhausted else "needs_fix"
    context["resume_cursor"] = "root_cause_handoff" if exhausted else "fix_required"
    context["updated_at"] = utc_now()
    context["last_attempt_result"] = "RECORDED"
    _write_json(task_context_path(project_root), context)
    if exhausted:
        write_bug_episode(project_root, context)
        write_root_cause_handoff(project_root, context)
    return context, exhausted


def prepare_failure_budget_retry(
    project_root: Path,
    current_snapshot_hash: str,
    resume_hypothesis: str | None = None,
) -> tuple[bool, str]:
    """Block blind retries; reopen an exhausted budget only for new code plus a hypothesis."""
    context = load_task_context(project_root)
    if not context:
        return True, "No durable task context yet."
    failure = context.get("failure_budget", {})
    remaining = int(failure.get("remaining", failure.get("limit", 3)))
    if remaining > 0:
        return True, f"Failure budget remaining: {remaining}"

    manifest_path = project_root / ".agent/state/review_run.json"
    last_snapshot_hash = ""
    if manifest_path.exists():
        try:
            last_snapshot_hash = json.loads(manifest_path.read_text(encoding="utf-8")).get("snapshot_hash", "")
        except Exception:
            last_snapshot_hash = ""
    if not resume_hypothesis or not resume_hypothesis.strip():
        return False, "Failure budget exhausted. Provide --resume-hypothesis after making a scoped code change."
    if not last_snapshot_hash or current_snapshot_hash == last_snapshot_hash:
        return False, "Failure budget exhausted and the task snapshot is unchanged; another review would repeat the same attempt."

    now = utc_now()
    history = context.setdefault("failure_budget_history", [])
    history.append({
        "closed_at": now,
        "resume_hypothesis": resume_hypothesis.strip(),
        "previous_snapshot_hash": last_snapshot_hash,
        "new_snapshot_hash": current_snapshot_hash,
        "attempts": list(failure.get("failed_attempts", [])),
    })
    limit = max(1, int(failure.get("limit", 3)))
    context["failure_budget"] = {
        "limit": limit,
        "used": 0,
        "remaining": limit,
        "failed_attempts": [],
    }
    context["status"] = "retry_authorized"
    context["resume_cursor"] = "review_retry"
    context.setdefault("events", []).append({
        "at": now,
        "type": "failure_budget_reopened",
        "detail": resume_hypothesis.strip(),
    })
    context["updated_at"] = now
    _write_json(task_context_path(project_root), context)
    return True, "Failure budget reopened for a changed snapshot and explicit hypothesis."


def build_evidence_manifest(
    project_root: Path,
    task_id: str,
    snapshot_before: str,
    snapshot_after: str,
    results: list[dict],
) -> dict:
    required_results = [item for item in results if item.get("required", False)]
    status = (
        "PASS"
        if snapshot_before == snapshot_after
        and all(item.get("status") == "PASS" for item in required_results)
        else "FAIL"
    )
    payload = {
        "schema_version": EVIDENCE_SCHEMA_VERSION,
        "run_id": str(uuid.uuid4()),
        "task_id": task_id,
        "created_at": utc_now(),
        "snapshot_before": snapshot_before,
        "snapshot_after": snapshot_after,
        "fresh": snapshot_before == snapshot_after,
        "status": status,
        "results": results,
    }
    _write_json(evidence_manifest_path(project_root), payload)
    update_task_context(
        project_root,
        status="verified" if status == "PASS" else "verification_failed",
        resume_cursor="review_ready" if status == "PASS" else "verify_required",
        event_type="verification",
        detail=f"Evidence run {payload['run_id']} finished with {status}",
        evidence_ref=".agent/state/EVIDENCE_MANIFEST.json",
    )
    return payload


def validate_fresh_evidence(
    project_root: Path,
    task_id: str | None,
    current_snapshot: str,
    required_stages: list[str],
) -> tuple[bool, list[str], dict]:
    path = evidence_manifest_path(project_root)
    if not path.exists():
        return False, ["EVIDENCE_MANIFEST.json is missing."], {}
    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        return False, [f"EVIDENCE_MANIFEST.json is invalid: {exc}"], {}

    issues = []
    if task_id and manifest.get("task_id") != task_id:
        issues.append("Evidence task_id does not match TASK_SCOPE.json.")
    if not manifest.get("fresh") or manifest.get("snapshot_after") != current_snapshot:
        issues.append("Verification evidence is stale for the current repository snapshot.")
    by_stage = {item.get("stage"): item for item in manifest.get("results", [])}
    for stage in required_stages:
        item = by_stage.get(stage)
        if not item:
            issues.append(f"Required evidence stage '{stage}' is missing.")
        elif item.get("status") != "PASS":
            issues.append(f"Required evidence stage '{stage}' did not pass.")
    if manifest.get("status") != "PASS":
        issues.append("Evidence manifest status is not PASS.")
    return not issues, issues, manifest


def knowledge_root(project_root: Path) -> Path:
    return project_root / ".agent/knowledge"


def ensure_knowledge_layout(project_root: Path) -> None:
    root = knowledge_root(project_root)
    for relative in ("memory/bugs", "memory/decisions", "learn", "later"):
        (root / relative).mkdir(parents=True, exist_ok=True)
    readme = root / "README.md"
    if not readme.exists():
        readme.write_text(
            "# Operational Knowledge\n\n"
            "- `memory/`: facts, decisions, and bug episodes used by agents.\n"
            "- `learn/`: curated explanations for humans; never generated by default.\n"
            "- `later/`: out-of-scope findings captured without widening the active task.\n\n"
            "Markdown is the source of truth. Any future search index must be rebuildable.\n",
            encoding="utf-8",
        )


def write_later_finding(project_root: Path, task_id: str, files: list[str]) -> Path | None:
    if not files:
        return None
    ensure_knowledge_layout(project_root)
    path = knowledge_root(project_root) / "later" / f"{_slug(task_id)}.md"
    lines = [
        f"# Deferred findings for {task_id}",
        "",
        f"Updated: {utc_now()}",
        "",
        "These changed files were outside the active task boundary. They were recorded, not fixed:",
        "",
    ]
    lines.extend(f"- `{item}`" for item in sorted(set(files)))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return path


def write_bug_episode(project_root: Path, context: dict) -> Path:
    ensure_knowledge_layout(project_root)
    task_id = context.get("task_id", "task")
    path = knowledge_root(project_root) / "memory/bugs" / f"{_slug(task_id)}.md"
    attempts = context.get("failure_budget", {}).get("failed_attempts", [])
    lines = [
        "---",
        f'title: "Failure budget exhausted: {task_id}"',
        f'description: "Repeated task attempts exhausted the configured failure budget"',
        "tags: [failure-budget, blocked-handoff, pipeline]",
        "type: episode",
        "importance: 4",
        "---",
        "",
        f"# Failure budget exhausted: {task_id}",
        "",
        "## Symptom",
        context.get("feature", "Unknown task failure"),
        "",
        "## Attempts",
    ]
    for index, attempt in enumerate(attempts, 1):
        lines.append(
            f"{index}. **{attempt.get('stage', 'unknown')}** — "
            f"{attempt.get('hypothesis', 'No hypothesis')} — {attempt.get('evidence', '')}"
        )
    lines.extend(
        [
            "",
            "## Root Cause",
            "Unknown. Investigation stopped after the configured failure budget.",
            "",
            "## Prevention",
            "Resume only with a new testable hypothesis or new evidence.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")
    return path


def write_root_cause_handoff(project_root: Path, context: dict) -> Path:
    reports = project_root / ".agent/reports"
    reports.mkdir(parents=True, exist_ok=True)
    path = reports / "ROOT_CAUSE_HANDOFF.md"
    attempts = context.get("failure_budget", {}).get("failed_attempts", [])
    lines = [
        "# ROOT_CAUSE_HANDOFF.md",
        "",
        "## Status: BLOCKED_HANDOFF",
        f"- task_id: {context.get('task_id', 'unknown')}",
        f"- failure_budget: {len(attempts)} / {context.get('failure_budget', {}).get('limit', 3)}",
        "",
        "## Failed Attempts",
        "",
    ]
    for index, attempt in enumerate(attempts, 1):
        lines.extend(
            [
                f"### {index}. {attempt.get('stage', 'unknown')}",
                f"- Hypothesis: {attempt.get('hypothesis', '')}",
                f"- Evidence: {attempt.get('evidence', '')}",
                f"- Run: {attempt.get('run_id') or 'n/a'}",
                "",
            ]
        )
    lines.extend(
        [
            "## Resume Condition",
            "Provide a new falsifiable hypothesis or new observable evidence. Do not repeat an exhausted attempt.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")
    return path


import os

class ReviewLifecycleGuard:
    def __init__(self, project_root: Path, task_id: str, mode: str, checkpoint_authorization: dict | None = None):
        if checkpoint_authorization is None:
            raise ValueError("checkpoint_authorization cannot be None")
        self.project_root = project_root
        self.task_id = task_id
        self.mode = mode
        self.checkpoint_authorization = checkpoint_authorization
        self.lock_path = self.project_root / ".agent" / "state" / f"{task_id}_{mode}.lock"
        self._fd = None

    def acquire(self):
        try:
            # os.O_CREAT | os.O_EXCL | os.O_WRONLY is cross-platform atomic file creation
            self._fd = os.open(str(self.lock_path), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            os.write(self._fd, str(os.getpid()).encode())
        except FileExistsError:
            raise RuntimeError("RUN_ALREADY_ACTIVE")

    def release(self):
        if self._fd is not None:
            os.close(self._fd)
            try:
                os.remove(str(self.lock_path))
            except OSError:
                pass
            self._fd = None

    def __enter__(self):
        self.acquire()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.release()

