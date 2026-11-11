"""Read-only comparison of this slice with its published implementation baseline."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = 'ea0eb0513e48ed650c2c04d259bfc4baee7f3021'
for name in ('selected-assets.json', 'art-bindings.json'):
    relative = f'godot/data/{name}'
    baseline = json.loads(subprocess.check_output(['git', '-C', str(ROOT), 'show', f'{BASE}:{relative}']))
    actual = json.loads((ROOT / relative).read_text())
    if name == 'selected-assets.json':
        removed = [item for item in actual['assets'] if item.get('id') == 'gravekeeper-fall-v1']
        assert len(removed) == 1, 'Expected exactly one new gravekeeper fall asset.'
        actual['assets'].remove(removed[0])
        assert actual['actorClips'].pop('gravekeeper.fall')['appearanceId'] == 'gravekeeper-fall-v1'
    else:
        clips = actual['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']
        assert clips['death'] == 'gravekeeper.fall'
        clips['death'] = baseline['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']['death']
    assert baseline == actual, f'Unrelated production data changed: {relative}'
print(f'Compared with baseline {BASE}.')
print('Both production data documents are semantically identical after removing only gravekeeper-fall-v1, gravekeeper.fall and the c01.gravekeeper death binding change.')
