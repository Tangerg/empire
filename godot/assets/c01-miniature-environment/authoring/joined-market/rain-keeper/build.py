import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
random.seed(18)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def mat(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.88;return m
stone=[mat('Limestone '+str(i),(.33+i*.014,.35+i*.013,.32+i*.012)) for i in range(5)]
plaster=mat('Warm lime plaster',(.66,.61,.46)); timber=mat('Aged oak',(.19,.16,.12)); dark=mat('Interior shadow',(.045,.055,.052)); glass=mat('Dark amber window',(.16,.20,.19)); metal=mat('Blackened iron',(.10,.12,.12)); cloth=mat('Faded oxblood awning',(.36,.15,.105)); slates=[mat('Slate '+str(i),(.20+i*.013,.25+i*.012,.29+i*.012)) for i in range(7)]
collection=None
def cube(name,loc,size,material,bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material)
 if bevel:
  m=o.modifiers.new('Hand softened edge','BEVEL');m.width=bevel;m.segments=2;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
 if collection: collection.objects.link(o);bpy.context.collection.objects.unlink(o)
 return o
def mesh(name,verts,faces,material):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);collection.objects.link(o);me.materials.append(material);return o
def beam(name,a,b,width,material=timber):
 p=(Vector(a)+Vector(b))/2;o=cube(name,p,(width,width,(Vector(b)-Vector(a)).length),material,.008);o.rotation_euler=(Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler();return o
def masonry(x1,x2,y,z0,h,depth=.18):
 rows=round(h/.18)
 for j in range(rows):
  z=z0+(j+.5)*h/rows; x=x1
  while x<x2-.02:
   w=min(random.uniform(.25,.42),x2-x);cube('Dressed foundation course',(x+w/2,y,z),(w-.012,depth,h/rows-.009),random.choice(stone),.012);x+=w

def roof(cx,cy,w,d,eave,rise):
 # Continuous structural roof, slate courses are visible geometry, not roof cubes.
 for side in (-1,1):
  vs=[(cx-w/2,cy,eave+rise),(cx+w/2,cy,eave+rise),(cx+w/2,cy+side*d/2,eave),(cx-w/2,cy+side*d/2,eave)]
  mesh('Slate roof backing',vs,[(0,1,2,3)],slates[0])
  rows=5;cols=max(5,round(w/.24))
  for j in range(rows):
   t0=j/rows;t1=min(1,(j+1.14)/rows)
   for i in range(cols+1):
    offset=.5 if j%2 else 0
    x0=max(cx-w/2,cx-w/2+(i-offset)*w/cols)+.004
    x1=min(cx+w/2,cx-w/2+(i+1-offset)*w/cols)-.004
    if x1<=x0:continue
    ys=cy+side*t0*d/2;ye=cy+side*t1*d/2;zs=eave+rise*(1-t0)+.018;ze=eave+rise*(1-t1)+.018
    v=[]
    for j2 in range(5):
     u=j2/4;xx=x0+(x1-x0)*u;curve=.028*(2*u-1)**2
     v.extend([(xx,ys,zs+curve),(xx,ye,ze+curve)])
    mesh('Overlapping clay pan tile',v,[(2*k,2*k+2,2*k+3,2*k+1) for k in range(4)],random.choice(slates))
  beam('Heavy roof verge',(cx-w/2,cy,eave+rise+.02),(cx-w/2,cy+side*d/2,eave+.02),.075)
  beam('Heavy roof verge',(cx+w/2,cy,eave+rise+.02),(cx+w/2,cy+side*d/2,eave+.02),.075)
  beam('Eave fascia',(cx-w/2,cy+side*d/2,eave),(cx+w/2,cy+side*d/2,eave),.08)
 beam('Raised ridge', (cx-w/2,cy,eave+rise+.03),(cx+w/2,cy,eave+rise+.03),.07,slates[1])
def gable(cx,cy,w,d,z,h):
 mesh('Lime gable',[(cx-w/2,cy-d/2,z),(cx+w/2,cy-d/2,z),(cx,cy-d/2,z+h),(cx-w/2,cy+d/2,z),(cx+w/2,cy+d/2,z),(cx,cy+d/2,z+h)],[(0,1,2),(3,5,4)],plaster)
def window(x,y,z,w=.42,h=.64):
 cube('Recessed casement',(x,y+.015,z),(w,.09,h),dark,.018)
 cube('Window glass',(x,y-.043,z),(w-.08,.025,h-.08),glass)
 for dx in (-w/2,w/2):cube('Window jamb',(x+dx,y-.07,z),(.06,.13,h+.1),timber,.01)
 for dz in (-h/2,h/2):cube('Window lintel',(x,y-.075,z+dz),(w+.10,.14,.055),timber,.01)
 cube('Casement mullion',(x,y-.09,z),(.034,.035,h),timber)
 cube('Stone sill',(x,y-.08,z-h/2-.06),(w+.18,.16,.075),stone[3],.015)
 cube('Wood shutter',(x-w/2-.15,y-.02,z),(.20,.065,h+.04),timber,.015)
def arch(cx,y,z0,width,height,depth):
 r=width/2;spring=z0+height-r;th=.18
 # true open masonry arch. no solid wall plane behind.
 for s in (-1,1):
  masonry(cx+s*r-th/2,cx+s*r+th/2,y,z0,height-r,depth)
 n=9
 for i in range(n):
  a=i*math.pi/n+.012;b=(i+1)*math.pi/n-.012
  v=[]
  for yy in (y-depth/2,y+depth/2):
   for rr,t in ((r,a),(r,b),(r+th,b),(r+th,a)):v.append((cx+rr*math.cos(t),yy,spring+rr*math.sin(t)))
  mesh('Arch voussoir',v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],stone[i%5])
def start(name):
 global collection
 collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
def export(name):
 # Preserve a roof-only mesh for Godot's authored occlusion; one vertex-color material.
 palette=bpy.data.materials.get('Matte vertex color')
 if palette is None:
  palette=mat('Matte vertex color',(.5,.5,.5));nodes=palette.node_tree.nodes;vc=nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';palette.node_tree.links.new(vc.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
 groups={'Roof':[],'Architecture':[]}
 for o in list(collection.objects):
  attr=o.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
  color=o.data.materials[0].diffuse_color
  for i in range(len(attr.data)):attr.data[i].color=color
  o.data.materials.clear();o.data.materials.append(palette)
  roof=any(k in o.name for k in ['roof','slate','verge','fascia','ridge'])
  groups['Roof' if roof else 'Architecture'].append(o)
 for label,objs in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in objs:o.select_set(True)
  bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();bpy.context.object.name=label
 bpy.ops.object.select_all(action='DESELECT')
 for o in collection.objects:o.select_set(True)
 bpy.ops.export_scene.gltf(filepath=str(ROOT/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True)
 result=list(collection.objects)
 for o in result:o.name=name+'_'+o.name
 return result
start('Rain street keeper')
slates=[mat('Burnt terracotta '+str(i),(.40+i*.016,.19+i*.011,.12+i*.008)) for i in range(7)]
# Deep rear room, low open street-facing rain shelter; all eaves remain inside the plot.
cube('Stone foot',(0,.18,.13),(2.55,2.44,.26),stone[1],.025)
cube('Keeper rear room',(-.20,.51,.96),(2.14,1.63,1.66),plaster,.035)
masonry(-1.26,.87,-.35,0,.42,.20)
for x in (-1.25,.85):cube('Corner post',(x,-.34,1.02),(.12,.15,1.50),timber,.012)
beam('Front lintel',(-1.26,-.34,1.68),(.86,-.34,1.68),.13)
roof(-.20,.49,2.38,1.88,1.78,.58)
# End gables, built into the ridge height rather than left open.
for x in (-1.39,.99):mesh('Gable plaster',[(x,-.45,1.78),(x,1.43,1.78),(x,.49,2.36)],[(0,1,2)],plaster)
# Recessed solid door beneath shelter.
cube('Door recess',(-.58,-.35,.87),(.60,.12,1.10),dark,.022)
for x in (-.92,-.24):cube('Door stone jamb',(x,-.42,.87),(.13,.22,1.13),stone[3],.018)
cube('Door head',(-.58,-.42,1.47),(.81,.22,.14),stone[3],.022)
cube('Door oak',(-.58,-.42,.85),(.50,.045,1.00),timber,.01)
window(.41,-.36,1.02,.42,.66)
# Right end window is visible from game camera.
before=set(collection.objects)
window(0,0,1.06,.46,.64)
from mathutils import Matrix
for o in set(collection.objects)-before:o.matrix_world=Matrix.Translation((.88,.62,0))@Matrix.Rotation(math.pi/2,4,'Z')@o.matrix_world
# A low lean-to roof on open braced supports creates negative space in front.
roof(-.18,-.92,2.32,.92,1.31,.20)
for x in (-1.28,.92):
 cube('Shelter stone pedestal',(x,-1.30,.30),(.24,.24,.35),stone[2],.02)
 cube('Shelter post',(x,-1.30,.90),(.13,.13,1.02),timber,.015)
 beam('Shelter knee brace',(x,-1.30,1.03),(x+(.30 if x<0 else -.30),-1.30,1.31),.075)
beam('Shelter front beam',(-1.32,-1.30,1.32),(.96,-1.30,1.32),.13)
# Rainwater trough in the right-hand return, physically hollow, not a solid cube.
for x in (1.05,1.38):cube('Basin long wall',(x,-.47,.32),(.10,1.25,.34),stone[2],.02)
for y in (-1.05,.10):cube('Basin end',(1.215,y,.32),(.42,.10,.34),stone[2],.02)
cube('Basin bed',(1.215,-.47,.17),(.35,1.16,.08),stone[0],.01)
cube('Still water',(1.215,-.47,.27),(.22,1.06,.015),glass)
# Off-centre chimney marks the keeper's small stove.
for j in range(5):cube('Chimney course',(-.70,.90,2.06+j*.13),(.29,.31,.125),stone[j%5],.012)
cube('Chimney coping',(-.70,.90,2.69),(.39,.41,.10),stone[3],.015)
objs=export('c01_w1_rain_keeper')
collection=None
floor=mat('Studio earth',(.23,.26,.22));cube('Studio plane',(0,0,-.11),(4.4,4.4,.20),floor,.02)
world=bpy.context.scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.40,.48,.52,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
bpy.ops.object.light_add(type='AREA',location=(-3,-5,8));bpy.context.object.data.energy=850;bpy.context.object.data.size=6
bpy.ops.object.camera_add(location=(7,-10,8));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,1.0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=5.7;bpy.context.scene.camera=cam
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=40;sc.cycles.use_denoising=False;sc.render.resolution_x=720;sc.render.resolution_y=600;sc.render.resolution_percentage=100;sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG';sc.render.filepath=str(ROOT/'w1-rain-keeper-study.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'w1-rain-keeper.blend'));bpy.ops.render.render(write_still=True)
