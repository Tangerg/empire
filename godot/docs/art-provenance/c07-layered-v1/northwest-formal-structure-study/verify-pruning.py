"""Audit the exact removal, with nonzero-material and pointer-cue negative controls."""
from pathlib import Path
import json
import math
from PIL import Image

root = Path(__file__).resolve().parent
read = lambda name: json.loads((root / name).read_text())
original = read('northwest-platform-surface.json')
pruned = read('northwest-platform-pruned-leaf-surface.json')
proof = read('zero-support-pruning-proof.json')
asset = read('northwest-platform-leaf-objects.json')['objects']['northwest-gate-study']
alpha = Image.open(root.parent / 'empire-c01/godot' / asset['resourcePath'][6:]).getchannel('A')
removed = {entry['supportId'] for entry in proof['removed']}

def bounds(points):
    return min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)

def check(ids, pointer):
    target = bounds(pointer)
    for member in original['members']:
        if member['supportId'] not in ids:
            continue
        assert member['coverage'] == 'native-alpha', 'non-native material cannot be discarded'
        x0, y0, x1, y1 = bounds(member['uvPixels'])
        rectangle = (max(0, math.floor(x0) - 2), max(0, math.floor(y0) - 2), min(alpha.width, math.ceil(x1) + 3), min(alpha.height, math.ceil(y1) + 3))
        assert alpha.crop(rectangle).getextrema()[1] == 0, 'nonzero material must remain'
        region = bounds(member['polygonPixels'])
        assert not (region[0] <= target[2] and target[0] <= region[2] and region[1] <= target[3] and target[1] <= region[3]), 'conservative pointer intersection must remain'

check(removed, asset['pointerPolygonPixels'])
assert pruned['supports'] == [s for s in original['supports'] if s['id'] not in removed]
assert pruned['members'] == [m for m in original['members'] if m['supportId'] not in removed]
negative_results = []
nonzero = next(m['supportId'] for m in original['members'] if m['supportId'] not in removed and m['coverage'] == 'native-alpha')
for name, ids, target, expected in [
    ('remove-material', removed | {nonzero}, asset['pointerPolygonPixels'], 'nonzero material must remain'),
    ('ignore-pointer-cue', removed, next(m['polygonPixels'] for m in original['members'] if m['supportId'] in removed), 'conservative pointer intersection must remain'),
]:
    try:
        check(ids, target)
    except AssertionError as error:
        assert str(error) == expected, (name, str(error))
        negative_results.append({'name': name, 'expectedFailure': str(error)})
    else:
        raise AssertionError(f'Negative control unexpectedly passed: {name}')
result = {'removedWholeSupports': len(removed), 'remainingGeometryUvSupportOrderExact': True, 'negativeControls': negative_results}
(root / 'zero-support-verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
