#!/usr/bin/env python3
"""Export registered transparent cutouts and substitute only their baseline SVG images."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from PIL import Image

NS = 'http://www.w3.org/2000/svg'
XLINK = 'http://www.w3.org/1999/xlink'


def alpha_report(image):
    if image.mode != 'RGBA':
        raise ValueError('A genuine RGBA source is required')
    alpha = image.getchannel('A')
    histogram = alpha.histogram()
    edge = max(max(alpha.crop(box).getdata()) for box in
        [(0,0,image.width,1),(0,image.height-1,image.width,image.height),
         (0,0,1,image.height),(image.width-1,0,image.width,image.height)])
    if not histogram[0] or sum(histogram[240:]) == 0 or edge > 1:
        raise ValueError(f'Invalid source transparency: perimeter maximum {edge}')
    return {'alphaRange':alpha.getextrema(),'transparentPixels':histogram[0],
        'nearOpaquePixels':sum(histogram[240:]),'edgeMax':edge,
        'visibleBounds':alpha.point(lambda value:255 if value >= 128 else 0).getbbox()}


def build(config_path, output):
    config = json.loads(config_path.read_text())
    output.mkdir(parents=True, exist_ok=True)
    tree = ET.parse(config['baselineSvg'])
    report = {'kind':'transparent-assets-in-staged-fixture-not-live-gameplay',
        'baselineSvgSha256':hashlib.sha256(Path(config['baselineSvg']).read_bytes()).hexdigest(),
        'config':config,'assets':{},'placements':[],'fullAnimationComplete':False}
    for identity, spec in config['assets'].items():
        source = Path(spec['source'])
        image = Image.open(source)
        qa = alpha_report(image)
        scale = spec['logicalPerSourcePixel']
        if scale <= 0 or not all(0 <= spec['sourceFoot'][axis] <= image.size[axis] for axis in (0,1)):
            raise ValueError('Invalid scale or source registration point')
        # Retain the entire authored canvas. Uniform SVG scaling corrects rounding.
        logical = [image.width*scale,image.height*scale]
        for density in (1,2):
            size = [max(1,round(value*density)) for value in logical]
            image.resize(size,Image.Resampling.LANCZOS).save(output/f'{identity}@{density}x.png')
        matches = 0
        for node in tree.getroot().iter(f'{{{NS}}}image'):
            href = node.get('href') or node.get(f'{{{XLINK}}}href')
            if href != Path(spec['baselineSource']).as_uri(): continue
            if 'mirroredOnly' in spec and bool(node.get('transform')) != spec['mirroredOnly']: continue
            old_scale = float(node.get('width'))/spec['baselineCanvas'][0]
            point = [float(node.get('x'))+spec['baselineFoot'][0]*old_scale,
                     float(node.get('y'))+spec['baselineFoot'][1]*old_scale]
            for axis,key in enumerate(('x','y')): node.set(key,str(point[axis]-spec['sourceFoot'][axis]*scale))
            node.set('width',str(logical[0])); node.set('height',str(logical[1]))
            href = (output/f'{identity}@2x.png').resolve().as_uri()
            node.set('href',href); node.set(f'{{{XLINK}}}href',href)
            report['placements'].append({'identity':identity,'pointWorld':point,'transform':node.get('transform')})
            matches += 1
        if matches != spec['expectedInstances']:
            raise ValueError(f'{identity}: expected {spec["expectedInstances"]}, found {matches}')
        report['assets'][identity] = {'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
            'sourceSize':image.size,'alpha':qa,'logicalCanvas':logical,'sourceFoot':spec['sourceFoot'],
            'pivotLogical':[value*scale for value in spec['sourceFoot']],'instances':matches}
    svg = output/'scene.svg'; tree.write(svg,encoding='unicode')
    subprocess.run(['inkscape',str(svg),'--export-type=png',f'--export-filename={output/"scene@2x.png"}'],check=True,capture_output=True)
    with Image.open(output/'scene@2x.png') as image:
        image.resize((448,280),Image.Resampling.LANCZOS).save(output/'scene@1x.png')
    (output/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('config',type=Path)
    parser.add_argument('output',type=Path)
    args = parser.parse_args()
    build(args.config,args.output.resolve())
