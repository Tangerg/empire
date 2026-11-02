from pathlib import Path
import json
root = Path(__file__).resolve().parent
read = lambda name: json.loads((root / name).read_text())
write = lambda name, value: (root / name).write_text(json.dumps(value, indent=2) + '\n')
for name, false_local_front in [('background', 0), ('foreground', 80)]:
    surface = read('northwest-leaf-surface.json')
    for support in surface['supports']:
        support['frontPixels'][1] = false_local_front
    write(f'leaf-{name}-surface.json', surface)
    catalog = read('northwest-platform-leaf-objects.json')
    catalog['objects']['northwest-leaf-study']['surfaceGeometryPath'] = f'res://leaf-{name}-surface.json'
    write(f'leaf-{name}-objects.json', catalog)
    ground = read('northwest-platform-leaf-ground.json')
    ground['objectCatalogPath'] = f'res://leaf-{name}-objects.json'
    write(f'leaf-{name}-ground.json', ground)
    scene = read('northwest-platform-leaf-scene.json')
    scene['assembledGroundPath'] = f'res://leaf-{name}-ground.json'
    write(f'leaf-{name}-scene.json', scene)
