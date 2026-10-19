from PIL import Image
from pathlib import Path
import numpy as np,json,hashlib
b=Path(__file__).parent;d=b/'idle';a=np.array(Image.open(d/'master.png'))[:,:,3]
rois={'near-hind':[430,840,605,965],'far-hind':[645,830,820,950],'near-fore':[835,850,1030,984],'far-fore':[1050,840,1240,970]};feet=[]
for name,roi in rois.items():
 x0,y0,x1,y1=roi;yy,xx=np.where(a[y0:y1,x0:x1]>127);s=int(yy.max()+y0);xs=np.where(a[s-3:s+1,x0:x1]>127)[1]+x0
 feet.append({'paw':name,'roi':roi,'soleY':s,'bottomFourRowsMeanX':float(xs.mean())})
q=json.loads((d/'alpha-qa.json').read_text());mid=[float(np.mean([p['bottomFourRowsMeanX'] for p in feet])),float(np.mean([p['soleY'] for p in feet]))];sole_mean=(feet[0]['soleY']+feet[2]['soleY'])/2;body=sole_mean-56;scale=52/body
q.update({'feetMeasurement':feet,'sourceFootMidpoint':mid,'suggestedSourceFoot':[round(mid[0]),max(p['soleY'] for p in feet)],'nearSidePawMeanSoleY':sole_mean,'highestCapY':56,'bodyHeight':body,'proposedBodyScale':scale,'bodyScaleNumerator':52,'bodyScaleDenominator':body,'bodyMeasurementMethod':'Full mounted combination: cap top to mean near-side hind/fore wolf paw soles. Contact anchor x averages all four paw sole-band centers; y is the lowest wolf sole. Rider boot is not the ground anchor.','visibleWidthAlphaGreaterThan127':997,'projectedWidthLogicalAtProposedScale':997*scale,'targetWidthRangeLogical':[60,72],'widthCaveat':'Actual width 57.254555 is slightly below the initial 60–72 pilot target. No independent wolf expansion or rider shrinking was applied. Await coordinator native proportion review before changes or other poses.','negativeSpaceProbes':[]})
for name,cx,cy in [('hind-paw-gap',615,900),('fore-paw-gap',1025,900),('spear-wolf-gap',1000,405)]:
 candidates=[]
 for y in range(cy-25,cy+26):
  for x in range(cx-25,cx+26):
   block=a[y-3:y+4,x-3:x+4]
   if block.shape==(7,7) and not block.any():candidates.append(((x-cx)**2+(y-cy)**2,x,y))
 assert candidates
 _,x,y=min(candidates);q['negativeSpaceProbes'].append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':49,'maxAlpha':0})
q['semanticReview']='Native and white-background views inspected. Original bare face, dark braid, fur-edged stitched cap, gold earring, cream laced tunic, near steel shoulder plate, brown bracers, dark trousers and large boot retained. Bare rear hand and pale wrapped forward hand hold one complete spear; plain round side shield remains behind forearm. Four connected wolf legs and paws, large gray-brown head with cream cheek/chest masses, complete bushy tail, rope chest harness and rust-edged saddle blanket are distinct. Broad matte planes and chunky hands/head match the approved family. No rider facial hair, new heraldry, independent FX or ground shadow. Native alpha-1 perimeter specks retained; no pixels alpha >1 on the perimeter.'
q['proposedCommonFrameSize']=[128,80];q['proposedOrigin']=[14,6];q['proposedFullCanvasBounds']=[14,6,14+1586*scale,6+992*scale];q['status']='Idle candidate inspected; coordinator body scale and pose expansion approval pending'
for p in [d/'alpha-qa.json',b/'idle-review.json']:p.write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
req=json.loads((d/'request.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();assert sha(d/'prompt.txt')==req['promptSha256'];assert sha(d/'raw.png')==sha(d/'master.png')==sha(q['nativeSource'])==q['sha256'];assert q['perimeterCountAlphaGreaterThan1']==0
for p,h in req['referencedImageSha256'].items():assert sha(p)==h
p=b/'source-evidence.json';s=json.loads(p.read_text());s['status']=q['status'];s['idleActualMeasurement']={'nativeSize':q['size'],'capTopY':56,'meanNearPawSoleY':sole_mean,'bodyHeight':body,'proposedScale':'52/905.5','proposedSourceFoot':q['suggestedSourceFoot'],'actualVisibleWidthPixels':997,'actualLogicalVisibleWidth':997*scale,'widthCaveat':q['widthCaveat'],'candidateFrame':[128,80],'candidateOrigin':[14,6],'perimeterMaxAlpha':q['perimeterMaxAlpha']};p.write_text(json.dumps(s,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'sourceFoot':q['suggestedSourceFoot'],'scale':'52/905.5','width':997*scale,'wholeCanvasBounds':q['proposedFullCanvasBounds'],'integrity':'PASS','negativeSpaceProbes':q['negativeSpaceProbes']},indent=2))
