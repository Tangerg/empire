from pathlib import Path
import json,copy
base=Path('empire-c01/godot/data/scenes');out=Path('c07-static-preview');origins=json.loads((out/'formal-door-stock-origins.json').read_text())
for kind in ['late','spill']:
 cat=json.loads((base/'c01-07-granary-door-stock-surface-preview-objects.json').read_text());g=json.loads((base/'c01-07-granary-door-stock-surface-preview-ground.json').read_text());sc=json.loads((base/'c01-07-granary-door-stock-surface-preview.json').read_text())
 for name in ['rear','left','right']:
  s=json.loads((base/f'c01-07-granary-door-stock-{name}-surface.json').read_text());ox,oy=origins[name]
  if kind=='late':
   for p in s['supports']:p['frontPixels'][1]=260-oy
  elif name=='left':
   s['supports'].append({'id':'negative-corridor-intrusion','frontPixels':[530.5-ox,219-oy],'kind':'structural'})
   s['members'].append({'id':'negative-between-cell-centres','supportId':'negative-corridor-intrusion','polygonPixels':[[530-ox,225-oy],[531-ox,225-oy],[531-ox,227-oy],[530-ox,227-oy]],'uvPixels':[[1200,600]]*4,'tint':'ffffff','coverage':'opaque'})
  (out/f'formal-door-stock-{kind}-{name}-surface.json').write_text(json.dumps(s,indent=2));cat['objects'][f'granary-door-stock-{name}-surface']['surfaceGeometryPath']=f'res://formal-door-stock-{kind}-{name}-surface.json'
 (out/f'formal-door-stock-{kind}-objects.json').write_text(json.dumps(cat,indent=2));g['objectCatalogPath']=f'res://formal-door-stock-{kind}-objects.json';(out/f'formal-door-stock-{kind}-ground.json').write_text(json.dumps(g,indent=2));sc['assembledGroundPath']=f'res://formal-door-stock-{kind}-ground.json';(out/f'formal-door-stock-{kind}-scene.json').write_text(json.dumps(sc,indent=2))
