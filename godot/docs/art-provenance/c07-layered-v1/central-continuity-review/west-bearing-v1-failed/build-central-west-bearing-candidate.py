from pathlib import Path
import json,math
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';b=r/'empire-c01/godot/data/scenes';merged={'schema':1,'supports':[],'members':[]}
for name in ['cap','riser']:
 s=json.load(open(p/f'central-west-bearing-{name}-source.json'));d=json.load(open(p/f'central-west-bearing-{name}-surface.json'));points=[v for m in s['members'] for v in m['polygon']];origin=[math.floor(min(q[k] for q in points)) for k in [0,1]];delta=[origin[0]-680,origin[1]-474]
 for v in d['supports']:v['id']=name+'-'+v['id'];v['frontPixels']=[v['frontPixels'][k]+delta[k] for k in [0,1]];merged['supports'].append(v)
 for v in d['members']:v['id']=name+'-'+v['id'];v['supportId']=name+'-'+v['supportId'];v['polygonPixels']=[[q[k]+delta[k] for k in [0,1]] for q in v['polygonPixels']];merged['members'].append(v)
(p/'central-west-bearing-surface.json').write_text(json.dumps(merged,indent=2)+'\n')
s=json.load(open(b/'c01-07-selected-assembly.json'));g=json.load(open(b/'c01-07-selected-assembly-ground.json'));c=json.load(open(b/'c01-07-selected-assembly-objects.json'))
new=[]
for name,origin in [('central-west-bearing',[680,474]),('central-west-corner-pier',[680,468])]:
 c['objects'][name]={'resourcePath':'res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png','pixelSize':[1024,1536],'anchorPixels':[0,0],'surfaceGeometryPath':f'res://{name}-surface.json','contactShadowRadiiPixels':None};new.append({'id':name,'assetId':name,'contact':[v/16 for v in origin],'scale':1.,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
# Physical low support before supported props on exact shared-Z ties; no arbitrary depth bias.
i=next(i for i,v in enumerate(g['objects']) if v['id']=='central-west-wood-planter');g['objects'][i:i]=new
g['objects'].append({'id':'central-west-white-planter','assetId':'central-east-white-planter-v2','contact':[696/16,473/16],'scale':.036,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
g['objectCatalogPath']='res://central-west-bearing-candidate-objects.json';s['assembledGroundPath']='res://central-west-bearing-candidate-ground.json'
for suffix,v in [('',s),('-ground',g),('-objects',c)]: (p/f'central-west-bearing-candidate{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
