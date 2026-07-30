
import sys
sys.path.insert(0, '../.agents/runtime')
from review_pipeline import get_deterministic_snapshot, validate_scope, _dirty_file_fingerprint
from pathlib import Path
import json

repo = Path('.')
_, current_files = get_deterministic_snapshot(repo)
print('Current Files:', current_files)

baseline = json.load(open('../.agents/factory/RevitAddinSolution/.agent/state/task_baseline.json', encoding='utf-8'))
baseline_files = baseline.get('files', {})

# NOTE: current_files is returned as a SET of strings from get_deterministic_snapshot in our earlier print!
# Let's check type of current_files
print('Type of current_files:', type(current_files))

if isinstance(current_files, dict):
    paths = list(current_files.keys())
else:
    paths = list(current_files)

current_fingerprints = {p: _dirty_file_fingerprint(repo, p) for p in paths}
print('App.cs fingerprint:', current_fingerprints.get('src/Antigravity.Main/App.cs'))
print('Baseline fingerprint:', baseline_files.get('src/Antigravity.Main/App.cs'))

excl = [p for p, f in current_fingerprints.items() if baseline_files.get(p) == f]
print('Excluded:', excl)

task_files = sorted(p for p in paths if p not in excl)
print('Task files:', task_files)

scope_path = Path('../.agents/factory/RevitAddinSolution/.agent/context/TASK_SCOPE.json')
valid, issues = validate_scope(repo, task_files, scope_path)
print('VALID:', valid)
print('ISSUES:', issues)

