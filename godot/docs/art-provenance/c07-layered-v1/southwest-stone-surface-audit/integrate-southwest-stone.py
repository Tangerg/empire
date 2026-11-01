import json,pathlib,copy,struct
from PIL import Image
p=pathlib.Path('empire-c01/godot/data/scenes');q=pathlib.Path('c07-static-preview');load=lambda p:json.loads(p.read_text());write=lambda p,d:p.write_text(json.dumps(d,indent=2)+'\n');f=lambda x:struct.unpack('f',struct.pack('f',x))[0]
g=load(p/'c01-07-selected-assembly-ground.json');c=load(p/'c01-07-selected-assembly-objects.json');s=load(p/'c01-07-selected-assembly.json')
ng=copy.deepcopy(g);ng['objectCatalogPath']='res://selected-southwest-before-objects.json';ns=copy.deepcopy(s);ns['assembledGroundPath']='res://selected-southwest-before-ground.json'
for name,data in [('objects',c),('ground',ng),('scene',ns)]:write(q/f'selected-southwest-before-{name}.json',data)
d=load(q/'southwest-gate-v1-sections.json');scale=f(d['scale']);trans=list(map(f,d['translation']));size=list(Image.open('empire-c01/godot/'+d['textureResourcePath'][6:]).size)
def pointer(t,sc):return [[f(f(v[i]-t[i])/sc) for i in range(2)] for v in d['pointerSourcePolygon']]
asset={'resourcePath':d['textureResourcePath'],'pixelSize':size,'anchorPixels':[0,0],'contactShadowRadiiPixels':None,'surfaceGeometryPath':'res://data/scenes/c01-07-southwest-stone-surface.json','pointerPolygonPixels':pointer(trans,scale)}
assert 'southwest-gate-stone' not in [i['id'] for i in g['objects']]
c['objects']['southwest-gate-stone-surface-v1']=asset;g['objects'].append({'id':'southwest-gate-stone','assetId':'southwest-gate-stone-surface-v1','contact':[v/16 for v in trans],'scale':scale,'selectionCell':d['selectionCell'],'ownsMapStructureBody':True,'pointer':'target'})
write(p/'c01-07-selected-assembly-ground.json',g);write(p/'c01-07-selected-assembly-objects.json',c)
src=(q/'SouthwestGateStoneProbe.cs').read_text().replace('class SouthwestGateStoneProbe','class FormalSouthwestGateStoneProbe').replace('candidate?"res://data/scenes/c01-07-council-preview.json":"res://data/scenes/c01-07.json"','roofFit?"res://selected-southwest-roof-fit-scene.json":behind?"res://selected-southwest-background-scene.json":"res://data/scenes/c01-07-selected-assembly.json"').replace('ground=new SectionedSpriteGround(r.Ground(rules,scene),geometry)','ground=r.Ground(rules,scene)').replace('StartsWith("SWGate_")','StartsWith("southwest-gate-stone__")').replace('res://southwest-gate-stone-','res://formal-southwest-gate-stone-')
(q/'FormalSouthwestGateStoneProbe.cs').write_text(src);(q/'FormalSouthwestGateStoneProbe.tscn').write_text('[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://FormalSouthwestGateStoneProbe.cs" id="1"]\n[node name="FormalSouthwestGateStoneProbe" type="Node"]\nscript = ExtResource("1")\n')
surf=load(p/'c01-07-southwest-stone-surface.json')
for kind in ['background','roof-fit']:
 nc=copy.deepcopy(c);ng=copy.deepcopy(g);ns=copy.deepcopy(s);a=nc['objects']['southwest-gate-stone-surface-v1']
 if kind=='background':
  bad=copy.deepcopy(surf)
  for su in bad['supports']:su['frontPixels'][1]=f(f(884-trans[1])/scale)
  write(q/'selected-southwest-background-surface.json',bad);a['surfaceGeometryPath']='res://selected-southwest-background-surface.json'
 else:
  bs=f(.18464351005484458);bt=list(map(f,[83.16361974405848,723.6243144424132]));inst=next(i for i in ng['objects'] if i['id']=='southwest-gate-stone');inst['scale']=bs;inst['contact']=[v/16 for v in bt];a['pointerPolygonPixels']=pointer(bt,bs)
 ng['objectCatalogPath']=f'res://selected-southwest-{kind}-objects.json';ns['assembledGroundPath']=f'res://selected-southwest-{kind}-ground.json'
 for name,data in [('objects',nc),('ground',ng),('scene',ns)]:write(q/f'selected-southwest-{kind}-{name}.json',data)
