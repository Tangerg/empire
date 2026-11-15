from pathlib import Path
exec((Path(__file__).resolve().parent/'primitives.py').read_text(),globals())
from mathutils import Matrix
start('C01 S1 Forge')
# A public-facing apron is visual only, entirely inside the already blocked parcel.
cube('Continuous stone foot',(0,0,.09),(5.72,2.70,.18),stone[1],.025)
brick=[mat('Kiln brick '+str(i),(.31+i*.018,.16+i*.012,.105+i*.009)) for i in range(5)]
slates=[mat('Muted smoked roof '+str(i),(.19+i*.011,.23+i*.010,.24+i*.010)) for i in range(7)]
# Left enclosed hot shop and a low right cart bay share a back wall, not one shed box.
cube('Hot shop rear',(-1.36,1.08,.96),(2.72,.22,1.75),plaster,.02)
cube('Hot shop west return',(-2.62,.05,.97),(.22,2.12,1.76),plaster,.02)
# Front reveals surround a deliberately wide workshop door.
for cx,w,h,z in [(-2.32,.62,1.73,.98),(-.31,.63,1.73,.98),(-1.32,1.42,.28,1.70)]:cube('Thick front wall',(cx,-.98,z),(w,.24,h),plaster,.018)
for x in (-2.00,-.65):cube('Oak workshop jamb',(x,-1.115,.88),(.13,.17,1.49),timber,.012)
cube('Oak door head',(-1.32,-1.115,1.61),(1.60,.19,.16),timber,.012)
# Genuine dark interior, roof and side returns make depth instead of a painted rectangle.
cube('Interior back wall',(-1.35,.85,.92),(2.35,.12,1.55),dark)
masonry(-2.68,-2.03,-1.12,.10,.48,.18);masonry(-.61,-.02,-1.12,.10,.48,.18)
for x in (-2.60,-.06):cube('Structural hot-shop corner',(x,-1.12,1.08),(.12,.15,1.54),timber,.012)
# Smoke hood and hearth, visible through the front opening.
for x in (-1.90,-1.08):cube('Hearth cheek',(x,.30,.56),(.22,.62,.93),random.choice(brick),.018)
cube('Hearth lintel',(-1.49,.30,1.05),(1.04,.69,.20),brick[2],.02)
cube('Hearth coal bed',(-1.49,.13,.34),(.57,.40,.08),metal)
cube('Hearth hood',(-1.49,.48,1.41),(1.0,.65,.52),brick[1],.02)
for j in range(9):cube('Brick chimney course',(-1.49,.54,1.70+j*.12),(.62,.64,.116),brick[j%5],.015)
cube('Chimney crown',(-1.49,.54,2.79),(.75,.77,.12),stone[2],.02)
# Main roof and gable; left wing rises above the lower east loading roof.
roof(-1.34,.05,2.93,2.57,1.84,.57)
for x in (-2.805,.125):mesh('Hot shop gable',[(x,-1.235,1.84),(x,1.335,1.84),(x,.05,2.41)],[(0,1,2)],plaster)
# The loading hall has a wide cart opening on east +X, and an open southern bay.
cube('Loading rear wall',(1.38,1.08,.74),(2.65,.22,1.31),plaster,.02)
for x,y in ((.33,-1.06),(2.72,-1.06),(2.72,.99)):
 cube('Loading pier stone',(x,y,.31),(.27,.27,.44),stone[2],.025)
 cube('Loading oak upright',(x,y,.94),(.17,.17,1.11),timber,.012)
beam('Loading lintel',(.25,-1.06,1.49),(2.82,-1.06,1.49),.18)
beam('Cart opening head',(2.72,-1.10,1.49),(2.72,1.08,1.49),.19)
for y in (-1.01,.96):beam('Cart corner knee',(2.72,y,1.12),(2.72,y+(.29 if y<0 else -.29),1.49),.10)
beam('Southern knee',(.33,-1.06,1.15),(.70,-1.06,1.49),.10)
slates=[mat('Weathered oxide clay '+str(i),(.30+i*.012,.155+i*.008,.103+i*.006)) for i in range(7)]
roof(1.50,.05,2.79,2.60,1.55,.36)
# Recessed work, no objects spill onto the goods route.
cube('Work table top',(.83,.45,.73),(1.28,.59,.13),timber,.016)
for x in (.37,1.29):cube('Work table trestle',(x,.45,.43),(.16,.46,.52),timber,.012)
cube('Anvil stump',(1.87,-.30,.37),(.44,.45,.57),timber,.025)
cube('Iron anvil foot',(1.87,-.30,.70),(.43,.29,.09),metal,.012)
cube('Iron anvil waist',(1.87,-.30,.82),(.22,.23,.22),metal,.015)
cube('Iron anvil face',(1.87,-.30,.98),(.62,.28,.12),metal,.015)
mesh('Anvil horn',[(2.18,-.43,.92),(2.18,-.17,.92),(2.18,-.17,1.04),(2.18,-.43,1.04),(2.43,-.30,.99)],[(0,3,2,1),(0,1,4),(1,2,4),(2,3,4),(3,0,4)],metal)
# Modest low apron and end drainage trough; no step painted into legal ground.
for i in range(10):cube('Apron stone',(i*.55-2.475,-1.28,.15),(.53,.32,.12),stone[i%5],.012)
# Export a single embedded opaque palette, two stable named groups, UV0 only.
objects=list(collection.objects);colors=[]
for o in objects:
 c=tuple(round(float(v),5) for v in o.data.materials[0].diffuse_color)
 if c not in colors:colors.append(c)
def srgb(c):return 12.92*c if c<=.0031308 else 1.055*c**(1/2.4)-.055
image=bpy.data.images.new('Forge original palette',width=len(colors)*8,height=8,alpha=True)
image.colorspace_settings.name='sRGB'
image.pixels=[v for y in range(8) for col in colors for x in range(8) for v in (*[srgb(c) for c in col[:3]],1)]
image.filepath_raw=str(ROOT/'forge-palette.png');image.file_format='PNG';image.save();image.pack()
material=mat('C01 original forge matte palette',(1,1,1));p=material.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.93
tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest';material.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
groups={'Roof':[],'Walls':[]}
for o in objects:
 c=tuple(round(float(v),5) for v in o.data.materials[0].diffuse_color);u=(colors.index(c)+.5)/len(colors)
 uv=o.data.uv_layers.get('UVMap') or o.data.uv_layers.new(name='UVMap');o.data.uv_layers.active=uv;uv.active_render=True
 for loop in uv.data:loop.uv=(u,.5)
 o.data.materials.clear();o.data.materials.append(material)
 group='Roof' if any(k in o.name.lower() for k in ('roof','slate','verge','fascia','ridge','chimney','crown')) else 'Walls';groups[group].append(o)
for label,obs in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();bpy.context.object.name=label
bpy.ops.object.select_all(action='DESELECT')
for o in collection.objects:o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(ROOT/'c01_market_6x3.glb'),export_format='GLB',use_selection=True,export_yup=True,export_apply=True)
collection=None
floor=mat('Studio floor',(.23,.26,.22));cube('Studio only floor',(0,0,-.11),(7.2,4.2,.20),floor,.02)
world=bpy.context.scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.40,.48,.52,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
bpy.ops.object.light_add(type='AREA',location=(-3,-5,9));bpy.context.object.data.energy=1050;bpy.context.object.data.size=7
bpy.ops.object.camera_add(location=(17,-17,24));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=8.4;bpy.context.scene.camera=cam
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=48;sc.cycles.use_denoising=False;sc.render.resolution_x=960;sc.render.resolution_y=640;sc.render.resolution_percentage=100;sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG';sc.render.filepath=str(ROOT/'forge-default-angle.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'forge-original.blend'));bpy.ops.render.render(write_still=True)
