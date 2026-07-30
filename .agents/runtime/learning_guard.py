"""Compact operational memory for the dual-agent pipeline.

Learning Guard turns past reports and trajectories into small, durable context
files.  It deliberately avoids auto-editing prompts or production code.  The
pipeline can include MEMORY_CONTEXT.md in Antigravity and Codex prompts without
paying the token cost of the full knowledge base.
"""

from __future__ import annotations

import json
import re
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


SCHEMA_VERSION = 1
DEFAULT_MAX_CONTEXT_CHARS = 4000

PATTERNS = [
    {
        "id": "missing_bridge_action",
        "severity": "P1",
        "terms": ["missing required parameter: -action", "dual_orchestrate.ps1 -mode"],
        "prevention": "Call dual_orchestrate.ps1 with -Action and -Project. Never use -Mode alone.",
    },
    {
        "id": "invalid_codex_schema",
        "severity": "P1",
        "terms": ["invalid_json_schema", "uniqueitems", "text.format.schema"],
        "prevention": "Keep Codex structured-output schemas within the supported subset; do not use uniqueItems.",
    },
    {
        "id": "dirty_baseline_or_scope",
        "severity": "P1",
        "terms": ["out_of_scope", "no_task_delta", "blocked_scope", "blocked_baseline"],
        "prevention": "Initialize a clean task baseline before copying task changes; never widen Allowed to hide unrelated dirty files.",
    },
    {
        "id": "blind_retry_after_failure_budget",
        "severity": "P1",
        "terms": ["failure budget exhausted", "resume-hypothesis", "unchanged snapshot"],
        "prevention": "Do not rerun the same hypothesis. Change scoped code or provide a new falsifiable resume hypothesis.",
    },
    {
        "id": "fake_background_run",
        "severity": "P1",
        "terms": ["no log outputs", "wait for pipeline", "timer has expired"],
        "prevention": "Verify an active process plus fresh review_run.json/report timestamps before claiming background work is running.",
    },
    {
        "id": "antigravity_auth_or_fixer",
        "severity": "P2",
        "terms": ["not logged into antigravity", "auth_required", "no fixer command configured"],
        "prevention": "Run doctor before auto-fix. If Antigravity is not READY, write handoff and stop instead of claiming fixer progress.",
    },
    {
        "id": "review_batch_context_split",
        "severity": "P2",
        "terms": ["test project links", "reviewed in a different batch", "batch reviews"],
        "prevention": "Keep related production, project, and test files together in review context where possible.",
    },
    {
        "id": "codex_sandbox_degraded",
        "severity": "P3",
        "terms": ["orchestrator_helper_launch_failed", "sandbox helper"],
        "prevention": "Treat sandbox helper errors as a diagnostic warning unless Codex cannot return a valid review contract.",
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    temp.replace(path)


def _read_text(path: Path, limit: int = 60000) -> str:
    if not path.exists() or not path.is_file():
        return ""
    text = path.read_text(encoding="utf-8", errors="replace")
    return text[-limit:]


def _iter_jsonl(path: Path) -> list[dict]:
    if not path.exists():
        return []
    rows = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if not line.strip():
            continue
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return rows


def _compact(value: str, limit: int = 220) -> str:
    value = re.sub(r"\s+", " ", value or "").strip()
    return value[:limit].rstrip() + ("..." if len(value) > limit else "")


def _classify(text: str) -> list[dict]:
    lower = text.lower()
    matches = []
    for item in PATTERNS:
        hit_terms = [term for term in item["terms"] if term in lower]
        if hit_terms:
            matches.append({
                "id": item["id"],
                "severity": item["severity"],
                "matched_terms": hit_terms,
                "prevention": item["prevention"],
            })
    return matches


def _collect_sources(project_root: Path, task_id: str) -> list[dict]:
    reports = project_root / ".agent/reports"
    knowledge = project_root / ".agent/knowledge"
    learning = project_root / ".agent/learning"
    sources = []

    for name in (
        "DUAL_AGENT_REPORT.md",
        "CODEX_REVIEW.md",
        "ROOT_CAUSE_HANDOFF.md",
        "FIXER_HANDOFF.md",
        "RELEASE_GATE_REPORT.md",
        "GUARDRAILS_REPORT.md",
        "DUAL_AGENT_DOCTOR.json",
        "FIXER_COMMAND_REPORT.json",
    ):
        path = reports / name
        text = _read_text(path)
        if text:
            sources.append({"path": str(path), "kind": "report", "text": text})

    bug_dir = knowledge / "memory/bugs"
    if bug_dir.exists():
        for path in sorted(bug_dir.glob("*.md"))[-20:]:
            text = _read_text(path)
            if text:
                sources.append({"path": str(path), "kind": "bug_memory", "text": text})

    trajectory = learning / "trajectories" / f"{task_id}.jsonl"
    rows = _iter_jsonl(trajectory)[-30:]
    for row in rows:
        sources.append({
            "path": str(trajectory),
            "kind": "trajectory",
            "text": json.dumps(row, ensure_ascii=False),
        })
    return sources


def build_learning_guard(project_root: Path, task_id: str, feature: str, mode: str,
                         *, max_context_chars: int = DEFAULT_MAX_CONTEXT_CHARS) -> dict:
    """Build compact memory context and tripwire report for one task."""
    sources = _collect_sources(project_root, task_id)
    findings = []
    for source in sources:
        for match in _classify(source["text"]):
            findings.append({**match, "source": source["path"], "kind": source["kind"]})

    counts = Counter(item["id"] for item in findings)
    by_id = {}
    for item in findings:
        by_id.setdefault(item["id"], item)

    rules = []
    for pattern_id, count in counts.most_common():
        item = by_id[pattern_id]
        rules.append({
            "id": pattern_id,
            "severity": item["severity"],
            "count": count,
            "prevention": item["prevention"],
            "example_source": item["source"],
        })

    recent_outcomes = []
    for source in sources:
        if source["kind"] == "trajectory":
            try:
                row = json.loads(source["text"])
            except json.JSONDecodeError:
                continue
            recent_outcomes.append({
                "status": row.get("status"),
                "mode": row.get("mode"),
                "reason": _compact(row.get("reason", "")),
            })
    recent_outcomes = recent_outcomes[-8:]

    payload = {
        "schema_version": SCHEMA_VERSION,
        "generated_at": utc_now(),
        "task_id": task_id,
        "feature": feature,
        "mode": mode,
        "source_count": len(sources),
        "matched_rule_count": len(rules),
        "rules": rules,
        "recent_outcomes": recent_outcomes,
        "token_policy": {
            "strategy": "compact_index_first",
            "max_context_chars": max_context_chars,
            "full_memory_loaded_by_default": False,
        },
    }

    context = render_memory_context(payload, max_context_chars=max_context_chars)
    context_dir = project_root / ".agent/context"
    reports_dir = project_root / ".agent/reports"
    context_dir.mkdir(parents=True, exist_ok=True)
    reports_dir.mkdir(parents=True, exist_ok=True)
    (context_dir / "MEMORY_CONTEXT.md").write_text(context, encoding="utf-8")
    _write_json(reports_dir / "LEARNING_GUARD.json", payload)
    (reports_dir / "LEARNING_GUARD.md").write_text(render_learning_guard_report(payload), encoding="utf-8")
    return payload


def render_memory_context(payload: dict, *, max_context_chars: int = DEFAULT_MAX_CONTEXT_CHARS) -> str:
    lines = [
        "# MEMORY_CONTEXT.md",
        "",
        "Compact operational memory for this task. Use these prevention rules before planning, coding, fixing, or reviewing.",
        "",
        f"- task_id: {payload.get('task_id')}",
        f"- mode: {payload.get('mode')}",
        f"- source_count: {payload.get('source_count')}",
        f"- matched_rule_count: {payload.get('matched_rule_count')}",
        "",
        "## Prevention Rules",
    ]
    rules = payload.get("rules", [])
    if not rules:
        lines.append("- No recurring project-specific failures matched. Continue with normal guardrails.")
    else:
        for item in rules[:8]:
            lines.append(
                f"- {item['severity']} {item['id']} ({item['count']} hits): {item['prevention']}"
            )
    lines.extend(["", "## Recent Outcomes"])
    outcomes = payload.get("recent_outcomes", [])
    if not outcomes:
        lines.append("- No recent trajectory outcomes recorded for this task.")
    else:
        for item in outcomes[-5:]:
            lines.append(f"- {item.get('mode')} {item.get('status')}: {item.get('reason')}")
    lines.extend([
        "",
        "## Token Policy",
        "- This file is the default memory payload for agents.",
        "- Load full bug memories only when a listed prevention rule is directly relevant.",
    ])
    text = "\n".join(lines) + "\n"
    if len(text) <= max_context_chars:
        return text
    return text[:max_context_chars].rstrip() + "\n\n[TRUNCATED: compact memory context exceeded budget]\n"


def render_learning_guard_report(payload: dict) -> str:
    lines = [
        "# LEARNING_GUARD.md",
        "",
        "## Status: PASS",
        f"- task_id: {payload.get('task_id')}",
        f"- mode: {payload.get('mode')}",
        f"- generated_at: {payload.get('generated_at')}",
        f"- source_count: {payload.get('source_count')}",
        f"- matched_rule_count: {payload.get('matched_rule_count')}",
        "",
        "## Token Strategy",
        "- Agents read `.agent/context/MEMORY_CONTEXT.md` by default.",
        "- Full memories stay in `.agent/knowledge/**` and are loaded only when a rule matches the active issue.",
        "",
        "## Rules",
    ]
    if not payload.get("rules"):
        lines.append("- No recurring rules matched.")
    else:
        for item in payload["rules"]:
            lines.append(f"- **{item['id']}** [{item['severity']}] hits={item['count']}: {item['prevention']}")
            lines.append(f"  source: `{item['example_source']}`")
    lines.append("")
    return "\n".join(lines)

