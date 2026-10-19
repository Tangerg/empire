from pathlib import Path
from PIL import Image
import numpy as np,json
b=Path(__file__).parent
spec={
'walk-a':{'feet':[('near-left-forward',[580,850,890,980]),('far-right-rear',[890,780,1075,910])],'probes':[('left-arm-gap',540,620),('right-arm-gap',1110,720),('leg-gap',860,800)],'review':'Heavy forward near-left planted stone foot and raised/rear far-right toe contact; far-right arm counter-swings forward while near-left arm swings back. Original headless mound, six tablets, three amber lamps and gate retain size and identity. No duplicated limbs or added effects.'},
'attack':{'feet':[('left-braced',[290,820,570,965]),('right-braced',[890,810,1200,930])],'probes':[('left-arm-gap',450,615),('below-sweeping-arm',1200,630),('leg-gap',740,840)],'review':'Clear braced broad-legged outward forearm/fist strike to screen-right, with counterbalancing left arm. Central gate, six tablets and all three amber lanterns stay legible; full right fist stays inside canvas. Attack projected width increases due to extension, not a new body scale. No detached weapon, generic rune, face or independent FX.'},
'walk-b':{'feet':[('near-left-rear',[535,795,750,915]),('far-right-forward',[905,825,1240,970])],'probes':[('left-arm-gap',608,580),('right-arm-gap',1111,680),('leg-gap',818,850)],'review':'Opposite contact to walk-a: far-right foot forward and planted, near-left foot back/toe contact. Near-left arm now counter-swings forward; far-right arm swings back. Exact asymmetrical tablet crown and three-lantern placement not mirrored. Broad native body/gate scale preserved.'}}
for pose,spec in spec.items():
 p=b/pose/'alpha-qa.json';q=json.loads(p.read_text());a=np.array(Image.open(b/pose/'master.png').getchannel('A'));feet=[]
 for name,roi in spec['feet']:
  x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);sole=int(ys.max()+y0);ys,xs=np.where(a[max(y0,sole-3):sole+1,x0:x1]>127);feet.append({'foot':name,'roi':roi,'soleY':sole,'bottomFourRowsMeanX':float(xs.mean()+x0)})
 meanx=sum(f['bottomFourRowsMeanX'] for f in feet)/2;meany=sum(f['soleY'] for f in feet)/2;foot=[round(meanx),max(f['soleY'] for f in feet)]
 probes=[]
 for name,x,y in spec['probes']:
  r=a[y-3:y+4,x-3:x+4]
  probes.append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':int((r==0).sum()),'maxAlpha':int(r.max())})
 q.update({'feetMeasurement':feet,'sourceFootMidpoint':[meanx,meany],'suggestedSourceFoot':foot,'sharedBodyScale':60/878,'sharedScaleFraction':'60/878','bodyScaleDenominatorFromAcceptedIdle':878,'bodyScaleNumerator':60,'projectedWidthLogicalAtSharedScale':(q['bboxAlphaGreaterThan127'][2]-q['bboxAlphaGreaterThan127'][0])*60/878,'negativeSpaceProbes':probes,'semanticReview':spec['review']})
 p.write_text(json.dumps(q,indent=2)+'\n');print(pose,json.dumps({'feet':feet,'sourceFoot':foot,'offset':[(821-foot[0])*60/878,(946-foot[1])*60/878],'probes':probes,'width':q['projectedWidthLogicalAtSharedScale']},indent=2))
