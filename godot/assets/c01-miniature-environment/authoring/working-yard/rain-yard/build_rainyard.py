"""Original E2 rain-yard compound; Blender Z-up, glTF Y-up; no gameplay geometry."""
from pathlib import Path
from collections import defaultdict
import math,json,hashlib
import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parent
STONE = ((.32,.33,.29),(.38,.385,.33),(.35,.356,.31),(.405,.40,.35))
OAK = (.19,.153,.105)
LIGHT_OAK = (.253,.20,.136)
REVEAL = (.048,.048,.043)
LIME = (.61,.55,.41)
SLATE = ((.195,.234,.255),(.208,.252,.273),(.221,.263,.278),(.235,.275,.29))
IRON = (.096,.107,.102)
GROUPS = defaultdict(lambda: ([],[],[]))

def face(group, vertices, faces, tint):
    points, polygons, colors = GROUPS[group]
    n = len(points)
    points.extend(vertices)
    polygons.extend(tuple(n+i for i in f) for f in faces)
    colors.extend([tint] * len(faces))

def box(group, lo, hi, tint):
    x,y,z=lo; X,Y,Z=hi
    face(group, [(x,y,z),(X,y,z),(X,Y,z),(x,Y,z),(x,y,Z),(X,y,Z),(X,Y,Z),(x,Y,Z)],
         [(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)], tint)

def strut(a,b,thick,tint):
    v,w=Vector(a),Vector(b)
    along=(w-v).normalized()
    side=along.cross(Vector((0,1,0))).normalized()*(thick/2)
    deep=side.normalized().cross(along)*(thick/2)
    points=[tuple(end+i*side+j*deep) for end in (v,w) for i,j in ((-1,-1),(1,-1),(1,1),(-1,1))]
    face("Walls",points,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],tint)

def slate_plane(x0,x1,eave_y,ridge_y,eave_z,ridge_z,seed):
    # A continuous deck keeps the cut between slate pieces from revealing sky.
    f=(0,1,2,3) if eave_y<ridge_y else (3,2,1,0)
    face("Roof",[(x0,eave_y,eave_z-.015),(x1,eave_y,eave_z-.015),
               (x1,ridge_y,ridge_z-.015),(x0,ridge_y,ridge_z-.015)],[f],SLATE[0])
    rows=7; cols=max(4,round((x1-x0)/.24))
    for r in range(rows):
        r0=r/rows; r1=min(1,(r+1.07)/rows)
        y0=(1-r0)*eave_y+r0*ridge_y
        y1=(1-r1)*eave_y+r1*ridge_y
        z0=(1-r0)*eave_z+r0*ridge_z + r*.0022
        z1=(1-r1)*eave_z+r1*ridge_z + r*.0022
        cuts=[x0]+[x0+(x1-x0)*(j+(.5 if r%2 else 0))/cols for j in range(1,cols)]+[x1]
        for c,(left,right) in enumerate(zip(cuts,cuts[1:])):
            rgt = right - .006
            f=(0,1,2,3) if eave_y<ridge_y else (3,2,1,0)
            face("Roof",[(left,y0,z0),(rgt,y0,z0),(rgt,y1,z1),(left,y1,z1)], [f], SLATE[(c*11+r*5+seed)%len(SLATE)])
            face("Roof",[(left,y0,z0-.017),(rgt,y0,z0-.017),(rgt,y0,z0),(left,y0,z0)],
                 [(0,1,2,3)] if eave_y<ridge_y else [(3,2,1,0)], SLATE[(c+r+seed)%len(SLATE)])
    for y,z in ((eave_y,eave_z),):
        box("Roof",(x0,y-.026,z-.026),(x1,y+.026,z+.026),SLATE[0])


def yard():
    box("Walls",(-1.91,-1.91,0),(1.91,1.91,.09),STONE[0])
    # Whole court remains inside the canonical blocked footprint, not a new road.
    for row in range(5):
        for col in range(5):
            x=-.68+col*.49; y=-1.72+row*.36
            box("Walls",(x,y,.09),(x+.475,y+.345,.115),STONE[(row+col*3)%4])
    # Closed back wall, asymmetric storage window, genuinely open loading front.
    box("Walls",(-1.78,1.67,.09),(1.78,1.83,1.37),STONE[1])
    box("Walls",(-1.78,.25,.09),(-1.60,1.67,1.37),LIME)
    box("Walls",(1.60,.25,.09),(1.78,1.67,1.37),STONE[2])
    box("Walls",(-1.60,1.28,.10),(-.98,1.65,.55),LIGHT_OAK)
    box("Walls",(-.79,1.31,.10),(-.23,1.65,.39),OAK)
    box("Walls",(.96,1.30,.10),(1.56,1.65,.72),OAK)
    # The open central bay is recessed, with no front wall behind its columns.
    for x in (-1.70,-.58,.61,1.70):
        box("Walls",(x-.10,.20,.09),(x+.10,.42,.27),STONE[2])
        box("Walls",(x-.057,.25,.27),(x+.057,.37,1.34),OAK)
        if x < 1.6: strut((x,.31,1.01),(x+.26,.31,1.29),.065,LIGHT_OAK)
    box("Walls",(-1.8,.23,1.27),(1.80,.40,1.38),OAK)
    # One shuttered shallow loft above the left bay breaks the repetitive facade.
    box("Walls",(-1.62,.34,1.05),(-.67,.45,1.34),LIGHT_OAK)
    for x in (-1.54,-1.35,-1.16,-.97,-.78):
        box("Walls",(x,.326,1.05),(x+.025,.35,1.34),OAK)
    box("Walls",(1.00,1.32,.72),(1.53,1.61,.754),(.32,.225,.135))
    # Main ridge is rearward, leaving most foreground as a low enclosed court.
    slate_plane(-1.89,1.89,.12,1.0,1.40,1.91,1)
    slate_plane(-1.89,1.89,1.91,1.0,1.40,1.91,2)
    # A single continuous folded cap seals the meeting planes without coplanar rails.
    face("Roof",[(-1.91,.90,1.91),(1.91,.90,1.91),(1.91,1.0,1.985),(-1.91,1.0,1.985),
                 (-1.91,1.10,1.91),(1.91,1.10,1.91)],
         [(0,1,2,3),(3,2,5,4),(0,3,4),(1,5,2)],SLATE[1])
    for x,flip in ((-1.77,False),(1.77,True)):
        face("Walls",[(x,.28,1.32),(x,1.78,1.32),(x,1.0,1.87)],
             [(0,1,2)] if flip else [(0,2,1)],LIME)
    # Attached lower west shelter: different ridge/height, same construction.
    box("Walls",(-1.82,-1.69,.09),(-1.66,.33,.98),STONE[1])
    for y in (-1.52,-.55):
        box("Walls",(-.91,y-.08,.09),(-.73,y+.08,.23),STONE[1])
        box("Walls",(-.87,y-.043,.23),(-.77,y+.043,.93),OAK)
    box("Walls",(-.90,-1.70,.90),(-.74,.36,1.00),LIGHT_OAK)
    # Side shed slopes across X. Transform the reusable roof-plane strip.
    before=len(GROUPS["Roof"][0])
    slate_plane(-1.73,.39,.70,1.91,1.0,1.22,4)
    vertices=GROUPS["Roof"][0]
    for i in range(before,len(vertices)):
        x,y,z=vertices[i]; vertices[i]=(-y,x,z)
    box("Walls",(-1.58,-1.49,.1),(-1.04,-.80,.47),LIGHT_OAK)
    box("Walls",(-1.55,-1.46,.47),(-1.07,-.83,.50),OAK)
    # Low court walls and a closed gate tell the player this is not walkable.
    for x0,x1 in ((-1.86,-.56),(.32,1.86)):
        for row in range(3):
            count=max(2,round((x1-x0)/.38))
            for col in range(count):
                a=x0+(x1-x0)*col/count; b=x0+(x1-x0)*(col+1)/count
                box("Walls",(a+.005,-1.86,.09+row*.105),(b-.005,-1.69,.19+row*.105),STONE[(row+col)%4])
        box("Walls",(x0,-1.89,.405),(x1,-1.66,.455),STONE[2])
    box("Walls",(1.69,-1.69,.09),(1.86,.27,.40),STONE[1])
    box("Walls",(1.66,-1.69,.40),(1.89,.27,.45),STONE[2])
    for x in (-.59,.35):
        box("Walls",(x-.07,-1.88,.09),(x+.07,-1.67,.69),STONE[2])
    for x in (-.48,-.32,-.16,0,.16):
        box("Walls",(x,-1.80,.13),(x+.13,-1.735,.57),LIGHT_OAK)
    for z in (.20,.49):
        box("Walls",(-.50,-1.825,z),(.30,-1.79,z+.045),OAK)
    # Bundled stock behind the closed gate belongs to the enclosed store yard.
    box("Walls",(.86,-.80,.115),(1.42,-.19,.48),OAK)
    for z in (.18,.40):
        box("Walls",(.84,-.82,z),(1.44,-.18,z+.035),LIGHT_OAK)

def finish():
    mat=bpy.data.materials.new("Rainyard matte painted timber and masonry")
    mat.use_nodes=True
    bs=mat.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Roughness"].default_value=.93
    paint=mat.node_tree.nodes.new("ShaderNodeVertexColor");paint.layer_name="Paint"
    mat.node_tree.links.new(paint.outputs["Color"],bs.inputs["Base Color"])
    for name,(vertices,faces,colors) in GROUPS.items():
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update();mesh.materials.append(mat)
        layer=mesh.color_attributes.new(name="Paint",type="BYTE_COLOR",domain="CORNER")
        for polygon,color in zip(mesh.polygons,colors):
            for idx in polygon.loop_indices:layer.data[idx].color=(*color,1)
        node=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(node)
    points=[v for verts,_,_ in GROUPS.values() for v in verts]
    lo=[min(v[i] for v in points) for i in range(3)];hi=[max(v[i] for v in points) for i in range(3)]
    assert lo[0]>=-2 and hi[0]<=2 and lo[1]>=-2 and hi[1]<=2 and lo[2]>=0
    triangles=sum(len(face)-2 for _,faces,_ in GROUPS.values() for face in faces)
    out=OUT/"c01_rainyard_4x4.glb"
    bpy.ops.export_scene.gltf(filepath=str(out),export_format="GLB",export_yup=True,export_apply=True)
    (OUT/"report.json").write_text(json.dumps({"base":"ea5bc5065c12fc65fb5f832f283ca76108cc61b6","replaces":"c01_store_4x4.glb","placement":[24,0,18],"bounds_blender":[lo,hi],"bounds_godot":[[lo[0],lo[2],-hi[1]],[hi[0],hi[2],-lo[1]]],"triangles":triangles,"meshes":["Walls","Roof"],"surfaces":2,"visual_only":True,"sha256":hashlib.sha256(out.read_bytes()).hexdigest()},indent=2))

bpy.ops.wm.read_factory_settings(use_empty=True)
yard();finish()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.008))
floor=bpy.data.materials.new("Preview ground only");floor.diffuse_color=(.30,.29,.24,1);bpy.context.object.data.materials.append(floor)
bpy.ops.object.camera_add(location=(8,-8,10.82))
camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.6))-camera.location).to_track_quat("-Z","Y").to_euler();camera.data.type="ORTHO";camera.data.ortho_scale=6.5
scene=bpy.context.scene;scene.camera=camera
bpy.ops.object.light_add(type="AREA",location=(-3,-5,9));light=bpy.context.object;light.data.energy=850;light.data.size=6;light.rotation_euler=(-light.location).to_track_quat("-Z","Y").to_euler()
scene.world=bpy.data.worlds.new("Preview world");scene.world.color=(.30,.33,.38);scene.render.engine="CYCLES";scene.cycles.device="CPU";scene.cycles.samples=32;scene.cycles.use_denoising=False;scene.render.threads_mode="FIXED";scene.render.threads=2
scene.render.resolution_x=800;scene.render.resolution_y=720;scene.render.resolution_percentage=100;scene.view_settings.view_transform="AgX"
scene.render.filepath=str(OUT/"rainyard-preview.png")
bpy.ops.render.render(write_still=True)
