from pathlib import Path
import json

root = Path(__file__).resolve().parent
read = lambda name: json.loads((root / name).read_text())
write = lambda name, value: (root / name).write_text(json.dumps(value, indent=2) + "\n")
objects = read('northwest-platform-objects.json')
objects['objects']['northwest-leaf-study'] = {
    'resourcePath': 'res://assets/native/c07-layered-v1/southeast-gate-v1-candidate.png',
    'pixelSize': [1326, 1186], 'anchorPixels': [0, 0], 'contactShadowRadiiPixels': None,
    'surfaceGeometryPath': 'res://northwest-leaf-surface.json',
}
ground = read('northwest-platform-ground.json')
ground['objectCatalogPath'] = 'res://northwest-platform-leaf-objects.json'
ground['objects'].append({'id': 'northwest-leaf-study', 'assetId': 'northwest-leaf-study',
    'contact': [153 / 16, 93 / 16], 'scale': 1, 'selectionCell': None,
    'ownsMapStructureBody': False, 'pointer': 'pass-through'})
scene = read('northwest-platform-scene.json')
scene['assembledGroundPath'] = 'res://northwest-platform-leaf-ground.json'
for kind, value in [('objects', objects), ('ground', ground), ('scene', scene)]:
    write(f'northwest-platform-leaf-{kind}.json', value)
# Read-only audit reference used by the existing formal support identity check.
write('northwest-platform-leaf-surface.json', read('northwest-platform-surface.json'))
