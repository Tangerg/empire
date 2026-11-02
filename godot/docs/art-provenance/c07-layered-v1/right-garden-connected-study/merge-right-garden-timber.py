"""Reproducible offset-only merge of seven offline structural-owner exports.
Only the merged explicit surface is loaded at runtime, not these construction curves.
"""
from pathlib import Path
import json,math
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');index=json.load(open(p/'right-garden-timber-source-index.json'));origin=[540,400];result={'schema':1,'supports':[],'members':[]}
for name in index['sourceNames']:
 s=json.load(open(p/f'right-garden-timber-{name}-source.json'));d=json.load(open(p/f'right-garden-timber-{name}-surface.json'));points=[v for m in s['members']for v in m['polygon']];local=[math.floor(min(v[k]for v in points))for k in [0,1]];delta=[local[k]-origin[k]for k in [0,1]]
 for v in d['supports']:v['id']=name+'-'+v['id'];v['frontPixels']=[v['frontPixels'][k]+delta[k]for k in [0,1]];result['supports'].append(v)
 for v in d['members']:v['id']=name+'-'+v['id'];v['supportId']=name+'-'+v['supportId'];v['polygonPixels']=[[q[k]+delta[k]for k in [0,1]]for q in v['polygonPixels']];result['members'].append(v)
(p/'right-garden-timber-merged-surface.json').write_text(json.dumps(result,indent=2)+'\n')
s=json.load(open(p/'west-inner-garden-stone-study.json'));g=json.load(open(p/'west-inner-garden-stone-study-ground.json'));c=json.load(open(p/'west-inner-garden-stone-study-objects.json'));c['objects']['right-garden-timber']={'resourcePath':'res://assets/native/c07-layered-v1/training-fence-v2-rejected.png','pixelSize':[1187,1325],'anchorPixels':[0,0],'surfaceGeometryPath':'res://right-garden-timber-merged-surface.json','contactShadowRadiiPixels':None};g['objects'].append({'id':'right-garden-timber','assetId':'right-garden-timber','contact':[v/16 for v in origin],'scale':1,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'});s['assembledGroundPath']='res://right-garden-frame-study-ground.json';g['objectCatalogPath']='res://right-garden-frame-study-objects.json'
for suffix,j in [('',s),('-ground',g),('-objects',c)]:(p/f'right-garden-frame-study{suffix}.json').write_text(json.dumps(j,indent=2)+'\n')
print('supports',len(result['supports']),'members',len(result['members']))
