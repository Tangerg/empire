"""Geometry-preserving PBR palette atlas. No runtime files overwritten."""
from pathlib import Path
import bpy,struct,zlib,json,os,sys,argparse
parser=argparse.ArgumentParser()
parser.add_argument('--source',required=True,type=Path)
parser.add_argument('--output',required=True,type=Path)
parser.add_argument('--dark-plinth',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
SRC=args.source.resolve();OUT=args.output.resolve()
if SRC==OUT:
    raise ValueError('Use a separate output folder; validate before replacing runtime resources.')
OUT.mkdir(parents=True,exist_ok=True)
def png(path,pixels,size=64):
 raw=b''.join(b'\x00'+bytes(pixels[y*size*4:(y+1)*size*4]) for y in range(size))
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
def srgb(x):return round(255*(12.92*x if x<=.0031308 else 1.055*x**(1/2.4)-.055))
reports=[]
for src in sorted(SRC.glob('*.glb')):
 if src.stem=='c01_market_beacon':continue
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.ops.import_scene.gltf(filepath=str(src))
 objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];old=[]
 for o in objects:
  for mat in o.data.materials:
   if mat not in old:old.append(mat)
 colors=bytearray([255]*(64*64*4));orm=bytearray([255]*(64*64*4));entries=[]
 for i,mat in enumerate(old):
  bs=mat.node_tree.nodes.get('Principled BSDF');rgb=list(bs.inputs['Base Color'].default_value[:3]);rough=bs.inputs['Roughness'].default_value;metal=bs.inputs['Metallic'].default_value
  original=rgb.copy()
  if args.dark_plinth and 'sandstone' in mat.name.lower():rgb=[rgb[0]*.56,rgb[1]*.60,rgb[2]*.66]
  cx=i%8;cy=i//8
  for yy in range(cy*8,(cy+1)*8):
   for xx in range(cx*8,(cx+1)*8):
    k=(yy*64+xx)*4;colors[k:k+4]=bytes([*(srgb(v) for v in rgb),255]);orm[k:k+4]=bytes([255,round(rough*255),round(metal*255),255])
  entries.append({'material':mat.name,'source_linear_color':original,'atlas_linear_color':rgb,'roughness':rough,'metallic':metal,'uv':[(cx*8+4)/64,1-(cy*8+4)/64]})
 colorpath=OUT/(src.stem+'-palette.png');ormpath=OUT/(src.stem+'-orm.png');png(colorpath,colors);png(ormpath,orm)
 new=bpy.data.materials.new('C01 unified matte palette');new.use_nodes=True;nt=new.node_tree;bs=nt.nodes.get('Principled BSDF')
 a=nt.nodes.new('ShaderNodeTexImage');a.image=bpy.data.images.load(str(colorpath));a.interpolation='Closest';nt.links.new(a.outputs['Color'],bs.inputs['Base Color'])
 b=nt.nodes.new('ShaderNodeTexImage');b.image=bpy.data.images.load(str(ormpath));b.image.colorspace_settings.name='Non-Color';b.interpolation='Closest';sep=nt.nodes.new('ShaderNodeSeparateRGB');nt.links.new(b.outputs['Color'],sep.inputs[0]);nt.links.new(sep.outputs['G'],bs.inputs['Roughness']);nt.links.new(sep.outputs['B'],bs.inputs['Metallic'])
 counts=[]
 for obj in objects:
  me=obj.data;counts.append(sum(len(p.vertices)-2 for p in me.polygons));mapping=[old.index(m) for m in me.materials];uv=me.uv_layers.get('UVMap') or me.uv_layers.new(name='UVMap');me.uv_layers.active=uv
  for poly in me.polygons:
   p=entries[mapping[poly.material_index]]['uv']
   for k in poly.loop_indices:uv.data[k].uv=p
  me.materials.clear();me.materials.append(new)
  for poly in me.polygons:poly.material_index=0
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(OUT/src.name),export_format='GLB',use_selection=True,export_yup=True,export_apply=True)
 reports.append({'file':src.name,'source_materials':len(old),'new_materials':1,'mesh_objects':len(objects),'source_triangles':sum(counts),'intentional_plinth_grade':args.dark_plinth,'palette':entries})
(OUT/'palette-report.json').write_text(json.dumps(reports,indent=2))
