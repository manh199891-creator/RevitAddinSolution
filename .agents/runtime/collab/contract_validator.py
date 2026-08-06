"""Pure validation of dual-writer plan and contract artifacts."""
from __future__ import annotations

import re
from typing import Any

HEX40 = re.compile(r"^[0-9a-fA-F]{40}$")


def _errors(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def validate_plan_package(package: dict[str, Any]) -> tuple[bool, list[str]]:
    errors: list[str] = []
    if not isinstance(package, dict):
        return False, ["plan package must be an object"]
    _errors(bool(package.get("schema_version")), "missing schema_version", errors)
    _errors(bool(package.get("plan_id")), "missing plan_id", errors)
    _errors(bool(package.get("base_commit")), "missing base commit", errors)
    if package.get("base_commit"):
        _errors(bool(HEX40.fullmatch(str(package["base_commit"]))), "base_commit must be a 40-character commit SHA", errors)
    criteria = package.get("acceptance_criteria")
    _errors(isinstance(criteria, list) and bool(criteria), "missing acceptance criteria", errors)
    if isinstance(criteria, list):
        for index, item in enumerate(criteria):
            _errors(isinstance(item, dict) and bool(item.get("id")) and bool(item.get("text")),
                    f"acceptance_criteria[{index}] must have id and text", errors)
    _errors(isinstance(package.get("scope"), dict), "missing scope", errors)
    return not errors, errors


def validate_contract_seed(seed: dict[str, Any], plan: dict[str, Any] | None = None) -> tuple[bool, list[str]]:
    errors: list[str] = []
    if not isinstance(seed, dict):
        return False, ["contract seed must be an object"]
    for key in ("schema_version", "contract_id", "plan_id", "base_commit", "approval_status"):
        _errors(bool(seed.get(key)), f"missing {key}", errors)
    _errors(seed.get("approval_status") in {"PENDING", "APPROVED", "BLOCKED"}, "invalid approval_status", errors)
    _errors(isinstance(seed.get("acceptance_criteria"), list) and bool(seed.get("acceptance_criteria")),
            "contract seed requires acceptance criteria", errors)
    if plan and seed.get("plan_id") != plan.get("plan_id"):
        errors.append("contract seed plan_id does not match plan")
    return not errors, errors


def require_valid(result: tuple[bool, list[str]]) -> None:
    valid, errors = result
    if not valid:
        raise ValueError("; ".join(errors))
