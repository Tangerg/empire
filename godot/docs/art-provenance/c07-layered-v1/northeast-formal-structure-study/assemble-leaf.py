"""Add the independent leaf to the single NE study, with no extra site owner."""
from pathlib import Path
import json, math
root=Path(__file__).resolve().parent
read=lambda name:json.loads((root/name).read_text())
source=read('northeast-leaf-source.json')
points=[p for m in source['members'] for p in m['polygon']]
origin=[math.floor(min(p[i] for p in points)) for i in range(2)]
objects=read('northeast-interface-objects.json')
objects['objects']['northeast-leaf-study']={'resourcePath':source['textureResourcePath'],'pixelSize':[1326,1186],'anchorPixels':[0,0],'contactShadowRadiiPixels':None,'surfaceGeometryPath':'res://northeast-leaf-surface.json'}
ground=read('northeast-interface-ground.json');ground['objectCatalogPath']='res://northeast-leaf-objects.json'
ground['objects'].append({'id':'northeast-leaf-study','assetId':'northeast-leaf-study','contact':[v/16 for v in origin],'scale':1,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
scene=read('northeast-interface-scene.json');scene['assembledGroundPath']='res://northeast-leaf-ground.json'
for kind,data in [('objects',objects),('ground',ground),('scene',scene)]:
 (root/f'northeast-leaf-{kind}.json').write_text(json.dumps(data,indent=2)+'\n')
