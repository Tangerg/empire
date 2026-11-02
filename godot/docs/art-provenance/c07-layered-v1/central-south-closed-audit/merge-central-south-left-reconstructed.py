from pathlib import Path
import json,math
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');s=json.load(open(p/'central-south-left-reconstructed-source.json'));pts=[q for part in s['parts'] for q in part['polygon']];O=[math.floor(min(q[k] for q in pts)) for k in [0,1]];d={'schema':1,'supports':[],'members':[]}
for part in s['parts']:
 n=part['id'];a=json.load(open(p/f'south-left-{n}-surface.json'));lo=[math.floor(min(q[k] for q in part['polygon'])) for k in [0,1]]
 for z in a['supports']:
  z['id']=n+'-'+z['id'];z['frontPixels']=[z['frontPixels'][k]+lo[k]-O[k] for k in [0,1]]
  if n=='rooted-foliage':
   z['id']='physical-soil-root';z['frontPixels']=[745-O[0],514-O[1]]
   if any(v['id']==z['id'] for v in d['supports']):continue
  d['supports'].append(z)
 for v in a['members']:
  v['id']=n+'-'+v['id'];v['supportId']='physical-soil-root' if n=='rooted-foliage' else n+'-'+v['supportId'];v['polygonPixels']=[[q[k]+lo[k]-O[k] for k in [0,1]] for q in v['polygonPixels']];d['members'].append(v)
(p/'central-south-left-reconstructed-surface.json').write_text(json.dumps(d,indent=2)+'\n')
scene=json.load(open(p/'central-south-bearing-study.json'));g=json.load(open(p/'central-south-bearing-study-ground.json'));c=json.load(open(p/'central-south-bearing-study-objects.json'));c['objects']['central-south-left-reconstructed']={'resourcePath':'res://assets/native/c07-layered-v1/central-front-wood-planter-v1-candidate.png','pixelSize':[1312,1199],'anchorPixels':[0,0],'surfaceGeometryPath':'res://central-south-left-reconstructed-surface.json','contactShadowRadiiPixels':None}
i=next(v for v in g['objects'] if v['id']=='central-south-left-planter');i['assetId']='central-south-left-reconstructed';i['contact']=[v/16 for v in O];i['scale']=1.
g['objectCatalogPath']='res://central-south-reconstructed-objects.json';scene['assembledGroundPath']='res://central-south-reconstructed-ground.json'
for suffix,v in [('',scene),('-ground',g),('-objects',c)]: (p/f'central-south-reconstructed{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
print('origin',O,'supports',len(d['supports']),'members',len(d['members']))
