from pathlib import Path
import json,copy
root=Path(__file__).resolve().parent
read=lambda n:json.loads((root/n).read_text())
write=lambda n,v:(root/n).write_text(json.dumps(v,indent=2)+'\n')
for name,delta in [('late-depth',80),('early-depth',-80),('road-invasion',0),('flat-foot',0)]:
 surface=read('northeast-leaf-surface.json')
 if name=='flat-foot':
  for support in surface['supports']:support['frontPixels'][1]=41
 elif delta:
  for support in surface['supports']:support['frontPixels'][1]+=delta
 else:
  # Deliberately fill certified original road, with the same valid timber material.
  member=copy.deepcopy(surface['members'][0]);member['id']='wrong-road-fill';member['polygonPixels']=[[25,10],[29,10],[29,28],[25,28]];member['uvPixels']=[[740,840],[745,840],[745,860],[740,860]];surface['members'].append(member)
 write(f'northeast-leaf-{name}-surface.json',surface)
 objects=read('northeast-leaf-objects.json');objects['objects']['northeast-leaf-study']['surfaceGeometryPath']=f'res://northeast-leaf-{name}-surface.json'
 ground=read('northeast-leaf-ground.json');ground['objectCatalogPath']=f'res://northeast-leaf-{name}-objects.json'
 scene=read('northeast-leaf-scene.json');scene['assembledGroundPath']=f'res://northeast-leaf-{name}-ground.json'
 for kind,data in [('objects',objects),('ground',ground),('scene',scene)]:write(f'northeast-leaf-{name}-{kind}.json',data)
