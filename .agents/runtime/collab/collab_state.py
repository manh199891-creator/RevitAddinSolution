"""Small host-owned state machine; workers cannot advance it."""
from __future__ import annotations

from enum import StrEnum


class InvalidTransition(ValueError):
    pass


class CollabState(StrEnum):
    PLAN_AUTHORED = "PLAN_AUTHORED"
    PLAN_REVIEWED = "PLAN_REVIEWED"
    CONTRACT_SEED_APPROVED = "CONTRACT_SEED_APPROVED"
    WORK_SPLIT_APPROVED = "WORK_SPLIT_APPROVED"
    RESULTS_COLLECTED = "RESULTS_COLLECTED"
    REVIEW_PENDING = "REVIEW_PENDING"
    INTEGRATION_AUTHORIZED = "INTEGRATION_AUTHORIZED"
    COMPLETED = "COMPLETED"
    BLOCKED = "BLOCKED"


TRANSITIONS = {
    CollabState.PLAN_AUTHORED: {CollabState.PLAN_REVIEWED, CollabState.BLOCKED},
    CollabState.PLAN_REVIEWED: {CollabState.CONTRACT_SEED_APPROVED, CollabState.BLOCKED},
    CollabState.CONTRACT_SEED_APPROVED: {CollabState.WORK_SPLIT_APPROVED, CollabState.BLOCKED},
    CollabState.WORK_SPLIT_APPROVED: {CollabState.RESULTS_COLLECTED, CollabState.BLOCKED},
    CollabState.RESULTS_COLLECTED: {CollabState.REVIEW_PENDING, CollabState.BLOCKED},
    CollabState.REVIEW_PENDING: {CollabState.INTEGRATION_AUTHORIZED, CollabState.BLOCKED},
    CollabState.INTEGRATION_AUTHORIZED: {CollabState.COMPLETED, CollabState.BLOCKED},
    CollabState.COMPLETED: set(), CollabState.BLOCKED: set(),
}


def validate_transition(current: CollabState | str, target: CollabState | str) -> bool:
    current, target = CollabState(current), CollabState(target)
    return target in TRANSITIONS[current]


class StateMachine:
    def __init__(self, state: CollabState = CollabState.PLAN_AUTHORED):
        self.state = state

    def transition(self, target: CollabState | str, actor: str = "host") -> CollabState:
        if actor.casefold() not in {"host", "chatgpt"}:
            raise InvalidTransition("only the host or ChatGPT may mutate lifecycle state")
        target = CollabState(target)
        if not validate_transition(self.state, target):
            raise InvalidTransition(f"{self.state} -> {target} is not allowed")
        self.state = target
        return self.state
