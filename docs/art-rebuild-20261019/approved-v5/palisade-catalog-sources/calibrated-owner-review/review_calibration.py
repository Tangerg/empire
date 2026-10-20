from pathlib import Path
import sys,json
from PIL import Image,ImageDraw
r=Path.cwd();sys.path.insert(0,str(r/'tools/art'));from export_environment_overrides import render_cell
b=r/'docs/art-rebuild-20261019/approved-v5/palisade-catalog-sources';d=json.loads((b/'proposed-registration.json').read_text());out=b/'calibrated-owner-review';out.mkdir(exist_ok=True);sheet=Image.new('RGB',(1000,4*240),'#ddd');dr=ImageDraw.Draw(sheet);records=[]
for i,a in enumerate(d['entries']):
 source=Image.open(r/a['source']).convert('RGBA');reg={'scale':92/max(source.size),'origin':[2,2],'sourceFoot':a['measuredSourceFoot']};native,hd,anchor=render_cell(source,reg);native.save(out/(a['id']+'-96.png'));hd.save(out/(a['id']+'-192.png'))
 rec={'id':a['id'],'source':a['source'],'sha256':a['sha256'],'registration':reg,'derivedContactAnchor':anchor,'nativeAlpha128Bbox':native.getchannel('A').point(lambda v:255 if v>=128 else 0).getbbox()};records.append(rec);dr.text((0,i*240),a['id']+' calibrated sourceFoot '+str(anchor),fill='black')
 for j,bg in enumerate(['white','black','#777e4d']):
  for im,x in [(native,0),(hd,110)]:
   frame=Image.new('RGBA',im.size,bg);frame.alpha_composite(im);sheet.paste(frame.convert('RGB'),(j*330+x,i*240+28))
sheet.save(out/'native-white-black-olive.png');(out/'registration-review.json').write_text(json.dumps(records,indent=2)+'\n')
