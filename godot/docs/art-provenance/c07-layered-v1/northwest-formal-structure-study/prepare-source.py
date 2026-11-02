from pathlib import Path
import json,copy
root=Path(__file__).resolve().parent
load=lambda name:json.loads((root/name).read_text())
write=lambda name,data:(root/name).write_text(json.dumps(data,indent=2)+'\n')
for i,c in enumerate(load('northwest-native-surfaces.json')['components']):
 for m in c['members']:m['coverage']='native-alpha'
 write(f'jamb-{i}.json',c)
old=load('northwest-lookout-mount.json')
for m in old['members']:m['coverage']='native-alpha'
write('mount-before.json',old)
new=copy.deepcopy(old)
new['members'][0]['uv']=[[155,664],[246,704],[246,753],[155,714]]
new['members'][1]['uv']=[[970,650],[1025,627],[1025,725],[970,747]]
for m in new['members']:m['tint']='ffffff'
write('mount-stone-material.json',new)
write('material-change-scope.json',{'scope':'One bounded material correction; no geometry/support/UV changes outside mount; not visual acceptance','oldUvWas':'right tower banner cloth, not masonry','geometryAndSupportUnchanged': all(a['polygon']==b['polygon'] for a,b in zip(old['members'],new['members'])) and old['supportLine']==new['supportLine'],'sourceTextureUnchanged':True})
