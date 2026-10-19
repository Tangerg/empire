#!/usr/bin/env python3
"""Place generated alpha decoration above ground and below unchanged actors."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image
from assemble_approved_slice import alpha_report

NS='http://www.w3.org/2000/svg'; XLINK='http://www.w3.org/1999/xlink'


def build(config, output):
    data=json.loads(config.read_text());tree=ET.parse(data['baselineSvg']);root=tree.getroot()
    original_children=[ET.tostring(child) for child in root]
    group=ET.Element(f'{{{NS}}}g',{'id':'generated-edge-decoration'})
    report={'kind':'generated-alpha-visual-overlays','gameplayTopologyChanged':False,
        'baselineSha256':hashlib.sha256(Path(data['baselineSvg']).read_bytes()).hexdigest(),
        'placements':[],'pixelOperations':'uniform resize and authored-alpha composition only'}
    for item in data['placements']:
        path=Path(item['source']);image=Image.open(path);qa=alpha_report(image);bbox=qa['visibleBounds']
        extent=bbox[2]-bbox[0] if item['axis']=='width' else bbox[3]-bbox[1]
        scale=item['visibleExtent']/extent;cx,cy=item['center']
        x=cx-(bbox[0]+bbox[2])*.5*scale;y=cy-(bbox[1]+bbox[3])*.5*scale
        attrs={'href':path.resolve().as_uri(),f'{{{XLINK}}}href':path.resolve().as_uri(),
            'x':str(x),'y':str(y),'width':str(image.width*scale),'height':str(image.height*scale)}
        if item.get('flipX'):attrs['transform']=f'translate({2*cx},0) scale(-1,1)'
        if item.get('flipY'):attrs['transform']=f'translate(0,{2*cy}) scale(1,-1)'
        group.append(ET.Element(f'{{{NS}}}image',attrs))
        report['placements'].append({**item,'sourceSha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'visibleSize':[(bbox[2]-bbox[0])*scale,(bbox[3]-bbox[1])*scale],'sourceAlpha':qa})
    # Registered standing assets are direct children; inserting before the first
    # pine keeps decoration beneath bridges, actors and their contact markers.
    index=next(i for i,n in enumerate(list(root)) if n.tag==f'{{{NS}}}image' and 'pine%40' in n.get('href',''))
    root.insert(index,group)
    if [ET.tostring(child) for child in root if child is not group] != original_children:
        raise ValueError('underlying scene changed')
    report['allUnderlyingSvgElementsUnchanged']=True
    output.mkdir(parents=True,exist_ok=True)
    svg=output/'scene.svg';tree.write(svg,encoding='unicode')
    subprocess.run(['inkscape',str(svg),'--export-type=png',f'--export-filename={output/"scene@2x.png"}'],check=True,capture_output=True)
    with Image.open(output/'scene@2x.png') as image:image.resize((448,280),Image.Resampling.LANCZOS).save(output/'scene@1x.png')
    (output/'overlay-provenance.json').write_text(json.dumps(report,indent=2)+'\n')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('config',type=Path);p.add_argument('output',type=Path)
    a=p.parse_args();build(a.config,a.output)
