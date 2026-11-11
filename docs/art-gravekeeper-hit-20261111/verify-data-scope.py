"""Read-only comparison of this slice with its published implementation baseline."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = '9dc658041886002d35f9b89647a701852d6a2047'
for name in ('selected-assets.json', 'art-bindings.json'):
    relative = f'godot/data/{name}'
    baseline = json.loads(subprocess.check_output(['git', '-C', str(ROOT), 'show', f'{BASE}:{relative}']))
    actual = json.loads((ROOT / relative).read_text())
    if name == 'selected-assets.json':
        removed = [item for item in actual['assets'] if item.get('id') == 'gravekeeper-recoil-v1']
        assert len(removed) == 1, 'Expected exactly one new gravekeeper recoil asset.'
        actual['assets'].remove(removed[0])
        assert actual['actorClips'].pop('gravekeeper.recoil')['appearanceId'] == 'gravekeeper-recoil-v1'
    else:
        clips = actual['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']
        assert clips['hit'] == 'gravekeeper.recoil'
        clips['hit'] = baseline['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']['hit']
    assert baseline == actual, f'Unrelated production data changed: {relative}'
print(f'Compared with baseline {BASE}.')
print('Both production data documents are semantically identical after removing only gravekeeper-recoil-v1, gravekeeper.recoil and the c01.gravekeeper hit binding change.')
