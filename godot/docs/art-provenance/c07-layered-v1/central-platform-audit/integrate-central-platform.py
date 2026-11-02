from pathlib import Path
import json,shutil
r=Path('/workspace/scratch/84c9fe948755');p=r/'empire-c01/godot/data/scenes';o=r/'c07-static-preview';g=json.load(open(p/'c01-07-selected-assembly-ground.json'));c=json.load(open(p/'c01-07-selected-assembly-objects.json'));assert 'central-platform' not in c['objects']
shutil.copy2(o/'central-platform-surface.json',p/'c01-07-central-platform-surface.json')
c['objects']['central-platform']={'resourcePath':'res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png','pixelSize':[1024,1536],'anchorPixels':[0,0],'surfaceGeometryPath':'res://data/scenes/c01-07-central-platform-surface.json','contactShadowRadiiPixels':None}
index=next(j for j,v in enumerate(g['objects']) if v['id']=='central-landmark-core');g['objects'].insert(index,{'id':'central-platform','assetId':'central-platform','contact':[714/16,448/16],'scale':1.0,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
# Explicit assembly order puts the supported core after its carrying plane on shared-Z samples.
for f,d in [('c01-07-selected-assembly-ground.json',g),('c01-07-selected-assembly-objects.json',c)]: (p/f).write_text(json.dumps(d,indent=2)+'\n')
print('CENTRAL_PLATFORM_SELECTED',len(g['objects']))
