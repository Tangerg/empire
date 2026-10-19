from pathlib import Path
from PIL import Image,ImageDraw
import json,numpy as np
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im.getchannel('A'));sol=[]
for name,roi in [('image-left-boot',[325,850,590,980]),('image-right-boot',[750,850,1050,980])]:
 x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);sol.append({'name':name,'roi':roi,'soleCenterXBottomFourRows':float((xs[ys>=ys.max()-3]+x0).mean()),'soleY':int(ys.max()+y0)})
top=47;height=sum(x['soleY'] for x in sol)/2-top;scale=44/height;foot=[round(sum(x['soleCenterXBottomFourRows'] for x in sol)/2),max(x['soleY'] for x in sol)];frame=[104,64];origin=[8,4];probes=[]
for name,region in [('under-spear-arm',[900,570,960,625]),('between-boots',[600,840,750,930])]:
 x0,y0,x1,y1=region;found=[]
 for y in range(y0,y1-6):
  for x in range(x0,x1-6):
   if a[y:y+7,x:x+7].max()==0:found.append((x,y))
 if found:
  x,y=found[len(found)//2];probes.append({'name':name,'rectangle':[x,y,x+7,y+7],'maxAlpha':0,'zeroPixels':49})
q=json.load(open(d/'alpha-qa.json'));q.update({'status':'Native idle candidate inspected; coordinator review pending','bodyTopY':top,'soleMeasurements':sol,'bodyHeight':height,'proposedLogicalBodyHeight':44,'proposedScaleRatio':f'44/{height:g}','proposedScale':scale,'proposedSourceFoot':foot,'proposedFrame':frame,'proposedOrigin':origin,'negativeSpaceProbes':probes,'semanticReview':'Named young clean-shaven stern Cain, navy gold-striped helmet, broad gold shoulder plates, navy heavy plate/tabard, burgundy scarf/cape, dark gloves/boots and source gold-diamond navy rectangular shield retained. Rules-required spear held in original image-right baton hand; source shield remains image-left. No horse, no generic cream shield, no new iconography. Original command baton reserved for independent portrait.','styleReview':'Chunky large head/gloves/boots, dark contour and large matte planes. Very limited broad shape variation, no dense microtexture.','bodyMeasurementDefinition':'Helmet top47 to mean soles959.5 yields912.5 source pixels. Foot center from mean sole bottom-four-row centers; y966.','rawEqualsMaster':(d/'raw.png').read_bytes()==(d/'master.png').read_bytes()})
for p in [d/'alpha-qa.json',b/'idle-review.json']:p.write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
def render(img,s,o):return img.transform(tuple(frame),Image.Transform.AFFINE,(1/s,0,-o[0]/s,0,1/s,-o[1]/s),resample=Image.Resampling.BICUBIC)
render(im,scale,origin).save(d/'qa-native-frame-rgba.png');tor=Image.open(b/'references/approved-torren-idle.png').convert('RGBA');mir=Image.open(b/'references/approved-mirelle-idle.png').convert('RGBA');proof=Image.new('RGB',(624,456),'#999');dr=ImageDraw.Draw(proof)
for row,color in enumerate(['white','#151c25','#7c8c76']):
 for col,(name,img,s,ft) in enumerate([('Cain 44',im,scale,foot),('Approved Torren 44',tor,44/894,[742,964]),('Approved Mirelle 44',mir,44/919,[758,967])]):
  r=render(img,s,[52-ft[0]*s,54-ft[1]*s]);bg=Image.new('RGBA',tuple(frame),color);bg.alpha_composite(r);proof.paste(bg.convert('RGB').resize((208,128),Image.Resampling.NEAREST),(col*208,row*152+24));dr.text((col*208+5,row*152+5),name,fill='black')
proof.save(d/'native-scale-comparison.png');e=json.load(open(b/'source-evidence.json'));e['idleCandidate']={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'sha256':q['sha256'],'nativeSize':list(im.size),'proposedScaleRatio':f'44/{height:g}','sourceFoot':foot,'review':str(b/'idle-review.json'),'comparison':str(d/'native-scale-comparison.png')};e['coordinatorIdleContract']='2026-10-19 22:31UTC confirmed stage01 named foot officer, battle spear replaces baton, portrait preserves baton';(b/'source-evidence.json').write_text(json.dumps(e,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'scaleRatio':f'44/{height:g}','foot':foot,'soles':sol,'strictEdgeCount':q['perimeterCountAlphaGreaterThan1'],'probes':probes},indent=2))
