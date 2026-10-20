from PIL import Image,ImageDraw
from pathlib import Path
import numpy as np,json,hashlib,math
p=Path(__file__).parent;s=54/880.5;origin=(14,6);native=(1586,992);poses=['idle','walk-a','attack','walk-b'];rois=json.loads((p/'foot-rois.json').read_text());planted={'idle':['near-hind','far-hind','near-fore'],'walk-a':['far-hind','near-fore'],'walk-b':['near-hind','far-fore'],'attack':['near-hind','far-hind','near-fore','far-fore']}
rows={}
for pose in poses:
 im=Image.open(p/pose/'master.png').convert('RGBA');a=np.array(im.getchannel('A'));feet=[]
 for name,(x0,y0,x1,y1) in rois[pose].items():
  yy,xx=np.where(a[y0:y1,x0:x1]>127);bottom=int(yy.max()+y0);xs=np.where(a[bottom-3:bottom+1,x0:x1]>127)[1]+x0;feet.append({'foot':name,'roi':[x0,y0,x1,y1],'soleY':bottom,'bottomFourRowsMeanX':float(xs.mean()),'planted':name in planted[pose]})
 support=[f for f in feet if f['planted']];contactX=float(np.mean([f['bottomFourRowsMeanX'] for f in support]));contactY=max(f['soleY'] for f in support)
 q={'nativeSize':list(im.size),'nativeSha256':hashlib.sha256((p/pose/'master.png').read_bytes()).hexdigest(),'rawByteIdentical':(p/pose/'raw.png').read_bytes()==(p/pose/'master.png').read_bytes(),'perimeterAlphaMax':int(max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())),'boundsAlpha128':im.getchannel('A').point(lambda v:255 if v>=128 else 0).getbbox(),'padRight':native[0]-im.width,'padBottom':native[1]-im.height,'feet':feet,'plantedContactCentroid':[contactX,contactY]}
 rows[pose]=q
base=rows['idle']['plantedContactCentroid'];canonical=[900,981]
for pose in poses:
 q=rows[pose];raw=q['plantedContactCentroid'];q['sourceSupportAnchor']=[canonical[0]+raw[0]-base[0],raw[1]];q['registrationOffsetLogical']=[(canonical[0]-q['sourceSupportAnchor'][0])*s,(canonical[1]-q['sourceSupportAnchor'][1])*s];q['fullCanvasBoundsLogical']=[origin[0]+q['registrationOffsetLogical'][0],origin[1]+q['registrationOffsetLogical'][1],origin[0]+q['registrationOffsetLogical'][0]+native[0]*s,origin[1]+q['registrationOffsetLogical'][1]+native[1]*s]
 (p/pose/'alpha-qa.json').write_text(json.dumps(q,indent=2))
 im=Image.open(p/pose/'master.png').convert('RGBA')
 for bgname,bg in [('black',(25,25,25,255)),('white',(255,255,255,255)),('olive',(94,106,73,255))]:
  out=Image.new('RGBA',im.size,bg);out.alpha_composite(im);out.convert('RGB').save(p/pose/f'qa-{bgname}.png')
reg={'sharedScale':s,'scaleFraction':'54/880.5','canonicalSourceFoot':canonical,'commonSourceCanvas':list(native),'frameSize':[128,80],'origin':list(origin),'sourceFootMethod':'Preserve approved idle reference pivot[900,981]. Measure actual planted paw bottom-four-row centers, exclude raised paws from contact set, and use changes in planted-contact mean relative to idle. Constant idle calibration900-minus-idle-contact-x applies to every pose. Y is lowest planted claw. This prevents a lifted foot from pulling grounded registration.','idleActualPlantedContactCentroid':base,'constantSourceXCalibration':canonical[0]-base[0],'registrationPolicy':'Uniform54/880.5 full native canvas for all poses; only transparent right padding1px for walkA/B and measured whole-canvas translation. No per-pose fit, trim, art edits or alpha cleanup.','poses':rows,'maximumAbsoluteRegistrationLogical':max(abs(v) for q in rows.values() for v in q['registrationOffsetLogical']),'status':'Planted-support method and final quartet pending coordinator review'}
(p/'registration-evidence.json').write_text(json.dumps(reg,indent=2))
(p/'export-preview').mkdir(exist_ok=True)
for density in [1,2]:
 sheet=Image.new('RGBA',(512*density,80*density))
 for i,pose in enumerate(poses):
  im=Image.open(p/pose/'master.png').convert('RGBA');padded=Image.new('RGBA',native);padded.alpha_composite(im,(0,0));off=rows[pose]['registrationOffsetLogical'];ds=s*density;ox=(origin[0]+off[0])*density;oy=(origin[1]+off[1])*density
  out=padded.transform((128*density,80*density),Image.Transform.AFFINE,(1/ds,0,-ox/ds,0,1/ds,-oy/ds),resample=Image.Resampling.BICUBIC);out.save(p/'export-preview'/f'{pose}-{density}x.png');sheet.alpha_composite(out,(i*128*density,0))
 sheet.save(p/'export-preview'/f'quartet-{density}x.png')
 for bgname,bg in [('black',(25,25,25,255)),('white',(255,255,255,255)),('olive',(94,106,73,255))]:
  panel=Image.new('RGBA',(512*density,102*density),bg);panel.alpha_composite(sheet);dr=ImageDraw.Draw(panel)
  for i,pose in enumerate(poses):dr.text(((i*128+30)*density,85*density),pose,fill=(225,225,225) if bgname=='black' else (25,25,25))
  panel.save(p/f'qa-logical-quartet-{bgname}-{density}x.png')
  if density==1:panel.resize((2048,408),Image.Resampling.NEAREST).save(p/f'qa-logical-quartet-{bgname}-4x.png')
for bgname,bg in [('white','white'),('black',(25,25,25))]:
 panel=Image.new('RGB',(3172,2040),bg);d=ImageDraw.Draw(panel)
 for i,pose in enumerate(poses):
  im=Image.open(p/pose/f'qa-{bgname}.png');x=(i%2)*1586;y=(i//2)*1020;panel.paste(im,(x,y));d.text((x+30,y+995),pose,fill='gray')
 panel.save(p/f'qa-native-quartet-{bgname}.jpg',quality=94)
print(json.dumps({'maximumOffset':reg['maximumAbsoluteRegistrationLogical'],'poses':{k:{'contact':v['plantedContactCentroid'],'effective':v['sourceSupportAnchor'],'offset':v['registrationOffsetLogical'],'perimeter':v['perimeterAlphaMax'],'size':v['nativeSize']} for k,v in rows.items()}},indent=2))
