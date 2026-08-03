"""Adapters and handoff contracts for the Antigravity + Codex workflow.

The factory deliberately uses a single-writer topology: Antigravity may edit the
live task worktree, while Codex receives a read-only snapshot for independent
review.  The agents communicate through versioned files instead of sharing chat
state, which keeps retries resumable and auditable.
"""

from __future__ import annotations

import json
import hashlib
import os
import shutil
import subprocess
import uuid
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from pathlib import Path


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _find_executable(candidates: list[str]) -> str | None:
    for candidate in candidates:
        resolved = shutil.which(candidate)
        if resolved and Path(resolved).suffix.lower() != ".ps1":
            return resolved
        path = Path(candidate)
        if path.is_file():
            return str(path)
    return None


def find_codex() -> str | None:
    """Prefer native/cmd launchers; PowerShell shims may be policy-blocked."""
    return _find_executable([
        "codex",
        "codex.cmd",
        "codex.exe",
        r"C:\Users\Admin\AppData\Local\Programs\OpenAI\Codex\bin\codex.EXE",
        r"C:\Users\Admin\AppData\Roaming\npm\codex.cmd",
    ])


def find_antigravity() -> str | None:
    return _find_executable([
        "agy.exe",
        "agy",
        r"C:\Users\Admin\AppData\Local\agy\bin\agy.exe",
    ])


@dataclass
class RuntimeCheck:
    name: str
    status: str
    executable: str | None = None
    detail: str = ""


INFERENCE_SENTINEL = "AGY_INFERENCE_READY"


def _terminate_process_tree(proc, grace_seconds: int = 10) -> tuple[str, str]:
    """Terminate a complete CLI process tree and drain its pipes."""
    if os.name == "nt":
        try:
            subprocess.run(
                ["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                capture_output=True, timeout=grace_seconds,
            )
        except Exception:
            pass
    try:
        proc.kill()
    except Exception:
        pass
    try:
        return proc.communicate(timeout=grace_seconds)
    except Exception:
        return "", ""


def _probe(command: list[str], cwd: Path, timeout: int = 20) -> tuple[int, str]:
    try:
        proc = subprocess.Popen(
            command,
            cwd=cwd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        stdout, stderr = proc.communicate(timeout=timeout)
        output = ((stdout or "") + "\n" + (stderr or "")).strip()
        return proc.returncode, output[-4000:]
    except subprocess.TimeoutExpired:
        stdout, stderr = _terminate_process_tree(proc)
        return -1, f"Probe timed out after {timeout}s"
    except Exception as exc:
        return -1, str(exc)


def diagnose(project_root: Path) -> dict:
    """Return machine-readable diagnostics without changing the repository."""
    source_root = project_root / "source-code"
    checks: list[RuntimeCheck] = []

    codex = find_codex()
    if not codex:
        checks.append(RuntimeCheck("codex", "MISSING", detail="Codex CLI executable not found"))
    else:
        code, output = _probe([codex, "--version"], source_root)
        status = "READY" if code == 0 else "BROKEN"
        previous_manifest = project_root / ".agent/state/review_run.json"
        if previous_manifest.exists():
            previous_text = previous_manifest.read_text(encoding="utf-8", errors="replace")
            if "orchestrator_helper_launch_failed" in previous_text:
                output = (
                    output + "\nPrevious real review could not launch the Windows sandbox helper; "
                    "repository inspection was degraded. Current Codex CLI probe still decides readiness."
                ).strip()
        checks.append(RuntimeCheck(
            "codex", status, codex,
            output or f"exit_code={code}",
        ))

    agy = find_antigravity()
    if not agy:
        checks.append(RuntimeCheck("antigravity", "MISSING", detail="agy CLI executable not found"))
    else:
        code, output = _probe([agy, "models"], source_root, timeout=30)
        lower = output.lower()
        if "not logged" in lower or "please sign in" in lower:
            status = "AUTH_REQUIRED"
        elif "access is denied" in lower and code != 0:
            status = "PERMISSION_BLOCKED"
        elif code != 0:
            status = "BROKEN"
        else:
            probe_prompt = (
                "Read-only runtime readiness probe. Do not edit files. "
                f"Output exactly {INFERENCE_SENTINEL} and nothing else."
            )
            infer_code, infer_output = _probe(
                [agy, "--print", "--mode", "plan", "--print-timeout", "30s", probe_prompt],
                source_root, timeout=30,
            )
            infer_lower = infer_output.lower()
            if "not logged" in infer_lower or "please sign in" in infer_lower:
                status = "AUTH_REQUIRED"
            elif infer_code == -1 and "timed out" in infer_lower:
                status = "INFERENCE_TIMEOUT"
            elif infer_code != 0:
                status = "BROKEN"
            elif infer_output.strip() != INFERENCE_SENTINEL:
                status = "BROKEN_OUTPUT"
            else:
                status = "READY"
            output = (output + "\nInference: " + infer_output).strip()
        checks.append(RuntimeCheck("antigravity", status, agy, output or f"exit_code={code}"))

    return {
        "schema_version": 1,
        "checked_at": utc_now(),
        "project_root": str(project_root),
        "topology": "single_writer_independent_verifier",
        "writer": "antigravity",
        "verifier": "codex",
        "checks": [asdict(item) for item in checks],
        "ready": all(item.status == "READY" for item in checks),
    }


def build_handoff(project_root: Path, task_id: str, feature: str, cycle: int,
                  mode: str, review_manifest: dict) -> dict:
    task_context_path = project_root / ".agent/context/TASK_CONTEXT.json"
    task_context = {}
    if task_context_path.exists():
        task_context = json.loads(task_context_path.read_text(encoding="utf-8"))
    return {
        "schema_version": 1,
        "handoff_id": str(uuid.uuid4()),
        "created_at": utc_now(),
        "from_agent": "codex",
        "to_agent": "antigravity",
        "role_contract": {
            "writer": "antigravity",
            "verifier": "codex",
            "concurrent_writes_allowed": False,
        },
        "task_id": task_id,
        "feature": feature,
        "mode": mode,
        "cycle": cycle,
        "review_run_id": review_manifest.get("run_id"),
        "review_snapshot_hash": review_manifest.get("snapshot_hash"),
        "allowed_files": task_context.get("scope", {}).get("allowed_files", []),
        "forbidden": task_context.get("scope", {}).get("forbidden", []),
        "findings": review_manifest.get("findings", []),
        "required_inputs": [
            ".agent/reports/CODEX_REVIEW.md",
            ".agent/context/MEMORY_CONTEXT.md",
            ".agent/context/TASK_CONTEXT.json",
            ".agent/context/TASK_SCOPE.json",
            ".agent/context/ACCEPTANCE_CRITERIA.md",
        ],
        "completion_contract": {
            "must_change_snapshot": True,
            "must_stay_in_scope": True,
            "must_run_verification": True,
            "must_not_claim_codex_pass": True,
        },
    }


def write_handoff(project_root: Path, handoff: dict) -> Path:
    path = project_root / ".agent/reports/FIXER_HANDOFF.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(handoff, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return path


def antigravity_prompt(handoff_path: Path) -> str:
    return f"""You are the single writer in a dual-agent software pipeline.
Read the machine handoff at {handoff_path} and every required input it names.
Fix only evidence-backed Codex findings and only inside allowed_files. Never edit
forbidden paths or pipeline state/reports. Add focused regression tests. Do not
run Codex and do not claim review passed. Finish by summarizing changed files,
tests run, remaining risks, and a falsifiable retry hypothesis.
"""


def _scoped_writer_snapshot(project_root: Path, handoff: dict) -> str:
    hasher = hashlib.sha256()
    roots = [project_root / "source-code", project_root]
    files = {}
    for pattern in handoff.get("allowed_files", []):
        normalized = str(pattern).replace("\\", "/")
        for root in roots:
            try:
                for path in root.glob(normalized):
                    if path.is_file() and ".agent/state" not in path.as_posix() and ".agent/reports" not in path.as_posix():
                        files[str(path.resolve()).lower()] = path
            except (OSError, ValueError):
                continue
    for key, path in sorted(files.items()):
        hasher.update(key.encode("utf-8", errors="replace"))
        hasher.update(b"\0")
        hasher.update(path.read_bytes())
        hasher.update(b"\0")
    return hasher.hexdigest()


def run_antigravity_fixer(project_root: Path, handoff: dict, *, timeout_seconds: int = 900,
                           model: str | None = None, agent: str | None = None) -> tuple[bool, str]:
    """Run Antigravity non-interactively as the only writer.

    Authentication and home-directory permissions are intentionally treated as
    infrastructure failures.  The pipeline never falls back to an unsafe shell
    command or pretends a fixer is running.
    """
    executable = find_antigravity()
    reports = project_root / ".agent/reports"
    reports.mkdir(parents=True, exist_ok=True)
    if not executable:
        return False, "ANTIGRAVITY_CLI_MISSING"

    handoff_path = write_handoff(project_root, handoff)
    command = [
        executable,
        "--print",
        "--mode", "accept-edits",
        "--print-timeout", f"{max(60, timeout_seconds)}s",
    ]
    if model:
        command.extend(["--model", model])
    if agent:
        command.extend(["--agent", agent])
    command.append(antigravity_prompt(handoff_path))

    started = utc_now()
    snapshot_before = _scoped_writer_snapshot(project_root, handoff)
    proc = None
    return_code = None
    reason_code = "WRITER_LAUNCH_FAILED"
    try:
        proc = subprocess.Popen(
            command,
            cwd=project_root / "source-code",
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        stdout, stderr = proc.communicate(timeout=timeout_seconds + 30)
        return_code = proc.returncode
        combined = ((stdout or "") + "\n" + (stderr or "")).strip()
        lower = combined.lower()
        if "not logged" in lower or "please sign in" in lower:
            status, reason, reason_code = "BLOCKED_AUTH", "ANTIGRAVITY_AUTH_REQUIRED", "WRITER_AUTH_REQUIRED"
        elif return_code == 0:
            status, reason, reason_code = "PASS", "Antigravity fixer completed", "WRITER_COMPLETED"
        else:
            status, reason, reason_code = "INFRA_FAIL", f"Antigravity exited with code {return_code}", "WRITER_NONZERO_EXIT"
    except subprocess.TimeoutExpired:
        stdout, stderr = _terminate_process_tree(proc)
        combined = ((stdout or "") + "\n" + (stderr or "")).strip()
        status, reason, reason_code = "INFRA_FAIL", f"Antigravity timeout after {timeout_seconds}s", "WRITER_TIMEOUT"
    except Exception as exc:
        combined = str(exc)
        status, reason, reason_code = "INFRA_FAIL", f"Antigravity launch failed: {exc}", "WRITER_LAUNCH_FAILED"

    snapshot_after = _scoped_writer_snapshot(project_root, handoff)
    artifact_changed = snapshot_before != snapshot_after
    
    if status == "PASS" and not artifact_changed:
        status = "BLOCKED_NO_FIX_DELTA"
        reason_code = "WRITER_NO_DELTA"
        reason = "Antigravity completed without changing scoped source artifacts."
        
    report = {
        "schema_version": 1,
        "started_at": started,
        "completed_at": utc_now(),
        "status": status,
        "reason": reason,
        "reason_code": reason_code,
        "handoff_id": handoff.get("handoff_id"),
        "executable": executable,
        "exit_code": return_code,
        "output": combined[-20000:],
        "snapshot_before": snapshot_before,
        "snapshot_after": snapshot_after,
        "artifact_changed": artifact_changed,
    }
    (reports / "FIXER_COMMAND_REPORT.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    return status == "PASS", reason
