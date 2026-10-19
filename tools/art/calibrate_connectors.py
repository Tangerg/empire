#!/usr/bin/env python3
"""Calibration-only continuous native NESW connectors; never production writes."""
import argparse, hashlib, importlib.util, json
from pathlib import Path
from PIL import Image, ImageChops, ImageFilter
from export_surface_overrides import match_edges
from export_environment_overrides import PACK


def builder(root):
    path=root/PACK/'environment-builder-v1/tools/build_environment_builder_v1.py'
    spec=importlib.util.spec_from_file_location('legacy_environment_geometry',path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module,path


def geometry(module,mask,water=False):
    # Existing canonical width/topology; remove noisy interior and warped ports.
    return module.mask_shape(64,mask,False,.57 if water else .48)


def compose(texture,shape,bank):
    ring=ImageChops.subtract(shape.filter(ImageFilter.MaxFilter(9)),shape.filter(ImageFilter.MaxFilter(3)))
    out=Image.new('RGBA',(64,64),(*bank,0));out.putalpha(ring.point(lambda a:round(a*220/255)))
    fill=texture.convert('RGBA');fill.putalpha(shape);out.alpha_composite(fill)
    return out


def edge(shape):
    outer=shape.filter(ImageFilter.MaxFilter(11));inner=shape.filter(ImageFilter.MinFilter(7))
    ring=ImageChops.subtract(outer,shape);rut=ImageChops.subtract(shape,inner)
    out=Image.new('RGBA',(64,64),(78,68,49,0));out.putalpha(ring.point(lambda a:min(180,a)))
    ink=Image.new('RGBA',(64,64),(59,48,35,0));ink.putalpha(rut.point(lambda a:min(110,a)))
    out.alpha_composite(ink);return out


def build(root,output,river_material=None):
    root=root.resolve();output=output.resolve()
    if output.is_relative_to(root/'packages'):
        raise ValueError('calibration output may not replace package assets')
    mod,code=builder(root);env=root/PACK/'environment-builder-v1';output.mkdir(parents=True,exist_ok=True)
    report={'kind':'calibration-only-native-connector-geometry','productionMutation':False,'topology':'NESW16; index mask*4+variant','widthRatios':{'road':.48,'water':.57},'removed':['variant-dependent mask warp','~128 interior alpha noise','forced255 port rectangles','variant-specific decorative edge marks'],'textureTreatment':'original master cell resize plus outermost-row/column edge matching only','geometrySource':{'path':str(code),'sha256':hashlib.sha256(code.read_bytes()).hexdigest()},'sources':{},'outputs':{}}
    for name,source,water,bank in [('route-dirt-road','route-materials-4x4.png',False,(72,61,45)),('water-slow-river','water-materials-4x4.png',True,(74,67,53))]:
        path=env/'masters'/source;raw=Image.open(path).convert('RGB');textures=[]
        for i in range(4):
            box=(round(i*raw.width/4),0,round((i+1)*raw.width/4),round(raw.height/4))
            textures.append(raw.crop(box).resize((64,64),Image.Resampling.LANCZOS))
        if water and river_material is not None:
            material=Image.open(river_material)
            if material.width != material.height or material.width < 64 or material.convert('RGBA').getchannel('A').getextrema() != (255,255):
                raise ValueError('river material must be square opaque PNG at least64px')
            textures=[material.convert('RGB').resize((64,64),Image.Resampling.LANCZOS) for _ in range(4)]
        textures=match_edges(textures)
        report['sources'][name]={'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'row':0,'columns':[0,1,2,3]}
        if water and river_material is not None:
            report['sources'][name]={'path':str(river_material),'sha256':hashlib.sha256(Path(river_material).read_bytes()).hexdigest(),'variants':'same single generated material; no invented variants'}
        sheets={name:Image.new('RGBA',(1024,256))}
        if not water:sheets['route-edge-dirt-road']=Image.new('RGBA',(1024,256))
        for mask in range(16):
            shape=geometry(mod,mask,water)
            for variant in range(4):
                i=mask*4+variant;xy=(i%16*64,i//16*64)
                sheets[name].paste(compose(textures[variant],shape,bank),xy)
                if not water:sheets['route-edge-dirt-road'].paste(edge(shape),xy)
        for atlas,sheet in sheets.items():
            for density,im,suffix in [(2,sheet,'@2x'),(1,sheet.resize((512,128),Image.Resampling.LANCZOS),'')]:
                p=output/f'{atlas}{suffix}.png';im.save(p);report['outputs'][p.name]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'dimensions':im.size,'density':density,'cells':64}
    (output/'provenance.json').write_text(json.dumps(report,indent=2)+'\n');return report

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--river-material',type=Path);a=p.parse_args();print(json.dumps(build(a.root,a.output_dir,a.river_material),indent=2))
