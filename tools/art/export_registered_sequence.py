#!/usr/bin/env python3
"""Pack a visual sequence with one shared canvas, scale, origin and pivot."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image,ImageDraw
from assemble_approved_slice import alpha_report

NS='http://www.w3.org/2000/svg'


def validate(config):
    names=list(config['frames'])
    if names not in [['idle','walk-a','attack','walk-b'],['normal','damaged','captured']]:
        raise ValueError('explicit semantic frame order required')
    size=None;hashes=[]
    for path in config['frames'].values():
        image=Image.open(path);alpha_report(image)
        if size and size!=image.size:raise ValueError('shared authored canvas required; no independent fit')
        size=image.size;hashes.append(hashlib.sha256(Path(path).read_bytes()).hexdigest())
    if len(set(hashes))!=len(hashes):raise ValueError('duplicate source frames')
    scale=config['scale'];origin=config['origin'];frame=config['frameSize']
    if scale<=0 or any(origin[i]<0 or origin[i]+size[i]*scale>frame[i] for i in (0,1)):
        raise ValueError('full authored canvas must fit at the single shared scale')
    for name in names:
        offset=config.get('registrationOffsets',{}).get(name,[0,0])
        if len(offset)!=2 or any(abs(v)>2 for v in offset):raise ValueError('registration translation limited to two logical pixels')
        if any(origin[i]+offset[i]<0 or origin[i]+offset[i]+size[i]*scale>frame[i] for i in (0,1)):raise ValueError('registration would clip full canvas')
    return size


def export(config_path, output):
    config=json.loads(config_path.read_text());size=validate(config);output.mkdir(parents=True,exist_ok=True)
    scale=config['scale'];origin=config['origin'];fw,fh=config['frameSize'];frames={};reports=[]
    for name,path in config['frames'].items():
        frames[name]={}
        offset=config.get('registrationOffsets',{}).get(name,[0,0])
        for density in (1,2):
            svg=ET.Element(f'{{{NS}}}svg',{'viewBox':f'0 0 {fw} {fh}','width':str(fw*density),'height':str(fh*density)})
            ET.SubElement(svg,f'{{{NS}}}image',{'href':Path(path).resolve().as_uri(),'x':str(origin[0]+offset[0]),'y':str(origin[1]+offset[1]),'width':str(size[0]*scale),'height':str(size[1]*scale)})
            svg_path=output/f'{name}@{density}x.svg';ET.ElementTree(svg).write(svg_path,encoding='unicode')
            png=output/f'{name}@{density}x.png'
            subprocess.run(['inkscape',str(svg_path),'--export-type=png',f'--export-filename={png}'],check=True,capture_output=True)
            image=Image.open(png).convert('RGBA');qa=alpha_report(image)
            if qa['edgeMax']!=0:raise ValueError('packed frame has nontransparent outer edge')
            frames[name][density]=image
            reports.append({'frame':name,'density':density,'source':path,'registrationOffset':offset,'sourceSha256':hashlib.sha256(Path(path).read_bytes()).hexdigest(),'outputAlpha':qa})
    for density in (1,2):
        sheet=Image.new('RGBA',(fw*density*len(frames),fh*density))
        for index,name in enumerate(frames):sheet.paste(frames[name][density],(index*fw*density,0))
        sheet.save(output/f'sheet@{density}x.png')
    order=['idle','walk-a','walk-b','walk-a','walk-b','idle','attack','idle'] if 'idle' in frames else list(frames)
    gif_frames=[]
    for name in order:
        canvas=Image.new('RGBA',(fw*2,fh*2),'#66764a');canvas.alpha_composite(frames[name][2]);gif_frames.append(canvas.convert('RGB'))
    gif_frames[0].save(output/'preview.gif',save_all=True,append_images=gif_frames[1:],duration=180,loop=0,disposal=2)
    report={'config':config,'authoredCanvas':size,'logicalPivot':[origin[i]+config['sourceFoot'][i]*scale for i in (0,1)],'frames':reports,'independentFrameFit':False,'gameplayFootprintChanged':False,'semanticMotionReview':'required separately; distinct hashes alone are insufficient','liveGamePlayback':'not performed'}
    (output/'sequence.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('config',type=Path);p.add_argument('output',type=Path);a=p.parse_args();export(a.config,a.output)
