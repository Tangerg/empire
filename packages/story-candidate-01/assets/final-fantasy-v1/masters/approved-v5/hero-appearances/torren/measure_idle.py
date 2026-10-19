from pathlib import Path
from PIL import Image,ImageDraw
import json,numpy as np
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im.getchannel('A'));sol=[]
for name,roi in [('image-left-boot',[360,840,650,980]),('image-right-boot',[830,850,1120,980])]:
 x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);sol.append({'name':name,'roi':roi,'soleCenterXBottomFourRows':float((xs[ys>=ys.max()-3]+x0).mean()),'soleY':int(ys.max()+y0)})
top=66;height=sum(x['soleY'] for x in sol)/2-top;scale=44/height;foot=[round(sum(x['soleCenterXBottomFourRows'] for x in sol)/2),max(x['soleY'] for x in sol)];frame=[104,64];origin=[8,4];probes=[]
for name,region in [('under-right-arm',[570,490,645,540]),('between-boots',[650,850,810,940])]:
 x0,y0,x1,y1=region;found=[]
 for y in range(y0,y1-6):
  for x in range(x0,x1-6):
   if a[y:y+7,x:x+7].max()==0:found.append((x,y))
 x,y=found[len(found)//2];probes.append({'name':name,'rectangle':[x,y,x+7,y+7],'maxAlpha':0,'zeroPixels':49})
q=json.load(open(d/'alpha-qa.json'));q.update({'status':'Native idle candidate inspected; coordinator review pending','bodyTopY':top,'soleMeasurements':sol,'bodyHeight':height,'proposedLogicalBodyHeight':44,'proposedScaleRatio':f'44/{height:g}','proposedScale':scale,'proposedSourceFoot':foot,'proposedFrame':frame,'proposedOrigin':origin,'negativeSpaceProbes':probes,'semanticReview':'Young tired dark-brow face with light stubble, no large beard or later cheek scar; gray kettle helmet/ear guards, gray scarf, gray-brown quilted coat, source pendant, bare sword hand, brown boots and repaired wooden shield retained. Explicit rule-based right-hand straight sword replaces original portrait spear. Opposite arm remains behind shield, no duplicated hand.','styleCaveat':'Head/hand/boot proportions and broad steel planes match family; quilted coat still has some mottled small surface marks from original reference. Submit native44 comparison before deciding whether a targeted cleanup redraw is warranted. Source pixels unchanged.','bodyMeasurementDefinition':'Helmet dome top66 to mean soles960 =>894. Footx is mean of sole bottom-four-row center samples rounded742; footy964.','rawEqualsMaster':(d/'raw.png').read_bytes()==(d/'master.png').read_bytes()})
for p in [d/'alpha-qa.json',b/'idle-review.json']:p.write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
def render(img,s,o):return img.transform(tuple(frame),Image.Transform.AFFINE,(1/s,0,-o[0]/s,0,1/s,-o[1]/s),resample=Image.Resampling.BICUBIC)
render(im,scale,origin).save(d/'qa-native-frame-rgba.png');sw=Image.open('/workspace/shared/empire-art-rebuild/approved-v5/swordsman/master.png').convert('RGBA');proof=Image.new('RGB',(416,456),'#999');dr=ImageDraw.Draw(proof)
for row,color in enumerate(['white','#151c25','#7c8c76']):
 for col,(name,img,s,ft) in enumerate([('Torren44',im,scale,foot),('Approved swordsman44',sw,1/18,[800,887])]):
  r=render(img,s,[52-ft[0]*s,54-ft[1]*s]);bg=Image.new('RGBA',tuple(frame),color);bg.alpha_composite(r);proof.paste(bg.convert('RGB').resize((208,128),Image.Resampling.NEAREST),(col*208,row*152+24));dr.text((col*208+5,row*152+5),name,fill='black')
proof.save(d/'native-scale-comparison.png');e=json.load(open(b/'source-evidence.json'));e['status']='First named Torren stage01 battle idle candidate inspected and measured; review pending';e['idleCandidate']={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'sha256':q['sha256'],'nativeSize':list(im.size),'proposedScaleRatio':f'44/{height:g}','sourceFoot':foot,'review':str(b/'idle-review.json'),'comparison':str(d/'native-scale-comparison.png')};(b/'source-evidence.json').write_text(json.dumps(e,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'scaleRatio':f'44/{height:g}','foot':foot,'soles':sol,'strictEdgeCount':q['perimeterCountAlphaGreaterThan1'],'probes':probes},indent=2))
