from pathlib import Path
import json,math,copy,numpy as np
from PIL import Image
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';s=.14505957;t=np.array([54.7883672,-4.29011913]);g=json.loads((p/'northwest-gate-v1-initial-sections.json').read_text());a=np.asarray(Image.open(r/'empire-c01/godot/assets/native/c07-layered-v1/northwest-gate-v1-stone-candidate.png'))[:,:,3]
g['excludePolygons']=[[[420,820],[525,820],[525,1186],[420,1186]],[[760,840],[908,840],[908,1030],[760,1030]]];g['scope']='UNBOUND native architecture surface reconstruction candidate; original RGBA regions reused as UV, rules unchanged'
(p/'northwest-native-base.json').write_text(json.dumps(g,indent=2)+'\n')
def src(v):return (np.array(v)*s+t).tolist()
def make(name,quads,support):
 return {'textureResourcePath':g['textureResourcePath'],'namePrefix':name,'supportLine':[src(v) for v in support],'members':[{'id':str(i),'polygon':[src(v) for v in pos],'uv':uv,'tint':'ffffff'}for i,(pos,uv)in enumerate(quads)]}
left=[]
for y0,y1,dx0,dx1,dy0,dy1 in [(820,880,0,-10,0,0),(880,1186,-10,-10,0,6)]:
 uv=[[420,y0],[525,y0],[525,y1],[420,y1]];pos=[[420,y0+dy0],[525+dx0,y0+dy0],[525+dx1,y1+dy1],[420,y1+dy1]];left.append((pos,uv))
def owned_low(x,y0,y1):
 ys=np.where(a[y0:y1+1,x]>=128)[0]
 return int(ys[-1]+y0) if len(ys) else None
support=[];left_witness=[]
for x in range(420,526):
 y=owned_low(x,880,1185)
 if y is not None:
  mapped=[420+(x-420)*95/105,y+(y-880)*6/306];support.append(mapped);left_witness.append({'uv':[x,y],'nativeSourceFoot':src(mapped),'alpha':int(a[y,x])})
L=make('NWLeftJambSurface',left,support)
uv=[[760,840],[908,840],[908,1030],[760,1030]];pos=[[760,840],[908,840],[908,1058],[760,1058]]
support=[];right_witness=[]
for x in range(760,909):
 y=owned_low(x,840,1030)
 if y is not None:
  mapped=[x,y+(y-840)*28/190];support.append(mapped);right_witness.append({'uv':[x,y],'nativeSourceFoot':src(mapped),'alpha':int(a[y,x])})
R=make('NWRightJambSurface',[(pos,uv)],support)
for c,witnesses in [(L,left_witness),(R,right_witness)]:
 pts=[v for m in c['members']for v in m['polygon']];lo=math.floor(min(v[0]for v in pts))-1;hi=math.ceil(max(v[0]for v in pts))+1
 a0,b0=c['supportLine'][0],c['supportLine'][min(10,len(c['supportLine'])-1)];slope=(b0[1]-a0[1])/(b0[0]-a0[0]);c['supportLine'].insert(0,[lo,a0[1]+(lo-a0[0])*slope])
 a0,b0=c['supportLine'][-11],c['supportLine'][-1];slope=(b0[1]-a0[1])/(b0[0]-a0[0]);c['supportLine'].append([hi,b0[1]+(hi-b0[0])*slope])
 c['physicalSupportWitnesses']=witnesses
 c['supportScope']='Physical support derives only from owned lower-jamb UV domain and its affine mapping. Upper curved-arch/no-material columns and subpixel margins use explicit virtual extension from the nearest physical baseline; not claimed physical pixels. Alpha128 locates measured edge only, native rendering alpha unchanged.'
(p/'northwest-surface-support-audit.json').write_text(json.dumps({'leftPhysicalColumns':len(left_witness),'rightPhysicalColumns':len(right_witness),'withdrawn':'Initial whole-image low(x) used upper/unowned pixels at left521..525 and right760, not valid physical supports. This table supersedes that claim.','components':[{'name':c['namePrefix'],'first':w[0],'last':w[-1],'virtualEndpoints':[c['supportLine'][0],c['supportLine'][-1]]}for c,w in [(L,left_witness),(R,right_witness)]]},indent=2)+'\n')
def source_support(curve,x):
 for a0,b0 in zip(curve,curve[1:]):
  if x<=b0[0]:return a0[1]+(b0[1]-a0[1])*(x-a0[0])/(b0[0]-a0[0])
 raise ValueError('shared structural support outside span')
# Retained upper material of a jamb shares its newly authored lower jamb support.
# Otherwise one wall has two depth owners at the UV reconstruction boundary.
sections=[]
for original_section in g['sections']:
 # y550 is the base of upper tower/lookout module, above the arch ring; do not
 # propagate the reconstructed arch-jamb support into separate upper roofs.
 xs=[v[0]for v in original_section['polygonPixels']];x0,x1=min(xs),max(xs)
 upper=copy.deepcopy(original_section);upper['id']+='-separate-upper-module';upper['polygonPixels']=[[x0,0],[x1,0],[x1,550],[x0,550]]
 section=copy.deepcopy(original_section);section['id']+='-arch-connected-facade';section['polygonPixels']=[[x0,550],[x1,550],[x1,1186],[x0,1186]]
 sections.extend([upper,section])
 for foot in section['supportLinePixels']:
  x=foot[0]
  owner=L if 420<=x<=525 else R if 760<=x<=908 else None
  if owner is not None:foot[1]=(source_support(owner['supportLine'],x*s+t[0])-t[1])/s
 g['supportOwnerContract']='Rebuilt lower-jamb support curve is also authoritative for retained upper material at same structural x; no independent old upper-foot progression.'
g['sections']=sections
(p/'northwest-native-base.json').write_text(json.dumps(g,indent=2)+'\n')
(p/'northwest-native-surfaces.json').write_text(json.dumps({'components':[L,R]},indent=2)+'\n')
