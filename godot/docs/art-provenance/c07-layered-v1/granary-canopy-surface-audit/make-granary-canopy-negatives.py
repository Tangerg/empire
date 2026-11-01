from pathlib import Path
import json,copy
base=Path('empire-c01/godot/data/scenes');out=Path('c07-static-preview')
source=json.loads((out/'granary-canopy-geometry.json').read_text());source['members'].append({'id':'deliberate-filled-bay','polygon':[[380,154],[504,154],[504,207],[439,223],[384,216]],'uv':[[408,450]]*5,'tint':'ffffff'});(out/'formal-granary-canopy-filled-source.json').write_text(json.dumps(source,indent=2))
s=json.loads((base/'c01-07-granary-canopy-surface.json').read_text());cat=json.loads((base/'c01-07-granary-canopy-surface-preview-objects.json').read_text());g=json.loads((base/'c01-07-granary-canopy-surface-preview-ground.json').read_text());sc=json.loads((base/'c01-07-granary-canopy-surface-preview.json').read_text())
for kind in ['single-west-foot','filled-hole']:
 t=copy.deepcopy(s)
 if kind=='single-west-foot':
  for support in t['supports']:support['frontPixels'][1]=216-125
 (out/f'formal-granary-canopy-{kind}-surface.json').write_text(json.dumps(t,indent=2))
 c=copy.deepcopy(cat);c['objects']['granary-canopy-surface']['surfaceGeometryPath']=f'res://formal-granary-canopy-{kind}-surface.json';(out/f'formal-granary-canopy-{kind}-objects.json').write_text(json.dumps(c,indent=2))
 gg=copy.deepcopy(g);gg['objectCatalogPath']=f'res://formal-granary-canopy-{kind}-objects.json';(out/f'formal-granary-canopy-{kind}-ground.json').write_text(json.dumps(gg,indent=2))
 ss=copy.deepcopy(sc);ss['assembledGroundPath']=f'res://formal-granary-canopy-{kind}-ground.json';(out/f'formal-granary-canopy-{kind}-scene.json').write_text(json.dumps(ss,indent=2))
