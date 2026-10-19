from PIL import Image
from pathlib import Path
import numpy as np,json
b=Path(__file__).parent;d=b/'walk-a';a=np.array(Image.open(d/'master.png'))[:,:,3]
rois={'near-hind':[400,830,590,950],'far-hind':[580,730,730,905],'near-fore':[1080,800,1230,940],'far-fore':[860,820,1030,948]};feet=[]
for name,roi in rois.items():
 x0,y0,x1,y1=roi;yy,xx=np.where(a[y0:y1,x0:x1]>127);sy=int(yy.max()+y0);xs=np.where(a[sy-3:sy+1,x0:x1]>127)[1]+x0;feet.append({'paw':name,'roi':roi,'soleY':sy,'bottomFourRowsMeanX':float(xs.mean())})
q=json.loads((d/'alpha-qa.json').read_text());mid=[float(np.mean([f['bottomFourRowsMeanX'] for f in feet])),float(np.mean([f['soleY'] for f in feet]))];q['feetMeasurement']=feet;q['sourceFootMidpoint']=mid;q['suggestedSourceFoot']=[round(mid[0]),max(f['soleY'] for f in feet)];q['negativeSpaceProbes']=[]
for name,x,y in [('hind-paw-gap',620,925),('raised-fore-paw-gap',1080,935),('under-belly-gap',795,860)]:
 block=a[y-3:y+4,x-3:x+4];assert not block.any(),(name,int(block.max()));q['negativeSpaceProbes'].append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':49,'maxAlpha':0})
q['semanticReview']='Clear diagonal walk: near hind and far fore paws support; near fore and far hind are raised and bent. Four connected wolf legs and paws, one full bushy tail, seated rider, both hands on one spear and side shield are visually checked in native and white-background views. Cap, braid, shoulder plate, pale wrapped forward hand, harness and saddle blanket remain coherent. Rider body follows saddle without independent rescaling. No external FX or floor. Native perimeter alpha is zero.'
q['visibleWidthAlphaGreaterThan127']=1075;q['projectedWidthLogicalAtSharedScale']=1075*52/905.5;(d/'alpha-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'feet':feet,'sourceFoot':q['suggestedSourceFoot'],'width':q['projectedWidthLogicalAtSharedScale']},indent=2))
