#!/usr/bin/env python3
"""Material-only SVG substitutions using existing road/water/shore masks."""
import argparse, hashlib, json, subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
from urllib.parse import unquote
from PIL import Image
from export_transition_overrides import replace_mask_material

NS='http://www.w3.org/2000/svg'
XLINK='http://www.w3.org/1999/xlink'


def build(scene, grass, earth, connectors, water, output):
    output.mkdir(parents=True,exist_ok=True)
    tree=ET.parse(scene); replacements=[]; shore_outputs={}
    for node in tree.getroot().iter(f'{{{NS}}}image'):
        old=Path(unquote((node.get('href') or node.get(f'{{{XLINK}}}href')).removeprefix('file://')))
        target=None
        if old.parent.name=='ground-base': target=grass
        elif old.name in ('route-dirt-road@2x.png','route-edge-dirt-road@2x.png'): target=connectors/old.name
        elif old.name=='water-area-64.png': target=water
        elif old.name=='transition-meadow-earth@2x.png':
            if old not in shore_outputs:
                baseline=Image.open(old); source=Image.open(earth)
                changed,_,_=replace_mask_material(baseline,source,64)
                if changed.getchannel('A').tobytes()!=baseline.getchannel('A').tobytes(): raise ValueError('shore mask changed')
                target=output/old.name;changed.save(target)
                shore_outputs[old]=target
            target=shore_outputs[old]
        if target:
            node.set('href',target.resolve().as_uri());node.set(f'{{{XLINK}}}href',target.resolve().as_uri())
            replacements.append({'old':str(old),'new':str(target),'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
    expected={'ground':sum(Path(r['old']).parent.name=='ground-base' for r in replacements),
              'shore':sum(Path(r['old']).name=='transition-meadow-earth@2x.png' for r in replacements)}
    if expected!={'ground':1,'shore':28}: raise ValueError(f'unexpected scene material contract: {expected}')
    out=output/'scene.svg';tree.write(out,encoding='unicode')
    subprocess.run(['inkscape',str(out),'--export-type=png',f'--export-filename={output/"scene@2x.png"}'],check=True,capture_output=True)
    with Image.open(output/'scene@2x.png') as image: image.resize((448,280),Image.Resampling.LANCZOS).save(output/'scene@1x.png')
    (output/'material-provenance.json').write_text(json.dumps({'baselineSvg':str(scene),'replacements':replacements,'shoreAlphaUnchanged':True,'gameplayTopologyUnchanged':True,'unchangedLayers':['grass-mass patches','single bank cutout','actor and object positions','all clipping geometry'],'kind':'static layered scene, not gameplay capture'},indent=2)+'\n')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('scene','grass','earth','connectors','water','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();build(a.scene,a.grass,a.earth,a.connectors,a.water,a.output)
