from pathlib import Path
import json,copy
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');scene=json.load(open(p/'central-front-wood-planter-candidate.json'));ground=json.load(open(p/'central-front-wood-planter-candidate-ground.json'));catalog=json.load(open(p/'central-front-wood-planter-candidate-objects.json'))
for mode in ['background-depth','wrong-registration']:
 s=copy.deepcopy(scene);g=copy.deepcopy(ground);c=copy.deepcopy(catalog);n='central-front-wood-planter-negative-'+mode
 if mode=='background-depth':
  for v in c['objects']['central-front-wood-planter-v1']['depthStrips']:v['groundFrontPixels'][1]=0
 elif mode=='late-front-depth':
  for v in c['objects']['central-front-wood-planter-v1']['depthStrips']:v['groundFrontPixels'][1]=1190
 else:next(v for v in g['objects'] if v['id']=='central-front-wood-planter')['contact'][0]+=20/16
 g['objectCatalogPath']='res://'+n+'-objects.json';s['assembledGroundPath']='res://'+n+'-ground.json'
 for suffix,d in [('',s),('-ground',g),('-objects',c)]: (p/(n+suffix+'.json')).write_text(json.dumps(d,indent=2)+'\n')
d=json.load(open(p/'central-front-wood-planter-design.json'));d['worldAnchor'][1]-=20;d['hiddenBack'][1]-=20;(p/'central-front-wood-planter-negative-p-foot-design.json').write_text(json.dumps(d,indent=2)+'\n')
