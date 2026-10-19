"""Produce a fixed-camera scale comparison and separate camera comparison."""
import argparse
import json
from itertools import combinations
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

NAME='selected-layer-area-water-bridge-bank'


def rectangle_overlap(a,b):
    return max(0,min(a[2],b[2])-max(a[0],b[0])) * max(0,min(a[3],b[3])-max(a[1],b[1]))


def build(source, output):
    output.mkdir(parents=True,exist_ok=True)
    reports={key:json.loads((source/f'scene-proportion-{key}'/'registration-and-provenance.json').read_text())[0] for key in ('S0','P1','H1','P1-camera')}
    baseline=reports['S0']
    anchor_fields=('identity','pointWorld','sourceFoot','sourceSha256')
    anchors=lambda report:[{key:item[key] for key in anchor_fields} for item in report['substitutions']]
    for key,report in reports.items():
        if anchors(report)!=anchors(baseline): raise ValueError(f'object placement/source changed in {key}')
        if report['groundStudy']!=baseline['groundStudy']: raise ValueError(f'ground changed in {key}')
        if key!='P1-camera' and report['viewBox']!=baseline['viewBox']: raise ValueError('scale comparison camera changed')
    measurements={}
    for key,report in reports.items():
        objects=report['substitutions']; camera=report['viewBox'];view=[camera[0],camera[1],camera[0]+camera[2],camera[1]+camera[3]]
        overlaps=[]
        for (i,a),(j,b) in combinations(enumerate(objects),2):
            area=rectangle_overlap(a['visibleWorldBounds'],b['visibleWorldBounds'])
            if area: overlaps.append({'indices':[i,j],'identities':[a['identity'],b['identity']],'boundingRectangleIntersectionArea':area})
        measurements[key]={'config':report['proportionStudy'],'camera':camera,'objects':objects,'visualBoundingBoxOverlaps':overlaps,'viewportClippedObjects':[i for i,item in enumerate(objects) if rectangle_overlap(item['visibleWorldBounds'],view)<rectangle_overlap(item['visibleWorldBounds'],item['visibleWorldBounds'])-1e-6]}
    payload={'scope':'presentation sizes only; terrain cells, occupancy, collision and object standing points unchanged','overlapCaveat':'rectangle intersections are visual warnings, not measured opaque-pixel overlap or gameplay collisions','sets':measurements}
    (output/'proportion-review.json').write_text(json.dumps(payload,indent=2)+'\n')
    font=ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc',18)
    def board(keys, labels, filename):
        canvas=Image.new('RGB',(640*len(keys),566),'#ebe9df'); draw=ImageDraw.Draw(canvas)
        for i,(key,label) in enumerate(zip(keys,labels)):
            draw.text((640*i+12,8),label,font=font,fill='#202820')
            im=Image.open(source/f'scene-proportion-{key}'/f'{NAME}-native.png').convert('RGBA');back=Image.new('RGBA',im.size,'#303b35');back.alpha_composite(im);canvas.paste(back.convert('RGB'),(640*i,44))
        canvas.save(output/filename)
    board(('S0','P1','H1'),('S0 当前比例：人34 / 树90 / 堡128 / 路15','P1 均衡：人28 / 树76 / 堡144 / 路22','H1 建筑主次：人34 / 树70 / 堡160 / 路30（原拟32）'),'proportion-three-sets.png')
    board(('P1','P1-camera'),('P1 原镜头：物件比例不变','P1 收紧镜头：仅视野变化，约1.14倍'),'proportion-camera-only.png')
    return payload


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--source',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args();build(args.source,args.output)
