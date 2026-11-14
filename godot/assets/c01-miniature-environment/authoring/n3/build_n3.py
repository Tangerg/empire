"""N3-only third architecture pass; native footprint/placement unchanged."""
from pathlib import Path
import sys,json,math,os
import bpy
R=Path(os.environ['C01_N3_OUTPUT']);R.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
from build_assets import box,beam,mesh,material,join_parts
P=json.loads((R/'surface-palette.json').read_text())
def uv_roof(obj):
 layer=obj.data.uv_layers.new(name='UVMap')
 for poly in obj.data.polygons:
  for k,uv in zip(poly.loop_indices,[(0,.75),(1,.75),(1,0),(0,0)]):layer.data[k].uv=uv

def build(name,depth,upper):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 mats={k:material(k,v) for k,v in P['colors'].items()};roofmat=material('PaintedSlateSurface',(.18,.19,.24))
 length=3.78;span=depth-.20;front=-span/2+( .16 if upper else .04);back=span/2;eave=1.65 if upper else 1.30;rise=.55 if upper else .28;ridge=eave+rise;half=depth/2-.045
 box('Recessed lime wall',(0,(front+back)/2,(.24+eave)/2),(length,back-front,eave-.24),mats['plaster'],.035)
 # A true solid contact base, not a platform painting or loose decorative rubble.
 for row in range(2):
  for i in range(10):box('Front plinth stone',(-1.89+(i+.5)*.378,front-.018,.065+row*.13),(.365,.12,.13),mats['stone'+str(i%3)],.013)
  for i in range(10):box('Back plinth stone',(-1.89+(i+.5)*.378,back+.012,.065+row*.13),(.365,.10,.13),mats['stone'+str((i+1)%3)],.013)
 for side in [-1,1]:
  box('End stone plinth',(side*1.86,(front+back)/2,.13),(.12,back-front,.26),mats['stone1'],.018)
  x=side*length/2;mesh('Solid plaster gable',[(x,-span/2,eave),(x,span/2,eave),(x,0,ridge)],[(0,1,2)],mats['plaster'])
  beam('Gable ridge frame',(x,-half,eave),(x,0,ridge),.075,mats['oak']);beam('Gable ridge frame',(x,half,eave),(x,0,ridge),.075,mats['oak'])
 # Nonrepeating three-bay shop frontage with a wider middle entry.
 y=front-.035
 for x in [-1.85,-.63,.63,1.85]:box('Front structural post',(x,y,(eave+.24)/2),(.10,.10,eave-.24),mats['oak'],.012)
 for z in [.27,eave-.055]:box('Front carved rail',(0,y,z),(3.80,.10,.10),mats['oak'],.012)
 doorheight=1.12 if upper else 1.04;bottom=.20;center=bottom+doorheight/2
 box('Central shop entrance recess',(0,y-.01,center),(.78,.035,doorheight),mats['inset'],.01)
 for i in range(7):box('Wide oak door plank',(-.32+i*.107,y-.03,center),(.10,.04,doorheight-.025),mats['worn_oak'],.008)
 for z in [bottom+.22,bottom+doorheight-.18]:box('Door strap',(0,y-.06,z),(.72,.025,.038),mats['iron'],.003)
 for x,ww in [(-1.23,.92),(1.23,.82)]:
  h=.60 if upper else .44;z=.89 if upper else .81
  box('Deep display window',(x,y-.008,z),(ww,.035,h),mats['inset'],.01)
  for xx in [x-ww/2,x+ww/2]:box('Window oak jamb',(xx,y-.035,z),(.05,.07,h+.09),mats['worn_oak'],.008)
  for zz in [z-h/2,z+h/2]:box('Display lintel',(x,y-.045,zz),(ww+.10,.10,.055),mats['worn_oak'],.008)
  box('Shop window mullion',(x,y-.035,z),(.035,.07,h),mats['oak'],.005)
  if upper:box('Upper transom',(x,y-.035,z+.12),(ww,.07,.035),mats['oak'],.005)
 # Rear wall deliberately quiet, not another copied public shopfront.
 for x in [-1.85,0,1.85]:box('Rear structural post',(x,back+.02,(eave+.24)/2),(.08,.07,eave-.24),mats['oak'],.012)
 box('Rear lintel',(0,back+.02,eave-.06),(3.80,.07,.09),mats['oak'],.012)
 body=list(bpy.context.scene.objects)
 for side in [-1,1]:
  v=[(-1.95,0,ridge),(1.95,0,ridge),(1.95,side*half,eave),(-1.95,side*half,eave)]
  o=mesh('Low polygon tiled roof',v,[(0,1,2,3)] if side==1 else [(3,2,1,0)],roofmat)
  # Assign vertex-index based UVs so opposite winding cannot rotate the tile courses.
  layer=o.data.uv_layers.new(name='UVMap');coords=[(0,.75),(1,.75),(1,0),(0,0)]
  for loop in o.data.loops:layer.data[loop.index].uv=coords[loop.vertex_index]
  beam('Continuous rounded eave',(-1.95,side*half,eave),(1.95,side*half,eave),.055,mats['slate_cap'])
 for i in range(13):box('Silhouette ridge cap',(-1.95+(i+.5)*.3,0,ridge+.025),(.294,.13,.055),mats['slate_cap'],.016)
 if upper:
  # The facade is recessed. Cloth remains entirely in the SAME4x2 wall footprint.
  verts=[];faces=[]
  for i in range(9):
   x=-1.70+i*.425;verts.extend([(x,front-.02,1.42),(x,-.96,1.29-.018*math.sin(i*math.pi/2))])
  for i in range(8):faces.append((i*2,i*2+1,i*2+3,i*2+2))
  awn=mesh('Bounded market cloth awning',verts,faces,mats['canvas']);awn.data.materials.append(mats['canvas_stripe'])
  for i,p in enumerate(awn.data.polygons):p.material_index=i%2
  mod=awn.modifiers.new('Cloth thickness','SOLIDIFY');mod.thickness=.009;bpy.context.view_layer.objects.active=awn;bpy.ops.object.modifier_apply(modifier=mod.name)
  for x in [-1.68,1.68]:beam('Awning wall bracket',(x,front,1.18),(x,-.94,1.29),.035,mats['oak'])
 roof=[o for o in bpy.context.scene.objects if o not in body]
 walls=join_parts(body,'Walls');roof=join_parts(roof,'Roof')
 unified=bpy.data.materials.new('C01 N3 unified matte atlas');unified.use_nodes=True;nt=unified.node_tree;bs=nt.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.91
 tex=nt.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(R/'c01_n3_surface_albedo.png'));nt.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 normal=nt.nodes.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(R/'c01_n3_surface_normal.png'));normal.image.colorspace_settings.name='Non-Color';nm=nt.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.55;nt.links.new(normal.outputs['Color'],nm.inputs['Color']);nt.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
 for obj in [walls,roof]:
  me=obj.data;old=list(me.materials);uv=me.uv_layers.get('UVMap') or me.uv_layers.new(name='UVMap')
  for p in me.polygons:
   key=old[p.material_index].name.split('.')[0]
   if key!='PaintedSlateSurface':
    for k in p.loop_indices:uv.data[k].uv=P['uv'][key]
  me.materials.clear();me.materials.append(unified)
  for p in me.polygons:p.material_index=0
 bpy.ops.object.empty_add(type='PLAIN_AXES');root=bpy.context.object;root.name=name
 for obj in [walls,roof]:obj.parent=root
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(R/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True)
 return {'name':name,'footprint':[4,depth],'eave':eave,'ridge_height':ridge,'door_height':doorheight,'surfaces':2,'geometry_triangles':sum(len(p.vertices)-2 for o in [walls,roof] for p in o.data.polygons)}
report=[build('c01_n3_upper_market',2,True),build('c01_n3_lower_gallery',1,False)]
(R/'build-report.json').write_text(json.dumps(report,indent=2))
