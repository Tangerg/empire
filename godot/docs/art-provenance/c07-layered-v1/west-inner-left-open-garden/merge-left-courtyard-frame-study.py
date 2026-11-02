from pathlib import Path
from PIL import Image
import json,math
p=Path(__file__).resolve().parent;r=p.parent/'empire-c01/godot';layout=json.load(open(p/'left-courtyard-frame-new-layout.json'));origin=[360,420];out={'schema':1,'supports':[],'members':[]}
for component in layout['components']:
 name=component['id'];data=json.load(open(p/f'left-courtyard-frame-{name}-surface.json'));vertices=[q for m in component['members']for q in m['polygon']];local=[math.floor(min(q[k] for q in vertices)) for k in (0,1)];delta=[local[k]-origin[k]for k in(0,1)]
 for support in data['supports']:
  support['id']=name+'-'+support['id'];support['frontPixels']=[support['frontPixels'][k]+delta[k]for k in(0,1)];out['supports'].append(support)
 for member in data['members']:
  member['id']=name+'-'+member['id'];member['supportId']=name+'-'+member['supportId'];member['polygonPixels']=[[q[k]+delta[k]for k in(0,1)]for q in member['polygonPixels']];out['members'].append(member)
(p/'left-courtyard-frame-merged-surface.json').write_text(json.dumps(out,indent=2)+'\n')
scene=json.load(open(r/'data/scenes/c01-07-selected-assembly.json'));ground=json.load(open(r/'data/scenes/c01-07-selected-assembly-ground.json'));catalog=json.load(open(r/'data/scenes/c01-07-selected-assembly-objects.json'));texture=layout['components'][0]['textureResourcePath']
catalog['objects']['left-courtyard-open-frame']={'resourcePath':texture,'pixelSize':list(Image.open(r/texture[6:]).size),'anchorPixels':[0,0],'surfaceGeometryPath':'res://left-courtyard-frame-merged-surface.json','contactShadowRadiiPixels':None}
ground['objects'].append({'id':'left-courtyard-open-frame','assetId':'left-courtyard-open-frame','contact':[v/16 for v in origin],'scale':1,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
scene['assembledGroundPath']='res://left-courtyard-frame-study-ground.json';ground['objectCatalogPath']='res://left-courtyard-frame-study-objects.json'
for suffix,value in [('',scene),('-ground',ground),('-objects',catalog)]:(p/f'left-courtyard-frame-study{suffix}.json').write_text(json.dumps(value,indent=2)+'\n')
print(len(out['supports']),'support entries',len(out['members']),'triangles')
