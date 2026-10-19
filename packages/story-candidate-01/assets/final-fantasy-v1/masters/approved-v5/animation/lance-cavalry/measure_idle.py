from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im)[:,:,3]
q=json.loads((d/'alpha-qa.json').read_text());rois={'near-hind':[340,870,490,985],'far-hind':[550,870,695,975],'near-fore':[700,890,850,990],'far-fore':[900,880,1050,985]};feet={}
for name,(x0,y0,x1,y1) in rois.items():
 yy,xx=np.where(a[y0:y1,x0:x1]>127);yy+=y0;xx+=x0;sole=int(yy.max());feet[name]={'roi':[x0,y0,x1,y1],'soleY':sole,'bottomFourRowsMeanX':float(xx[yy>sole-4].mean())}
yy,xx=np.where(a[0:200,500:730]>127);crown=int(yy.min());nearmean=(feet['near-hind']['soleY']+feet['near-fore']['soleY'])/2;h=nearmean-crown;s=56/h;foot=[round(np.mean([f['bottomFourRowsMeanX'] for f in feet.values()])),max(f['soleY'] for f in feet.values())]
probes=[]
for name,cx,cy in [('hind-leg-gap',510,915),('middle-leg-gap',691,878),('fore-leg-gap',870,910),('lance-to-horse-gap',1150,510)]:
 vals=[]
 for y in range(cy-20,cy+21):
  for x in range(cx-20,cx+21):
   v=a[y-3:y+4,x-3:x+4]
   if v.shape==(7,7) and not v.any():vals.append(((x-cx)**2+(y-cy)**2,x,y))
 assert vals,name;_,x,y=min(vals);probes.append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':49,'maxAlpha':0})
q.update({'bodyMeasurement':{'helmetCrownY':crown,'hoofMeasurements':feet,'meanNearSideHoofSoleY':nearmean,'bodyReferenceSourcePixels':h,'bodyTargetHeightLogicalPixels':56,'sharedScale':s,'scaleFraction':f'56/{h:g}','sourceFoot':foot,'visibleWidthLogical':(q['bboxAlphaGreaterThan127'][2]-q['bboxAlphaGreaterThan127'][0])*s,'nativeCanvas':list(im.size),'frameSize':[128,80],'origin':[14,6]},'negativeSpaceProbes':probes,'selectedAttempt':'attempt-02','semanticReview':'Original young clean-shaven helmeted soldier in navy/red lamellar, cream rectangular gold-strip shield, brown four-hoof armored horse and complete lance retained. Newly visible left glove controls reins at shield edge; right glove holds lance. All four hooves planted; no added symbols or weapons.','status':'Idle candidate; awaiting coordinator identity and scale gate before remaining poses'})
(b/'idle-review.json').write_text(json.dumps(q,indent=2)+'\n')
entries=[('Cavalry pilot56',im,s,foot),('Roderick56',Image.open('/workspace/shared/empire-art-rebuild/approved-v5/hero-appearances/roderick/idle/master.png').convert('RGBA'),56/947,[782,975]),('Wolf rider52',Image.open('/workspace/shared/empire-art-rebuild/approved-v5/animation/wolf-rider/idle/master.png').convert('RGBA'),52/905.5,[828,970]),('Swordsman44',Image.open('/workspace/shared/empire-art-rebuild/approved-v5/swordsman/master.png').convert('RGBA'),1/18,[800,887])]
review=Image.new('RGB',(1024,360),(218,218,218));draw=ImageDraw.Draw(review);bounds={}
for row,bgcol in enumerate(['white','black']):
 for col,(label,img,scale,anchor) in enumerate(entries):
  tx=64-anchor[0]*scale;ty=68-anchor[1]*scale;full=[tx,ty,tx+img.width*scale,ty+img.height*scale];assert min(full[:2])>=0 and full[2]<=128 and full[3]<=80;bounds[label]={'scale':scale,'sourceFoot':anchor,'fullCanvasBounds':full}
  preview=img.transform((128,80),Image.Transform.AFFINE,(1/scale,0,-tx/scale,0,1/scale,-ty/scale),resample=Image.Resampling.BICUBIC,fillcolor=(0,0,0,0));bg=Image.new('RGBA',(128,80),bgcol);bg.alpha_composite(preview);review.paste(bg.convert('RGB').resize((256,160),Image.Resampling.NEAREST),(col*256,row*180+20));draw.text((col*256+5,row*180+5),label+' / '+bgcol,fill=(0,0,0))
review.save(d/'native-scale-comparison.png');(d/'native-scale-comparison.json').write_text(json.dumps({'method':'QA only full-canvas uniform affine transform at declared commonground68. No source pixels edited, no trim or independent body fitting.','entries':bounds},indent=2)+'\n')
print(json.dumps(q['bodyMeasurement'],indent=2))
