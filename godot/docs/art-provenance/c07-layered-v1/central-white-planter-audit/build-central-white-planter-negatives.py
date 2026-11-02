from pathlib import Path
import json,copy
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');scene=json.load(open(p/'central-white-planter-candidate.json'));ground=json.load(open(p/'central-white-planter-candidate-ground.json'));catalog=json.load(open(p/'central-white-planter-candidate-objects.json'))
for mode in ['background-depth','wrong-registration']:
 s=copy.deepcopy(scene);g=copy.deepcopy(ground);c=copy.deepcopy(catalog);n='central-white-planter-negative-'+mode
 if mode=='background-depth':
  for v in c['objects']['central-east-white-planter-v2']['depthStrips']:v['groundFrontPixels'][1]=0
 else:next(v for v in g['objects'] if v['id']=='central-east-white-planter')['contact'][0]+=20/16
 g['objectCatalogPath']='res://'+n+'-objects.json';s['assembledGroundPath']='res://'+n+'-ground.json'
 for suffix,d in [('',s),('-ground',g),('-objects',c)]: (p/(n+suffix+'.json')).write_text(json.dumps(d,indent=2)+'\n')
d=json.load(open(p/'central-white-planter-design.json'));d['worldAnchor'][0]+=13;d['hiddenBack'][0]+=13;(p/'central-white-planter-negative-p-foot-design.json').write_text(json.dumps(d,indent=2)+'\n')
