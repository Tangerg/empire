"""Remove only whole native-alpha supports proven empty under the formal Linear sampler.

This is an offline export projection, not a runtime cache or a second support owner.
The 2-texel halo conservatively encloses Linear's four neighbouring samples.
Pointer cue intersection is geometric, so empty pieces overlapping its polygon are retained.
"""
from pathlib import Path
import json
import math
from PIL import Image

root = Path(__file__).resolve().parent
read = lambda name: json.loads((root / name).read_text())
write = lambda name, value: (root / name).write_text(json.dumps(value, indent=2) + '\n')
surface = read('northwest-platform-surface.json')
catalog = read('northwest-platform-leaf-objects.json')
asset = catalog['objects']['northwest-gate-study']
image = Image.open(root.parent / 'empire-c01/godot' / asset['resourcePath'][6:]).getchannel('A')
pointer = asset['pointerPolygonPixels']

def bounds(points):
    return min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)

def overlaps(a, b):
    return a[0] <= b[2] and b[0] <= a[2] and a[1] <= b[3] and b[1] <= a[3]

removed = []
for support in surface['supports']:
    members = [m for m in surface['members'] if m['supportId'] == support['id']]
    if not members or any(m['coverage'] != 'native-alpha' for m in members):
        continue
    if any(overlaps(bounds(m['polygonPixels']), bounds(pointer)) for m in members):
        continue
    rectangles = []
    for member in members:
        x0, y0, x1, y1 = bounds(member['uvPixels'])
        rectangle = (max(0, math.floor(x0) - 2), max(0, math.floor(y0) - 2),
                     min(image.width, math.ceil(x1) + 3), min(image.height, math.ceil(y1) + 3))
        if image.crop(rectangle).getextrema()[1] != 0:
            break
        rectangles.append(rectangle)
    else:
        removed.append({'supportId': support['id'], 'memberIds': [m['id'] for m in members], 'zeroAlphaUvRectangles': rectangles})

ids = {v['supportId'] for v in removed}
surface['supports'] = [s for s in surface['supports'] if s['id'] not in ids]
surface['members'] = [m for m in surface['members'] if m['supportId'] not in ids]
write('northwest-platform-pruned-leaf-surface.json', surface)
write('zero-support-pruning-proof.json', {'scope': 'Whole zero-alpha supports only; remaining geometry/UV/contact/order unchanged, pointer target bounds disjoint.', 'removed': removed, 'remainingSupports': len(surface['supports']), 'remainingMembers': len(surface['members'])})
asset['surfaceGeometryPath'] = 'res://northwest-platform-pruned-leaf-surface.json'
write('northwest-platform-pruned-leaf-objects.json', catalog)
ground = read('northwest-platform-leaf-ground.json')
ground['objectCatalogPath'] = 'res://northwest-platform-pruned-leaf-objects.json'
write('northwest-platform-pruned-leaf-ground.json', ground)
scene = read('northwest-platform-leaf-scene.json')
scene['assembledGroundPath'] = 'res://northwest-platform-pruned-leaf-ground.json'
write('northwest-platform-pruned-leaf-scene.json', scene)
print('Removed', len(removed), 'whole empty supports; remain', len(surface['supports']))
