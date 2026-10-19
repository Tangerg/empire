from PIL import Image,ImageDraw
from pathlib import Path
import numpy as np,json,hashlib
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png');a=np.asarray(im.getchannel('A'));feet=[]
rois={'near-hind':[410,865,540,970],'far-hind':[650,870,785,960],'near-fore':[845,875,980,980],'far-fore':[1080,880,1220,970]}
for name,r in rois.items():
 x0,y0,x1,y1=r;yy,xx=np.where(a[y0:y1,x0:x1]>127);s=int(yy.max()+y0);xs=np.where(a[s-3:s+1,x0:x1]>127)[1]+x0;feet.append({'hoof':name,'roi':r,'soleY':s,'bottomFourRowsMeanX':float(xs.mean())})
hairY=int(np.where(a[:280,600:870]>127)[0].min());mean=(feet[0]['soleY']+feet[2]['soleY'])/2;body=mean-hairY;scale=54/body;anchor=[round(np.mean([f['bottomFourRowsMeanX'] for f in feet])),max(f['soleY'] for f in feet)];q=json.loads((d/'alpha-qa.json').read_text());q.update({'hairCrownY':hairY,'feetMeasurement':feet,'nearSideHoofSoleMeanY':mean,'bodyHeightSource':body,'sharedScale':scale,'sharedScaleFraction':f'54/{body}','targetWholeMountedHeight':54,'sourceFoot':anchor,'measurementMethod':'Rider hair crown to mean near-side hind and fore hoof soles; contact x averages four hoof bottom-four-row centers and y is lowest hoof. Rider boot excluded.','logicalVisibleWidth':(q['alphaGreater127Bounds'][2]-q['alphaGreater127Bounds'][0])*scale,'candidateFrame':[128,80],'candidateOrigin':[14,6],'fullCanvasBounds':[14,6,14+im.width*scale,6+im.height*scale],'negativeSpaceProbes':[]})
for n,x,y in [('hindleg-gap',584,880),('middle-gap',805,884),('foreleg-gap',1015,898),('antler-gap',1150,213)]:
 v=a[y-3:y+4,x-3:x+4];q['negativeSpaceProbes'].append({'name':n,'rect':[x-3,y-3,x+4,y+4],'maxAlpha':int(v.max()),'zeroPixels':int((v==0).sum())})
q['semanticReview']='Original pointed ear, tied dark hair, bare face, green leaf shoulders, brown straps and boots, rear-hand short spear and forward-hand reins retained. Original ear-detail crop confirms small gold drop ornament. One white four-hoof stag, branched brown antlers, bridle and blue/gold geometric saddlecloth retained. No extra figures, effects or ground. Four complete hooves and negative spaces are readable. Native perimeter zero; all alpha-1 dust remains unchanged.'
q['status']='Identity and measured54logical scale proposal pending coordinator gate; other poses not generated'
(b/'idle-review.json').write_text(json.dumps(q,indent=2));(d/'alpha-qa.json').write_text(json.dumps(q,indent=2))
# QA composites only, each immutable full canvas is uniformly resized using its approved shared scale.
refs=[('Human44',Path('/workspace/shared/empire-art-rebuild/approved-v5/swordsman/master.png'),1/18,(800,887)),('Wolf52',Path('/workspace/shared/empire-art-rebuild/approved-v5/animation/wolf-rider/idle/master.png'),52/905.5,(828,970)),('Stag54',d/'master.png',scale,anchor),('Roderick56',Path('/workspace/shared/empire-art-rebuild/approved-v5/hero-appearances/roderick/idle/master.png'),56/947,(782,975))]
for bgname,bg in [('white',(255,255,255,255)),('black',(25,25,25,255)),('olive',(94,106,73,255))]:
 panel=Image.new('RGBA',(512,100),bg);draw=ImageDraw.Draw(panel)
 for i,(label,p,s,f) in enumerate(refs):
  src=Image.open(p);res=src.resize((round(src.width*s),round(src.height*s)),Image.Resampling.LANCZOS);x=round(i*128+64-f[0]*s);y=round(78-f[1]*s);panel.alpha_composite(res,(x,y));draw.text((i*128+25,85),label,fill=(220,220,220) if bgname=='black' else (25,25,25))
 panel.save(b/f'idle-scale-{bgname}-native.png');panel.resize((1536,300),Image.Resampling.NEAREST).save(b/f'idle-scale-{bgname}-3x.png')
print(json.dumps({'feet':feet,'sourceFoot':anchor,'scale':q['sharedScaleFraction'],'width':q['logicalVisibleWidth'],'body':body,'fullCanvasBounds':q['fullCanvasBounds'],'negativeSpace':q['negativeSpaceProbes']},indent=2))
