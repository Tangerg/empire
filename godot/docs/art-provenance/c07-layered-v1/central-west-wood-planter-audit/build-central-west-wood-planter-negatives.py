from pathlib import Path
import json,copy
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');s=json.load(open(p/'central-west-wood-planter-candidate.json'));g=json.load(open(p/'central-west-wood-planter-candidate-ground.json'));next(v for v in g['objects'] if v['id']=='central-west-wood-planter')['contact'][0]+=20/16;s['assembledGroundPath']='res://central-west-wood-planter-negative-registration-ground.json'
for n,d in [('central-west-wood-planter-negative-registration.json',s),('central-west-wood-planter-negative-registration-ground.json',g)]: (p/n).write_text(json.dumps(d,indent=2)+'\n')
d=json.load(open(p/'central-west-wood-planter-design.json'));d['worldAnchor'][1]-=70;d['hiddenBack'][1]-=70;(p/'central-west-wood-planter-negative-p-foot-design.json').write_text(json.dumps(d,indent=2)+'\n')
