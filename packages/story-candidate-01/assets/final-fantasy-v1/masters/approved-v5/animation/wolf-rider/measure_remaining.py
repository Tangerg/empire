from PIL import Image
from pathlib import Path
import numpy as np,json,math
b=Path(__file__).parent;scale=52/905.5
sets={'attack':{'near-hind':[135,800,330,920],'far-hind':[500,770,730,920],'near-fore':[800,840,1040,970],'far-fore':[1100,800,1330,940]},'walk-b':{'near-hind':[390,760,540,905],'far-hind':[640,820,825,940],'near-fore':[980,860,1170,970],'far-fore':[1150,770,1260,890]}}
probes={'attack':[('hind-paw-gap',400,850),('fore-paw-gap',1070,920),('under-belly-gap',735,850)],'walk-b':[('raised-hind-paw-gap',580,900),('raised-fore-paw-gap',1180,920),('under-belly-gap',875,880)]}
reviews={'attack':'Clear forward lean and two-hand spear thrust while the wolf braces all four paws in a wide stance. Four connected wolf legs, one full bushy tail, two rider arms/hands on one shaft, and the attached plain round side shield are visually verified in native and white-background views. Cap, braid, shoulder plate, face, clothing, harness and saddle blanket remain coherent. Forward hand remains pale against the bare near hand; fine wrap divisions are simplified. No independent impact FX or ground shadow. The projected spear span is materially longer than idle; this variation is explicitly recorded for native sequence review, not claimed as exact length continuity.','walk-b':'Opposite diagonal walk to walk-a: near fore and far hind paws planted, near hind and far fore lifted. Four connected wolf legs and four paws, one full bushy tail, rider following the saddle, two hands on one resting spear, and side shield are clear in native and white-background views. Original cap, braid, cream tunic, near shoulder plate, pale wrapped forward hand, saddle blanket and harness retained. No independent FX, floor or shadow.'}
for pose,rois in sets.items():
 d=b/pose;a=np.array(Image.open(d/'master.png'))[:,:,3];q=json.loads((d/'alpha-qa.json').read_text());feet=[]
 for paw,roi in rois.items():
  x0,y0,x1,y1=roi;yy,xx=np.where(a[y0:y1,x0:x1]>127);sy=int(yy.max()+y0);xs=np.where(a[sy-3:sy+1,x0:x1]>127)[1]+x0;feet.append({'paw':paw,'roi':roi,'soleY':sy,'bottomFourRowsMeanX':float(xs.mean())})
 mid=[float(np.mean([p['bottomFourRowsMeanX'] for p in feet])),float(np.mean([p['soleY'] for p in feet]))];q['feetMeasurement']=feet;q['sourceFootMidpoint']=mid;q['suggestedSourceFoot']=[round(mid[0]),max(p['soleY'] for p in feet)];q['negativeSpaceProbes']=[]
 for name,cx,cy in probes[pose]:
  choices=[]
  for y in range(cy-25,cy+26):
   for x in range(cx-25,cx+26):
    block=a[y-3:y+4,x-3:x+4]
    if block.shape==(7,7) and not block.any():choices.append(((x-cx)**2+(y-cy)**2,x,y))
  assert choices,(pose,name)
  _,x,y=min(choices);q['negativeSpaceProbes'].append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':49,'maxAlpha':0})
 q['semanticReview']=reviews[pose];bb=q['bboxAlphaGreaterThan127'];q['visibleWidthAlphaGreaterThan127']=bb[2]-bb[0];q['projectedWidthLogicalAtSharedScale']=(bb[2]-bb[0])*scale
 (d/'alpha-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n');print(pose,feet,q['suggestedSourceFoot'],q['projectedWidthLogicalAtSharedScale'])
# Visual endpoint estimates, not hidden physical-length reconstruction.
ends={'idle':[[499,550],[1254,229]],'walk-a':[[513,511],[1280,216]],'attack':[[417,411],[1548,332]],'walk-b':[[512,507],[1262,207]]}
for pose,points in ends.items():
 d=b/pose;q=json.loads((d/'alpha-qa.json').read_text());span=math.dist(*points);q['spearProjectionMeasurement']={'estimatedButtAndTipSourceCoordinates':points,'approximateProjectedSpanSourcePixels':span,'approximateProjectedSpanLogicalPixels':span*scale,'method':'Native visual endpoint estimates along the spear overall butt-to-tip line; not a claim of recovered 3D weapon length.'};(d/'alpha-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
print('spear spans',{p:round(math.dist(*v),2) for p,v in ends.items()})
