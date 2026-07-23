import subprocess, sys, json, fnmatch, hashlib, uuid, time, shutil, tempfile
from pathlib import Path
from datetime import datetime, timezone
import os
from learning_guard import build_learning_guard

class ReviewStatus:
    QUEUED = "QUEUED"
    PREPARING = "PREPARING"
    RUNNING = "RUNNING"
    PASS = "PASS"
    FAIL = "FAIL"
    INFRA_FAIL = "INFRA_FAIL"
    STALE = "STALE"
    CANCELLED = "CANCELLED"
    BLOCKED_SCOPE = "BLOCKED_SCOPE"
    BLOCKED_NO_DELTA = "BLOCKED_NO_DELTA"
    BLOCKED_BASELINE = "BLOCKED_BASELINE"


def _terminate_timed_out_process(proc, grace_seconds=10):
    """Terminate a timed-out Codex process without waiting forever on open pipes."""
    if os.name == "nt":
        try:
            subprocess.run(
                ["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                capture_output=True,
                timeout=grace_seconds,
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


def _copy_review_workspace(repo_root, review_cwd):
    """Copy reviewable source without recursive factory junctions or build output."""
    shutil.copytree(
        repo_root,
        review_cwd,
        ignore=shutil.ignore_patterns(
            ".agents", ".agent", ".git", "bin", "obj", "__pycache__"
        ),
    )

def _cleanup_dir(path):
    if path:
        shutil.rmtree(path, ignore_errors=True)


def _terminate_process_tree(proc):
    """Best-effort process-tree termination so timeout evidence returns promptly."""
    if os.name == "nt":
        try:
            import psutil
            parent = psutil.Process(proc.pid)
            children = parent.children(recursive=True)
            for child in reversed(children):
                child.kill()
            parent.kill()
            psutil.wait_procs(children + [parent], timeout=2)
            return
        except Exception:
            subprocess.run(
                ["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                capture_output=True,
            )
            return
    proc.kill()

def _run_git(repo_root, args):
    result = subprocess.run(["git"] + args, cwd=repo_root, capture_output=True)
    if result.returncode != 0:
        stderr = result.stderr.decode("utf-8", errors="replace")
        raise Exception(f"git {' '.join(args)} failed: {stderr}")
    return result.stdout


def _snapshot_status_args(included_files=None):
    """Build a bounded git-status query that cannot re-enter the local factory.

    In local mode ``source-code`` may be a junction to the repository while the
    factory itself lives below ``.agents/factory``.  Letting Git descend into
    that directory re-enters the same worktree indefinitely and also mixes
    generated pipeline reports into the source snapshot.
    """
    args = ["status", "--porcelain=v2", "-z", "-uall", "--"]
    if included_files:
        args.extend(sorted(set(included_files)))
    else:
        args.append(".")
    args.extend([
        ":(exclude).agents/factory",
        ":(exclude).agents/factory/**",
    ])
    return args


def _snapshot_visible_mapping(files):
    """Drop local-factory metadata from current and legacy snapshot state."""
    return {
        path: value
        for path, value in files.items()
        if not path.replace("\\", "/").lstrip("./").startswith("agents/factory/")
        and path.replace("\\", "/").lstrip("./") != "agents/factory"
    }

def get_deterministic_snapshot(repo_root, included_files=None):
    """
    Runs git status --porcelain=v2 -z -uall to get all changed files.
    Calculates a stable hash of the file contents + metadata.
    Returns: (snapshot_hash, included_files)
    """
    status_args = _snapshot_status_args(included_files)
    raw = _run_git(repo_root, status_args)
    base_rev_result = subprocess.run(["git", "rev-parse", "--verify", "HEAD"], cwd=repo_root, capture_output=True)
    base_rev = base_rev_result.stdout.strip() if base_rev_result.returncode == 0 else b"UNBORN_HEAD"
    
    # -z separates records with \0.
    # Note: Renames in v2 format use two \0 separated fields: path \0 origPath.
    # We will manually iterate the null-terminated strings.
    parts = raw.split(b'\0')
    
    records = []
    changed_files = set()
    i = 0
    while i < len(parts):
        part_bytes = parts[i]
        part = part_bytes.decode('utf-8', errors='surrogateescape')
        if not part:
            i += 1
            continue
            
        if part.startswith('? '):
            # Untracked: "? <path>"
            path = part[2:]
            changed_files.add(path)
            records.append((path, part, None))
            i += 1
        elif part.startswith('1 ') or part.startswith('u '):
            # Ordinary / Unmerged: "1 xy sub mH mI mW hH hI path"
            tokens = part.split(' ', 8)
            if len(tokens) >= 9:
                path = tokens[8]
                changed_files.add(path)
                records.append((path, part, None))
            i += 1
        elif part.startswith('2 '):
            # Renamed: "2 xy sub mH mI mW hH hI Xscore path" followed by "\0 origPath"
            tokens = part.split(' ', 9)
            if len(tokens) >= 10:
                path = tokens[9]
                changed_files.add(path)
                orig = parts[i + 1].decode('utf-8', errors='surrogateescape') if i + 1 < len(parts) else ""
                records.append((path, part, orig))
            i += 2 # Skip the origPath
        else:
            i += 1
            
    # Hash calculation
    # We hash the sorted paths and their contents to ensure determinism
    sorted_files = sorted(list(changed_files))
    hasher = hashlib.sha256()
    hasher.update(b"base_rev\0")
    hasher.update(base_rev)
    hasher.update(b"\0status_v2_z\0")
    hasher.update(raw)
    hasher.update(b"\0records\0")
    for path, record, orig in sorted(records, key=lambda r: (r[0], r[1], r[2] or "")):
        hasher.update(record.encode("utf-8", errors="surrogateescape"))
        hasher.update(b"\0")
        if orig is not None:
            hasher.update(orig.encode("utf-8", errors="surrogateescape"))
        hasher.update(b"\0")
    
    for f in sorted_files:
        f_path = repo_root / f
        hasher.update(f.encode('utf-8', errors="surrogateescape"))
        hasher.update(b'\0')
        index_blob = subprocess.run(["git", "show", f":{f}"], cwd=repo_root, capture_output=True)
        if index_blob.returncode == 0:
            hasher.update(b"INDEX\0")
            hasher.update(index_blob.stdout)
            hasher.update(b"\0")
        if f_path.exists():
            # If it's a file, hash the contents
            if f_path.is_file():
                try:
                    hasher.update(b"WORKTREE\0")
                    with open(f_path, 'rb') as file_obj:
                        while chunk := file_obj.read(8192):
                            hasher.update(chunk)
                except Exception as e:
                    hasher.update(f"ERROR:{e}".encode('utf-8'))
        else:
            # File was deleted
            hasher.update(b'DELETED')
        hasher.update(b'\0')
        
    return hasher.hexdigest(), sorted_files


def _dirty_file_fingerprint(repo_root, relative_path):
    """Fingerprint both index and worktree state for one dirty path."""
    path = relative_path.replace('\\', '/')
    hasher = hashlib.sha256()
    hasher.update(path.encode("utf-8", errors="surrogateescape"))
    for args in (
        ["status", "--porcelain=v2", "-z", "--", path],
        ["diff", "--binary", "HEAD", "--", path],
    ):
        result = subprocess.run(["git"] + args, cwd=repo_root, capture_output=True)
        hasher.update(b"\0")
        hasher.update(result.stdout)
        hasher.update(b"\0")
        hasher.update(result.stderr)
    index_blob = subprocess.run(["git", "show", f":{path}"], cwd=repo_root, capture_output=True)
    hasher.update(b"\0INDEX\0")
    hasher.update(index_blob.stdout if index_blob.returncode == 0 else b"MISSING")
    worktree_path = repo_root / path
    hasher.update(b"\0WORKTREE\0")
    if worktree_path.is_file():
        hasher.update(worktree_path.read_bytes())
    else:
        hasher.update(b"MISSING")
    return hasher.hexdigest()


def capture_task_baseline(project_root, task_id):
    """Persist dirty state that predates a task; never recapture during mode transitions."""
    repo_root = project_root / "source-code"
    snapshot_hash, dirty_files = get_deterministic_snapshot(repo_root)
    base_result = subprocess.run(["git", "rev-parse", "--verify", "HEAD"], cwd=repo_root, capture_output=True, text=True)
    baseline = {
        "schema_version": 1,
        "task_id": task_id,
        "captured_at": datetime.now().astimezone().isoformat(),
        "base_revision": base_result.stdout.strip() if base_result.returncode == 0 else "UNBORN_HEAD",
        "snapshot_hash": snapshot_hash,
        "files": {path: _dirty_file_fingerprint(repo_root, path) for path in dirty_files},
    }
    state_dir = project_root / ".agent/state"
    state_dir.mkdir(parents=True, exist_ok=True)
    path = state_dir / "task_baseline.json"
    path.write_text(json.dumps(baseline, indent=2), encoding="utf-8")
    return baseline


def evaluate_task_scope(repo_root, task_scope_path, baseline_path=None, expected_task_id=None, require_delta=False):
    """Return one authoritative scope decision for guardrails and Codex review."""
    try:
        scope_data = json.loads(task_scope_path.read_text(encoding="utf-8"))
    except Exception:
        scope_data = {}
    explicit_files = {
        str(path).replace("\\", "/").lstrip("./")
        for path in scope_data.get("included_files", [])
        if isinstance(path, str) and path.strip()
    }
    snapshot_hash, current_files = get_deterministic_snapshot(
        repo_root,
        included_files=explicit_files or None,
    )
    baseline = None
    if baseline_path and baseline_path.exists():
        try:
            candidate = json.loads(baseline_path.read_text(encoding="utf-8"))
            if not expected_task_id or candidate.get("task_id") == expected_task_id:
                baseline = candidate
        except Exception:
            baseline = None

    current_fingerprints = {path: _dirty_file_fingerprint(repo_root, path) for path in current_files}
    baseline_files = _snapshot_visible_mapping(baseline.get("files", {})) if baseline else {}
    if explicit_files:
        # Shared worktrees can contain many unrelated dirty files. An explicit
        # inclusion set is authoritative for this task and isolates its review
        # snapshot without modifying or hiding the other changes.
        task_files = sorted(path for path in current_files if path in explicit_files)
        excluded_preexisting = sorted(path for path in current_files if path not in explicit_files)
        baseline_removed = []
    else:
        excluded_preexisting = sorted(
            path for path, fingerprint in current_fingerprints.items()
            if baseline_files.get(path) == fingerprint
        )
        task_files = sorted(path for path in current_files if path not in excluded_preexisting)
        baseline_removed = sorted(path for path in baseline_files if path not in current_fingerprints)

    valid, issues = validate_scope(repo_root, task_files, task_scope_path, expected_task_id=expected_task_id)
    if baseline_removed:
        status = ReviewStatus.BLOCKED_BASELINE
        issues = issues + [f"BASELINE_CHANGED_OR_CLEANED: {path}" for path in baseline_removed]
    elif not valid:
        configuration_error = any(
            issue.startswith(("TASK_SCOPE.json is missing", "Failed to parse TASK_SCOPE.json", "Schema validation failed", "Missing schema"))
            for issue in issues
        )
        status = ReviewStatus.INFRA_FAIL if configuration_error else ReviewStatus.BLOCKED_SCOPE
    elif require_delta and not task_files:
        status = ReviewStatus.BLOCKED_NO_DELTA
        issues = ["NO_TASK_DELTA: no source change was created after task initialization"]
    else:
        status = ReviewStatus.PASS

    task_hasher = hashlib.sha256()
    task_hasher.update((baseline.get("snapshot_hash", "NO_BASELINE") if baseline else "NO_BASELINE").encode("utf-8"))
    for path in task_files:
        task_hasher.update(path.encode("utf-8", errors="surrogateescape"))
        task_hasher.update(current_fingerprints[path].encode("ascii"))
    return {
        "status": status,
        "snapshot_hash": task_hasher.hexdigest() if baseline else snapshot_hash,
        "task_files": task_files,
        "excluded_preexisting_files": excluded_preexisting,
        "baseline_removed_files": baseline_removed,
        "issues": issues,
        "baseline_present": baseline is not None,
    }

def get_review_config(repo_root):
    """
    Reads codex_review config from project_profile.json, applying defaults and env var overrides.
    """
    default_config = {
        "timeout_seconds": 180,
        "max_diff_chars": 150000,
        "max_file_chars": 100000,
        "batch_max_files": 3,
        "batch_max_chars": 70000,
        "max_timeout_retries": 2,
        "quick_max_files": 3,
        "quick_max_changed_lines": 50,
        "deep_min_files": 11,
        "deep_min_changed_lines": 301,
        "focused_retry_enabled": True,
        "compact_retry_context": True,
        "final_deep_review_required": True,
        "sensitive_paths": [
            "**/auth/**", "**/security/**", "**/permissions/**",
            "**/*secret*", "**/*credential*", "**/migrations/**"
        ],
    }
    
    profile_path = repo_root / ".agent/project_profile.json"
    if profile_path.exists():
        try:
            profile = json.loads(profile_path.read_text(encoding="utf-8"))
            if "codex_review" in profile:
                default_config.update(profile["codex_review"])
            if "review_routing" in profile:
                default_config.update(profile["review_routing"])
        except:
            pass
            
    # Env var overrides
    if "CODEX_TIMEOUT_SECONDS" in os.environ:
        try: default_config["timeout_seconds"] = int(os.environ["CODEX_TIMEOUT_SECONDS"])
        except: pass
    if "CODEX_REVIEW_MAX_DIFF_CHARS" in os.environ:
        try: default_config["max_diff_chars"] = int(os.environ["CODEX_REVIEW_MAX_DIFF_CHARS"])
        except: pass
    if "CODEX_REVIEW_MAX_FILE_CHARS" in os.environ:
        try: default_config["max_file_chars"] = int(os.environ["CODEX_REVIEW_MAX_FILE_CHARS"])
        except: pass
        
    return default_config


def determine_review_tier(repo_root, included_files, config):
    """Route a review by change size and sensitive paths."""
    normalized = [path.replace("\\", "/") for path in included_files]
    sensitive_patterns = config.get("sensitive_paths", [])
    def matches_sensitive(path, pattern):
        candidates = [pattern]
        if pattern.startswith("**/"):
            candidates.append(pattern[3:])
        return any(fnmatch.fnmatch(path, candidate) for candidate in candidates)

    sensitive_files = [
        path for path in normalized
        if any(matches_sensitive(path, pattern) for pattern in sensitive_patterns)
    ]
    changed_lines = 0
    diff_result = subprocess.run(
        ["git", "diff", "--numstat", "HEAD", "--", *normalized],
        cwd=repo_root,
        capture_output=True,
    )
    diff_output = diff_result.stdout if diff_result.returncode == 0 else b""
    for line in diff_output.decode("utf-8", errors="replace").splitlines():
        parts = line.split("\t")
        if len(parts) >= 2:
            for value in parts[:2]:
                if value.isdigit():
                    changed_lines += int(value)
    tracked_output = _run_git(repo_root, ["ls-files", "--", *normalized])
    tracked = set(tracked_output.decode("utf-8", errors="replace").splitlines())
    for relative in normalized:
        if relative in tracked:
            continue
        candidate = repo_root / relative
        if candidate.is_file():
            try:
                changed_lines += len(candidate.read_text(encoding="utf-8", errors="replace").splitlines())
            except OSError:
                pass

    file_count = len(normalized)
    if (
        sensitive_files
        or file_count >= int(config.get("deep_min_files", 11))
        or changed_lines >= int(config.get("deep_min_changed_lines", 301))
    ):
        tier = "DEEP"
    elif (
        file_count <= int(config.get("quick_max_files", 3))
        and changed_lines <= int(config.get("quick_max_changed_lines", 50))
    ):
        tier = "QUICK"
    else:
        tier = "STANDARD"
    return {
        "tier": tier,
        "file_count": file_count,
        "changed_lines": changed_lines,
        "sensitive_files": sensitive_files,
    }

def validate_scope(repo_root, changed_files, task_scope_path, expected_task_id=None):
    if not task_scope_path.exists():
        return False, ["TASK_SCOPE.json is missing. Guardrail validation failed."]
        
    try:
        scope = json.loads(task_scope_path.read_text(encoding="utf-8"))
    except Exception as e:
        return False, [f"Failed to parse TASK_SCOPE.json: {e}"]
        
    try:
        import jsonschema
        schema_path = Path(__file__).parent / "schemas" / "task_scope.schema.json"
        if not schema_path.exists():
            return False, [f"Missing schema: {schema_path}"]
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
        jsonschema.validate(instance=scope, schema=schema)
    except Exception as e:
        return False, [f"Schema validation failed: {e}"]
        
    if expected_task_id and scope.get("task_id") != expected_task_id:
        return False, [f"Task ID mismatch. Expected '{expected_task_id}', found '{scope.get('task_id')}' in TASK_SCOPE.json"]
        
    allowed = scope.get("allowed_files", [])
    forbidden = scope.get("forbidden", [])
    
    # Path traversal check
    repo_abs = repo_root.resolve()
    
    issues = []
    for f in changed_files:
        # Resolve path
        f_path = (repo_root / f).resolve()
        try:
            f_path.relative_to(repo_abs)
        except ValueError:
            issues.append(f"PATH_TRAVERSAL: {f}")
            continue
            
        f_norm = f.replace('\\', '/')
        # Strict glob match
        is_forbidden = any(fnmatch.fnmatchcase(f_norm, p) or fnmatch.fnmatchcase(f_norm, p + "/*") for p in forbidden)
        if is_forbidden:
            issues.append(f"FORBIDDEN: {f}")
            continue
            
        is_allowed = any(fnmatch.fnmatchcase(f_norm, p) or fnmatch.fnmatchcase(f_norm, p + "/*") for p in allowed)
        if not is_allowed:
            issues.append(f"OUT_OF_SCOPE: {f}")
            
    if issues:
        return False, issues
    return True, []

def collect_review_diff(repo_root, included_files, max_diff_chars=120000, max_file_chars=40000):
    """
    Collects the exact changed-file evidence that Codex must review.
    We do this ourselves instead of relying on `codex review --uncommitted`
    because some Codex CLI versions reject --uncommitted when a custom prompt
    is provided.
    """
    if not included_files:
        return "No changed files.", False, False

    evidence = []
    is_truncated = False
    file_truncated = False
    current_len = 0
    
    def append_chunk(chunk):
        nonlocal current_len, is_truncated
        if is_truncated: return
        if current_len + len(chunk) > max_diff_chars:
            allowed = max_diff_chars - current_len
            evidence.append(chunk[:allowed] + "\n\n[TRUNCATED: evidence too large]")
            is_truncated = True
        else:
            evidence.append(chunk)
            current_len += len(chunk)

    commands = [
        ("Staged diff", ["diff", "--cached", "--"] + included_files),
        ("Unstaged diff", ["diff", "--"] + included_files),
    ]

    for title, args in commands:
        if is_truncated: break
        result = subprocess.run(["git"] + args, cwd=repo_root, capture_output=True, text=True, encoding="utf-8", errors="replace")
        if result.returncode == 0 and result.stdout.strip():
            append_chunk(f"## {title}\n```diff\n{result.stdout}\n```\n")

    # Include full text for untracked files, because git diff does not show them.
    for rel_path in included_files:
        if is_truncated: break
        tracked = subprocess.run(["git", "ls-files", "--error-unmatch", rel_path], cwd=repo_root, capture_output=True)
        file_path = repo_root / rel_path
        if tracked.returncode != 0 and file_path.exists() and file_path.is_file():
            try:
                content = file_path.read_text(encoding="utf-8", errors="replace")
                if len(content) > max_file_chars:
                    content = content[:max_file_chars] + "\n\n[TRUNCATED: file exceeded max_file_chars]"
                    file_truncated = True
                append_chunk(f"## Untracked file: {rel_path}\n```\n{content}\n```\n")
            except Exception as e:
                append_chunk(f"## Untracked file: {rel_path}\n[Could not read file: {e}]\n")

    evidence_str = "\n".join(evidence) if evidence else "No textual diff found for included files."
    return evidence_str, is_truncated, file_truncated

def build_codex_prompt(manifest, project_root, config):
    reviewed_files_json = json.dumps(manifest['included_files'], ensure_ascii=False)
    prompt = f"""
You are the Codex Reviewer. You must review the provided code changes strictly according to the plan and acceptance criteria.
DO NOT review files outside the Included Files list.

## Context
Run ID: {manifest['run_id']}
Task ID: {manifest['task_id']}
Feature Name: {manifest['feature_name']}
Snapshot Hash: {manifest['snapshot_hash']}
Review Tier: {manifest.get('review_tier', 'STANDARD')}
Review Mode: {manifest.get('review_mode', 'FULL')}

Tier contract:
- QUICK: prioritize blocking correctness, secrets, injection, and missing focused tests.
- STANDARD: review correctness, security, quality, tests, scope, and project rules.
- DEEP: trace data flow, trust boundaries, migration/compatibility risks, and exploit or regression scenarios.

## Included Files (Only review these!)
"""
    for f in manifest['included_files']:
        prompt += f"- {f}\n"

    memory_path = project_root / ".agent/context/MEMORY_CONTEXT.md"
    if memory_path.exists():
        memory = memory_path.read_text(encoding="utf-8", errors="replace")
        prompt += f"\n## Operational Memory (Compact)\n```\n{memory}\n```\n"

    if manifest.get("review_mode") == "FOCUSED_RETRY":
        prompt += "\n## Previous Open Findings\n"
        for finding in manifest.get("previous_findings", []):
            prompt += (
                f"- {finding.get('severity', 'UNKNOWN')} "
                f"{finding.get('file', '?')}:{finding.get('line', '?')} — "
                f"{finding.get('title', '?')}: {finding.get('body', '')}\n"
            )

    # Retry reviews retain the complete current diff and acceptance criteria,
    # but omit unchanged planning boilerplate. Release reviews load everything.
    context_files = manifest['acceptance_files']
    if not (manifest.get("review_mode") == "FOCUSED_RETRY" and config.get("compact_retry_context", True)):
        context_files = manifest['plan_files'] + manifest['acceptance_files']
    for context_file in context_files:
        fpath = project_root / context_file
        if fpath.exists():
            content = fpath.read_text(encoding='utf-8', errors='replace')
            prompt += f"\n## File: {context_file}\n```\n{content}\n```\n"

    repo_root = Path(manifest["repository_root"])
    prompt += "\n## Changed File Evidence\n"
    diff_str, is_truncated, file_truncated = collect_review_diff(
        repo_root, 
        manifest["included_files"], 
        max_diff_chars=config.get("max_diff_chars", 120000),
        max_file_chars=config.get("max_file_chars", 40000)
    )
    prompt += diff_str
    prompt += "\n"
    manifest["evidence_truncated"] = is_truncated or file_truncated
    manifest["file_truncated"] = file_truncated
    if manifest["evidence_truncated"]:
        manifest["evidence_snippet"] = diff_str[-200:]


    # Strictly require Output Contract
    prompt += f"""
## Output Contract
You MUST output your review strictly in the following JSON format. Do not include markdown code blocks around the JSON if it breaks parsing.
{{
  "VERDICT": "FAIL",
  "REVIEWED_RUN_ID": "run_id_here",
  "REVIEWED_SNAPSHOT_HASH": "snapshot_hash_here",
  "REVIEWED_FILES": {reviewed_files_json},
  "FINDINGS": [
    {{
      "severity": "P1",
      "file": "path/to/file",
      "line": 123,
      "title": "Short title",
      "body": "Detailed description"
    }}
  ]
}}

If there are absolutely zero issues, VERDICT must be "PASS" and FINDINGS must be [].
If there is any issue, VERDICT must be "FAIL".
"""
    return prompt

def parse_codex_result(stdout, stderr, exit_code, expected_run_id, expected_hash, expected_files=None):
    if exit_code != 0:
        return ReviewStatus.INFRA_FAIL, "Codex exited with non-zero code.", []
        
    if not stdout.strip():
        return ReviewStatus.INFRA_FAIL, "Empty output from Codex.", []
        
    # Attempt to parse JSON from stdout
    try:
        # Extract json block if they wrap it in markdown
        json_str = stdout
        if "```json" in json_str:
            json_str = json_str.split("```json")[1].split("```")[0]
        elif "```" in json_str:
            json_str = json_str.split("```")[1].split("```")[0]
            
        data = json.loads(json_str.strip())
        
        if data.get("REVIEWED_RUN_ID") != expected_run_id or data.get("REVIEWED_SNAPSHOT_HASH") != expected_hash:
            return ReviewStatus.STALE, "Run ID or Hash mismatch in Codex output.", data.get("FINDINGS", [])
        if expected_files is not None and data.get("REVIEWED_FILES") != expected_files:
            return ReviewStatus.STALE, "Reviewed files mismatch in Codex output.", data.get("FINDINGS", [])
            
        verdict = data.get("VERDICT")
        findings = data.get("FINDINGS", [])
        
        if verdict == "PASS" and len(findings) == 0:
            return ReviewStatus.PASS, "Review passed with 0 findings.", []
        elif verdict == "PASS" and len(findings) > 0:
            # Conflict: PASS but has findings
            return ReviewStatus.FAIL, "Review marked PASS but contains findings.", findings
        else:
            return ReviewStatus.FAIL, "Review found issues.", findings
            
    except json.JSONDecodeError:
        return ReviewStatus.INFRA_FAIL, "Failed to parse JSON output contract from Codex.", []
    except Exception as e:
        return ReviewStatus.INFRA_FAIL, f"Parser error: {e}", []

def _ensure_review_report(project_root, manifest, status, reason, stdout="", stderr="", exit_code=None, findings=None):
    reports_dir = project_root / ".agent/reports"
    reviews_dir = reports_dir / "codex-reviews"
    reviews_dir.mkdir(parents=True, exist_ok=True)
    canonical_report = reports_dir / "CODEX_REVIEW.md"

    run_id = manifest.get("run_id", "unknown")
    if canonical_report.exists():
        existing = canonical_report.read_text(encoding="utf-8", errors="replace")
        if f"- run_id: {run_id}" in existing:
            return

    findings = findings or []
    snapshot_hash = manifest.get("snapshot_hash", "unknown")
    task_id = manifest.get("task_id", "unknown")
    ts_now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    report_md = f"""# CODEX_REVIEW.md

## Review Metadata
- mode: {manifest.get('review_mode', 'real_review')}
- review_tier: {manifest.get('review_tier', 'STANDARD')}
- run_id: {run_id}
- task_id: {task_id}
- completed_at: {ts_now}
- reviewed_diff_hash: {snapshot_hash}
- status: {status}
- placeholder: false
- reason: {reason}
- exit_code: {exit_code}
{f"- reason_code: {manifest.get('reason_code')}" if manifest.get('reason_code') else ""}

## Findings
"""
    if not findings:
        report_md += "No findings.\n"
    else:
        for f in findings:
            report_md += f"- **{f.get('severity', 'UNKNOWN')}**: `{f.get('file', '?')}:{f.get('line', '?')}` - {f.get('title', '?')}\n  {f.get('body', '')}\n"

    batches = manifest.get("batches", [])
    if batches:
        report_md += f"""
## Batch Reviews Summary
"""
        def format_batch_summary(b, level=0):
            indent = "  " * level
            res = f"{indent}- **Batch {b.get('batch_id')}**: {len(b.get('files', []))} files, {b.get('status', 'UNKNOWN')}\n"
            if b.get("children"):
                for child in b.get("children"):
                    res += format_batch_summary(child, level + 1)
            return res
            
        for b in batches:
            report_md += format_batch_summary(b)

    report_md += f"""
## Raw Output
### Stdout
```
{stdout}
```
### Stderr
```
{stderr}
```
"""
    (reviews_dir / f"{run_id}.md").write_text(report_md, encoding="utf-8")
    canonical_report.write_text(report_md, encoding="utf-8")

def build_review_batches(repo_root, included_files, config):
    batch_max_files = config.get("batch_max_files", 3)
    batch_max_chars = config.get("batch_max_chars", 70000)
    batches = []
    
    current_batch_files = []
    current_batch_chars = 0
    batch_id = 1
    
    for f in included_files:
        diff_str, _, file_truncated = collect_review_diff(repo_root, [f], max_diff_chars=batch_max_chars, max_file_chars=config.get("max_file_chars", 40000))
        f_len = len(diff_str)
        
        if f_len >= batch_max_chars:
            if current_batch_files:
                batches.append({
                    "batch_id": batch_id,
                    "files": current_batch_files,
                    "evidence_chars": current_batch_chars,
                    "status": "QUEUED",
                    "reason": "",
                    "run_id": None,
                    "duration_seconds": 0
                })
                batch_id += 1
                current_batch_files = []
                current_batch_chars = 0
            
            batches.append({
                "batch_id": batch_id,
                "files": [f],
                "evidence_chars": f_len,
                "status": "QUEUED",
                "reason": "",
                "run_id": None,
                "duration_seconds": 0,
                "needs_chunk_review": True
            })
            batch_id += 1
            continue
            
        if len(current_batch_files) >= batch_max_files or current_batch_chars + f_len > batch_max_chars:
            batches.append({
                "batch_id": batch_id,
                "files": current_batch_files,
                "evidence_chars": current_batch_chars,
                "status": "QUEUED",
                "reason": "",
                "run_id": None,
                "duration_seconds": 0
            })
            batch_id += 1
            current_batch_files = [f]
            current_batch_chars = f_len
        else:
            current_batch_files.append(f)
            current_batch_chars += f_len
            
    if current_batch_files:
        batches.append({
            "batch_id": batch_id,
            "files": current_batch_files,
            "evidence_chars": current_batch_chars,
            "status": "QUEUED",
            "reason": "",
            "run_id": None,
            "duration_seconds": 0
        })
        
    return batches

def build_codex_prompt_batch(manifest, project_root, config, batch_files):
    # Same as build_codex_prompt but uses batch_files for Included Files list and evidence gathering
    reviewed_files_json = json.dumps(batch_files, ensure_ascii=False)
    prompt = f"""
You are the Codex Reviewer. You must review the provided code changes strictly according to the plan and acceptance criteria.
DO NOT review files outside the Included Files list.

## Context
Run ID: {manifest['run_id']}
Task ID: {manifest['task_id']}
Feature Name: {manifest['feature_name']}
Snapshot Hash: {manifest['snapshot_hash']}
Review Tier: {manifest.get('review_tier', 'STANDARD')}

Tier contract:
- QUICK: prioritize blocking correctness, secrets, injection, and missing focused tests.
- STANDARD: review correctness, security, quality, tests, scope, and project rules.
- DEEP: trace data flow, trust boundaries, migration/compatibility risks, and exploit or regression scenarios.

## Included Files (Only review these!)
"""
    for f in batch_files:
        prompt += f"- {f}\n"

    memory_path = project_root / ".agent/context/MEMORY_CONTEXT.md"
    if memory_path.exists():
        memory = memory_path.read_text(encoding="utf-8", errors="replace")
        prompt += f"\n## Operational Memory (Compact)\n```\n{memory}\n```\n"

    # Load context files
    for context_file in manifest['plan_files'] + manifest['acceptance_files']:
        fpath = project_root / context_file
        if fpath.exists():
            content = fpath.read_text(encoding='utf-8', errors='replace')
            prompt += f"\n## File: {context_file}\n```\n{content}\n```\n"

    repo_root = Path(manifest["repository_root"])
    prompt += "\n## Changed File Evidence\n"
    diff_str, is_truncated, file_truncated = collect_review_diff(
        repo_root, 
        batch_files, 
        max_diff_chars=config.get("max_diff_chars", 120000),
        max_file_chars=config.get("max_file_chars", 40000)
    )
    prompt += diff_str
    prompt += "\n"
    
    # We update manifest for truncation info
    # In batch mode, if any batch truncates, the manifest should mark it.
    if is_truncated or file_truncated:
        manifest["evidence_truncated"] = True
    if file_truncated:
        manifest["file_truncated"] = True
        
    if is_truncated or file_truncated:
        manifest["evidence_snippet"] = diff_str[-200:]

    # Strictly require Output Contract
    prompt += f"""
## Output Contract
You MUST output your review strictly in the following JSON format. Do not include markdown code blocks around the JSON if it breaks parsing.
{{
  "VERDICT": "FAIL",
  "REVIEWED_RUN_ID": "run_id_here",
  "REVIEWED_SNAPSHOT_HASH": "snapshot_hash_here",
  "REVIEWED_FILES": {reviewed_files_json},
  "FINDINGS": [
    {{
      "severity": "P1",
      "file": "path/to/file",
      "line": 123,
      "title": "Short title",
      "body": "Detailed description"
    }}
  ]
}}
"""
    return prompt

def _run_codex_review_batch_single(batch, manifest, project_root, repo_root, config, codex_executable):
    batch_id = batch["batch_id"]
    batch_files = batch["files"]
    batch_started = time.perf_counter()
    prompt_started = time.perf_counter()
    prompt = build_codex_prompt_batch(manifest, project_root, config, batch_files)
    prompt_ms = int((time.perf_counter() - prompt_started) * 1000)
    
    temp_review_dir = Path(tempfile.mkdtemp(prefix=f"codex_review_{manifest['run_id']}_b{batch_id}_"))
    review_cwd = temp_review_dir / "source-code"
    workspace_started = time.perf_counter()
    _copy_review_workspace(repo_root, review_cwd)
    workspace_ms = int((time.perf_counter() - workspace_started) * 1000)
        
    last_message_path = temp_review_dir / "codex_last_message.txt"
    start_time = time.time()
    
    try:
        codex_command = [
                codex_executable,
                "exec",
                "--sandbox",
                "read-only",
                "--skip-git-repo-check",
                "--ephemeral",
                "--output-last-message",
                str(last_message_path),
                "--output-schema",
                str(Path(__file__).parent / "schemas" / "codex_review_output.schema.json"),
                "-",
            ]
        if os.name == "nt" and Path(codex_executable).suffix.lower() in {".cmd", ".bat"}:
            # Keep the batch wrapper and its child process in one visible tree so
            # taskkill /T can enforce timeouts without waiting for orphaned pipes.
            codex_command = ["cmd.exe", "/d", "/s", "/c", *codex_command]
        proc = subprocess.Popen(
            codex_command,
            cwd=review_cwd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=(subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0),
        )
        
        timeout = int(config.get("timeout_seconds", 180))
        codex_started = time.perf_counter()
        stdout, stderr = proc.communicate(input=prompt, timeout=timeout)
        codex_ms = int((time.perf_counter() - codex_started) * 1000)
        exit_code = proc.returncode
        if last_message_path.exists():
            last_message = last_message_path.read_text(encoding="utf-8", errors="replace").strip()
            if last_message:
                stdout = last_message
                
        # Parse result
        parse_started = time.perf_counter()
        status, reason, findings = parse_codex_result(stdout, stderr, exit_code, manifest['run_id'], manifest['snapshot_hash'], batch_files)
        parse_ms = int((time.perf_counter() - parse_started) * 1000)
        
        batch["status"] = status
        batch["reason"] = reason
        batch["findings"] = findings
        batch["stdout"] = stdout
        batch["stderr"] = stderr
        batch["exit_code"] = exit_code
        batch["duration_seconds"] = int(time.time() - start_time)
        batch["run_id"] = f"{manifest['run_id']}_b{batch_id}"
        batch["timings_ms"] = {
            "prompt_build": prompt_ms,
            "workspace_prepare": workspace_ms,
            "codex_exec": codex_ms,
            "parse_report": parse_ms,
            "total": int((time.perf_counter() - batch_started) * 1000),
        }
        batch["prompt_chars"] = len(prompt)
        
        return batch
        
    except subprocess.TimeoutExpired:
        _terminate_process_tree(proc)
        stdout, stderr = proc.communicate()
        exit_code = -1
        
        batch["status"] = ReviewStatus.INFRA_FAIL
        batch["reason"] = f"Timeout ({timeout}s)"
        batch["reason_code"] = "CODEX_TIMEOUT"
        batch["findings"] = []
        batch["stdout"] = stdout
        batch["stderr"] = stderr
        batch["exit_code"] = exit_code
        batch["duration_seconds"] = int(time.time() - start_time)
        batch["timings_ms"] = {
            "prompt_build": prompt_ms,
            "workspace_prepare": workspace_ms,
            "codex_exec": int((time.perf_counter() - codex_started) * 1000),
            "parse_report": 0,
            "total": int((time.perf_counter() - batch_started) * 1000),
        }
        batch["prompt_chars"] = len(prompt)
        return batch
        
    except Exception as e:
        batch["status"] = ReviewStatus.INFRA_FAIL
        batch["reason"] = f"Error running codex: {e}"
        batch["findings"] = []
        batch["duration_seconds"] = int(time.time() - start_time)
        batch["timings_ms"] = {
            "prompt_build": prompt_ms,
            "workspace_prepare": workspace_ms,
            "codex_exec": 0,
            "parse_report": 0,
            "total": int((time.perf_counter() - batch_started) * 1000),
        }
        batch["prompt_chars"] = len(prompt)
        return batch
        
    finally:
        _cleanup_dir(temp_review_dir)

def run_codex_review_batch(batch, manifest, project_root, repo_root, config, codex_executable, retry_count=0):
    result = _run_codex_review_batch_single(batch, manifest, project_root, repo_root, config, codex_executable)
    
    if result.get("reason_code") == "CODEX_TIMEOUT":
        max_retries = config.get("max_timeout_retries", 2)
        if len(batch["files"]) > 1 and retry_count < max_retries:
            child_results = []
            for i, f in enumerate(batch["files"]):
                child_batch = {
                    "batch_id": f"{batch['batch_id']}.{i+1}",
                    "files": [f],
                    "status": "QUEUED"
                }
                child_res = run_codex_review_batch(
                    child_batch, manifest, project_root, repo_root, config, codex_executable, retry_count + 1
                )
                child_results.append(child_res)
            
            # Save children in parent
            batch["children"] = child_results
        elif len(batch["files"]) == 1:
            batch["status"] = ReviewStatus.INFRA_FAIL
            batch["reason_code"] = "CODEX_TIMEOUT_SINGLE_LARGE_FILE"
            batch["needs_chunk_review"] = True
            
    return batch

def aggregate_batch_results(batches):
    final_status = ReviewStatus.PASS
    all_findings = []
    
    # Status Priority: INFRA_FAIL > STALE > FAIL > PASS
    # Note: parse_codex_result handles STALE_SOURCE_CHANGED which returns STALE
    has_infra_fail = False
    has_stale = False
    has_fail = False
    
    def process_batch(b):
        nonlocal has_infra_fail, has_stale, has_fail, all_findings
        
        children = b.get("children")
        if children:
            for child in children:
                process_batch(child)
            return
            
        st = b.get("status")
        if st == ReviewStatus.INFRA_FAIL:
            has_infra_fail = True
        elif st == ReviewStatus.STALE:
            has_stale = True
        elif st == ReviewStatus.FAIL:
            has_fail = True
            
        all_findings.extend(b.get("findings", []))
        
    for b in batches:
        process_batch(b)
        
    if has_infra_fail:
        final_status = ReviewStatus.INFRA_FAIL
    elif has_stale:
        final_status = ReviewStatus.STALE
    elif has_fail:
        final_status = ReviewStatus.FAIL
        
    return final_status, all_findings


def _artifact_snapshot(project_root, mode, artifact_files, source_hash):
    context_dir = (project_root / ".agent/context").resolve()
    hasher = hashlib.sha256()
    hasher.update(mode.encode("utf-8"))
    hasher.update(b"\0")
    hasher.update(source_hash.encode("utf-8"))
    normalized = []
    for rel_path in artifact_files:
        candidate = (context_dir / rel_path).resolve()
        try:
            candidate.relative_to(context_dir)
        except ValueError:
            raise ValueError(f"Artifact path escapes .agent/context: {rel_path}")
        if not candidate.is_file() or candidate.stat().st_size == 0:
            raise ValueError(f"Artifact is missing or empty: {rel_path}")
        normalized.append(rel_path.replace("\\", "/"))
        hasher.update(rel_path.encode("utf-8"))
        hasher.update(b"\0")
        hasher.update(candidate.read_bytes())
        hasher.update(b"\0")
    return hasher.hexdigest(), normalized


def _artifact_source_snapshot(project_root):
    """Hash HEAD plus only source files explicitly admitted by task scope."""
    repo_root = project_root / "source-code"
    rev = subprocess.run(
        ["git", "rev-parse", "HEAD"], cwd=repo_root, capture_output=True,
        text=True, encoding="utf-8", errors="replace",
    )
    head = rev.stdout.strip() if rev.returncode == 0 else "NO_HEAD"
    scope_path = project_root / ".agent/context/TASK_SCOPE.json"
    scope = json.loads(scope_path.read_text(encoding="utf-8")) if scope_path.exists() else {}
    files = {}
    for pattern in scope.get("allowed_files", []):
        normalized = str(pattern).replace("\\", "/")
        if normalized.startswith((".agent/", ".agents/")) or Path(normalized).is_absolute():
            continue
        try:
            for path in repo_root.glob(normalized):
                if path.is_file():
                    files[path.relative_to(repo_root).as_posix()] = path
        except (OSError, ValueError):
            continue
    hasher = hashlib.sha256()
    hasher.update(head.encode("utf-8", errors="replace"))
    hasher.update(b"\0")
    for relative, path in sorted(files.items()):
        hasher.update(relative.encode("utf-8"))
        hasher.update(b"\0")
        hasher.update(path.read_bytes())
        hasher.update(b"\0")
    return hasher.hexdigest(), sorted(files)


def get_artifact_snapshot(project_root, mode, artifact_files):
    """Return the same immutable snapshot used by artifact review preflight."""
    source_hash, _ = _artifact_source_snapshot(project_root)
    return _artifact_snapshot(project_root, mode, artifact_files, source_hash)


def _atomic_write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(content, encoding="utf-8")
    temporary.replace(path)


def reconcile_stale_review(project_root, stale_after_seconds=240, now=None):
    """Terminalize an expired RUNNING review and publish one canonical status."""
    manifest_path = project_root / ".agent/state/review_run.json"
    if not manifest_path.exists():
        return None
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("status") != ReviewStatus.RUNNING or not manifest.get("heartbeat_at"):
        return None
    current = now or datetime.now(timezone.utc)
    heartbeat = datetime.fromisoformat(str(manifest["heartbeat_at"]).replace("Z", "+00:00"))
    if heartbeat.tzinfo is None:
        heartbeat = heartbeat.replace(tzinfo=datetime.now().astimezone().tzinfo)
    age = (current - heartbeat.astimezone(timezone.utc)).total_seconds()
    if age <= stale_after_seconds:
        return None
    completed_at = current.isoformat()
    reason = f"Review heartbeat expired after {int(age)}s; run terminalized as STALE."
    manifest.update(status=ReviewStatus.STALE, completed_at=completed_at,
                    heartbeat_at=completed_at, reason=reason,
                    reason_code="STALE_HEARTBEAT")
    context_path = project_root / ".agent/context/TASK_CONTEXT.json"
    context = json.loads(context_path.read_text(encoding="utf-8")) if context_path.exists() else {}
    context.update(status="stale", resume_cursor="review_recovery", updated_at=completed_at)
    context.setdefault("events", []).append({"at": completed_at, "type": "review_stale", "detail": reason})
    workflow_path = project_root / ".agent/state/workflow_state.json"
    workflow = json.loads(workflow_path.read_text(encoding="utf-8")) if workflow_path.exists() else {}
    workflow.update(task_id=manifest.get("task_id"), dual_mode=manifest.get("review_mode", "unknown"),
                    dual_status="stale", next_step="review_recovery", dual_reason=reason)
    authority = {
        "status_version": 1, "status": ReviewStatus.STALE,
        "task_id": manifest.get("task_id"), "mode": manifest.get("review_mode", "unknown"),
        "run_id": manifest.get("run_id"), "snapshot_hash": manifest.get("snapshot_hash", ""),
        "reason_code": "STALE_HEARTBEAT", "reason": reason,
        "terminal": True, "completed_at": completed_at,
    }
    report = (
        "# DUAL_AGENT_REPORT.md\n\n## Status: STALE\n"
        f"- status_version: 1\n- task_id: {authority['task_id']}\n- mode: {authority['mode']}\n"
        f"- run_id: {authority['run_id']}\n- snapshot_hash: {authority['snapshot_hash']}\n"
        f"- completed_at: {completed_at}\n- reason: {reason}\n"
    )
    # pipeline_status is the commit marker and is written last.
    _atomic_write(manifest_path, json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    _atomic_write(context_path, json.dumps(context, indent=2, ensure_ascii=False) + "\n")
    _atomic_write(workflow_path, json.dumps(workflow, indent=2, ensure_ascii=False) + "\n")
    _atomic_write(project_root / ".agent/reports/DUAL_AGENT_REPORT.md", report)
    _atomic_write(project_root / ".agent/state/pipeline_status.json",
                  json.dumps(authority, indent=2, ensure_ascii=False) + "\n")
    return authority


def run_codex_artifact_review(project_root, task_id, feature_name, codex_executable, mode, artifact_files):
    """Review research/plan artifacts against an immutable repository snapshot."""
    run_id = str(uuid.uuid4())
    repo_root = project_root / "source-code"
    config = get_review_config(project_root)
    learning_guard = build_learning_guard(project_root, task_id, feature_name, mode)
    try:
        source_hash, _ = _artifact_source_snapshot(project_root)
        snapshot_hash, reviewed_files = _artifact_snapshot(project_root, mode, artifact_files, source_hash)
    except Exception as e:
        return _write_infra_fail(
            project_root, run_id, f"Artifact preparation failed: {e}", task_id=task_id
        )

    rev_res = subprocess.run(["git", "rev-parse", "HEAD"], cwd=repo_root, capture_output=True, text=True)
    base_rev = rev_res.stdout.strip() if rev_res.returncode == 0 else "unknown"
    manifest = {
        "schema_version": 1,
        "run_id": run_id,
        "task_id": task_id,
        "feature_name": feature_name,
        "review_mode": mode,
        "repository_root": str(repo_root),
        "source_project_root": str(project_root),
        "base_revision": base_rev,
        "included_files": reviewed_files,
        "excluded_files": [],
        "plan_files": [],
        "acceptance_files": [],
        "snapshot_hash": snapshot_hash,
        "source_snapshot_hash": source_hash,
        "learning_guard": {
            "matched_rule_count": learning_guard.get("matched_rule_count", 0),
            "context": ".agent/context/MEMORY_CONTEXT.md",
            "report": ".agent/reports/LEARNING_GUARD.md",
        },
        "report_path": f".agent/reports/codex-reviews/{run_id}.md",
        "status": ReviewStatus.RUNNING,
        "started_at": datetime.now(timezone.utc).isoformat(),
        "heartbeat_at": datetime.now(timezone.utc).isoformat(),
        "completed_at": None,
        "exit_code": None,
    }
    state_dir = project_root / ".agent/state"
    state_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = state_dir / "review_run.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    criteria = {
        "research": (
            "Check repository grounding, source traceability, fact/inference separation, "
            "assumptions, alternatives, risks, feasibility, and whether the recommendation "
            "is strong enough to support planning."
        ),
        "plan": (
            "Check repository grounding, exact impacted files, dependencies, sequencing, "
            "test strategy, rollback, acceptance criteria, unresolved decisions, and whether "
            "an implementer can execute without guessing."
        ),
    }[mode]
    max_chars = 500000
    evidence_parts = []
    evidence_chars = 0
    truncated = False
    for rel_path in artifact_files:
        artifact_path = project_root / ".agent/context" / rel_path
        content = artifact_path.read_text(encoding="utf-8", errors="replace")
        block = f"\n## Artifact: {rel_path}\n{content}\n"
        remaining = max_chars - evidence_chars
        if remaining <= 0:
            truncated = True
            break
        if len(block) > remaining:
            block = block[:remaining]
            truncated = True
        evidence_parts.append(block)
        evidence_chars += len(block)

    included_file_lines = "\n".join(f"- {path}" for path in reviewed_files)
    prompt = f"""You are the independent Codex reviewer for a dual-agent pipeline.
Review mode: {mode}
Task ID: {task_id}
Feature: {feature_name}
Run ID: {run_id}
Snapshot Hash: {snapshot_hash}

Use the repository available in the current working directory to verify claims as needed.
{criteria}
## Included Files (Only review these deliverables!)
{included_file_lines}

## Operational Memory (Compact)
```
{(project_root / ".agent/context/MEMORY_CONTEXT.md").read_text(encoding="utf-8", errors="replace") if (project_root / ".agent/context/MEMORY_CONTEXT.md").exists() else "No compact memory context available."}
```

## Review Instructions
Do not edit files.
{"WARNING: Artifact evidence was truncated; fail if the omitted evidence prevents a reliable verdict." if truncated else ""}
{''.join(evidence_parts)}

Output only this JSON contract:
{{
  "VERDICT": "FAIL",
  "REVIEWED_RUN_ID": "{run_id}",
  "REVIEWED_SNAPSHOT_HASH": "{snapshot_hash}",
  "REVIEWED_FILES": {json.dumps(reviewed_files, ensure_ascii=False)},
  "FINDINGS": [
    {{"severity": "P1", "file": "artifact", "line": 1, "title": "Short title", "body": "Actionable detail"}}
  ]
}}
Use VERDICT="PASS" only when FINDINGS is exactly [].
Use VERDICT="FAIL" when there is one or more finding. Valid severities are P0, P1, P2, and P3.
"""

    temp_review_dir = Path(tempfile.mkdtemp(prefix=f"codex_artifact_{run_id}_"))
    review_cwd = temp_review_dir / "source-code"
    _copy_review_workspace(repo_root, review_cwd)
    last_message_path = temp_review_dir / "codex_last_message.txt"
    stdout = ""
    stderr = ""
    exit_code = None
    try:
        proc = subprocess.Popen(
            [codex_executable, "exec", "--sandbox", "read-only", "--skip-git-repo-check",
             "--ephemeral", "--output-last-message", str(last_message_path),
             "--output-schema", str(Path(__file__).parent / "schemas" / "codex_review_output.schema.json"), "-"],
            cwd=review_cwd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        timeout = int(config.get("timeout_seconds", 180))
        stdout, stderr = proc.communicate(input=prompt, timeout=timeout)
        exit_code = proc.returncode
        if last_message_path.exists():
            last_message = last_message_path.read_text(encoding="utf-8", errors="replace").strip()
            if last_message:
                stdout = last_message
        status, reason, findings = parse_codex_result(
            stdout, stderr, exit_code, run_id, snapshot_hash, reviewed_files
        )
    except subprocess.TimeoutExpired:
        stdout, stderr = _terminate_timed_out_process(proc)
        exit_code = -1
        status, reason, findings = ReviewStatus.INFRA_FAIL, f"Timeout ({timeout}s)", []
        manifest["reason_code"] = "CODEX_TIMEOUT"
    except Exception as e:
        status, reason, findings = ReviewStatus.INFRA_FAIL, f"Error running Codex: {e}", []
    finally:
        _cleanup_dir(temp_review_dir)

    if status == ReviewStatus.PASS:
        try:
            post_source_hash, _ = _artifact_source_snapshot(project_root)
            post_hash, _ = _artifact_snapshot(project_root, mode, artifact_files, post_source_hash)
            if post_hash != snapshot_hash:
                status = ReviewStatus.STALE
                reason = "Repository or artifact changed during review."
        except Exception as e:
            status = ReviewStatus.INFRA_FAIL
            reason = f"Post-review snapshot failed: {e}"

    manifest.update({
        "status": status,
        "reason": reason,
        "findings": findings,
        "completed_at": datetime.now(timezone.utc).isoformat(),
        "heartbeat_at": datetime.now(timezone.utc).isoformat(),
        "exit_code": exit_code,
        "evidence_truncated": truncated,
    })
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    _ensure_review_report(project_root, manifest, status, reason, stdout, stderr, exit_code, findings)
    return manifest


def run_codex_review(project_root, task_id, feature_name, codex_executable, review_purpose="release"):
    """
    Orchestrates the entire review pipeline.
    """
    run_id = str(uuid.uuid4())
    pipeline_started = time.perf_counter()
    repo_root = project_root / "source-code"
    
    # 1. Get Base Revision
    rev_res = subprocess.run(["git", "rev-parse", "HEAD"], cwd=repo_root, capture_output=True, text=True)
    base_rev = rev_res.stdout.strip() if rev_res.returncode == 0 else "unknown"
    
    # 2. Evaluate the task delta against the immutable task baseline.
    try:
        task_scope = project_root / ".agent/context/TASK_SCOPE.json"
        evaluation = evaluate_task_scope(
            repo_root,
            task_scope,
            project_root / ".agent/state/task_baseline.json",
            expected_task_id=task_id,
            require_delta=True,
        )
        snapshot_hash = evaluation["snapshot_hash"]
        changed_files = evaluation["task_files"]
    except Exception as e:
        return _write_infra_fail(project_root, run_id, f"Failed to evaluate task scope: {e}", task_id=task_id, feature_name=feature_name, base_rev=base_rev)

    if evaluation["status"] != ReviewStatus.PASS:
        msg = "Task delta validation blocked:\n" + "\n".join(evaluation["issues"])
        return _write_terminal_failure(
            project_root, run_id, evaluation["status"], msg,
            task_id=task_id, feature_name=feature_name, base_rev=base_rev,
            snapshot_hash=snapshot_hash,
            excluded_files=evaluation["excluded_preexisting_files"],
        )
        
    # 4. Route by risk and prepare manifest. Code retries may use compact
    # context; release is always a full DEEP review.
    config = get_review_config(project_root)
    learning_guard = build_learning_guard(project_root, task_id, feature_name, review_purpose)
    routing = determine_review_tier(repo_root, changed_files, config)
    previous_manifest = {}
    previous_manifest_path = project_root / ".agent/state/review_run.json"
    if previous_manifest_path.exists():
        try:
            previous_manifest = json.loads(previous_manifest_path.read_text(encoding="utf-8"))
        except Exception:
            previous_manifest = {}
    previous_findings = previous_manifest.get("findings", [])
    focused_retry = (
        review_purpose == "code"
        and bool(config.get("focused_retry_enabled", True))
        and previous_manifest.get("task_id") == task_id
        and previous_manifest.get("status") == ReviewStatus.FAIL
        and bool(previous_findings)
    )
    if review_purpose == "release" and config.get("final_deep_review_required", True):
        routing["tier"] = "DEEP"
    elif focused_retry and not routing.get("sensitive_files"):
        routing["tier"] = "STANDARD"
    manifest = {
        "schema_version": 1,
        "run_id": run_id,
        "task_id": task_id,
        "feature_name": feature_name,
        "repository_root": str(repo_root),
        "source_project_root": str(project_root),
        "base_revision": base_rev,
        "included_files": changed_files,
        "excluded_files": evaluation["excluded_preexisting_files"],
        "plan_files": [".agent/context/PLAN.md", ".agent/context/TECHNICAL_DESIGN.md"],
        "acceptance_files": [".agent/context/ACCEPTANCE_CRITERIA.md"],
        "snapshot_hash": snapshot_hash,
        "review_tier": routing["tier"],
        "review_purpose": review_purpose,
        "review_mode": "FOCUSED_RETRY" if focused_retry else "FULL",
        "previous_findings": previous_findings if focused_retry else [],
        "review_metrics": routing,
        "learning_guard": {
            "matched_rule_count": learning_guard.get("matched_rule_count", 0),
            "context": ".agent/context/MEMORY_CONTEXT.md",
            "report": ".agent/reports/LEARNING_GUARD.md",
        },
        "report_path": f".agent/reports/codex-reviews/{run_id}.md",
        "status": ReviewStatus.PREPARING,
        "pid": None,
        "started_at": datetime.now(timezone.utc).isoformat(),
        "heartbeat_at": datetime.now(timezone.utc).isoformat(),
        "completed_at": None,
        "exit_code": None
    }
    
    state_dir = project_root / ".agent/state"
    state_dir.mkdir(parents=True, exist_ok=True)
    reports_dir = project_root / ".agent/reports"
    reviews_dir = reports_dir / "codex-reviews"
    reviews_dir.mkdir(parents=True, exist_ok=True)
    
    manifest_path = state_dir / "review_run.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    
    # 5. Build and Execute Batches
    batches = build_review_batches(repo_root, changed_files, config)
    
    manifest["batches"] = batches
    manifest["status"] = ReviewStatus.RUNNING
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    
    for batch in batches:
        run_codex_review_batch(batch, manifest, project_root, repo_root, config, codex_executable)
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        
    # 7. Aggregate and Parse Result
    status, findings = aggregate_batch_results(batches)
    manifest["findings"] = findings
    reason = ""
    
    def get_all_batches(b_list):
        for b in b_list:
            yield b
            if b.get("children"):
                yield from get_all_batches(b.get("children"))
                
    all_batches_flat = list(get_all_batches(batches))

    # 7.5 Check post-run snapshot
    if status == ReviewStatus.PASS:
        try:
            post_evaluation = evaluate_task_scope(
                repo_root,
                project_root / ".agent/context/TASK_SCOPE.json",
                project_root / ".agent/state/task_baseline.json",
                expected_task_id=task_id,
                require_delta=True,
            )
            post_hash = post_evaluation["snapshot_hash"]
            if post_hash != snapshot_hash:
                status = ReviewStatus.STALE
                reason = "Source code changed during review."
        except Exception as e:
            status = ReviewStatus.INFRA_FAIL
            reason = f"Failed to get post-run snapshot: {e}"
            
    if not reason:
        if status == ReviewStatus.PASS:
            reason = f"Review passed with {len(findings)} findings."
        elif status == ReviewStatus.FAIL:
            reason = f"Review failed with {len(findings)} findings."
        elif status == ReviewStatus.INFRA_FAIL:
            reasons = [b.get("reason", "") for b in all_batches_flat if b.get("status") == ReviewStatus.INFRA_FAIL]
            reason = "INFRA_FAIL: " + " | ".join(filter(None, reasons))
            if any(b.get("reason_code") == "CODEX_TIMEOUT_SINGLE_LARGE_FILE" for b in all_batches_flat):
                manifest["reason_code"] = "CODEX_TIMEOUT_SINGLE_LARGE_FILE"
            elif any(b.get("reason_code") == "CODEX_TIMEOUT" for b in all_batches_flat):
                manifest["reason_code"] = "CODEX_TIMEOUT"
        elif status == ReviewStatus.STALE:
            reasons = [b.get("reason", "") for b in all_batches_flat if b.get("status") == ReviewStatus.STALE]
            reason = "STALE: " + " | ".join(filter(None, reasons))
    
    # 8. Write Report
    ts_now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    report_md = f"""# CODEX_REVIEW.md

## Review Metadata
- mode: real_review_batched
- review_tier: {manifest.get('review_tier', 'STANDARD')}
- review_mode: {manifest.get('review_mode', 'FULL')}
- review_purpose: {manifest.get('review_purpose', 'release')}
- run_id: {run_id}
- task_id: {task_id}
- completed_at: {ts_now}
- reviewed_diff_hash: {snapshot_hash}
- status: {status}
- placeholder: false
- reason: {reason}
"""
    if manifest.get('reason_code'):
        report_md += f"- reason_code: {manifest.get('reason_code')}\n"

    report_md += f"""
## Findings
"""
    if not findings:
        report_md += "No findings.\n"
    else:
        for f in findings:
            report_md += f"- **{f.get('severity', 'UNKNOWN')}**: `{f.get('file', '?')}:{f.get('line', '?')}` - {f.get('title', '?')}\n  {f.get('body', '')}\n"

    report_md += f"""
## Batch Reviews Summary
"""
    def format_batch_summary(b, level=0):
        indent = "  " * level
        res = f"{indent}- **Batch {b.get('batch_id')}**: {len(b.get('files', []))} files, {b.get('status', 'UNKNOWN')}\n"
        if b.get("children"):
            for child in b.get("children"):
                res += format_batch_summary(child, level + 1)
        return res

    for b in batches:
        report_md += format_batch_summary(b)

    report_md += "\n## Timing\n"
    for b in all_batches_flat:
        timings = b.get("timings_ms", {})
        if timings:
            report_md += (
                f"- Batch {b.get('batch_id')}: prompt={timings.get('prompt_build', 0)}ms, "
                f"workspace={timings.get('workspace_prepare', 0)}ms, "
                f"codex={timings.get('codex_exec', 0)}ms, parse={timings.get('parse_report', 0)}ms, "
                f"total={timings.get('total', 0)}ms, prompt_chars={b.get('prompt_chars', 0)}\n"
            )

    report_md += f"""
## Raw Output
"""
    def format_batch_raw(b):
        if b.get("children"):
            res = ""
            for child in b.get("children"):
                res += format_batch_raw(child)
            return res
        res = f"### Batch {b['batch_id']}\n"
        res += f"#### Stdout\n```\n{b.get('stdout', '')}\n```\n"
        res += f"#### Stderr\n```\n{b.get('stderr', '')}\n```\n"
        return res

    for b in batches:
        report_md += format_batch_raw(b)
        
    report_file = reviews_dir / f"{run_id}.md"
    report_file.write_text(report_md, encoding="utf-8")
    
    # Update canonical pointer
    canonical_report = reports_dir / "CODEX_REVIEW.md"
    canonical_report.write_text(report_md, encoding="utf-8")
    
    # Write diff hash if PASS
    if status == ReviewStatus.PASS:
        (state_dir / "reviewed_diff_hash.txt").write_text(snapshot_hash, encoding="utf-8")
        
    batch_exit_codes = [b.get("exit_code", 0) for b in batches]
    exit_code = max(batch_exit_codes) if batch_exit_codes else 0
    if -1 in batch_exit_codes:
        exit_code = -1
        
    manifest["pipeline_total_ms"] = int((time.perf_counter() - pipeline_started) * 1000)
    return _write_finished(project_root, manifest, status, reason, "", "", exit_code)

def _write_terminal_failure(project_root, run_id, status, reason, task_id="unknown", feature_name="unknown", base_rev="unknown", snapshot_hash="unknown", excluded_files=None):
    state_dir = project_root / ".agent/state"
    state_dir.mkdir(parents=True, exist_ok=True)
    manifest = {
        "schema_version": 1,
        "run_id": run_id,
        "task_id": task_id,
        "feature_name": feature_name,
        "repository_root": str(project_root / "source-code"),
        "source_project_root": str(project_root),
        "base_revision": base_rev,
        "included_files": [],
        "excluded_files": excluded_files or [],
        "plan_files": [],
        "acceptance_files": [],
        "snapshot_hash": snapshot_hash,
        "report_path": "",
        "status": status,
        "pid": None,
        "started_at": None,
        "heartbeat_at": None,
        "completed_at": datetime.now().isoformat(),
        "exit_code": -1,
        "reason": reason
    }
    
    try:
        import jsonschema
        schema_path = Path(__file__).parent / "schemas" / "review_run.schema.json"
        if not schema_path.exists():
            raise Exception(f"Missing schema: {schema_path}")
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
        jsonschema.validate(instance=manifest, schema=schema)
    except Exception as e:
        manifest["status"] = ReviewStatus.INFRA_FAIL
        manifest["reason"] = f"Manifest schema validation failed: {e}"
        
    (state_dir / "review_run.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    _ensure_review_report(project_root, manifest, manifest["status"], manifest["reason"], "", "", manifest["exit_code"])
    return manifest


def _write_infra_fail(project_root, run_id, reason, task_id="unknown", feature_name="unknown", base_rev="unknown", snapshot_hash="unknown"):
    return _write_terminal_failure(
        project_root, run_id, ReviewStatus.INFRA_FAIL, reason,
        task_id=task_id, feature_name=feature_name, base_rev=base_rev,
        snapshot_hash=snapshot_hash,
    )

def _write_finished(project_root, manifest, status, reason, stdout, stderr, exit_code, reason_code=None):
    manifest["status"] = status
    manifest["completed_at"] = datetime.now().isoformat()
    manifest["exit_code"] = exit_code
    manifest["reason"] = reason
    if reason_code:
        manifest["reason_code"] = reason_code
    
    try:
        import jsonschema
        schema_path = Path(__file__).parent / "schemas" / "review_run.schema.json"
        if not schema_path.exists():
            raise Exception(f"Missing schema: {schema_path}")
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
        jsonschema.validate(instance=manifest, schema=schema)
    except Exception as e:
        manifest["status"] = ReviewStatus.INFRA_FAIL
        manifest["reason"] = f"Manifest schema validation failed: {e}"
        
    state_dir = project_root / ".agent/state"
    (state_dir / "review_run.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    _ensure_review_report(project_root, manifest, status, reason, stdout, stderr, exit_code)
    
    # Save a JSON copy for history
    reviews_dir = project_root / ".agent/reports/codex-reviews"
    reviews_dir.mkdir(parents=True, exist_ok=True)
    (reviews_dir / f"{manifest['run_id']}.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    
    return manifest

