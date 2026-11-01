from pathlib import Path
import copy,json
p=Path('/workspace/scratch/84c9fe948755/empire-c01/godot/data/scenes');o=Path('/workspace/scratch/84c9fe948755/c07-static-preview')
scene=json.load(open(p/'c01-07-selected-assembly.json'));ground=json.load(open(p/'c01-07-selected-assembly-ground.json'));catalog=json.load(open(p/'c01-07-selected-assembly-objects.json'))
for mode in ('missing-cargo','late-middle-foot'):
 s=copy.deepcopy(scene);g=copy.deepcopy(ground);c=copy.deepcopy(catalog);prefix='southern-supply-stock-negative-'+mode
 if mode=='missing-cargo':g['objects']=[i for i in g['objects'] if not(i['id'].startswith('southern-supply-stock-') or i['id']=='southern-supply-right-bales')]
 else:
  doc=json.load(open(p/'c01-07-southern-supply-stock-middle-crate-surface.json'))
  for support in doc['supports']:support['frontPixels'][1]=100 # actual world830, behind-front regression, geometry/UV unchanged
  (o/(prefix+'-surface.json')).write_text(json.dumps(doc,indent=2)+'\n');c['objects']['southern-supply-stock-middle-crate']['surfaceGeometryPath']='res://'+prefix+'-surface.json'
 s['assembledGroundPath']='res://'+prefix+'-ground.json';g['objectCatalogPath']='res://'+prefix+'-objects.json'
 for suffix,data in [('',s),('-ground',g),('-objects',c)]: (o/(prefix+suffix+'.json')).write_text(json.dumps(data,indent=2)+'\n')
