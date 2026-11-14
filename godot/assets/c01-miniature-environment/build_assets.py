"""Deterministic original visual-only C01 kit. Blender Z-up -> glTF Y-up."""
from pathlib import Path
import math,random,json,os
import bpy
from mathutils import Vector
ROOT=Path(os.environ.get('C01_ASSET_OUTPUT',str(Path(__file__).resolve().parent)))
ROOT.mkdir(parents=True,exist_ok=True)
RNG=random.Random(11026)

def material(name,color,roughness=.88):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=roughness
    return m

def box(name,center,size,mat,bevel=.018,rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center);obj=bpy.context.object;obj.name=name;obj.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rotation:obj.rotation_euler=rotation
    obj.data.materials.append(mat)
    if bevel:
        mod=obj.modifiers.new('Soft handmade arris','BEVEL');mod.width=bevel;mod.segments=2
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=obj.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def beam(name,a,b,width,mat):
    vec=Vector(b)-Vector(a);o=box(name,(Vector(a)+Vector(b))/2,(width,width,vec.length),mat,.008);o.rotation_euler=vec.to_track_quat('Z','Y').to_euler();return o

def mesh(name,verts,faces,mat):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);me.materials.append(mat);return o

def join_parts(objects,name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');return o

def build(name,width,depth,axis='X'):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    # Work with ridge on X; a 90-degree rotation produces the long Y footprint.
    length,span=(width,depth) if axis=='X' else (depth,width)
    length-=.18;span-=.18
    large_door=name == 'c01_row_4x2'
    door_height=1.02 if large_door else .77
    door_center=.77 if large_door else .69
    plaster=material('C01 warm lime plaster',(.52,.46,.35));wood=material('C01 aged oak',(.23,.14,.075));woodlight=material('C01 worn oak edges',(.34,.23,.12));dark=material('C01 recessed window',(.075,.10,.105),.73)
    stones=[material(f'C01 sandstone {i}',(.33+.025*i,.31+.022*i,.26+.020*i)) for i in range(5)]
    slates=[material(f'C01 gray purple slate {i}',(.15+.012*i,.16+.011*i,.205+.013*i),.91) for i in range(7)]
    metal=material('C01 iron',(.095,.10,.105),.7)
    eave=1.33;rise=min(.60,span*.45);roof_top=eave+rise;half=span/2+.035
    box('Walls plaster mass',(0,0,.78),(length,span,1.12),plaster,.045)
    # Individual foundation stones are actual geometry, restrained uneven coursing.
    for row in range(2):
        n=max(3,round(length/.42));step=length/n
        for i in range(n):
            for side in [-1,1]:box('Foundation coursed stone',(-length/2+(i+.5)*step,side*(span/2+.009),.085+row*.145),(step-.012,.10,.137),RNG.choice(stones),.015)
        n=max(3,round(span/.42));step=span/n
        for i in range(n):
            for side in [-1,1]:box('Foundation end stone',(side*(length/2+.008),-span/2+(i+.5)*step,.085+row*.145),(.10,step-.012,.137),RNG.choice(stones),.015)
    # Gable ends fill roof volume; no open black hole under roof.
    for side in [-1,1]:
        x=side*length/2;mesh('Gable plaster',[(x,-span/2,eave),(x,span/2,eave),(x,0,roof_top-.035)],[(0,1,2)],plaster)
        beam('Gable rake timber',(x,-half,eave),(x,0,roof_top),.08,wood)
        beam('Gable rake timber',(x,half,eave),(x,0,roof_top),.08,wood)
        beam('Gable king post',(x,0,eave),(x,0,roof_top),.065,wood)
    for side in [-1,1]:
        y=side*(span/2+.025)
        for z in [.32,eave-.06]:box('Continuous oak rail',(0,y,z),(length+.04,.075,.095),wood,.012)
        bay_count=max(2,round(length/1.2));step=length/bay_count
        for i in range(bay_count+1):box('Timber upright',(-length/2+i*step,y,.81),(.09,.085,1.05),wood,.012)
        for i in range(bay_count):
            x=-length/2+(i+.5)*step;front=side==-1
            if i==bay_count//2 and front:
                # Recessed single door, not a navigable opening.
                box('Door recess',(x,y*1.008,door_center),(.49,.032,door_height),dark,.01)
                for plank in range(5):box('Door oak plank',(x-.19+plank*.095,y*1.022,door_center),(.088,.035,1.00 if large_door else .74),woodlight,.009)
                for z in ([.43,1.09] if large_door else [.47,.88]):box('Door iron strap',(x,y*1.045,z),(.43,.021,.032),metal,.004)
            else:
                window_width=min(.47,step*.46);z=.87
                box('Window deep inset',(x,y*1.009,z),(window_width,.035,.40),dark,.012)
                for xx in [x-window_width/2,x+window_width/2]:box('Window casing',(xx,y*1.035,z),(.045,.055,.47),woodlight,.008)
                for zz in [z-.22,z+.22]:box('Window lintel sill',(x,y*1.04,zz),(window_width+.09,.075,.045),woodlight,.008)
                box('Window mullion',(x,y*1.035,z),(.025,.05,.42),wood,.004)
                box('Window crossbar',(x,y*1.035,z),(window_width,.05,.025),wood,.004)
                # One shutter folded against wall avoids extending footprint into alley.
                box('Oak shutter',(x+window_width*.72,y*1.014,z),(window_width*.35,.042,.40),woodlight,.009)
            if step>.8:beam('Diagonal brace',(x-step*.38,y,.98),(x-step*.12,y,1.23),.047,wood)
    body=list(bpy.context.scene.objects)
    # Roof mesh base and overlapping rounded slate tiles share the roof subtree.
    slope=math.atan2(rise,half);roof_length=math.hypot(half,rise)
    for side in [-1,1]:
        verts=[(-length/2-.06,0,roof_top),(length/2+.06,0,roof_top),(length/2+.06,side*half,eave),(-length/2-.06,side*half,eave)]
        mesh('Roof continuous underlay',verts,[(0,1,2,3)] if side==1 else [(3,2,1,0)],slates[2])
        rows=max(4,round(roof_length/.23));cols=max(7,round(length/.29));dx=(length+.10)/cols;ds=roof_length/rows
        for row in range(rows):
            dist=(row+.54)*ds;y=side*dist*math.cos(slope);z=roof_top-dist*math.sin(slope)+.027
            for col in range(cols):
                x=-length/2-.05+(col+.5)*dx
                box('Slate overlapping course',(x,y,z),(dx-.006,ds+.042,.039),RNG.choice(slates),.009,(-side*slope,0,0))
    caps=max(6,round(length/.30))
    for i in range(caps):box('Ridge cap slate',(-length/2+(i+.5)*length/caps,0,roof_top+.045),(length/caps-.008,.15,.075),slates[3],.023)
    roof=[o for o in bpy.context.scene.objects if o not in body]
    b=join_parts(body,'Walls');r=join_parts(roof,'Roof')
    for obj in [b,r]:
        if axis=='Y':obj.rotation_euler[2]=math.pi/2
    bpy.ops.object.empty_add(type='PLAIN_AXES');root=bpy.context.object;root.name=name
    root.scale=(.96,.96,1)
    for obj in [b,r]:
        obj.parent=root
        obj.location.z=-.0165
    bpy.context.view_layer.update()
    bounds=[o.matrix_world@Vector(c) for o in [b,r] for c in o.bound_box]
    path=ROOT/(name+'.glb')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_apply=True)
    triangles=sum(len(poly.vertices)-2 for o in [b,r] for poly in o.data.polygons)
    return {'path':'res://assets/c01-miniature-environment/'+path.name,'footprint_cells':[width,depth],'ridge_axis':axis,'ground_anchor':[0,0,0],'height_m':max(point.z for point in bounds),'door_height_m':door_height,'roof_node':'Roof','wall_node':'Walls','mesh_objects':2,'triangles':triangles,'bytes':path.stat().st_size}

def main():
    specs=[('c01_house_3x6',3,6,'Y'),('c01_row_6x2',6,2,'X'),('c01_house_3x3',3,3,'Y'),('c01_store_4x4',4,4,'X'),('c01_market_6x3',6,3,'X'),('c01_row_4x2',4,2,'X')]
    if os.environ.get('C01_ASSET_ONLY'):
        specs=[s for s in specs if s[0] in os.environ['C01_ASSET_ONLY'].split(',')]
    assets=[]
    for spec in specs:
        if spec[0]=='c01_row_4x2':
            RNG.seed(11026)
        assets.append(build(*spec))
    if os.environ.get('C01_ASSET_ONLY') and (ROOT/'manifest.json').exists():
        previous=json.loads((ROOT/'manifest.json').read_text())['assets']
        assets=[a for a in previous if a['path'].split('/')[-1][:-4] not in os.environ['C01_ASSET_ONLY'].split(',')]+assets
    (ROOT/'manifest.json').write_text(json.dumps({'units':'1 meter = 1 canonical cell; Godot X=column,Z=row,Y=height','visual_only':True,'collisions':False,'cell_height_step_m':.75,'placement':'root centered on wall footprint, y=canonical cell elevation * .75; paths remain canonical','assets':assets},indent=2))
    # Save final module for artist inspection, not auto-imported by Godot.
    # Source of truth is this deterministic generator; no auto-imported .blend duplicate.

if __name__ == '__main__':
    main()
