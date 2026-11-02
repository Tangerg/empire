import json,copy
from pathlib import Path
r=Path('/workspace/scratch/84c9fe948755');g=r/'empire-c01/godot/data/scenes';o=r/'c07-static-preview';s0=json.load(open(g/'c01-07-selected-assembly.json'));l0=json.load(open(g/'c01-07-selected-assembly-ground.json'));c0=json.load(open(g/'c01-07-selected-assembly-objects.json'))
for mode in ['background-depth','wrong-registration']:
 s,l,c=copy.deepcopy(s0),copy.deepcopy(l0),copy.deepcopy(c0);prefix='central-landmark-negative-'+mode;i=next(i for i in l['objects'] if i['id']=='central-landmark-core');a=c['objects'][i['assetId']]
 if mode=='background-depth':
  for strip in a['depthStrips']:strip['groundFrontPixels'][1]=0
 else:i['contact'][0]-=10/16
 s['assembledGroundPath']='res://'+prefix+'-ground.json';l['objectCatalogPath']='res://'+prefix+'-objects.json'
 for suffix,d in [('',s),('-ground',l),('-objects',c)]: (o/(prefix+suffix+'.json')).write_text(json.dumps(d,indent=2)+'\n')
