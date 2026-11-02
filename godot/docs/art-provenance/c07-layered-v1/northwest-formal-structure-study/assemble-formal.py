from pathlib import Path
import json,copy,struct,math
from PIL import Image
root=Path(__file__).resolve().parent;repo=root.parent/'empire-c01/godot'
load=lambda p:json.loads(p.read_text());write=lambda p,v:p.write_text(json.dumps(v,indent=2)+'\n');f=lambda x:struct.unpack('f',struct.pack('f',x))[0]
def transform(v,scale,t):return [f(f(v[i])*scale+t[i]) for i in range(2)]
def merge(mount):
 result={'schema':1,'supports':[],'members':[]}
 for name in ['base','lookout','jamb-0','jamb-1',mount]:
  surface=load(root/('platform-base-surface.json' if name=='base' and mount=='mount-platform' else f'{name}-surface.json'))
  if name in ['base','lookout']:
   source=load(root/('northwest-lookout-base.json' if name=='base' else 'northwest-lookout-section.json'));sc=f(source['scale']);t=list(map(f,source['translation']))
  else:
   source=load(root/f'{name}.json');pts=[v for m in source['members'] for v in m['polygon']];sc=1.;t=[math.floor(min(p[i] for p in pts)) for i in range(2)]
  for s in surface['supports']:
   s['id']=name+'-'+s['id'];s['frontPixels']=transform(s['frontPixels'],sc,t);result['supports'].append(s)
  for m in surface['members']:
   m['id']=name+'-'+m['id'];m['supportId']=name+'-'+m['supportId'];m['polygonPixels']=[transform(p,sc,t) for p in m['polygonPixels']];result['members'].append(m)
 return result
for variant,mount in [('before','mount-before'),('stone','mount-stone-material'),('platform','mount-platform')]:
 surface=merge(mount);write(root/f'northwest-{variant}-surface.json',surface)
 ground=load(repo/'data/scenes/c01-07-selected-assembly-ground.json');catalog=load(repo/'data/scenes/c01-07-selected-assembly-objects.json');scene=load(repo/'data/scenes/c01-07-selected-assembly.json')
 src=load(root/'northwest-lookout-base.json');assert not any(o['selectionCell']==[11,11] for o in ground['objects'] if 'selectionCell'in o and o.get('ownsMapStructureBody'))
 catalog['objects']['northwest-gate-study']={'resourcePath':src['textureResourcePath'],'pixelSize':list(Image.open(repo/src['textureResourcePath'][6:]).size),'anchorPixels':[0,0],'contactShadowRadiiPixels':None,'surfaceGeometryPath':f'res://northwest-{variant}-surface.json','pointerPolygonPixels':src['pointerSourcePolygon']}
 ground['objects'].append({'id':'northwest-gate-study','assetId':'northwest-gate-study','contact':[0,0],'scale':1,'selectionCell':[11,11],'ownsMapStructureBody':True,'pointer':'target'})
 ground['objectCatalogPath']=f'res://northwest-{variant}-objects.json';scene['assembledGroundPath']=f'res://northwest-{variant}-ground.json'
 for name,data in [('objects',catalog),('ground',ground),('scene',scene)]:write(root/f'northwest-{variant}-{name}.json',data)
 print(variant,'supports',len(surface['supports']),'members',len(surface['members']))
a=merge('mount-before');b=merge('mount-stone-material')
# The variant is material-only: compare unprefixed support identities and all positions.
assert [s['frontPixels'] for s in a['supports']]==[s['frontPixels'] for s in b['supports']]
assert [m['polygonPixels'] for m in a['members']]==[m['polygonPixels'] for m in b['members']]
