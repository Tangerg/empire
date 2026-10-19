from pathlib import Path
from PIL import Image,ImageDraw
import json,numpy as np
b=Path(__file__).parent; d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im.getchannel('A'));sol=[]
for name,roi in [('image-left-boot',[440,850,680,1000]),('image-right-boot',[820,850,1100,1000])]:
 x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);sol.append({'name':name,'roi':roi,'soleCenterXBottomFourRows':float((xs[ys>=ys.max()-3]+x0).mean()),'soleY':int(ys.max()+y0)})
top=24;height=sum(x['soleY'] for x in sol)/2-top;scale=44/height;foot=[round(sum(x['soleCenterXBottomFourRows'] for x in sol)/2),max(x['soleY'] for x in sol)];frame=[104,64];origin=[8,4];probes=[]
for name,region in [('between-boots',[680,850,810,980]),('between-hammer-and-body',[1140,650,1230,750])]:
 x0,y0,x1,y1=region;found=[]
 for y in range(y0,y1-6):
  for x in range(x0,x1-6):
   if a[y:y+7,x:x+7].max()==0:found.append((x,y))
 if found:
  x,y=found[len(found)//2];probes.append({'name':name,'rectangle':[x,y,x+7,y+7],'maxAlpha':0,'zeroPixels':49})
q=json.load(open(d/'alpha-qa.json'));q.update({'status':'Native idle candidate inspected; coordinator review pending','bodyTopY':top,'soleMeasurements':sol,'bodyHeight':height,'proposedLogicalBodyHeight':44,'proposedScaleRatio':f'44/{height:g}','proposedScale':scale,'proposedSourceFoot':foot,'proposedFrame':frame,'proposedOrigin':origin,'negativeSpaceProbes':probes,'semanticReview':'Named adult woman with original brown swept hair/bun, brown eyes, rust coat, charcoal scarf, crossbody ledger satchel, keys, tags and wrench retained. Rule-correct engineer hammer is held in original wrench hand; wrench stowed. Small closed belt satchel represents charge. No generic helmet/apron identity.','styleReview':'Large head, broad bare hands/boots, thick contour and broad matte rust/brown/gray planes. Simple quilting, no fine realism.','bodyMeasurementDefinition':'Hair crown24 to mean boot soles; hammer and bag extent excluded. Source foot x is mean bottom-four-row boot centers and y is lower sole.','rawEqualsMaster':(d/'raw.png').read_bytes()==(d/'master.png').read_bytes()})
def render(img,s,o):return img.transform(tuple(frame),Image.Transform.AFFINE,(1/s,0,-o[0]/s,0,1/s,-o[1]/s),resample=Image.Resampling.BICUBIC)
r=render(im,scale,origin);r.save(d/'qa-native-frame-rgba.png');ra=np.asarray(r.getchannel('A'));edge=np.concatenate([ra[0],ra[-1],ra[:,0],ra[:,-1]]);q['derivedPerimeterMaxAlpha']=int(edge.max());q['derivedPerimeterNonzeroCount']=int((edge>0).sum())
for p in [d/'alpha-qa.json',b/'idle-review.json']:p.write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
proof=Image.new('RGB',(832,456),'#999');dr=ImageDraw.Draw(proof)
refs=[('Tasha 44',im,scale,foot)]
for hero in ['torren','cain','bran']:
 j=json.load(open(b.parent/hero/'idle-review.json'));refs.append((f'{hero.title()} 44',Image.open(b/'references'/f'style-{hero}-idle.png').convert('RGBA'),j['proposedScale'],j['proposedSourceFoot']))
for row,color in enumerate(['white','#151c25','#7c8c76']):
 for col,(name,img,s,ft) in enumerate(refs):
  r=render(img,s,[52-ft[0]*s,54-ft[1]*s]);bg=Image.new('RGBA',tuple(frame),color);bg.alpha_composite(r);proof.paste(bg.convert('RGB').resize((208,128),Image.Resampling.NEAREST),(col*208,row*152+24));dr.text((col*208+5,row*152+5),name,fill='black')
proof.save(d/'native-scale-comparison.png');print(json.dumps({'scaleRatio':f'44/{height:g}','foot':foot,'soles':sol,'strictEdgeCount':q['perimeterCountAlphaGreaterThan1'],'derivedEdgeMax':q['derivedPerimeterMaxAlpha'],'probes':probes},indent=2))
