from pathlib import Path
import json,math
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');b=p.parent/'empire-c01/godot/data/scenes'
s=json.load(open(p/'central-rear-west-group-source.json'));pts=[q for part in s['parts'] for q in part['polygon']];O=[math.floor(min(q[k] for q in pts)) for k in [0,1]];d={'schema':1,'supports':[],'members':[]}
for part in s['parts']:
 n=part['id'];a=json.load(open(p/f'rear-west-{n}-surface.json'));lo=[math.floor(min(q[k] for q in part['polygon'])) for k in [0,1]]
 for z in a['supports']:
  z['id']=n+'-'+z['id'];z['frontPixels']=[z['frontPixels'][k]+lo[k]-O[k] for k in [0,1]]
  if part['source'].get('groundProjectionHeight'):
   z['frontPixels'][1]+=part['source']['groundProjectionHeight']
  if n=='bed-foliage':
   z['id']='bed-root';z['frontPixels']=[s['foliageGroundRoot'][k]-O[k] for k in [0,1]]
   if any(v['id']==z['id'] for v in d['supports']):continue
  d['supports'].append(z)
 for v in a['members']:
  v['id']=n+'-'+v['id'];v['supportId']='bed-root' if n=='bed-foliage' else n+'-'+v['supportId'];v['polygonPixels']=[[q[k]+lo[k]-O[k] for k in [0,1]] for q in v['polygonPixels']];d['members'].append(v)
(p/'central-rear-west-group-surface.json').write_text(json.dumps(d,indent=2)+'\n')
scene=json.load(open(b/'c01-07-selected-assembly.json'));g=json.load(open(b/'c01-07-selected-assembly-ground.json'));c=json.load(open(b/'c01-07-selected-assembly-objects.json'));c['objects']['central-rear-west-group']={'resourcePath':s['parts'][0]['source']['textureResourcePath'],'pixelSize':[1312,1199],'anchorPixels':[0,0],'surfaceGeometryPath':'res://central-rear-west-group-surface.json','contactShadowRadiiPixels':None}
g['objects']=[v for v in g['objects'] if v['id']!='central-rear-west-group']
g['objects'].append({'id':'central-rear-west-group','assetId':'central-rear-west-group','contact':[v/16 for v in O],'scale':1.,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
g['objectCatalogPath']='res://central-rear-west-study-objects.json';scene['assembledGroundPath']='res://central-rear-west-study-ground.json'
for suffix,v in [('',scene),('-ground',g),('-objects',c)]: (p/f'central-rear-west-study{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
print('origin',O,'supports',len(d['supports']),'members',len(d['members']))
