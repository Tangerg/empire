import json,pathlib,copy
from PIL import Image
p=pathlib.Path('empire-c01/godot/data/scenes');q=pathlib.Path('c07-static-preview');load=lambda p:json.loads(p.read_text());write=lambda p,d:p.write_text(json.dumps(d,indent=2)+'\n')
g=load(p/'c01-07-selected-assembly-ground.json');c=load(p/'c01-07-selected-assembly-objects.json');s=load(p/'c01-07-selected-assembly.json')
ng=copy.deepcopy(g);ng['objectCatalogPath']='res://selected-southwest-leaf-before-objects.json';ns=copy.deepcopy(s);ns['assembledGroundPath']='res://selected-southwest-leaf-before-ground.json'
for name,data in [('objects',c),('ground',ng),('scene',ns)]:write(q/f'selected-southwest-leaf-before-{name}.json',data)
d=load(q/'southwest-door-leaf-geometry.json');origin=[188,847];size=list(Image.open('empire-c01/godot/'+d['textureResourcePath'][6:]).size)
asset={'resourcePath':d['textureResourcePath'],'pixelSize':size,'anchorPixels':[0,0],'contactShadowRadiiPixels':None,'surfaceGeometryPath':'res://data/scenes/c01-07-southwest-leaf-surface.json'}
assert 'southwest-gate-leaf' not in [i['id'] for i in g['objects']]
c['objects']['southwest-gate-leaf-surface-v1']=asset;g['objects'].append({'id':'southwest-gate-leaf','assetId':'southwest-gate-leaf-surface-v1','contact':[v/16 for v in origin],'scale':1,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
write(p/'c01-07-selected-assembly-ground.json',g);write(p/'c01-07-selected-assembly-objects.json',c)
src=(q/'SouthwestGateLeafProbe.cs').read_text().replace('class SouthwestGateLeafProbe','class FormalSouthwestGateLeafProbe')
src=src.replace('var sc=AuthoredScene.Load(candidate?"res://data/scenes/c01-07-council-preview.json":"res://data/scenes/c01-07.json");','var negative=OS.GetEnvironment("C07_LEAF_NEGATIVE");var sc=AuthoredScene.Load(negative.Length>0?"res://selected-southwest-leaf-"+negative+"-scene.json":"res://data/scenes/c01-07-selected-assembly.json");')
src=src.replace('ground=new TexturedPropGround(new SectionedSpriteGround(r.Ground(rules,scene),geometry),leafGeometry)','ground=r.Ground(rules,scene)').replace('StartsWith("SWGate_")','StartsWith("southwest-gate-stone__")').replace('StartsWith("SouthwestGateWoodLeaf")','StartsWith("southwest-gate-leaf__")').replace('res://southwest-gate-leaf-final-','res://formal-southwest-gate-leaf-')
(q/'FormalSouthwestGateLeafProbe.cs').write_text(src)
(q/'FormalSouthwestGateLeafProbe.tscn').write_text('[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://FormalSouthwestGateLeafProbe.cs" id="1"]\n[node name="FormalSouthwestGateLeafProbe" type="Node"]\nscript = ExtResource("1")\n')
surf=load(p/'c01-07-southwest-leaf-surface.json')
for tag,y in [('background',860),('foreground',940)]:
 bad=copy.deepcopy(surf)
 for su in bad['supports']:su['frontPixels'][1]=y-origin[1]
 write(q/f'selected-southwest-leaf-{tag}-surface.json',bad)
 nc=copy.deepcopy(c);nc['objects']['southwest-gate-leaf-surface-v1']['surfaceGeometryPath']=f'res://selected-southwest-leaf-{tag}-surface.json';ng=copy.deepcopy(g);ng['objectCatalogPath']=f'res://selected-southwest-leaf-{tag}-objects.json';ns=copy.deepcopy(s);ns['assembledGroundPath']=f'res://selected-southwest-leaf-{tag}-ground.json'
 for name,data in [('objects',nc),('ground',ng),('scene',ns)]:write(q/f'selected-southwest-leaf-{tag}-{name}.json',data)
