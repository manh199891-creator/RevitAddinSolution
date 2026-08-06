import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from collab.collab_state import CollabState, InvalidTransition, StateMachine, validate_transition


class StateTests(unittest.TestCase):
    def test_invalid_state_transition(self):
        self.assertFalse(validate_transition(CollabState.PLAN_AUTHORED, CollabState.COMPLETED))
        with self.assertRaises(InvalidTransition):
            StateMachine().transition(CollabState.COMPLETED)

    def test_agent_cannot_mutate_host_owned_lifecycle_state(self):
        machine = StateMachine()
        with self.assertRaises(InvalidTransition):
            machine.transition(CollabState.PLAN_REVIEWED, actor="codex")
        self.assertEqual(machine.state, CollabState.PLAN_AUTHORED)

    def test_host_owned_happy_path(self):
        machine = StateMachine()
        for state in (CollabState.PLAN_REVIEWED, CollabState.CONTRACT_SEED_APPROVED,
                      CollabState.WORK_SPLIT_APPROVED, CollabState.RESULTS_COLLECTED,
                      CollabState.REVIEW_PENDING, CollabState.INTEGRATION_AUTHORIZED,
                      CollabState.COMPLETED):
            machine.transition(state)
        self.assertEqual(machine.state, CollabState.COMPLETED)


if __name__ == "__main__":
    unittest.main()
