from pathlib import Path
import json,math
p=Path(__file__).parent;r=p.parent/'empire-c01/godot/data/scenes';layout=json.load(open(p/'left-garden-stone-new-layout.json'));origin=[300,420];out={'schema':1,'supports':[],'members':[]}
for c in layout['components']:
 name=c['id'];d=json.load(open(p/f'left-garden-stone-{name}-surface.json'));ps=[q for m in c['members']for q in m['polygon']];local=[math.floor(min(q[k]for q in ps))for k in(0,1)];delta=[local[k]-origin[k]for k in(0,1)]
 for v in d['supports']:v['id']=name+'-'+v['id'];v['frontPixels']=[v['frontPixels'][k]+delta[k]for k in(0,1)];out['supports'].append(v)
 for v in d['members']:v['id']=name+'-'+v['id'];v['supportId']=name+'-'+v['supportId'];v['polygonPixels']=[[q[k]+delta[k]for k in(0,1)]for q in v['polygonPixels']];out['members'].append(v)
(p/'left-garden-stone-merged-surface.json').write_text(json.dumps(out,indent=2)+'\n')
s=json.load(open(r/'c01-07-selected-assembly.json'));g=json.load(open(r/'c01-07-selected-assembly-ground.json'));c=json.load(open(r/'c01-07-selected-assembly-objects.json'));c['objects']['left-garden-stone']={'resourcePath':layout['components'][0]['textureResourcePath'],'pixelSize':[1536,1024],'anchorPixels':[0,0],'surfaceGeometryPath':'res://left-garden-stone-merged-surface.json','contactShadowRadiiPixels':None};g['objects'].append({'id':'left-garden-stone','assetId':'left-garden-stone','contact':[v/16 for v in origin],'scale':1,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'});s['assembledGroundPath']='res://left-garden-stone-study-ground.json';g['objectCatalogPath']='res://left-garden-stone-study-objects.json'
for suffix,v in [('',s),('-ground',g),('-objects',c)]:(p/f'left-garden-stone-study{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
print('supports',len(out['supports']),'members',len(out['members']))
for side in ['back','front']:
 x=(p/f'launch-left-garden-original-{side}.sh').read_text().replace('res://data/scenes/c01-07.json','res://left-garden-stone-study.json').replace('left-garden-original-'+side,'left-garden-stone-v1-'+side)
 start=x.index('export C07_STATIC_LABEL=');end=x.index('\nexport C07_STATIC_OUTPUT=',start);x=x[:start]+'export C07_STATIC_LABEL="LEFT GARDEN | NEW RULE-PRIORITY STONE CANDIDATE\nOriginal near foot516 conflicts with road; new finite foot503 means old rear pose504 is now FRONT.\nOnly stone walls/piers. Trellis/trees/back wall absent. No actions, AI, saves. Art unreviewed."'+x[end:];(p/f'launch-left-garden-stone-v1-{side}.sh').write_text(x)
