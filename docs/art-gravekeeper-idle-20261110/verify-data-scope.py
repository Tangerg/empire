"""Read-only comparison of this slice with its published implementation baseline."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = 'f358d5bc762a6830ef15ba3f5cd88ec998acf1e2'
for name in ('selected-assets.json', 'art-bindings.json'):
    relative = f'godot/data/{name}'
    baseline = json.loads(subprocess.check_output(['git', '-C', str(ROOT), 'show', f'{BASE}:{relative}']))
    actual = json.loads((ROOT / relative).read_text())
    if name == 'selected-assets.json':
        removed = [item for item in actual['assets'] if item.get('id') == 'gravekeeper-breathe-v1']
        assert len(removed) == 1, 'Expected exactly one new gravekeeper breathe asset.'
        actual['assets'].remove(removed[0])
        assert actual['actorClips'].pop('gravekeeper.breathe')['appearanceId'] == 'gravekeeper-breathe-v1'
    else:
        clips = actual['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']
        assert clips['idle'] == 'gravekeeper.breathe'
        clips['idle'] = baseline['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']['idle']
    assert baseline == actual, f'Unrelated production data changed: {relative}'
print(f'Compared with baseline {BASE}.')
print('Both production data documents are semantically identical after removing only gravekeeper-breathe-v1, gravekeeper.breathe and the c01.gravekeeper idle binding change.')
