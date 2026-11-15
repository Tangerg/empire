from pathlib import Path
import bpy, math, json, hashlib
from mathutils import Vector
R=Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
PAL={'lime':(.69,.63,.49,1),'chalk':(.78,.72,.57,1),'stone':(.32,.35,.30,1),'stone2':(.43,.44,.36,1),'oak':(.22,.16,.10,1),'wood':(.34,.25,.15,1),'slate':(.22,.28,.29,1),'slate2':(.245,.30,.31,1),'shadow':(.075,.095,.085,1),'iron':(.12,.13,.12,1),'brick':(.38,.25,.17,1),'cloth':(.43,.25,.20,1)}
MAT={}
for k,c in PAL.items():
 m=bpy.data.materials.new(k);m.diffuse_color=c;m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=c;m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.94;MAT[k]=m
body=[];roof=[]
def box(name,p,size,mat='stone',bevel=.012,group=body):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(MAT[mat]);group.append(o)
 if bevel:
  b=o.modifiers.new('Worn arris','BEVEL');b.width=bevel;b.segments=1;bpy.ops.object.modifier_apply(modifier=b.name)
 return o
def mesh(name,v,f,mat='stone',group=body):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);o.data.materials.append(MAT[mat]);group.append(o);return o
def beam(name,a,b,w=.07,mat='oak',group=body):
 v=Vector(b)-Vector(a);o=box(name,(Vector(a)+Vector(b))/2,(w,w,v.length),mat,.008,group);o.rotation_euler=v.to_track_quat('Z','Y').to_euler();return o
def plinth(x0,x1,y0,y1,top):
 for row in range(round(top/.13)):
  z=.065+row*.13
  for y in [y0+.045,y1-.045]:
   n=math.ceil((x1-x0)/.42)
   for i in range(n):box('Front masonry', (x0+(i+.5)*(x1-x0)/n,y,z),((x1-x0)/n-.012,.13,.125),'stone2' if (i+row)%4==0 else 'stone')
  for x in [x0+.045,x1-.045]:
   n=math.ceil((y1-y0)/.42)
   for i in range(n):box('End masonry',(x,y0+(i+.5)*(y1-y0)/n,z),(.13,(y1-y0)/n-.012,.125),'stone2' if (i+row)%4==0 else 'stone')
def front(x0,x1,y,floor,eave,holes,mat):
 previous=x0
 for a,b,lo,hi,kind in holes:
  if a>previous:box('Solid frontage pier',((a+previous)/2,y+.08,(floor+eave)/2),(a-previous,.16,eave-floor),mat)
  if lo>floor:box('Apron',((a+b)/2,y+.08,(lo+floor)/2),(b-a,.16,lo-floor),mat)
  box('Deep lintel',((a+b)/2,y+.08,(hi+eave)/2),(b-a,.16,eave-hi),mat)
  box('Recess',((a+b)/2,y+.22,(lo+hi)/2),(b-a,.02,hi-lo),'shadow',0)
  for x in [a-.03,b+.03]:box('Door window jamb',(x,y-.01,(lo+hi)/2),(.07,.20,hi-lo+.10),'wood',.01)
  for z in [lo,hi]:box('Opening sill header',((a+b)/2,y-.025,z),(b-a+.13,.23,.07),'stone2' if z==lo else 'wood',.008)
  if kind=='window':
   box('Mullion',((a+b)/2,y+.065,(lo+hi)/2),(.045,.06,hi-lo),'oak',.005)
   box('Crossbar',((a+b)/2,y+.065,(lo+hi)/2),(b-a,.06,.035),'oak',.005)
   box('Folded shutter',(a-.13,y-.025,(lo+hi)/2),(.16,.06,hi-lo),'wood',.01)
  else:
   n=7
   for i in range(n):box('Closed slatted gate',(a+(i+.5)*(b-a)/n,y+.17,(lo+hi)/2),((b-a)/n-.009,.04,hi-lo-.02),'wood',.005)
   for z in [lo+.23,hi-.18]:box('Forged strap',((a+b)/2,y+.14,z),(b-a-.05,.025,.035),'iron',.003)
  previous=b
 if previous<x1:box('Last frontage pier',((previous+x1)/2,y+.08,(floor+eave)/2),(x1-previous,.16,eave-floor),mat)
def shell(x0,x1,y0,y1,floor,eave,holes,mat):
 plinth(x0,x1,y0,y1,floor);front(x0,x1,y0,floor,eave,holes,mat)
 box('Rear wall',((x0+x1)/2,y1-.07,(floor+eave)/2),(x1-x0,.14,eave-floor),mat)
 for x in [x0+.07,x1-.07]:box('End wall',(x,(y0+y1)/2,(floor+eave)/2),(.14,y1-y0,eave-floor),mat)
 for x in [x0+.045,x1-.045]:box('Corner structural oak',(x,y0-.01,(floor+eave)/2),(.09,.09,eave-floor),'oak')
 box('Front upper beam',((x0+x1)/2,y0-.02,eave-.065),(x1-x0,.09,.09),'oak')
def gable_roof(x0,x1,y0,y1,eave,peak,axis):
 # Rotate roof basis only; the narrow high gable contrasts with a broad service roof.
 if axis=='y':mid=(x0+x1)/2;v=[(x0,y0,eave),(x1,y0,eave),(mid,y0,peak),(x0,y1,eave),(x1,y1,eave),(mid,y1,peak)];ends=[(0,1,2),(3,5,4)];surfs=[(v[2],v[5],v[3],v[0]),(v[2],v[5],v[4],v[1])]
 else:mid=(y0+y1)/2;v=[(x0,y0,eave),(x0,y1,eave),(x0,mid,peak),(x1,y0,eave),(x1,y1,eave),(x1,mid,peak)];ends=[(0,2,1),(3,4,5)];surfs=[(v[2],v[5],v[3],v[0]),(v[2],v[5],v[4],v[1])]
 mesh('Gable masonry',v,ends,'lime')
 for surf in surfs:
  A,B,C,D=map(Vector,surf);mesh('Slate backing',surf,[(0,1,2,3)],'slate',roof)
  for row in range(7):
   t0=row/7;t1=(row+1)/7
   n=max(4,round((B-A).length/.34))
   for col in range(-1,n):
    u0=max(0,(col+(row%2)*.5)/n);u1=min(1,(col+1+(row%2)*.5)/n)
    if u1-u0<.002:continue
    u0+=.001;u1-=.001
    def p(u,t):return A.lerp(B,u).lerp(D.lerp(C,u),t)+Vector((0,0,.012))
    mesh('Slate shingles',[p(u0,t0),p(u1,t0),p(u1,t1),p(u0,t1)],[(0,1,2,3)],'slate2' if (row+col*3)%6==0 else 'slate',roof)
  for a,b in [(A,D),(D,C),(C,B)]:beam('Eave barge trim',a,b,.065,'oak',roof)
 beam('Ridge cap',v[2],v[5],.09,'slate2',roof)
# A taller gabled service dwelling, recessed gate bay, and lower broad service wing.
shell(-2.82,-.80,-.84,.82,.26,1.64,[(-2.40,-1.73,.83,1.40,'window'),(-1.48,-1.03,.26,1.35,'door')],'chalk')
gable_roof(-2.90,-.72,-.92,.90,1.64,2.23,'y')
shell(.72,2.82,-.73,.80,.39,1.38,[(1.05,1.63,.85,1.18,'window'),(2.03,2.56,.39,1.22,'door')],'lime')
gable_roof(.64,2.90,-.81,.88,1.38,1.88,'x')
# Gate is visibly closed: the recess reads as an entry, never as a tactical through-route.
shell(-.78,.72,-.16,.78,.26,1.24,[(-.46,.44,.26,1.12,'door')],'stone2')
mesh('Connecting lean roof',[(-.83,-.25,1.28),(.78,-.25,1.28),(.78,.86,1.52),(-.83,.86,1.52)],[(0,1,2,3)],'slate',roof)
for x in [-.69,.63]:beam('Gate courtyard upright',(x,-.64,.10),(x,-.64,1.03),.085,'wood');beam('Gate angled brace',(x,-.64,.85),(x,-.17,1.20),.07,'oak')
beam('Gate courtyard lintel',(-.72,-.64,1.03),(.66,-.64,1.03),.12,'oak')
for i in range(2):box('Internal gate stair',(-.02,-.56+i*.18,.065*(i+1)),(1.13,.24,.13*(i+1)),'stone2',.012)
# Small porch bench and low footing sit wholly inside the blocked parcel.
box('Service bench',(1.02,-.87,.48),(.42,.16,.065),'wood')
for x in [.87,1.16]:box('Bench foot',(x,-.86,.30),(.07,.1,.34),'oak')
box('Chimney',(-2.40,.43,2.01),(.30,.34,.76),'stone',.02,roof);box('Chimney coping',(-2.40,.43,2.40),(.39,.43,.08),'stone2',.015,roof)
box('Chimney soot',(-2.40,.43,2.445),(.20,.24,.012),'shadow',0,roof)
# Palette has exactly one UV owner; default cube UVs must be replaced, never shadowed.
keys=list(PAL);im=bpy.data.images.new('Original N2 palette',width=64,height=64,alpha=True);im.pixels=[c for y in range(64) for x in range(64) for c in PAL[keys[min(len(keys)-1,x//16+y//16*4)]]];im.filepath_raw=str(R/'palette.png');im.file_format='PNG';im.save()
unified=bpy.data.materials.new('N2 matte palette');unified.use_nodes=True;nt=unified.node_tree;tex=nt.nodes.new('ShaderNodeTexImage');tex.image=im;tex.interpolation='Closest';nt.links.new(tex.outputs['Color'],nt.nodes['Principled BSDF'].inputs['Base Color']);nt.nodes['Principled BSDF'].inputs['Roughness'].default_value=.94
for group,name in [(body,'Walls'),(roof,'Roof')]:
 for o in group:
  uv=o.data.uv_layers.get('UVMap') or o.data.uv_layers.new(name='UVMap')
  for poly in o.data.polygons:
   k=keys.index(o.data.materials[poly.material_index].name);co=((k%4+.5)/4,(k//4+.5)/4)
   for li in poly.loop_indices:uv.data[li].uv=co
  o.data.materials.clear();o.data.materials.append(unified)
  for p in o.data.polygons:p.material_index=0
 bpy.ops.object.select_all(action='DESELECT')
 for o in group:o.select_set(True)
 bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();group[:]=[bpy.context.object];group[0].name=name
meshes=body+roof
for o in meshes:
 bpy.context.view_layer.objects.active=o;bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
 assert len(o.data.uv_layers)==1
 for p in o.data.polygons:assert len({tuple(o.data.uv_layers.active.data[i].uv) for i in p.loop_indices})==1
pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices];lo=[min(p[i] for p in pts) for i in range(3)];hi=[max(p[i] for p in pts) for i in range(3)];assert lo[0]>=-3 and hi[0]<=3 and lo[1]>=-1 and hi[1]<=1,(lo,hi)
bpy.ops.object.empty_add(type='PLAIN_AXES');root=bpy.context.object;root.name='C01 Northern Service Compound'
for o in meshes:o.parent=root
bpy.ops.object.select_all(action='SELECT');out=R/'c01_n2_service_compound.glb';bpy.ops.export_scene.gltf(filepath=str(out),export_format='GLB',use_selection=True,export_yup=True);bpy.ops.wm.save_as_mainfile(filepath=str(R/'c01_n2_service_compound.blend'))
report={'placement_godot':[19,1.5,6],'bounds_godot':[[lo[0],lo[2],-hi[1]],[hi[0],hi[2],-lo[1]]],'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'mesh_nodes':[o.name for o in meshes],'material_count':1,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'source_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'uv_verification':'one active centered swatch per polygon','status':'Offline model, not gameplay acceptance'};(R/'verification.json').write_text(json.dumps(report,indent=2))
# Default game camera sees +X and +Z; +Z maps to Blender -Y.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));ground=bpy.context.object;mat=bpy.data.materials.new('Backdrop');mat.diffuse_color=(.35,.37,.31,1);ground.data.materials.append(mat)
bpy.ops.object.camera_add(location=(8,-10,8));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=8.6;bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-3,-5,9));light=bpy.context.object;light.data.energy=1200;light.data.size=6
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=False;s.world.color=(.3,.3,.3);s.render.resolution_x=800;s.render.resolution_y=600;s.render.resolution_percentage=100;s.view_settings.view_transform='AgX';s.render.filepath=str(R/'n2-compound-review.png');bpy.ops.render.render(write_still=True)
s.render.resolution_x=320;s.render.resolution_y=240;s.cycles.samples=32;s.render.filepath=str(R/'n2-compound-small.png');bpy.ops.render.render(write_still=True)
