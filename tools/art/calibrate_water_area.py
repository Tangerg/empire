#!/usr/bin/env python3
"""Calibration-only area water over explicitly supplied, unchanged water cells."""
import argparse,hashlib,json
from pathlib import Path
from PIL import Image
from water_area import water_area_mask, water_period
NEIGHBORS=[(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)]

def render_water_area(material,occupied,size=64,map_size=None,uv_cells=4):
    cells={tuple(p) for p in occupied}
    if not cells or any(len(p)!=2 or any(type(v)!=int for v in p) for p in cells):raise ValueError('integer occupied cells required')
    source=Image.open(material)
    if source.width!=source.height or source.width<64 or source.convert('RGBA').getchannel('A').getextrema()!=(255,255):raise ValueError('square opaque material required')
    if type(uv_cells)!=int or uv_cells<1:raise ValueError('positive integer UV period required')
    period=size*uv_cells
    tile=water_period(source,period)
    left=min(x for x,y in cells);top=min(y for x,y in cells);right=max(x for x,y in cells)+1;bottom=max(y for x,y in cells)+1
    if map_size is not None:
        if len(map_size)!=2 or any(type(v)!=int or v<=0 for v in map_size) or any(x<0 or y<0 or x>=map_size[0] or y>=map_size[1] for x,y in cells):raise ValueError('cells outside declared map')
        left=top=0;right,bottom=map_size
    out=Image.new('RGBA',((right-left)*size,(bottom-top)*size));placements=[]
    for x,y in sorted(cells,key=lambda p:(p[1],p[0])):
        mask=sum(1<<i for i,(dx,dy) in enumerate(NEIGHBORS) if (x+dx,y+dy) in cells)
        u=(x%uv_cells)*size;v=(y%uv_cells)*size
        cell=tile.crop((u,v,u+size,v+size)).convert('RGBA');cell.putalpha(water_area_mask(size,mask));out.paste(cell,((x-left)*size,(y-top)*size));placements.append({'cell':[x,y],'mask8':mask})
    return out,{'kind':'calibration-only-area-water','worldOriginCells':[left,top],'logicalTileSize':32,'physicalTileSize':size,'occupiedCells':placements,'textureUVPeriodCells':uv_cells,'UVOriginWorldCells':[0,0],'sourceSha256':hashlib.sha256(Path(material).read_bytes()).hexdigest(),'geometry':'fullsharedinteriors;10percentinsetonlyexposedshores;concavediagonalnotches','changesGameplayCells':False,'outsideOccupiedCellsAlpha':0}

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
