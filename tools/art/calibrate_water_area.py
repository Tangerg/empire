#!/usr/bin/env python3
"""Calibration-only area water over explicitly supplied, unchanged water cells."""
import argparse,hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
from export_surface_overrides import match_edges
NEIGHBORS=[(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)]

def water_area_mask(size,mask8):
    if not 0<=mask8<=255 or size not in (32,64):raise ValueError('expected8bit mask and32/64 tile')
    scale=4;s=size*scale;inset=round(s*.10)
    n,ne,e,se,south,sw,w,nw=[bool(mask8&(1<<i)) for i in range(8)]
    out=Image.new('L',(s,s));d=ImageDraw.Draw(out)
    # Full shared edges; inset only at exposed shores. No cross-cell painting.
    d.rectangle((0 if w else inset,0 if n else inset,s-1 if e else s-inset-1,s-1 if south else s-inset-1),fill=255)
    # Preserve a concave land notch only where two cardinal water neighbors
    # meet a genuinely absent diagonal. This never hollows a2xN interior.
    for a,b,diagonal,cx,cy in [(n,w,nw,0,0),(n,e,ne,s,0),(south,e,se,s,s),(south,w,sw,0,s)]:
        if a and b and not diagonal:d.ellipse((cx-inset,cy-inset,cx+inset,cy+inset),fill=0)
    return out.resize((size,size),Image.Resampling.LANCZOS)

def render_water_area(material,occupied,size=64,map_size=None):
    cells={tuple(p) for p in occupied}
    if not cells or any(len(p)!=2 or any(type(v)!=int for v in p) for p in cells):raise ValueError('integer occupied cells required')
    source=Image.open(material)
    if source.width!=source.height or source.width<64 or source.convert('RGBA').getchannel('A').getextrema()!=(255,255):raise ValueError('square opaque material required')
    tile=match_edges([source.convert('RGB').resize((size,size),Image.Resampling.LANCZOS) for _ in range(4)])[0]
    left=min(x for x,y in cells);top=min(y for x,y in cells);right=max(x for x,y in cells)+1;bottom=max(y for x,y in cells)+1
    if map_size is not None:
        if len(map_size)!=2 or any(type(v)!=int or v<=0 for v in map_size) or any(x<0 or y<0 or x>=map_size[0] or y>=map_size[1] for x,y in cells):raise ValueError('cells outside declared map')
        left=top=0;right,bottom=map_size
    out=Image.new('RGBA',((right-left)*size,(bottom-top)*size));placements=[]
    for x,y in sorted(cells,key=lambda p:(p[1],p[0])):
        mask=sum(1<<i for i,(dx,dy) in enumerate(NEIGHBORS) if (x+dx,y+dy) in cells)
        cell=tile.convert('RGBA');cell.putalpha(water_area_mask(size,mask));out.paste(cell,((x-left)*size,(y-top)*size));placements.append({'cell':[x,y],'mask8':mask})
    return out,{'kind':'calibration-only-area-water','worldOriginCells':[left,top],'logicalTileSize':32,'physicalTileSize':size,'occupiedCells':placements,'sourceSha256':hashlib.sha256(Path(material).read_bytes()).hexdigest(),'geometry':'fullsharedinteriors;10percentinsetonlyexposedshores;concavediagonalnotches','changesGameplayCells':False,'outsideOccupiedCellsAlpha':0}

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--material',type=Path,required=True);p.add_argument('--cells-json',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--map-size',type=int,nargs=2);a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True)
    evidence=json.loads(a.cells_json.read_text())
    cells=[[c['x'],c['y']] for c in evidence['waterAndBridgeCells']] if isinstance(evidence,dict) else evidence
    if isinstance(evidence,dict):
        declared=[evidence['mapWidth'],evidence['mapHeight']]
        if a.map_size is not None and a.map_size!=declared:raise ValueError('map size conflicts with actual extracted evidence')
        a.map_size=declared
    for size in (32,64):
        im,r=render_water_area(a.material,cells,size,a.map_size);name=f'water-area-{size}.png';im.save(a.output_dir/name);r['cellsEvidenceSha256']=hashlib.sha256(a.cells_json.read_bytes()).hexdigest();r['outputSha256']=hashlib.sha256((a.output_dir/name).read_bytes()).hexdigest();(a.output_dir/f'water-area-{size}.json').write_text(json.dumps(r,indent=2)+'\n')
