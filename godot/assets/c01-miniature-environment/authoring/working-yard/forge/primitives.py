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
  m=o.modifiers.new('Hand softened edge','BEVEL');m.width=bevel;m.segments=1;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
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
  mesh('Slate roof backing',vs,[(3,2,1,0)] if side<0 else [(0,1,2,3)],slates[0])
  rows=5;cols=max(5,round(w/.24))
  for j in range(rows):
   t0=j/rows;t1=min(1,(j+1.14)/rows)
   for i in range(cols+1):
    offset=.5 if j%2 else 0
    x0=max(cx-w/2,cx-w/2+(i-offset)*w/cols)+.004
    x1=min(cx+w/2,cx-w/2+(i+1-offset)*w/cols)-.004
    if x1<=x0:continue
    ys=cy+side*t0*d/2;ye=cy+side*t1*d/2;zs=eave+rise*(1-t0)+.025+(rows-j)*.007;ze=eave+rise*(1-t1)+.025+(rows-j)*.007
    mesh('Overlapping slate',[(x0,ys,zs),(x1,ys,zs),(x1,ye,ze),(x0,ye,ze)],[(3,2,1,0)] if side<0 else [(0,1,2,3)],random.choice(slates))
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
