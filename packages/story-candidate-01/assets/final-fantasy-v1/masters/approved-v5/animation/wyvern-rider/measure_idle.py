from PIL import Image,ImageDraw
from pathlib import Path
import numpy as np,json
p=Path(__file__).parent;im=Image.open(p/'idle/master.png');a=np.asarray(im.getchannel('A'));feet=[]
rois={'near-hind':[490,870,700,980],'far-hind':[780,845,905,950],'near-fore':[910,910,1100,992],'raised-fore':[1065,815,1170,915]}
for name,r in rois.items():
 x0,y0,x1,y1=r;yy,xx=np.where(a[y0:y1,x0:x1]>127);s=int(yy.max()+y0);xs=np.where(a[s-3:s+1,x0:x1]>127)[1]+x0;feet.append({'foot':name,'roi':r,'soleY':s,'bottomFourRowsMeanX':float(xs.mean()),'planted':name!='raised-fore'})
hairY=int(np.where(a[:280,650:900]>127)[0].min());mean=(feet[0]['soleY']+feet[2]['soleY'])/2;body=mean-hairY;scale=54/body;anchor=[round(np.mean([f['bottomFourRowsMeanX'] for f in feet])),max(f['soleY'] for f in feet)];q=json.loads((p/'idle/alpha-qa.json').read_text());q.update({'hairCrownY':hairY,'feetMeasurement':feet,'nearSideSoleMeanY':mean,'bodyHeightSource':body,'sharedScale':scale,'sharedScaleFraction':f'54/{body}','targetWholeMountedHeight':54,'sourceFoot':anchor,'measurementMethod':'Hair crown to mean near-side hind/fore claw soles; source support x averages all four paw bottom-four-row centers including lifted forepaw stance projection; y lowest planted claw. Rider boot excluded.','logicalVisibleWidth':(q['bboxAlpha128'][2]-q['bboxAlpha128'][0])*scale,'candidateFrame':[128,80],'candidateOrigin':[14,6],'fullCanvasBounds':[14,6,14+im.width*scale,6+im.height*scale],'status':'Awaiting coordinator idle gate; other poses not generated.'})
(p/'idle-review.json').write_text(json.dumps(q,indent=2))
refs=[('Human44',Path('/workspace/shared/empire-art-rebuild/approved-v5/swordsman/master.png'),1/18,(800,887)),('Eagle52',p/'references/eagle-scout-approved-idle.png',52/944,(798,974)),('Stag54',p/'references/white-stag-rider-approved-idle.png',54/909.5,(822,975)),('Wyvern54',p/'idle/master.png',scale,anchor),('Dragon64',p/'references/ancient-dragon-approved-idle.png',64/940,(1024,962))]
for bgname,bg in [('white',(255,255,255,255)),('black',(25,25,25,255)),('olive',(94,106,73,255))]:
 panel=Image.new('RGBA',(640,104),bg);draw=ImageDraw.Draw(panel)
 for i,(label,fp,s,f) in enumerate(refs):
  src=Image.open(fp);res=src.resize((round(src.width*s),round(src.height*s)),Image.Resampling.LANCZOS);x=round(i*128+64-f[0]*s);y=round(80-f[1]*s);panel.alpha_composite(res,(x,y));draw.text((i*128+25,89),label,fill=(220,220,220) if bgname=='black' else (25,25,25))
 panel.save(p/f'idle-scale-{bgname}-native.png');panel.resize((1920,312),Image.Resampling.NEAREST).save(p/f'idle-scale-{bgname}-3x.png')
print(json.dumps({k:q[k] for k in ['hairCrownY','sourceFoot','feetMeasurement','sharedScaleFraction','logicalVisibleWidth','fullCanvasBounds']},indent=2))
