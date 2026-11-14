"""Original visual site emblem; owner and placement belong to the canonical bridge."""
from pathlib import Path
import math,json,sys
import bpy
sys.path.insert(0,str(Path(__file__).resolve().parent))
from build_assets import box,beam,mesh,material,join_parts
R=Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
stone=material('Beacon chalk sandstone',(.40,.37,.30));oak=material('Beacon aged oak',(.27,.17,.085));gold=material('Beacon worn brass',(.45,.32,.12),.8);cloth=material('Banner owner tint',(.42,.30,.12),.95)
box('Stepped stone foot',(0,0,.045),(.28,.28,.09),stone,.018)
box('Upper stone socket',(0,0,.105),(.20,.20,.07),stone,.014)
box('Square oak post',(0,0,.68),(.055,.055,1.13),oak,.008)
box('Carved post collar',(0,0,.25),(.08,.08,.08),gold,.008)
for side in [-1,1]:beam('Diagonal foot brace',(side*.095,0,.13),(0,0,.34),.035,oak)
# The flag extends inward (-X) from a cell's southeast-corner placement.
beam('Banner crossarm',(.025,0,1.18),(-.34,0,1.18),.032,oak)
box('Squared finial',(0,0,1.255),(.075,.075,.055),gold,.016)
body=list(bpy.context.scene.objects)
verts=[];faces=[];cols=8;rows=4
for row in range(rows+1):
 for col in range(cols+1):
  u=col/cols;v=row/rows;x=-.032-.29*u;y=.012*math.sin(u*math.pi*2.2)+.008*v;z=1.16-.34*v-.025*math.sin(u*math.pi)*v
  verts.append((x,y,z))
for row in range(rows):
 for col in range(cols):
  i=row*(cols+1)+col;faces.append((i,i+1,i+cols+2,i+cols+1))
banner=mesh('Banner',verts,faces,cloth)
# Thin cloth has a physical back surface, no alpha sorting or toy sheen.
mod=banner.modifiers.new('Cloth thickness','SOLIDIFY');mod.thickness=.005;bpy.context.view_layer.objects.active=banner;bpy.ops.object.modifier_apply(modifier=mod.name)
stand=join_parts(body,'Stand')
bpy.ops.object.empty_add(type='PLAIN_AXES');root=bpy.context.object;root.name='C01MarketBeacon';stand.parent=root;banner.parent=root
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(R/'c01_market_beacon.glb'),export_format='GLB',use_selection=True,export_yup=True,export_apply=True)
(R/'beacon-contract.json').write_text(json.dumps({'path':'res://assets/c01-miniature-environment/c01_market_beacon.glb','root':'C01MarketBeacon','owner_tint_mesh':'Banner','neutral_stand_mesh':'Stand','ground_anchor':[0,0,0],'recommended_offset_from_cell_center':[.32,0,.32],'horizontal_max_extent_local':{'x':[-.34,.14],'z':[-.14,.14]},'height_m':1.2825,'visual_only':True,'authority':'bridge owns exactly one capturable-site marker and metadata; battlefield does not instantiate this asset'},indent=2))
