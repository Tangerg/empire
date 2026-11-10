"""Read-only comparison of this slice with its published implementation baseline."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = 'd76f6fff0beec348e6ca3110fe439aeb318cd8bf'
for name in ('selected-assets.json', 'art-bindings.json'):
    relative = f'godot/data/{name}'
    baseline = json.loads(subprocess.check_output(['git', '-C', str(ROOT), 'show', f'{BASE}:{relative}']))
    actual = json.loads((ROOT / relative).read_text())
    if name == 'selected-assets.json':
        removed = [item for item in actual['assets'] if item.get('id') == 'archer-cheer-v1']
        assert len(removed) == 1, 'Expected exactly one new archer cheer asset.'
        actual['assets'].remove(removed[0])
        assert actual['actorClips'].pop('archer.cheer')['appearanceId'] == 'archer-cheer-v1'
    else:
        clips = actual['CANDIDATE_01_UNIT_ART']['c01.archer']['actionClips']
        assert clips['celebrate'] == 'archer.cheer'
        clips['celebrate'] = baseline['CANDIDATE_01_UNIT_ART']['c01.archer']['actionClips']['celebrate']
    assert baseline == actual, f'Unrelated production data changed: {relative}'
print(f'Compared with baseline {BASE}.')
print('Both production data documents are semantically identical after removing only archer-cheer-v1, archer.cheer and the c01.archer celebration binding change.')
