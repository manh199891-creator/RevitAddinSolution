"""Offline learning and gated evolution for AI Software Factory.

This module implements P2-P5 without allowing a model to rewrite production
artifacts directly.  Markdown/JSONL remain the durable source of truth; every
candidate is evaluated in shadow mode, gated, canaried, and explicitly promoted.
"""

from __future__ import annotations

import hashlib
import json
import re
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable


SCHEMA_VERSION = 1
TERMINAL_SUCCESS = {"PASS", "SMOKE_PASS", "ALLOW_RELEASE", "READY_FOR_RELEASE"}
DANGEROUS_PATTERNS = (
    r"ignore\s+(all\s+)?previous\s+instructions",
    r"(?:rm\s+-rf|del\s+/[fsq]|remove-item\s+.+-recurse)",
    r"(?:print|echo|upload|send).{0,40}(?:api[_ -]?key|token|credential|secret)",
    r"dangerously-skip-permissions",
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    temp.replace(path)


def _sha256(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8", errors="replace")).hexdigest()


def learning_root(project_root: Path) -> Path:
    return project_root / ".agent/learning"


def ensure_layout(project_root: Path) -> None:
    root = learning_root(project_root)
    for relative in ("trajectories", "datasets", "candidates", "runs", "canary"):
        (root / relative).mkdir(parents=True, exist_ok=True)


def append_trajectory(project_root: Path, event: dict) -> Path:
    """Append an immutable event to the task trajectory JSONL."""
    ensure_layout(project_root)
    task_id = str(event.get("task_id") or "unscoped")
    safe_id = re.sub(r"[^a-zA-Z0-9_-]+", "-", task_id).strip("-").lower() or "unscoped"
    path = learning_root(project_root) / "trajectories" / f"{safe_id}.jsonl"
    payload = {
        "schema_version": SCHEMA_VERSION,
        "event_id": str(uuid.uuid4()),
        "recorded_at": utc_now(),
        **event,
    }
    with path.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(payload, ensure_ascii=False) + "\n")
    return path


def record_pipeline_outcome(project_root: Path, *, task_id: str, feature: str,
                            mode: str, status: str, steps: list[dict], reason: str = "") -> Path:
    return append_trajectory(project_root, {
        "type": "pipeline_outcome",
        "task_id": task_id,
        "feature": feature,
        "mode": mode,
        "status": status,
        "success": status.upper() in TERMINAL_SUCCESS,
        "reason": reason,
        "steps": steps,
    })


def label_outcome(project_root: Path, task_id: str, label: str, note: str = "") -> Path:
    normalized = label.strip().lower()
    if normalized not in {"success", "failure", "partial", "regression"}:
        raise ValueError("label must be success, failure, partial, or regression")
    return append_trajectory(project_root, {
        "type": "human_label",
        "task_id": task_id,
        "label": normalized,
        "note": note,
    })


def _read_jsonl(path: Path) -> Iterable[dict]:
    if not path.exists():
        return []
    records = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        try:
            records.append(json.loads(line))
        except json.JSONDecodeError as exc:
            raise ValueError(f"Invalid JSONL at {path}:{line_number}: {exc}") from exc
    return records


def build_dataset(project_root: Path, name: str = "operations") -> dict:
    """Build a reproducible train/validation/holdout dataset from trajectories."""
    ensure_layout(project_root)
    events: list[dict] = []
    for path in sorted((learning_root(project_root) / "trajectories").glob("*.jsonl")):
        events.extend(_read_jsonl(path))

    latest_labels: dict[str, dict] = {}
    outcomes: list[dict] = []
    for event in events:
        if event.get("type") == "human_label":
            latest_labels[event.get("task_id", "")] = event
        elif event.get("type") == "pipeline_outcome":
            outcomes.append(event)

    examples = []
    for event in outcomes:
        task_id = event.get("task_id", "unknown")
        digest = int(_sha256(task_id)[:8], 16) % 100
        split = "train" if digest < 70 else ("validation" if digest < 85 else "holdout")
        label_event = latest_labels.get(task_id)
        label = label_event.get("label") if label_event else ("success" if event.get("success") else "failure")
        failed_steps = [
            step.get("name") for step in event.get("steps", [])
            if str(step.get("status", "")).upper() not in {"PASS", "DONE", "SKIPPED"}
        ]
        examples.append({
            "id": event.get("event_id"),
            "task_id": task_id,
            "input": event.get("feature", ""),
            "mode": event.get("mode", ""),
            "expected_outcome": label,
            "failed_steps": failed_steps,
            "evidence": event.get("reason", ""),
            "split": split,
            "rubric": {
                "required_terms": [],
                "forbidden_terms": ["ignore previous instructions", "dangerously-skip-permissions"],
            },
        })

    dataset = {
        "schema_version": SCHEMA_VERSION,
        "name": name,
        "created_at": utc_now(),
        "source": "trajectory_jsonl",
        "source_event_count": len(events),
        "examples": examples,
        "counts": {
            split: sum(1 for item in examples if item["split"] == split)
            for split in ("train", "validation", "holdout")
        },
    }
    path = learning_root(project_root) / "datasets" / f"{name}.json"
    _write_json(path, dataset)
    dataset["path"] = str(path)
    return dataset


def _security_findings(text: str) -> list[str]:
    return [pattern for pattern in DANGEROUS_PATTERNS if re.search(pattern, text, re.IGNORECASE | re.DOTALL)]


def _score_artifact(text: str, example: dict) -> float:
    rubric = example.get("rubric", {})
    required = [str(term).lower() for term in rubric.get("required_terms", [])]
    forbidden = [str(term).lower() for term in rubric.get("forbidden_terms", [])]
    lower = text.lower()
    required_score = 1.0 if not required else sum(term in lower for term in required) / len(required)
    forbidden_score = 1.0 if not any(term in lower for term in forbidden) else 0.0
    structure_score = 1.0 if ("#" in text and len(text.strip()) >= 40) else 0.5
    return round(0.55 * required_score + 0.30 * forbidden_score + 0.15 * structure_score, 6)


def shadow_evaluate(project_root: Path, baseline_path: Path, candidate_path: Path,
                    dataset_path: Path, *, min_improvement: float = 0.02,
                    max_growth: float = 0.25) -> dict:
    """Compare a candidate without changing the active artifact."""
    baseline = baseline_path.read_text(encoding="utf-8")
    candidate = candidate_path.read_text(encoding="utf-8")
    dataset = json.loads(dataset_path.read_text(encoding="utf-8"))
    examples = dataset.get("examples", [])
    per_example = []
    for example in examples:
        baseline_score = _score_artifact(baseline, example)
        candidate_score = _score_artifact(candidate, example)
        per_example.append({
            "id": example.get("id"),
            "split": example.get("split", "train"),
            "baseline": baseline_score,
            "candidate": candidate_score,
            "delta": round(candidate_score - baseline_score, 6),
        })

    def average(items: list[dict], key: str) -> float:
        return round(sum(item[key] for item in items) / len(items), 6) if items else 0.0

    split_metrics = {}
    for split in ("train", "validation", "holdout"):
        selected = [item for item in per_example if item["split"] == split]
        b_score, c_score = average(selected, "baseline"), average(selected, "candidate")
        split_metrics[split] = {
            "count": len(selected),
            "baseline": b_score,
            "candidate": c_score,
            "delta": round(c_score - b_score, 6),
        }

    growth = (len(candidate) - len(baseline)) / max(1, len(baseline))
    security_findings = _security_findings(candidate)
    holdout = split_metrics["holdout"]
    validation = split_metrics["validation"]
    enough_evidence = holdout["count"] > 0 and validation["count"] > 0
    eligible = (
        enough_evidence
        and holdout["delta"] >= 0
        and validation["delta"] >= min_improvement
        and growth <= max_growth
        and not security_findings
    )
    run = {
        "schema_version": SCHEMA_VERSION,
        "run_id": str(uuid.uuid4()),
        "created_at": utc_now(),
        "mode": "shadow",
        "baseline": str(baseline_path),
        "candidate": str(candidate_path),
        "dataset": str(dataset_path),
        "baseline_hash": _sha256(baseline),
        "candidate_hash": _sha256(candidate),
        "dataset_hash": _sha256(dataset_path.read_text(encoding="utf-8")),
        "growth": round(growth, 6),
        "max_growth": max_growth,
        "min_improvement": min_improvement,
        "security_findings": security_findings,
        "metrics": split_metrics,
        "per_example": per_example,
        "eligible_for_gate": eligible,
        "reason": "ELIGIBLE" if eligible else "INSUFFICIENT_OR_REGRESSING_EVIDENCE",
    }
    path = learning_root(project_root) / "runs" / f"{run['run_id']}.json"
    _write_json(path, run)
    run["path"] = str(path)
    return run


def gate_candidate(project_root: Path, run_path: Path, *, evidence_required: bool = True) -> dict:
    """Approve a shadow candidate for canary, never for direct production use."""
    run = json.loads(run_path.read_text(encoding="utf-8"))
    issues = []
    if not run.get("eligible_for_gate"):
        issues.append("Shadow evaluation is not eligible.")
    if run.get("mode") != "shadow":
        issues.append("Evaluation mode is not shadow.")
    if _sha256(Path(run["candidate"]).read_text(encoding="utf-8")) != run.get("candidate_hash"):
        issues.append("Candidate changed after evaluation.")
    if evidence_required:
        evidence_path = project_root / ".agent/state/EVIDENCE_MANIFEST.json"
        if not evidence_path.exists():
            issues.append("Fresh verification evidence is missing.")
        else:
            evidence = json.loads(evidence_path.read_text(encoding="utf-8"))
            if evidence.get("status") != "PASS" or not evidence.get("fresh"):
                issues.append("Verification evidence is not fresh PASS.")

    gate = {
        "schema_version": SCHEMA_VERSION,
        "gate_id": str(uuid.uuid4()),
        "created_at": utc_now(),
        "evaluation_run_id": run.get("run_id"),
        "candidate": run.get("candidate"),
        "candidate_hash": run.get("candidate_hash"),
        "status": "APPROVED_FOR_CANARY" if not issues else "BLOCKED",
        "issues": issues,
        "human_approval_required": True,
        "production_applied": False,
    }
    path = learning_root(project_root) / "candidates" / f"{gate['gate_id']}.json"
    _write_json(path, gate)
    gate["path"] = str(path)
    return gate


def start_canary(project_root: Path, gate_path: Path, percent: int = 10) -> dict:
    gate = json.loads(gate_path.read_text(encoding="utf-8"))
    if gate.get("status") != "APPROVED_FOR_CANARY":
        raise ValueError("Candidate is not approved for canary")
    if not 1 <= percent <= 50:
        raise ValueError("Canary percent must be between 1 and 50")
    canary = {
        "schema_version": SCHEMA_VERSION,
        "canary_id": str(uuid.uuid4()),
        "created_at": utc_now(),
        "gate_id": gate.get("gate_id"),
        "candidate": gate.get("candidate"),
        "candidate_hash": gate.get("candidate_hash"),
        "traffic_percent": percent,
        "status": "ACTIVE",
        "successes": 0,
        "failures": 0,
        "regressions": 0,
        "human_approval_required_for_promotion": True,
    }
    path = learning_root(project_root) / "canary" / f"{canary['canary_id']}.json"
    _write_json(path, canary)
    canary["path"] = str(path)
    return canary


def record_canary_outcome(canary_path: Path, outcome: str) -> dict:
    canary = json.loads(canary_path.read_text(encoding="utf-8"))
    normalized = outcome.lower()
    if normalized not in {"success", "failure", "regression"}:
        raise ValueError("outcome must be success, failure, or regression")
    canary[normalized + "s"] = int(canary.get(normalized + "s", 0)) + 1
    canary["updated_at"] = utc_now()
    total = canary.get("successes", 0) + canary.get("failures", 0) + canary.get("regressions", 0)
    if canary.get("regressions", 0) > 0:
        canary["status"] = "ROLLBACK_REQUIRED"
    elif total >= 10 and canary.get("successes", 0) / total >= 0.9:
        canary["status"] = "READY_FOR_HUMAN_PROMOTION"
    _write_json(canary_path, canary)
    return canary


def continuous_status(project_root: Path) -> dict:
    """Summarize P5 state; this command never promotes automatically."""
    ensure_layout(project_root)
    canaries = [json.loads(path.read_text(encoding="utf-8")) for path in sorted(
        (learning_root(project_root) / "canary").glob("*.json")
    )]
    return {
        "schema_version": SCHEMA_VERSION,
        "checked_at": utc_now(),
        "active": [item for item in canaries if item.get("status") == "ACTIVE"],
        "ready_for_human_promotion": [
            item for item in canaries if item.get("status") == "READY_FOR_HUMAN_PROMOTION"
        ],
        "rollback_required": [item for item in canaries if item.get("status") == "ROLLBACK_REQUIRED"],
        "auto_promotion_enabled": False,
    }
