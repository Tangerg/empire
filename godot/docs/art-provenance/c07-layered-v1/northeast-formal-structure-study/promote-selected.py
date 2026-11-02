"""One-shot frozen migration: reviewed export -> sole selected runtime resources."""
from pathlib import Path
import json,hashlib,shutil
root=Path(__file__).resolve().parent
repo=root.parent/'empire-c01';scenes=repo/'godot/data/scenes'
read=lambda p:json.loads(p.read_text())
write=lambda p,v:p.write_text(json.dumps(v,indent=2)+'\n')
ground=read(scenes/'c01-07-selected-assembly-ground.json');catalog=read(scenes/'c01-07-selected-assembly-objects.json')
assert len(ground['objects'])==63,'Migration expected frozen63-instance source; never append twice.'
sourceGround=read(root/'northeast-leaf-ground.json');sourceCatalog=read(root/'northeast-leaf-objects.json')
records=[]
for old,part in [('northeast-static-study','stone'),('northeast-leaf-study','leaf')]:
 entry=next(v for v in sourceGround['objects'] if v['id']==old)
 asset=sourceCatalog['objects'][entry['assetId']]
 src=root/asset['surfaceGeometryPath'].removeprefix('res://')
 name=f'c01-07-northeast-gate-{part}-surface.json';dst=scenes/name
 shutil.copy2(src,dst)
 asset['surfaceGeometryPath']='res://data/scenes/'+name
 key=f'northeast-gate-{part}-surface-v2'
 assert key not in catalog['objects']
 catalog['objects'][key]=asset
 entry['id']=f'northeast-gate-{part}';entry['assetId']=key;ground['objects'].append(entry)
 records.append({'part':part,'sourceSha256':hashlib.sha256(src.read_bytes()).hexdigest(),'runtimeSha256':hashlib.sha256(dst.read_bytes()).hexdigest()})
assert len(ground['objects'])==65
write(scenes/'c01-07-selected-assembly-ground.json',ground)
write(scenes/'c01-07-selected-assembly-objects.json',catalog)
write(root/'selected-integration.json',{'scope':'Frozen geometry migration to sole selected owner; official scene unchanged. Not a complete C07 quality claim.','instancesBefore':63,'instancesAfter':65,'surfaces':records})
