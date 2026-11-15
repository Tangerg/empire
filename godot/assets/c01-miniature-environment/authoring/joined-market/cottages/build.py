from pathlib import Path
import bpy, math, json, hashlib
from mathutils import Vector
R=Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
COLORS={'lime':(.69,.63,.49,1),'chalk':(.78,.72,.57,1),'stone':(.32,.35,.30,1),'stone2':(.43,.44,.36,1),'oak':(.22,.16,.10,1),'wood':(.34,.25,.15,1),'slate':(.22,.28,.29,1),'slate2':(.245,.30,.31,1),'shadow':(.075,.095,.085,1),'iron':(.12,.13,.12,1),'brick':(.38,.25,.17,1),'cloth':(.43,.25,.20,1)}
mats={}
for name,c in COLORS.items():
 m=bpy.data.materials.new(name);m.diffuse_color=c;m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=c;m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.92;mats[name]=m
roof=[];walls=[]
def box(name,center,size,mat='stone',bevel=.015,group=walls):
 bpy.ops.mesh.primitive_cube_add(size=1,location=center);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mats[mat])
 if bevel:
  b=o.modifiers.new('Stone edge','BEVEL');b.width=bevel;b.segments=1;bpy.ops.object.modifier_apply(modifier=b.name)
 group.append(o);return o
def mesh(name,vs,fs,mat,group=walls):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);o.data.materials.append(mats[mat]);group.append(o);return o
def beam(a,b,w=.06,mat='oak',group=walls):
 d=Vector(b)-Vector(a);o=box('Joined oak',(Vector(a)+Vector(b))/2,(w,w,d.length),mat,.007,group);o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return o
# Both dwellings share a party wall but step their floor and eaves; no false exterior stairs.
def house(y0,y1,x0,x1,floor,eave,peak,plaster):
 for y in [y0+.08,y1-.08]:
  box('End wall',((x0+x1)/2,y,(floor+eave)/2),(x1-x0,.16,eave-floor),plaster)
  mesh('Gable',[(x0,y,eave),(x1,y,eave),((x0+x1)/2,y,peak)],[(0,1,2)],plaster)
 box('Rear wall',(x1-.08,(y0+y1)/2,(floor+eave)/2),(.16,y1-y0,eave-floor),plaster)
 # Facing street windows and door have real wall reveals, not solid painted panes.
 door=y0+.55;win=y1-.65
 openings=[(door-.25,door+.25,floor,floor+1.02,'door'),(win-.30,win+.30,floor+.55,floor+1.03,'window')]
 prev=y0
 for a,b,lo,hi,kind in openings:
  if a<prev:continue
  box('Masonry pier',(x0+.09,(a+prev)/2,(floor+eave)/2),(.18,a-prev,eave-floor),plaster)
  if lo>floor:box('Window apron',(x0+.09,(a+b)/2,(lo+floor)/2),(.18,b-a,lo-floor),plaster)
  box('Opening lintel wall',(x0+.09,(a+b)/2,(eave+hi)/2),(.18,b-a,eave-hi),plaster)
  box('Recess interior',(x0+.20,(a+b)/2,(lo+hi)/2),(.025,b-a,hi-lo),'shadow',0)
  for yy in [a-.025,b+.025]:box('Revealed jamb',(x0+.015,yy,(lo+hi)/2),(.19,.065,hi-lo+.08),'wood',.008)
  for zz in [lo,hi]:box('Sill and header',(x0-.025,(a+b)/2,zz),(.25,b-a+.13,.06),'stone2' if zz==lo else 'wood',.008)
  if kind=='door':
   for i in range(6):box('Deep door planks',(x0+.15,a+(i+.5)*(b-a)/6,(lo+hi)/2),(.035,(b-a)/6-.008,hi-lo-.02),'wood',.004)
   for zz in [lo+.22,hi-.17]:box('Forged door strap',(x0+.12,(a+b)/2,zz),(.025,b-a-.04,.03),'iron',.003)
  else:
   box('Window mullion',(x0+.04,(a+b)/2,(lo+hi)/2),(.045,.04,hi-lo),'oak',.004)
   box('Window crossbar',(x0+.04,(a+b)/2,(lo+hi)/2),(.045,b-a,.04),'oak',.004)
   box('Single open shutter',(x0-.055,b+.18,(lo+hi)/2),(.075,.26,hi-lo+.08),'wood',.009)
  prev=b
 box('Last pier',(x0+.09,(prev+y1)/2,(floor+eave)/2),(.18,y1-prev,eave-floor),plaster)
 # Foundation courses wrap around the actual building; staggered joints, no random debris.
 for row in range(max(2,round(floor/.14))):
  z=.07+row*.14
  if z>floor:break
  for side in [x0+.035,x1-.035]:
   n=math.ceil((y1-y0)/.38)
   for i in range(n):
    ya=y0+i*(y1-y0)/n;yb=y0+(i+1)*(y1-y0)/n
    box('Foot masonry',(side,(ya+yb)/2,z),(.11,yb-ya-.015,.135),'stone2' if (i+row)%4==0 else 'stone',.012)
  for side in [y0+.03,y1-.03]:box('Continuous base return',((x0+x1)/2,side,z),(x1-x0,.1,.135),'stone',.01)
 for yy in [y0+.06,y1-.06]:box('Corner oak',(x0-.02,yy,(eave+floor)/2),(.085,.085,eave-floor),'oak')
 box('Upper rail',(x0-.018,(y0+y1)/2,eave-.09),(.09,y1-y0,.09),'oak')
 mid=(x0+x1)/2;xa=x0-.07;xb=x1+.07;ya=y0-.07;yb=y1+.07
 for edge in [xa,xb]:
  mesh('Continuous slate backing',[(mid,ya,peak+.005),(mid,yb,peak+.005),(edge,yb,eave+.005),(edge,ya,eave+.005)],[(0,1,2,3)],'slate',roof)
  # Slate courses are restrained continuous bands with a few visible joints.
  for j in range(8):
   t0=j/8;t1=(j+1)/8
   p0=mid+(edge-mid)*t0;p1=mid+(edge-mid)*t1
   h0=peak+(eave-peak)*t0+.02;h1=peak+(eave-peak)*t1+.02
   # Staggered actual panel seams give roof scale without noisy microgeometry.
   for k in range(-1,10):
    width=(yb-ya)/9;v0=max(ya,ya+(k+(j%2)*.5)*width);v1=min(yb,ya+(k+1+(j%2)*.5)*width)
    if v1-v0<.02:continue
    gap=.002
    mesh('Slate panel',[(p0,v0+gap,h0),(p0,v1-gap,h0),(p1,v1-gap,h1),(p1,v0+gap,h1)],[(0,1,2,3)],'slate2' if (k+j*3)%5==0 else 'slate',roof)
  beam((edge,ya,eave),(edge,yb,eave),.075,'oak',roof)
  for yy in [ya,yb]:beam((mid,yy,peak),(edge,yy,eave),.075,'oak',roof)
 beam((mid,ya,peak+.02),(mid,yb,peak+.02),.10,'slate2',roof)
# Long mass becomes a lower forward cottage plus narrower tall rear cottage, stepped side frontage.
house(-2.82,-.08,-1.27,1.17,.28,1.45,2.03,'lime')
house(-.12,2.80,-.93,1.23,.42,1.78,2.28,'chalk')
# Large chimney and party seam supply macro silhouette rather than a row of trim.
box('Chimney',(.70,.25,2.02),(.35,.37,.90),'brick',.025,roof)
box('Chimney cap',(.70,.25,2.48),(.44,.46,.09),'stone',.016,roof)
box('Chimney soot',(.70,.25,2.53),(.22,.24,.012),'shadow',0,roof)
# Lean-to entry shelter joins the recessed rear facade without encroaching the lane.
mesh('Shelter slate',[(-.97,.10,1.45),(-.97,1.05,1.45),(-1.42,1.05,1.25),(-1.42,.10,1.25)],[(0,1,2,3)],'slate',roof)
for yy in [.16,.99]:beam((-1.35,yy,.35),(-1.35,yy,1.26),.075,'oak');beam((-1.35,yy,1.10),(-.98,yy,1.41),.065,'oak')
# Deliberate side-facing service shutter on the visible forward gable.
box('Gable recess',(-.20,-2.91,.98),(.64,.025,.53),'shadow',.005)
for x in [-.55,.15]:box('Gable jamb',(x,-2.94,.98),(.065,.06,.61),'wood',.008)
for z in [.68,1.28]:box('Gable header',(-.20,-2.94,z),(.75,.06,.065),'wood',.008)
for i in range(6):box('Closed gable shutter',(-.48+i*.112,-2.95,.98),(.106,.045,.52),'wood',.005)
# Atlas stores the original palette so glTF has one opaque material and no external dependencies.
keys=list(COLORS);im=bpy.data.images.new('Original cottage palette',width=64,height=64,alpha=True)
pixels=[]
for y in range(64):
 for x in range(64):pixels.extend(COLORS[keys[min(len(keys)-1,(x//16)+(y//16)*4)]])
im.pixels=pixels;im.filepath_raw=str(R/'cottage_palette.png');im.file_format='PNG';im.save()
unified=bpy.data.materials.new('Handcrafted cottage matte');unified.use_nodes=True;nt=unified.node_tree;node=nt.nodes.new('ShaderNodeTexImage');node.image=im;node.interpolation='Closest';bs=nt.nodes['Principled BSDF'];nt.links.new(node.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.94
for objects,name in [(walls,'Walls'),(roof,'Roof')]:
 for o in objects:
  uv=o.data.uv_layers.get('UVMap') or o.data.uv_layers.new(name='UVMap')
  for p in o.data.polygons:
   index=keys.index(o.data.materials[p.material_index].name.split('.')[0]);co=((index%4+.5)/4,(index//4+.5)/4)
   for li in p.loop_indices:uv.data[li].uv=co
  o.data.materials.clear();o.data.materials.append(unified)
  for p in o.data.polygons:p.material_index=0
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[:]=[bpy.context.object];objects[0].name=name
allmesh=walls+roof
for obj in allmesh:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 obj.location.x=-obj.location.x;obj.scale.x=-1
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
coords=[o.matrix_world@v.co for o in allmesh for v in o.data.vertices]
mins=[min(v[i] for v in coords) for i in range(3)];maxs=[max(v[i] for v in coords) for i in range(3)]
assert mins[0]>=-1.5 and maxs[0]<=1.5 and mins[1]>=-3 and maxs[1]<=3,(mins,maxs)
bpy.ops.object.empty_add(type='PLAIN_AXES');root=bpy.context.object;root.name='C01 Joined Western Cottages'
for o in allmesh:o.parent=root
bpy.ops.object.select_all(action='SELECT');out=R/'c01_joined_cottages.glb';bpy.ops.export_scene.gltf(filepath=str(out),export_format='GLB',use_selection=True,export_yup=True)
bpy.ops.wm.save_as_mainfile(filepath=str(R/'c01_joined_cottages.blend'))
report={'source':'Original authored geometry and palette, no third-party assets','root_placement_godot':[10.5,1.5,8],'blender_bounds':[mins,maxs],'godot_bounds':[[mins[0],mins[2],-maxs[1]],[maxs[0],maxs[2],-mins[1]]],'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in allmesh),'mesh_names':[o.name for o in allmesh],'materials':1,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()};(R/'verification.json').write_text(json.dumps(report,indent=2))
# Offline render, not gameplay proof.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025));ground=bpy.context.object;ground.data.materials.append(mats['stone'])
bpy.ops.object.camera_add(location=(8,-10,8));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,1))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=8.7;bpy.context.scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-4,-5,9));light=bpy.context.object;light.data.energy=1100;light.data.shape='DISK';light.data.size=6
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64;scene.cycles.use_denoising=False;scene.world.color=(.3,.3,.3);scene.render.resolution_x=800;scene.render.resolution_y=700;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.filepath=str(R/'joined-cottages-review.png');bpy.ops.render.render(write_still=True)
