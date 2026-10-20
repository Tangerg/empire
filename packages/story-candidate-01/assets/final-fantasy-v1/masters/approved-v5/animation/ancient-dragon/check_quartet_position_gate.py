from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,re,xml.etree.ElementTree as ET
B=Path(__file__).parent;R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');d=json.load(open(B/'position-gate-actual.json'));density=4;W,H=220,160
actors=[]
for a in d['actors']:
 s=a['picture']['strip'];im=Image.open(R/s['href'].split('?')[0].lstrip('/')).convert('RGBA');fw=im.width//s['frameCount'];im=im.crop((0,0,fw,im.height));a.update(image=im,origin=[a['cell'][0]*32+s['x'],a['cell'][1]*32+s['y']],ratio=fw/s['frameWidth'],name=f"{a['type']}#{a['id']}@{a['cell']}");actors.append(a)
structures=[]
for layer in d['layers']:
 for i,p in enumerate(layer['pieces']):
  if 'data-candidate-art="structure"' not in p['markup']:continue
  root=ET.fromstring('<g>'+p['markup']+'</g>');g=root.find('g');tr=list(map(float,re.findall(r'[-+]?(?:\d*\.\d+|\d+)',g.attrib['transform'])));tx,ty,sc=tr;svg=g.find('svg');img=svg.find('image');im=Image.open(R/img.attrib['href'].split('?')[0].lstrip('/')).convert('RGBA');ratio=im.width/float(img.attrib['width']);x=-float(img.attrib['x'])*ratio;y=-float(img.attrib['y'])*ratio;fw=float(svg.attrib['width'])*ratio;fh=float(svg.attrib['height'])*ratio;im=im.crop((round(x),round(y),round(x+fw),round(y+fh)));foot=[p['x']+p['foot']['x'],p['y']+p['foot']['y']];structures.append({'name':Path(img.attrib['href']).stem+f"@{[p['x']/32,p['y']/32]}",'image':im,'origin':[p['x']+tx,p['y']+ty],'ratio':ratio/sc,'worldFoot':foot,'cell':[p['x']/32,p['y']/32]})
def place(ob,origin):
 ratio=ob['ratio'];sx,sy=ob['origin'];ox,oy=origin
 return ob['image'].transform((W*density,H*density),Image.Transform.AFFINE,(ratio/density,0,(ox-sx)*ratio,0,ratio/density,(oy-sy)*ratio),Image.Resampling.BICUBIC)
reg=json.load(open(B/'registration-evidence.json')); reports=[];sheet=Image.new('RGB',(W*5*2,(H+30)*2*2),'#e9e6dc');dr=ImageDraw.Draw(sheet)
for row,dragon in enumerate(a for a in actors if a['type']=='dragon'):
 foot=dragon['worldFoot'];origin=[foot[0]-110,foot[1]-115];near=[a for a in actors+structures if abs(a['worldFoot'][0]-foot[0])<150 and abs(a['worldFoot'][1]-foot[1])<140];item={'actorId':dragon['id'],'cell':dragon['cell'],'worldFoot':foot,'facing':dragon['facing'],'neighborActors':[{'name':a['name'],'cell':a['cell']} for a in near if a in actors and a['id']!=dragon['id']],'nearStructures':[{'name':a['name'],'cell':a['cell'],'worldFoot':a['worldFoot']} for a in near if a in structures],'variants':{}}
 for col,v in enumerate(['legacy','idle','walk-a','attack','walk-b']):
  drg=dragon.copy()
  if v!='legacy':
   s=64/940;f=reg['poses'][v]['sourceSupportAnchorEstimated'];drg.update(image=Image.open(B/v/'master.png').convert('RGBA'),ratio=1/s,origin=[foot[0]-f[0]*s,foot[1]-f[1]*s])
  placed=place(drg,origin);mask=np.array(placed.getchannel('A'))>127;ys,xs=np.where(mask);rep={'visibleWorldBounds':[xs.min()/density+origin[0],ys.min()/density+origin[1],(xs.max()+1)/density+origin[0],(ys.max()+1)/density+origin[1]],'overlaps':[]};canvas=Image.new('RGBA',placed.size,'#e9e6dc');draw=ImageDraw.Draw(canvas)
  for x in range(-5,6):
   xx=(110+x*32)*density;draw.line((xx,0,xx,H*density),fill='#d6d2c7')
  for y in range(-5,6):
   yy=(115+y*32)*density;draw.line((0,yy,W*density,yy),fill='#d6d2c7')
  for a in sorted(near,key=lambda a:(a['worldFoot'][1],a['worldFoot'][0])):
   pix=placed if a is dragon else place(a,origin)
   if a is not dragon:
    amask=np.array(pix.getchannel('A'))>127;intersection=int((mask&amask).sum())
    if intersection:
     front=(foot[1],foot[0])>(a['worldFoot'][1],a['worldFoot'][0]);rep['overlaps'].append({'target':a['name'],'areaLogical':intersection/density**2,'targetVisibleAlphaAreaFraction':intersection/int(amask.sum()),'dragonDrawsInFront':front})
   canvas.alpha_composite(pix)
  item['variants'][str(v)]=rep
  sheet.paste(canvas.convert('RGB').resize((W*2,H*2),Image.Resampling.LANCZOS),(col*W*2,row*(H+30)*2+60));dr.text((col*W*2+8,row*(H+30)*2+5),f"{dragon['cell']} | {v} | 2x analytical neighborhood",fill='black')
 reports.append(item)
result={'method':'Read-only actual engine-created Three Bridges state, actual CANDIDATE_01_ART pictures and structure transforms. Actor/structure alpha overlap at4 samples per logicalpixel, alpha>127. Full source canvases and actual pivots; no fitting. Both dragons actually face south and are NOT mirrored. No Inkscape or full-scene raster.','limitations':'Neighborhood diagram excludes scenery/trees, badge/ring/shadows and terrain materials. It is placement/overlap evidence only, NOT full-scene acceptance or live renderer validation. Original full-scene occlusion is not altered or claimed resolved.','reports':reports}
(B/'quartet-position-gate-report.json').write_text(json.dumps(result,indent=2));sheet.save(B/'quartet-position-gate-neighborhoods-2x.png');print(json.dumps(result,indent=2))
