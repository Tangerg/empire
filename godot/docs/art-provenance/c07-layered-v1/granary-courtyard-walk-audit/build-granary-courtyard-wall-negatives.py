from pathlib import Path
import json,copy
root=Path('/workspace/scratch/84c9fe948755');p=root/'empire-c01/godot/data/scenes';o=root/'c07-static-preview'
scene=json.load(open(p/'c01-07-selected-assembly.json'));ground=json.load(open(p/'c01-07-selected-assembly-ground.json'));catalog=json.load(open(p/'c01-07-selected-assembly-objects.json'))
for mode,part,worldFoot in [('flat-main-265','main',265),('background-pier','terminal-pier',0)]:
 s=copy.deepcopy(scene);g=copy.deepcopy(ground);c=copy.deepcopy(catalog);prefix='granary-courtyard-wall-negative-'+mode;id='granary-courtyard-wall-'+part;doc=json.load(open(p/f'c01-07-{id}-surface.json'));instance=next(i for i in g['objects'] if i['id']==id)
 for support in doc['supports']:support['frontPixels'][1]=worldFoot-instance['contact'][1]*16
 (o/(prefix+'-surface.json')).write_text(json.dumps(doc,indent=2)+'\n');c['objects'][id]['surfaceGeometryPath']='res://'+prefix+'-surface.json'
 s['assembledGroundPath']='res://'+prefix+'-ground.json';g['objectCatalogPath']='res://'+prefix+'-objects.json'
 for suffix,d in [('',s),('-ground',g),('-objects',c)]: (o/(prefix+suffix+'.json')).write_text(json.dumps(d,indent=2)+'\n')
