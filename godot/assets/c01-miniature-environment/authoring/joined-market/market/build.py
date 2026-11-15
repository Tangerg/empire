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
    mesh('Overlapping slate',[(x0,ys,zs),(x1,ys,zs),(x1,ye,ze),(x0,ye,ze)],[(0,1,2,3)],random.choice(slates))
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
start('Upper market')
# Within x +/-2 and y +/-1 including overhangs.
cube('Stone market body',(0,.10,.83),(3.65,1.63,1.66),plaster,.025)
masonry(-1.82,1.82,-.74,0,.46,.20)
# Asymmetric tall counting-house at left, stepped lower hall at right.
cube('Raised counting house',(-1.03,.08,1.99),(1.36,1.59,.72),plaster,.025)
for x in (-1.73,-.35,1.80):cube('Structural corner posts',(x,-.75,1.25),(.10,.13,1.55),timber,.012)
beam('Primary cross beam',(-1.78,-.77,1.65),(1.80,-.77,1.65),.12)
roof(-1.03,.02,1.64,1.72,2.37,.63)
roof(.73,.02,2.13,1.72,1.78,.50)
# end gable triangles on x ends, because ridge runs in x
for xx,ee,rr,dd in [(-1.85,2.37,.63,1.72),(-.21,2.37,.63,1.72),(1.795,1.78,.5,1.72)]:
 mesh('Gable infill',[(xx,.08-dd/2,ee),(xx,.08+dd/2,ee),(xx,.08,ee+rr)],[(0,1,2)],plaster)
window(-1.02,-.76,1.96,.48,.52)
window(.25,-.76,1.20,.48,.66);window(1.13,-.76,1.20,.40,.60)
# Deep central entrance near left seam, deliberately offset.
cube('Door dark recess',(-1.04,-.765,.88),(.59,.10,.98),dark,.035)
for x in (-1.37,-.71):cube('Entry stone jamb',(x,-.83,.88),(.13,.24,1.02),stone[3],.025)
cube('Door head',(-1.04,-.83,1.43),(.80,.24,.16),stone[2],.02)
cube('Solid oak door',(-1.04,-.82,.85),(.49,.05,.88),timber,.01)
for z in (.63,1.07):cube('Iron door straps',(-1.04,-.86,z),(.46,.03,.035),metal)
# Integrated robust chimney, restrained varied stone.
for j in range(6):cube('Chimney stone',(.82,.58,2.18+j*.13),(.30,.35,.125),stone[j%5],.012)
cube('Chimney coping',(.82,.58,2.94),(.40,.43,.10),stone[1],.02)
# The visible end elevation is authored too, rather than left as a blank cube.
before=set(collection.objects)
window(0,0,1.06,.48,.70)
from mathutils import Matrix
rot=Matrix.Rotation(math.pi/2,4,'Z')
for o in set(collection.objects)-before:
 o.matrix_world=Matrix.Translation((1.825,.13,0))@rot@o.matrix_world
cube('Rear corner oak',(1.80,.82,1.05),(.11,.11,1.2),timber,.012)
for j in range(3):
 for i in range(5):cube('End footing stone',(1.80,-.61+i*.30,.08+j*.15),(.16,.28,.14),stone[(i+j)%5],.012)
upper=export('c01_market_upper_rework')
start('Lower gallery')
# Three small uneven bays, their arches form real negative space.
cube('Gallery plinth',(0,0,.10),(3.82,.96,.20),stone[1],.018)
cube('Gallery back wall',(0,.36,.67),(3.67,.16,.99),plaster,.02)
for cx,w in ((-1.20,.80),(-.05,.92),(1.18,.78)):arch(cx,-.32,.20,w,1.10,.22)
cube('Gallery spandrel beam',(0,-.32,1.39),(3.72,.25,.18),stone[2],.025)
# Shorter gallery roof, end-height shaped silhouette and open bays.
roof(0,0,3.84,.86,1.48,.18)
for x in (-1.79,1.79):cube('Gallery side return',(x,.06,.72),(.16,.62,1.04),stone[3],.018)
# Stall fixtures entirely inside the solid plot, not in front route.
for x in (-1.2,1.16):
 cube('Market oak counter',(x,.06,.60),(.63,.38,.11),timber,.018)
 for dx in (-.23,.23):cube('Counter leg',(x+dx,.06,.39),(.07,.29,.42),timber,.008)
 for i in range(3):cube('Clay produce tray',(x-.19+i*.19,.02,.70),(.16,.22,.09),cloth,.018)
lower=export('c01_market_lower_rework')
# Stage on the actual two distinct foundation levels. Render only; not exported.
for o in upper:o.location+=Vector((0,0,1.5))
for o in lower:o.location+=Vector((0,-1.5,.75))
collection=None
floor=mat('Studio earth',(.23,.26,.22));cube('Upper terrain demo',(0,.05,.69),(4.5,2.3,1.62),floor,.025);cube('Lower terrain demo',(0,-1.65,.30),(4.5,1.15,.9),floor,.02)
# broader walkable forecourt strips only for studio context, no scene geometry exports.
cube('Open foreground court',(0,-2.9,.25),(5.5,1.4,.10),stone[1],.02)
for i in range(9):cube('Court paver',(i*.56-2.24,-2.65,.32),(.54,.65,.05),stone[i%5],.015)
world=bpy.context.scene.world;world.color=(.19,.19,.19);world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.40,.48,.52,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
bpy.ops.object.light_add(type='AREA',location=(-3,-5,10));bpy.context.object.data.energy=850;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=7
bpy.ops.object.camera_add(location=(7,-10,8));cam=bpy.context.object;cam.rotation_euler=(Vector((0,-.5,2))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=8.4;bpy.context.scene.camera=cam
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=48;sc.cycles.use_denoising=False;sc.render.resolution_x=960;sc.render.resolution_y=720;sc.render.resolution_percentage=100;sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG';sc.render.filepath=str(ROOT/'market-street-assembly.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'market-street-source.blend'));bpy.ops.render.render(write_still=True)
# Export validation describes local bounds rather than the elevated studio placement.
report={}
for name,objs,offset in [('upper',upper,Vector((0,0,1.5))),('lower',lower,Vector((0,-1.5,.75)))]:
 vs=[o.matrix_world@Vector(v)-offset for o in objs for v in o.bound_box];tris=sum(len(o.data.polygons)*2 for o in objs if o.type=='MESH');report[name]={'mesh_objects':len(objs),'approx_triangles':tris,'local_min':[min(v[i] for v in vs) for i in range(3)],'local_max':[max(v[i] for v in vs) for i in range(3)]}
(ROOT/'geometry-report.json').write_text(json.dumps(report,indent=2))
