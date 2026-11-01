import json,copy
from pathlib import Path
base=Path('empire-c01/godot/data/scenes');out=Path('c07-static-preview')
s=json.loads((base/'c01-07-granary-bin-surface.json').read_text())
for p in s['supports']:p['frontPixels'][1]=251-218
(out/'formal-granary-bin-flat-left-surface.json').write_text(json.dumps(s,indent=2))
c=json.loads((base/'c01-07-granary-bin-surface-preview-objects.json').read_text());c['objects']['granary-bin-surface']['surfaceGeometryPath']='res://formal-granary-bin-flat-left-surface.json';(out/'formal-granary-bin-flat-left-objects.json').write_text(json.dumps(c,indent=2))
g=json.loads((base/'c01-07-granary-bin-surface-preview-ground.json').read_text());g['objectCatalogPath']='res://formal-granary-bin-flat-left-objects.json';(out/'formal-granary-bin-flat-left-ground.json').write_text(json.dumps(g,indent=2))
sc=json.loads((base/'c01-07-granary-bin-surface-preview.json').read_text());sc['assembledGroundPath']='res://formal-granary-bin-flat-left-ground.json';(out/'formal-granary-bin-flat-left-scene.json').write_text(json.dumps(sc,indent=2))
