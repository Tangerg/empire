#!/usr/bin/env python3
"""Pack a visual sequence with one shared canvas, scale, origin and pivot."""
import argparse,hashlib,json,subprocess,math
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image,ImageDraw
from assemble_approved_slice import alpha_report

NS='http://www.w3.org/2000/svg'


def validate(config):
    names=list(config['frames'])
    if names not in [['idle','walk-a','attack','walk-b'],['idle','walk-a','carry-or-work','walk-b'],['normal','damaged','captured']]:
        raise ValueError('explicit semantic frame order required')
    size=None;hashes=[]
    for path in config['frames'].values():
        image=Image.open(path);alpha_report(image)
        tolerance=config.get('canvasPaddingTolerance',0)
        if tolerance not in (0,1,2):raise ValueError('only explicit up-to-two-pixel canvas padding is supported')
        if size and any(abs(size[i]-image.size[i])>tolerance for i in (0,1)):raise ValueError('shared authored canvas required; no independent fit')
        size=tuple(max(size[i],image.size[i]) for i in (0,1)) if size else image.size;hashes.append(hashlib.sha256(Path(path).read_bytes()).hexdigest())
    if len(set(hashes))!=len(hashes):raise ValueError('duplicate source frames')
    scale=config['scale'];origin=config['origin'];frame=config['frameSize']
    if not math.isfinite(scale) or len(origin)!=2 or len(frame)!=2 or not all(math.isfinite(v) for v in origin):raise ValueError('finite scale and two-dimensional origin/frame required')
    if any(type(v) is not int or v<=0 for v in frame):raise ValueError('positive integer frame dimensions required')
    if scale<=0 or any(origin[i]<0 or origin[i]+size[i]*scale>frame[i] for i in (0,1)):
        raise ValueError('full authored canvas must fit at the single shared scale')
    limit=config.get('registrationLimit',2)
    if not math.isfinite(limit) or not 0<=limit<=16:raise ValueError('explicit registration limit must be between zero and sixteen logical pixels')
    if limit>4:
        feet=config.get('sourceFeet')
        if not isinstance(feet,dict) or set(feet)!=set(names):raise ValueError('larger registration requires measured source feet for every pose')
        for name in names:
            foot=feet[name]
            if len(foot)!=2 or any(not math.isfinite(v) or v<0 or v>size[i] for i,v in enumerate(foot)):raise ValueError('invalid measured source foot')
            expected=[(config['sourceFoot'][i]-foot[i])*scale for i in (0,1)]
            actual=config.get('registrationOffsets',{}).get(name,[0,0])
            if any(abs(expected[i]-actual[i])>1e-8 for i in (0,1)):raise ValueError('registration must match measured source foot')
    for name in names:
        offset=config.get('registrationOffsets',{}).get(name,[0,0])
        if len(offset)!=2 or any(not math.isfinite(v) or abs(v)>limit for v in offset):raise ValueError('registration translation exceeds recorded logical-pixel limit')
        if any(origin[i]+offset[i]<0 or origin[i]+offset[i]+size[i]*scale>frame[i] for i in (0,1)):raise ValueError('registration would clip full canvas')
    return size


def export(config_path, output):
    config=json.loads(config_path.read_text());size=validate(config);output.mkdir(parents=True,exist_ok=True)
    scale=config['scale'];origin=config['origin'];fw,fh=config['frameSize'];frames={};reports=[]
    for name,path in config['frames'].items():
        frames[name]={}
        offset=config.get('registrationOffsets',{}).get(name,[0,0])
        source_size=Image.open(path).size
        for density in (1,2):
            svg=ET.Element(f'{{{NS}}}svg',{'viewBox':f'0 0 {fw} {fh}','width':str(fw*density),'height':str(fh*density)})
            ET.SubElement(svg,f'{{{NS}}}image',{'href':Path(path).resolve().as_uri(),'x':str(origin[0]+offset[0]),'y':str(origin[1]+offset[1]),'width':str(source_size[0]*scale),'height':str(source_size[1]*scale)})
            svg_path=output/f'{name}@{density}x.svg';ET.ElementTree(svg).write(svg_path,encoding='unicode')
            png=output/f'{name}@{density}x.png'
            subprocess.run(['inkscape',str(svg_path),'--export-type=png',f'--export-filename={png}'],check=True,capture_output=True)
            image=Image.open(png).convert('RGBA');qa=alpha_report(image)
            if qa['edgeMax']!=0:raise ValueError('packed frame has nontransparent outer edge')
            frames[name][density]=image
            reports.append({'frame':name,'density':density,'source':path,'registrationOffset':offset,'sourceCanvas':source_size,'transparentPaddingToCommonCanvas':[size[i]-source_size[i] for i in (0,1)],'sourceSha256':hashlib.sha256(Path(path).read_bytes()).hexdigest(),'outputAlpha':qa})
    for density in (1,2):
        sheet=Image.new('RGBA',(fw*density*len(frames),fh*density))
        for index,name in enumerate(frames):sheet.paste(frames[name][density],(index*fw*density,0))
        sheet.save(output/f'sheet@{density}x.png')
    action='carry-or-work' if 'carry-or-work' in frames else 'attack'
    order=['idle','walk-a','walk-b','walk-a','walk-b','idle',action,'idle'] if 'idle' in frames else list(frames)
    gif_frames=[]
    for name in order:
        canvas=Image.new('RGBA',(fw*2,fh*2),'#66764a');canvas.alpha_composite(frames[name][2]);gif_frames.append(canvas.convert('RGB'))
    gif_frames[0].save(output/'preview.gif',save_all=True,append_images=gif_frames[1:],duration=180,loop=0,disposal=2)
    report={'config':config,'authoredCanvas':size,'logicalPivot':[origin[i]+config['sourceFoot'][i]*scale for i in (0,1)],'frames':reports,'independentFrameFit':False,'gameplayFootprintChanged':False,'semanticMotionReview':'required separately; distinct hashes alone are insufficient','liveGamePlayback':'not performed'}
    (output/'sequence.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('config',type=Path);p.add_argument('output',type=Path);a=p.parse_args();export(a.config,a.output)
