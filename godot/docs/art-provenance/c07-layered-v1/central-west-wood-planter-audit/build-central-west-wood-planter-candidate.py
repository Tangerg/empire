"""Frozen provenance: shared asset, independent instance; selected assembly owns live placement."""
from pathlib import Path
import json
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';b=r/'empire-c01/godot/data/scenes';d=json.load(open(p/'central-west-wood-planter-design.json'));s=json.load(open(b/'c01-07-selected-assembly.json'));g=json.load(open(b/'c01-07-selected-assembly-ground.json'));g['objects']=[v for v in g['objects'] if v['id']!='central-west-wood-planter'];g['objects'].append({'id':'central-west-wood-planter','assetId':'central-front-wood-planter-v1','contact':[v/16 for v in d['worldAnchor']],'scale':d['scale'],'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'});s['assembledGroundPath']='res://central-west-wood-planter-candidate-ground.json'
for n,v in [('central-west-wood-planter-candidate.json',s),('central-west-wood-planter-candidate-ground.json',g)]: (p/n).write_text(json.dumps(v,indent=2)+'\n')
