"""Repository-relative, non-overlapping ownership validation."""
from __future__ import annotations

from typing import Any, Iterable

DEFAULT_PROTECTED_PATHS = (".agents", ".agent", ".github", "AGENTS.md", ".gitignore")
BROAD = {"*", "**", "**/*", "/*", "/**", "."}


def repository_path(value: str) -> str:
    if not isinstance(value, str) or not value:
        raise ValueError("path must be a non-empty POSIX repository path")
    if "\\" in value or value.startswith("./") or value.startswith("/") or ":" in value:
        raise ValueError(f"invalid repository-relative POSIX path: {value}")
    if value in BROAD or value.endswith("/**") and value.count("/") == 0:
        raise ValueError(f"broad repository-wide ownership is not allowed: {value}")
    parts = value.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        raise ValueError(f"invalid repository-relative path: {value}")
    return value


def _protected(path: str, protected: Iterable[str]) -> bool:
    lower = path.casefold()
    return any(lower == item.casefold() or lower.startswith(item.casefold().rstrip("/") + "/") for item in protected)


def _entries(owner: dict[str, Any]) -> list[str]:
    return list(owner.get("owned_files", [])) + list(owner.get("owned_create_prefixes", []))


def _overlap(left: str, right: str) -> bool:
    a, b = left.casefold().rstrip("/"), right.casefold().rstrip("/")
    return a == b or a.startswith(b + "/") or b.startswith(a + "/")


def validate_ownership(ownership: dict[str, Any], protected_paths: Iterable[str] = DEFAULT_PROTECTED_PATHS) -> tuple[bool, list[str]]:
    errors: list[str] = []
    if not isinstance(ownership, dict):
        return False, ["ownership must be an object"]
    owners = ownership.get("owners", ownership.get("workers", {}))
    if not isinstance(owners, dict) or not owners:
        return False, ["ownership must define workers"]
    codex_only = {repository_path(p).casefold() for p in ownership.get("codex_only_integration_files", [])}
    all_entries: list[tuple[str, str]] = []
    for worker, spec in owners.items():
        if not isinstance(spec, dict):
            errors.append(f"{worker}: ownership entry must be an object")
            continue
        for field in ("owned_files", "owned_create_prefixes", "shared_read_only"):
            for raw in spec.get(field, []):
                try:
                    path = repository_path(raw)
                except ValueError as exc:
                    errors.append(str(exc))
                    continue
                if field in {"owned_files", "owned_create_prefixes"}:
                    if _protected(path, protected_paths):
                        errors.append(f"protected path assigned to worker: {path}")
                    if worker.casefold() in {"antigravity", "anti", "agy"} and path.casefold() in codex_only:
                        errors.append(f"Codex-only integration file assigned to Antigravity: {path}")
                    all_entries.append((worker, path))
    for index, (worker, path) in enumerate(all_entries):
        for other, other_path in all_entries[index + 1:]:
            if worker != other and _overlap(path, other_path):
                errors.append(f"ownership overlap: {worker}:{path} and {other}:{other_path}")
    return not errors, errors


def validate_work_split(split: dict[str, Any], protected_paths: Iterable[str] = DEFAULT_PROTECTED_PATHS) -> tuple[bool, list[str]]:
    if not isinstance(split, dict) or not split.get("ownership"):
        return False, ["work split requires ownership"]
    return validate_ownership(split["ownership"], protected_paths)
