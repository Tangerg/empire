"""Read-only comparison of this slice with its published implementation baseline."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = '4c563eda239625ef115a8e8d3b2b1a61e042939c'
for name in ('selected-assets.json', 'art-bindings.json'):
    relative = f'godot/data/{name}'
    baseline = json.loads(subprocess.check_output(['git', '-C', str(ROOT), 'show', f'{BASE}:{relative}']))
    actual = json.loads((ROOT / relative).read_text())
    if name == 'selected-assets.json':
        removed = [item for item in actual['assets'] if item.get('id') == 'archer-fall-v1']
        assert len(removed) == 1, 'Expected exactly one new archer fall asset.'
        actual['assets'].remove(removed[0])
        assert actual['actorClips'].pop('archer.fall')['appearanceId'] == 'archer-fall-v1'
    else:
        clips = actual['CANDIDATE_01_UNIT_ART']['c01.archer']['actionClips']
        assert clips['death'] == 'archer.fall'
        clips['death'] = baseline['CANDIDATE_01_UNIT_ART']['c01.archer']['actionClips']['death']
    assert baseline == actual, f'Unrelated production data changed: {relative}'
print(f'Compared with baseline {BASE}.')
print('Both production data documents are semantically identical after removing only archer-fall-v1, archer.fall and the c01.archer death binding change.')
