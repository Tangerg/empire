"""Author the original East Weighhouse as two bounded, matte Godot meshes."""
from pathlib import Path
from collections import defaultdict
import math
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
    for y,z in ((eave_y,eave_z),(ridge_y,ridge_z)):
        box("Roof",(x0,y-.026,z-.026),(x1,y+.026,z+.026),SLATE[0])

def store_roof():
    x0,x1=-.64,1.94
    ridge=.11
    slate_plane(x0,x1,-.755,ridge,1.41,1.91,3)
    slate_plane(x0,x1,.805,ridge,1.41,1.91,1)
    for x,ix in ((x0+.055,0),(x1-.055,1)):
        face("Walls",[(x,-.69,1.33),(x,.74,1.33),(x,ridge,1.86)],
             [(0,2,1)] if ix==0 else [(0,1,2)], LIME)
    slate_plane(-1.93,-.59,-.865,.68,.97,1.24,2)
    box("Roof",(.94,.34,1.59),(1.25,.65,2.11),STONE[0])
    box("Roof",(.912,.312,2.08),(1.278,.678,2.16),STONE[1])

def stone_base():
    box("Walls",(-1.90,-.82,0),(1.89,.72,.10),STONE[0])
    for row in range(2):
        cuts=[-1.90]+[c+(row*.17) for c in [-1.52,-1.15,-.77,-.39,0,.39,.76,1.14,1.52] if c+row*.17<1.85]+[1.89]
        for index,(lo,hi) in enumerate(zip(cuts,cuts[1:])):
            y=-.79 + (row*.025)
            box("Walls",(lo+.006,y,.08+row*.081),(hi-.006,y+.102,.156+row*.081),STONE[(row+index)%4])
    box("Walls",(-.65,-.72,.215),(1.9,.75,.265),STONE[2])

def building():
    stone_base()
    box("Walls",(-.58,.60,.25),(1.86,.73,1.42),LIME)
    box("Walls",(-.58,-.68,.25),(-.43,.61,1.42),LIME)
    for lo,hi,bottom,top in ((-.685,-.33,.25,1.42),(.12,.61,.25,1.42),
                             (-.33,.12,.25,.63),(-.33,.12,1.03,1.42)):
        box("Walls",(1.69,lo,bottom),(1.86,hi,top),STONE[2])
    box("Walls",(1.63,-.33,.63),(1.66,.12,1.03),REVEAL)
    box("Walls",(1.84,-.388,.594),(1.931,.17,.654),STONE[1])
    for y in (-.25,-.10,.04):
        box("Walls",(1.70,y,.65),(1.784,y+.043,1.025),LIGHT_OAK)
    f0,f1=-.69,-.54
    for l,r,h,top in [(-.56,-.26,.265,1.42),(.60,.96,.265,1.42),(1.53,1.86,.265,1.42),
                      (-.26,.60,1.20,1.42),(.96,1.53,.265,.67),(.96,1.53,1.04,1.42)]:
        box("Walls",(l,f0,h),(r,f1,top),LIME)
    box("Walls",(-.26,-.30,.26),(.60,-.26,1.25),REVEAL)
    for x in (-.30,.61):
        box("Walls",(x-.047,-.745,.26),(x+.047,-.45,1.23),OAK)
    for z in (1.206,1.34):
        box("Walls",(-.56,-.752,z),(1.83,-.637,z+.072),OAK)
    box("Walls",(.96,-.51,.672),(1.53,-.47,1.04),REVEAL)
    for x in (1.06,1.23,1.40):
        box("Walls",(x,-.573,.67),(x+.055,-.48,1.03),LIGHT_OAK)
    box("Walls",(.88,-.776,.626),(1.58,-.515,.70),STONE[1])
    for x in (-1.76,-.87):
        box("Walls",(x-.11,-.807,.24),(x+.11,-.57,.41),STONE[1])
        box("Walls",(x-.056,-.755,.41),(x+.056,-.628,1.038),OAK)
    box("Walls",(-1.88,-.766,1.004),(-.57,-.623,1.088),LIGHT_OAK)
    box("Walls",(-1.88,.52,.25),(-.58,.675,1.19),STONE[0])
    strut((-1.75,-.68,.79),(-1.52,-.68,1.015),.075,LIGHT_OAK)
    strut((-.88,-.68,.79),(-1.10,-.68,1.015),.075,LIGHT_OAK)
    box("Walls",(-1.62,-.44,.24),(-1.015,-.018,.39),LIGHT_OAK)
    box("Walls",(-1.58,-.41,.39),(-1.066,-.067,.425),IRON)
    store_roof()

bpy.ops.wm.read_factory_settings(use_empty=True)
building()
mat=bpy.data.materials.new("Weighhouse • matte painted stone and wood")
mat.use_nodes=True
nodes=mat.node_tree.nodes
principled=nodes.get("Principled BSDF")
principled.inputs["Roughness"].default_value=.9
paint=nodes.new("ShaderNodeVertexColor")
paint.layer_name="Paint"
mat.node_tree.links.new(paint.outputs["Color"], principled.inputs["Base Color"])
for name,(vertices,faces,colors) in GROUPS.items():
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    mesh.materials.append(mat)
    layer=mesh.color_attributes.new(name="Paint",type="BYTE_COLOR",domain="CORNER")
    for polygon,color in zip(mesh.polygons,colors):
        for index in polygon.loop_indices: layer.data[index].color = (*color,1)
    node=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(node)
bpy.ops.export_scene.gltf(filepath=str(OUT/"c01_east_weighhouse.glb"),export_format="GLB",export_yup=True,export_apply=True)
