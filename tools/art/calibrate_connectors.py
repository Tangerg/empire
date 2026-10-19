#!/usr/bin/env python3
"""Calibration-only continuous native NESW connectors; never production writes."""
import argparse, hashlib, importlib.util, json, math
from pathlib import Path
from PIL import Image, ImageChops, ImageFilter
from export_surface_overrides import match_edges
from export_environment_overrides import PACK


def builder(root):
    path=root/PACK/'environment-builder-v1/tools/build_environment_builder_v1.py'
    spec=importlib.util.spec_from_file_location('legacy_environment_geometry',path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module,path


def road_width_ratio(road_width_logical):
    if not math.isfinite(road_width_logical) or not 1 <= road_width_logical <= 30:
        raise ValueError("road width must be finite and between 1 and 30 logical pixels; closed NESW sides need a margin")
    return road_width_logical / 32


def geometry(module,mask,water=False,road_width_logical=15.36):
    ratio = road_width_ratio(road_width_logical)
    shape = module.mask_shape(64,mask,False,.57 if water else ratio)
    if not water and road_width_logical > 15.36:
        # Wide rounded junctions otherwise leak tiny antialias differences into
        # adjacent port corners. Two HD rows give the one-pixel shoulder a
        # shared geometry neighborhood without changing material pixels.
        vertical = module.mask_shape(64,5,False,ratio)
        horizontal = module.mask_shape(64,10,False,ratio)
        for bit,box,source in ((1,(0,0,64,2),vertical),(2,(62,0,64,64),horizontal),(4,(0,62,64,64),vertical),(8,(0,0,2,64),horizontal)):
            shape.paste(source.crop(box) if mask & bit else 0,box)
        for box in ((0,0,2,2),(62,0,64,2),(0,62,2,64),(62,62,64,64)):
            shape.paste(0,box)
    return shape


def compose(texture,shape,bank,thin=False):
    ring=ImageChops.subtract(shape.filter(ImageFilter.MaxFilter(3)),shape) if thin else ImageChops.subtract(shape.filter(ImageFilter.MaxFilter(9)),shape.filter(ImageFilter.MaxFilter(3)))
    out=Image.new('RGBA',(64,64),(*bank,0));out.putalpha(ring.point(lambda a:round(a*(75 if thin else 220)/255)))
    fill=texture.convert('RGBA');fill.putalpha(shape);out.alpha_composite(fill)
    return out


def edge(shape):
    outer=shape.filter(ImageFilter.MaxFilter(11));inner=shape.filter(ImageFilter.MinFilter(7))
    ring=ImageChops.subtract(outer,shape);rut=ImageChops.subtract(shape,inner)
    out=Image.new('RGBA',(64,64),(78,68,49,0));out.putalpha(ring.point(lambda a:min(180,a)))
    ink=Image.new('RGBA',(64,64),(59,48,35,0));ink.putalpha(rut.point(lambda a:min(110,a)))
    out.alpha_composite(ink);return out


def build(root,output,river_material=None,road_material=None,road_width_logical=15.36):
    ratio = road_width_ratio(road_width_logical)
    root=root.resolve();output=output.resolve()
    if output.is_relative_to(root/'packages'):
        raise ValueError('calibration output may not replace package assets')
    mod,code=builder(root);env=root/PACK/'environment-builder-v1';output.mkdir(parents=True,exist_ok=True)
    report={'kind':'calibration-only-native-connector-geometry','productionMutation':False,'topology':'NESW16; index mask*4+variant','widthRatios':{'road':ratio,'water':.57},'roadWidthLogical':road_width_logical,'roadWidthSemantics':'nominal core cross-section; 32logical cell and NESW topology unchanged; thin shoulder adds up to0.5logical per side; maximum30 retains disconnected-edge margin','removed':['variant-dependent mask warp','~128 interior alpha noise','forced255 port rectangles','variant-specific decorative edge marks'],'roadPortGeometry':'widths above15.36 share canonical twoHDpixel straight-port neighborhoods and transparent2x2HDcorner guards; disconnected sides remain closed','roadShoulder':'oneHDpixelgeometryring75maxalpha;companionedgeblanktoavoiddoubleborder','textureTreatment':'original master cell resize plus outermost-row/column edge matching only','geometrySource':{'path':str(code),'sha256':hashlib.sha256(code.read_bytes()).hexdigest()},'sources':{},'outputs':{}}
    for name,source,water,bank in [('route-dirt-road','route-materials-4x4.png',False,(72,61,45)),('water-slow-river','water-materials-4x4.png',True,(74,67,53))]:
        path=env/'masters'/source;raw=Image.open(path).convert('RGB');textures=[]
        for i in range(4):
            box=(round(i*raw.width/4),0,round((i+1)*raw.width/4),round(raw.height/4))
            textures.append(raw.crop(box).resize((64,64),Image.Resampling.LANCZOS))
        override=river_material if water else road_material
        if override is not None:
            material=Image.open(override)
            if material.width != material.height or material.width < 64 or material.convert('RGBA').getchannel('A').getextrema() != (255,255):
                raise ValueError('river material must be square opaque PNG at least64px')
            textures=[material.convert('RGB').resize((64,64),Image.Resampling.LANCZOS) for _ in range(4)]
        textures=match_edges(textures)
        report['sources'][name]={'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'row':0,'columns':[0,1,2,3]}
        if override is not None:
            report['sources'][name]={'path':str(override),'sha256':hashlib.sha256(Path(override).read_bytes()).hexdigest(),'variants':'same single generated material; no invented variants'}
        sheets={name:Image.new('RGBA',(1024,256))}
        if not water:sheets['route-edge-dirt-road']=Image.new('RGBA',(1024,256))
        for mask in range(16):
            shape=geometry(mod,mask,water,road_width_logical)
            for variant in range(4):
                i=mask*4+variant;xy=(i%16*64,i//16*64)
                sheets[name].paste(compose(textures[variant],shape,bank,thin=not water),xy)
                if not water:sheets['route-edge-dirt-road'].paste(Image.new('RGBA',(64,64)),xy)
        for atlas,sheet in sheets.items():
            for density,im,suffix in [(2,sheet,'@2x'),(1,sheet.resize((512,128),Image.Resampling.LANCZOS),'')]:
                p=output/f'{atlas}{suffix}.png';im.save(p);report['outputs'][p.name]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'dimensions':im.size,'density':density,'cells':64}
    (output/'provenance.json').write_text(json.dumps(report,indent=2)+'\n');return report

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--river-material',type=Path);p.add_argument('--road-material',type=Path);p.add_argument('--road-width-logical',type=float,default=15.36);a=p.parse_args();print(json.dumps(build(a.root,a.output_dir,a.river_material,a.road_material,a.road_width_logical),indent=2))
