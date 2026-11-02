from pathlib import Path
import json
r=Path('/workspace/scratch/84c9fe948755');o=r/'c07-static-preview';p=r/'empire-c01/godot/data/scenes';g=json.load(open(p/'c01-07-selected-assembly-ground.json'));c=json.load(open(p/'c01-07-selected-assembly-objects.json'));candidate=json.load(open(o/'central-white-planter-candidate-ground.json'));assets=json.load(open(o/'central-white-planter-candidate-objects.json'));key='central-east-white-planter-v2';assert key not in c['objects'];assert len(candidate['objects'])==len(g['objects'])+1 and candidate['objects'][:-1]==g['objects'];assert {k:v for k,v in assets['objects'].items() if k!=key}==c['objects'];c['objects'][key]=assets['objects'][key];g['objects'].append(candidate['objects'][-1]);
for f,d in [('c01-07-selected-assembly-ground.json',g),('c01-07-selected-assembly-objects.json',c)]: (p/f).write_text(json.dumps(d,indent=2)+'\n')
print('WHITE_PLANTER_SELECTED',len(g['objects']))
