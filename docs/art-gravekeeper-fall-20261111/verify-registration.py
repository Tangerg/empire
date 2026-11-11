"""Read-only verification of the accepted drawings' exact runtime registration."""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
r = json.loads((HERE / 'extraction-review.json').read_text())
m = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
a = next(x for x in m['assets'] if x.get('id') == 'gravekeeper-fall-v1')
assert a['topicId'] == 'C01-UNIT-GRAVEKEEPER' and a['pixelDensity'] == 1
assert a['frames'] == 6 and a['frameOrder'] == r['frameOrder']
assert [a['physicalWidth'], a['physicalHeight']] == r['runtimeDimensions']
assert a['frameRegions'] == [x['frameRegion'] for x in r['frames']]
assert a['frameAnchors'] == [x['frameAnchor'] for x in r['frames']]
assert a['presentationScale'] == r['presentationScale'] == .1
assert hashlib.sha256((ROOT / 'godot/assets' / a['png']).read_bytes()).hexdigest() == a['sha256'] == r['runtimePngSha256']
assert m['actorClips']['gravekeeper.fall'] == {
    'unit': 'milliseconds', 'appearanceId': 'gravekeeper-fall-v1',
    'death': {'mode': 'after-clip', 'fadeMs': 160},
    'frames': [{'frame': name, 'span': span} for name, span in zip(r['frameOrder'], [60, 70, 90, 110, 110, 180])]}
b = json.loads((ROOT / 'godot/data/art-bindings.json').read_text())
assert b['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']['death'] == 'gravekeeper.fall'
print('PASS: exact runtime PNG, topic, six original frame crops/anchors, uniform scale and 620 ms finite fall followed by 160 ms terminal fade match accepted source registration.')
