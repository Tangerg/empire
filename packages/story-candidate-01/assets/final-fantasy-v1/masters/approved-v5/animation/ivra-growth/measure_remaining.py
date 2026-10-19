from pathlib import Path
import json
import numpy as np
from PIL import Image
b=Path(__file__).parent
scale=56/950.5
sets={'attack': {'near-hind':[410,830,650,950],'far-hind':[740,810,930,905],'near-fore':[940,860,1250,980],'far-fore':[1270,825,1560,955]}, 'walk-b': {'near-hind':[605,750,765,890],'far-hind':[800,825,940,930],'near-fore':[945,875,1200,990],'far-fore':[1280,775,1450,910]}}
probes={'attack': [('hindleg-gap',[690,860]),('foreleg-gap',[1250,900]),('tail-hook-interior',[390,645])], 'walk-b': [('raised-hind-paw-gap',[750,900]),('raised-fore-paw-gap',[1260,920]),('tail-hook-interior',[480,680])]}
reviews={'attack': 'Four connected legs and four braced paws, with the same pair of wings, were visually checked in native and white-background views. The head and neck reach right with the mouth open, fangs and tongue contained within the mouth. White branching left-neck scar, copper-red body, gray natural belly plates, gold eye, and curled tail remain identifiable. No breath plume, aura, rider or chains. The wing spread and forward braced stance broaden the projected silhouette to 84.665 logical pixels at the unchanged shared scale, versus idle 67.165; this requires coordinator native-sequence review and is not claimed as exact size invariance.', 'walk-b': 'Opposite diagonal support to walk-a: near-fore and far-hind paws planted, near-hind and far-fore raised. Four connected legs and four paws plus the same two back wings are visible. Closed mouth, gray horn crown, white left-neck scar, gray belly plates, compact tail and chunky copper-red identity retained. Native and white-background views inspected. No independent FX, rider or chains.'}
for pose,rois in sets.items():
 d=b/pose; a=np.array(Image.open(d/'master.png'))[:,:,3]; q=json.loads((d/'alpha-qa.json').read_text());feet=[]
 for paw,roi in rois.items():
  x0,y0,x1,y1=roi; yy,xx=np.where(a[y0:y1,x0:x1]>127);sy=int(yy.max()+y0);xs=np.where(a[sy-3:sy+1,x0:x1]>127)[1]+x0
  feet.append({'paw':paw,'roi':roi,'soleY':sy,'bottomFourRowsMeanX':float(xs.mean())})
 midpoint=[float(np.mean([p['bottomFourRowsMeanX'] for p in feet])),float(np.mean([p['soleY'] for p in feet]))]
 q['feetMeasurement']=feet;q['sourceFootMidpoint']=midpoint;q['suggestedSourceFoot']=[round(midpoint[0]),max(p['soleY'] for p in feet)]
 q['negativeSpaceProbes']=[]
 for name,(cx,cy) in probes[pose]:
  # Read-only probe placement: nearest fully transparent 7x7 block within 25 pixels of visually inspected gap.
  choices=[]
  for y in range(cy-25,cy+26):
   for x in range(cx-25,cx+26):
    block=a[y-3:y+4,x-3:x+4]
    if block.shape==(7,7) and not block.any(): choices.append(((x-cx)**2+(y-cy)**2,x,y))
  assert choices,(pose,name)
  _,x,y=min(choices);q['negativeSpaceProbes'].append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':49,'maxAlpha':0})
 q['semanticReview']=reviews[pose]
 bb=q['bboxAlphaGreaterThan127'];q['visibleWidthAlphaGreaterThan127']=bb[2]-bb[0];q['projectedWidthLogicalAtSharedScale']=(bb[2]-bb[0])*scale
 if pose=='walk-b':q['pawRoiCaveat']='Far-hind ROI stops at x940 to exclude the neighboring near-fore leg. The actual far-hind lowest claw band at x905–915, y921–924 is included in full.'
 (d/'alpha-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
 print(pose,q['suggestedSourceFoot'],q['projectedWidthLogicalAtSharedScale'],q['negativeSpaceProbes'])
