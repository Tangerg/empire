"""Read-only verification of the accepted drawings' exact runtime registration."""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
r = json.loads((HERE / 'extraction-review.json').read_text())
m = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
a = next(x for x in m['assets'] if x.get('id') == 'gravekeeper-recoil-v1')
assert a['topicId'] == 'C01-UNIT-GRAVEKEEPER' and a['pixelDensity'] == 1
assert a['frames'] == 4 and a['frameOrder'] == r['frameOrder']
assert [a['physicalWidth'], a['physicalHeight']] == r['runtimeDimensions']
assert a['frameRegions'] == [x['frameRegion'] for x in r['frames']]
assert a['frameAnchors'] == [x['frameAnchor'] for x in r['frames']]
assert a['presentationScale'] == r['presentationScale'] == .0688
assert hashlib.sha256((ROOT / 'godot/assets' / a['png']).read_bytes()).hexdigest() == a['sha256'] == r['runtimePngSha256']
assert m['actorClips']['gravekeeper.recoil'] == {
    'unit': 'milliseconds', 'appearanceId': 'gravekeeper-recoil-v1',
    'frames': [{'frame': name, 'span': span} for name, span in zip(r['frameOrder'], [40, 110, 90, 80])]}
b = json.loads((ROOT / 'godot/data/art-bindings.json').read_text())
assert b['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']['hit'] == 'gravekeeper.recoil'
print('PASS: exact runtime PNG, topic, four original frame crops/anchors, uniform scale and 320 ms finite reaction match accepted source registration.')
