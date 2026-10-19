from pathlib import Path
from PIL import Image,ImageDraw
import json,numpy as np
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im.getchannel('A'));sol=[]
for name,roi in [('image-left-boot',[475,850,720,990]),('image-right-boot',[820,850,1050,990])]:
 x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);sol.append({'name':name,'roi':roi,'soleCenterXBottomFourRows':float((xs[ys>=ys.max()-3]+x0).mean()),'soleY':int(ys.max()+y0)})
top=16;height=sum(x['soleY'] for x in sol)/2-top;scale=44/height;foot=[round(sum(x['soleCenterXBottomFourRows'] for x in sol)/2),max(x['soleY'] for x in sol)];frame=[104,64];origin=[8,4];probes=[]
for name,region in [('inside-bow-upper',[1020,365,1120,470]),('between-boots',[712,875,820,955]),('between-quiver-and-hood',[500,145,550,205])]:
 x0,y0,x1,y1=region;found=[]
 for y in range(y0,y1-6):
  for x in range(x0,x1-6):
   if a[y:y+7,x:x+7].max()==0:found.append((x,y))
 if found:
  x,y=found[len(found)//2];probes.append({'name':name,'rectangle':[x,y,x+7,y+7],'maxAlpha':0,'zeroPixels':49})
q=json.load(open(d/'alpha-qa.json'));q.update({'status':'Native idle candidate inspected; coordinator scale gate pending','bodyTopY':top,'soleMeasurements':sol,'bodyHeight':height,'proposedLogicalBodyHeight':44,'proposedScaleRatio':f'44/{height:g}','proposedScale':scale,'proposedSourceFoot':foot,'proposedFrame':frame,'proposedOrigin':origin,'negativeSpaceProbes':probes,'semanticReview':'Original youthful round clean-shaven face, broad brown eyes, dark-brown fringe, olive hood/cowl/cape, ragged gray-brown tunic, bare hands, wraps, source loop tool, belt rope, back quiver and netted provisions retained. Proper bow string; secondary border sword sheathed, net bag stowed to free right hand. No generic metal armor or blue tunic.','styleReview':'Large head, broad hands/boots, clean matte broad planes, thick contour. A few soft material tonal marks remain but no fine realism.','bodyMeasurementDefinition':'Hood crown16 to mean measured soles, weapon excluded. Source footx mean boot-bottom-four-row centers, footy lowest sole.','rawEqualsMaster':(d/'raw.png').read_bytes()==(d/'master.png').read_bytes()})
for p in [d/'alpha-qa.json',b/'idle-review.json']:p.write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
def render(img,s,o):return img.transform(tuple(frame),Image.Transform.AFFINE,(1/s,0,-o[0]/s,0,1/s,-o[1]/s),resample=Image.Resampling.BICUBIC)
render(im,scale,origin).save(d/'qa-native-frame-rgba.png')
models=[('Bran44',im,scale,foot)]
for key in ['torren','cain']:
 p=b.parent/key;data=json.load(open(p/'idle-review.json'));models.append((key.title()+'44',Image.open(p/'idle/master.png').convert('RGBA'),data['proposedScale'],data['proposedSourceFoot']))
proof=Image.new('RGB',(624,456),'#999');dr=ImageDraw.Draw(proof)
for row,color in enumerate(['white','#151c25','#7c8c76']):
 for col,(name,img,s,ft) in enumerate(models):
  r=render(img,s,[52-ft[0]*s,54-ft[1]*s]);bg=Image.new('RGBA',tuple(frame),color);bg.alpha_composite(r);proof.paste(bg.convert('RGB').resize((208,128),Image.Resampling.NEAREST),(col*208,row*152+24));dr.text((col*208+5,row*152+5),name,fill='black')
proof.save(d/'native-scale-comparison.png')
e=json.load(open(b/'source-evidence.json'));e['status']='First named Bran stage01 battle idle candidate measured; review pending';e['idleCandidate']={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'sha256':q['sha256'],'nativeSize':list(im.size),'proposedScaleRatio':f'44/{height:g}','sourceFoot':foot,'review':str(b/'idle-review.json'),'comparison':str(d/'native-scale-comparison.png')};(b/'source-evidence.json').write_text(json.dumps(e,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'scaleRatio':f'44/{height:g}','foot':foot,'soles':sol,'strictEdgeCount':q['perimeterCountAlphaGreaterThan1'],'probes':probes},indent=2))
