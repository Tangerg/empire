from pathlib import Path
import json,struct
from PIL import Image
p=Path(__file__).resolve().parent;repo=p.parent/'empire-c01/godot'
load=lambda path:json.loads(path.read_text());write=lambda path,value:path.write_text(json.dumps(value,indent=2)+'\n');f=lambda x:struct.unpack('f',struct.pack('f',x))[0]
g=load(p/'northeast-frozen-v2-sections.json');surface=load(p/'northeast-frozen-v2-surface.json');s=f(g['scale']);t=list(map(f,g['translation']))
origin=[1260.,0.]
write(p/'northeast-baseline-registration.json',{'localOrigin':origin,'scale':s,'translation':t})
def world(v): return [f(f(f(v[i])*s+t[i])-origin[i]) for i in range(2)]
for row in surface['supports']:row['frontPixels']=world(row['frontPixels'])
for row in surface['members']:row['polygonPixels']=[world(v) for v in row['polygonPixels']]
write(p/'northeast-baseline-surface.json',surface)
ground=load(repo/'data/scenes/c01-07-selected-assembly-ground.json');catalog=load(repo/'data/scenes/c01-07-selected-assembly-objects.json');scene=load(repo/'data/scenes/c01-07-selected-assembly.json')
assert not any(v.get('selectionCell')==[83,11] and v.get('ownsMapStructureBody') for v in ground['objects'])
catalog['objects']['northeast-static-study']={'resourcePath':g['textureResourcePath'],'pixelSize':list(Image.open(repo/g['textureResourcePath'][6:]).size),'anchorPixels':[0,0],'contactShadowRadiiPixels':None,'surfaceGeometryPath':'res://northeast-baseline-surface.json','pointerPolygonPixels':[[v[i]-origin[i] for i in range(2)] for v in g['pointerSourcePolygon']]}
ground['objects'].append({'id':'northeast-static-study','assetId':'northeast-static-study','contact':[v/16 for v in origin],'scale':1,'selectionCell':[83,11],'ownsMapStructureBody':True,'pointer':'target'})
ground['objectCatalogPath']='res://northeast-baseline-objects.json';scene['assembledGroundPath']='res://northeast-baseline-ground.json'
for name,value in [('objects',catalog),('ground',ground),('scene',scene)]:write(p/f'northeast-baseline-{name}.json',value)
print('UNSELECTED_BASELINE_ASSEMBLED',len(surface['supports']),len(surface['members']))
