"""Approved reusable NESW connector masks and thin-shoulder composition."""
import importlib.util
import math
from PIL import Image, ImageChops, ImageFilter
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


