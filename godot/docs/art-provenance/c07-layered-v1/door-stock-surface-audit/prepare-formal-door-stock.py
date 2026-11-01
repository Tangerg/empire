import json,copy,math
from pathlib import Path
from PIL import Image
out=Path('c07-static-preview');root=Path('empire-c01/godot/data/scenes');source=json.loads((out/'granary-door-stock-geometries.json').read_text())
cat=json.loads((root/'c01-07-granary-bin-surface-preview-objects.json').read_text());g=json.loads((root/'c01-07-granary-bin-surface-preview-ground.json').read_text());sc=json.loads((root/'c01-07-granary-bin-surface-preview.json').read_text());origins={}
for name,c in zip(['rear','left','right'],source['containers']):
 (out/f'formal-door-stock-{name}-source.json').write_text(json.dumps(c,indent=2))
 v=[p for m in c['members'] for p in m['polygon']];origin=[math.floor(min(p[i] for p in v)) for i in [0,1]];origins[name]=origin
 aid=f'granary-door-stock-{name}-surface';cat['objects'][aid]={'resourcePath':c['textureResourcePath'],'pixelSize':list(Image.open(Path('empire-c01/godot')/c['textureResourcePath'][6:]).size),'anchorPixels':[0,0],'surfaceGeometryPath':f'res://data/scenes/c01-07-granary-door-stock-{name}-surface.json','contactShadowRadiiPixels':None}
 g['objects'].append({'id':f'granary-door-stock-{name}','assetId':aid,'contact':[p/16 for p in origin],'scale':1,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
g['objectCatalogPath']='res://data/scenes/c01-07-granary-door-stock-surface-preview-objects.json';sc['assembledGroundPath']='res://data/scenes/c01-07-granary-door-stock-surface-preview-ground.json'
for suffix,obj in [('objects',cat),('ground',g)]: (root/f'c01-07-granary-door-stock-surface-preview-{suffix}.json').write_text(json.dumps(obj,indent=2))
(root/'c01-07-granary-door-stock-surface-preview.json').write_text(json.dumps(sc,indent=2));(out/'formal-door-stock-origins.json').write_text(json.dumps(origins,indent=2))
