from pathlib import Path
from PIL import Image
import numpy as np
import json,math,hashlib
p=Path(__file__).resolve().parents[1]
repo=p.parent/'empire-c01'
out=Path(__file__).resolve().parent
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])

def inside(q,poly):
 x,y=q;hit=False
 for a,b in zip(poly,poly[1:]+poly[:1]):
  if abs(cross(a,b,q))<1e-9 and min(a[0],b[0])-1e-9<=x<=max(a[0],b[0])+1e-9 and min(a[1],b[1])-1e-9<=y<=max(a[1],b[1])+1e-9:return True
  if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:hit=not hit
 return hit

def ptseg(P,Q,R):
 v=R-Q;t=np.clip(np.sum((P-Q)*v,axis=1)/np.sum(v*v,axis=1),0,1);N=Q+t[:,None]*v
 return np.sum((P-N)**2,axis=1),N,t

def cc(a,b):return a[:,0]*b[:,1]-a[:,1]*b[:,0]

def scan(A,B,poly,meta):
 hit=np.zeros(len(A),dtype=bool);dist=np.full(len(A),float('inf'));best=[None]*len(A);lo=np.min(poly,axis=0);hi=np.max(poly,axis=0)
 for P in [A,B]:
  for i in np.where(np.all(P>=lo,axis=1)&np.all(P<=hi,axis=1))[0]:
   if inside(P[i],poly):hit[i]=True
 for q,r in zip(poly,poly[1:]+poly[:1]):
  Q=np.broadcast_to(np.array(q,dtype=float),A.shape);R=np.broadcast_to(np.array(r,dtype=float),A.shape)
  o1=cc(B-A,Q-A);o2=cc(B-A,R-A);o3=cc(R-Q,A-Q);o4=cc(R-Q,B-Q);hit|=(o1*o2<0)&(o3*o4<0)
  for P,X,Y,kind in [(A,Q,R,'moving-start'),(B,Q,R,'moving-end'),(Q,A,B,'fixed-edge-start'),(R,A,B,'fixed-edge-end')]:
   D,N,T=ptseg(P,X,Y);hit|=D<1e-16
   for i in np.where(D<dist)[0]:best[i]={'kind':kind,'point':P[i].tolist(),'nearestPoint':N[i].tolist(),'baseEdge':[q,r],'parameter':float(T[i])}
   dist=np.minimum(dist,D)
 i=int(np.argmin(dist));ids=np.where(hit)[0]
 return {'testedSegments':len(A),'intersectingSegments':len(ids),'minimumDistance':0.0 if len(ids) else math.sqrt(float(dist[i])),'closest':dict(meta[i],segment=[A[i].tolist(),B[i].tolist()],**best[i]),'firstViolations':[dict(meta[int(j)],segment=[A[int(j)].tolist(),B[int(j)].tolist()]) for j in ids[:3]]}
def hull(points):
 pts=sorted(set(tuple(v) for v in points));a=[];b=[]
 for q in pts:
  while len(a)>1 and cross(a[-2],a[-1],q)<=0:a.pop()
  a.append(q)
 for q in reversed(pts):
  while len(b)>1 and cross(b[-2],b[-1],q)<=0:b.pop()
  b.append(q)
 return [list(v) for v in a[:-1]+b[:-1]]
def relation(a,b):
 A=np.array(a,float);B=np.roll(A,-1,axis=0);r=scan(A,B,b,[{'edgeIndex':i} for i in range(len(a))]);r['otherVerticesInside']=sum(inside(v,a) for v in b);r['groundDomainsDisjoint']=not r['intersectingSegments'] and not r['otherVerticesInside'];return r
frozen=json.load(open(p/'central-east-planter-review/rear-white-planter-new-base-walk-corridor.json'));platform=json.load(open(p/'central-platform-source.json'));pier=json.load(open(p/'central-south-pier-source.json'));members={m['id']:m for m in pier['members']}
# Derive COMPLETE plinth ground from top+actual riser height; not cap/screen bbox.
top=members['plinth-top']['polygon'];left=members['plinth-left']['polygon'];height=left[3][1]-left[0][1]
assert height==left[2][1]-left[1][1] and height>0
center=[[x,y+height] for x,y in top]
assert all(any(abs(q[0]-v[0])<1e-8 and abs(q[1]-v[1])<1e-8 for q in center) for v in pier['supportLine'])
configs={}
for name in ['left','right']:
 d=json.load(open(p/f'central-south-{name}-planter-design.json'));im=Image.open(p.parent/d['assetPath']);roi=d['stoneFootRoi'];feet=[]
 for x in range(roi[0],roi[2]):
  ys=[y for y in range(roi[1],roi[3]) if im.getpixel((x,y))[3]>127]
  if ys:
   uv=[x,max(ys)];feet.append({'texture':uv,'source':[d['worldAnchor'][k]+(uv[k]-d['anchor'][k])*d['scale'] for k in range(2)]})
 configs[name]={'design':d,'actualVisibleFeet':feet,'groundHull':hull([v['source'] for v in feet]+[d['hiddenBack']])}
polys={k:v['groundHull'] for k,v in configs.items()};polys['center']=center
pairs={a+'__'+b:relation(polys[a],polys[b]) for a,b in [('left','center'),('right','center'),('left','right')]}
old=[[762,521],[767,519],[772,521],[767,524]];negative=relation(polys['left'],old)
S=platform['supportLine'];platformRelations={}
for name,poly in polys.items():
 z=scan(np.array(S[:-1],float),np.array(S[1:],float),poly,[{'supportSegment':i} for i in range(len(S)-1)])
 offsets=[]
 for v in poly:
  y=491+(v[0]-714)*27/53 if v[0]<=767 else 518-(v[0]-767)*27/57
  offsets.append({'point':v,'frontOffset':v[1]-y})
 z['minimumVertexFrontOffset']=min(offsets,key=lambda q:q['frontOffset']);platformRelations[name]=z
legal=[tuple(v) for v in frozen['canonicalLegalCells']];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];aa=[];bb=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    d=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']]
    aa.append([16*a[k]+8+d[k] for k in range(2)]);bb.append([16*b[k]+8+d[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(aa);B=np.array(bb);walk={name:scan(A,B,poly,meta) for name,poly in polys.items()}
# Additional SOURCE-INFORMED contact band only, not an invented full old planter base.
# Deliberately bounded to manually observed stone-to-floor edge; ambiguity retained.
observedBand=[[736,511],[740,512],[744,515],[750,518],[749,519],[744,518],[740,515],[736,514]]
observedWalk=scan(A,B,observedBand,meta)
# Coherent continuous wall-base proposal near old enclosure line. NEW DESIGN, not original certification.
# Near edge follows source lower-band; 3px strip joins/overlaps current left foundation at its right end.
strip=[[736,512],[740,514],[749,519],[751,516],[742,511],[736,509]]
stripWalk=scan(A,B,strip,meta);stripLeft=relation(strip,polys['left'])
level=next(v for v in json.load(open(repo/'godot/data/grayflag-campaign-levels.json')) if v['id']=='c01-07')
# Integer scan provides witnesses only; enclosing occupied cell identities below explicitly retained.
stripSamples=[{'point':[x,y],'terrain':level['terrain'][y//16][x//16]} for y in range(509,520) for x in range(736,752) if inside([x,y],strip)]
r={'ready':True,'scope':'V2 unified static ground foot math, fresh formal design PNG feet and full plinth base from actual top+riser. No engine/GUI/actions. Prior v1 report untouched.','inputs':{'pier':'central-south-pier-source.json','pierSha256':hashlib.sha256((p/'central-south-pier-source.json').read_bytes()).hexdigest(),'planters':['central-south-left-planter-design.json','central-south-right-planter-design.json']},'centerDerivation':{'plinthTop':top,'riserHeight':height,'completeGroundFootprint':center,'note':'Shaft visual height has no effect on base.'},'planters':configs,'groundPairRelations':pairs,'platformFrontSupportRelations':platformRelations,'walkCorridors':walk,'negativeControl':{'oldCenterDiamond':old,'leftRelation':negative,'expected':'OLD diamond must intersect left hull; previous independently computed area0.012481600075261667 retained in v1 report.'},'oldLeftContactBandStudy':{'polygon':observedBand,'status':'Conservative SOURCE material-to-floor transition band, NOT exact original full footprint. Some gray edge is shadow-blended.','walk':observedWalk,'conclusion':'No contact with this bounded observed band means the original three stone/edge points cannot honestly be dismissed as a proven old foot/walk collision. It does not certify hidden rear/base.'},'continuousLeftStoneBaseSuggestion':{'groundPolygon':strip,'frontSupportLine':[[736,512],[740,514],[749,519]],'hiddenRear':'Rear strip closure is NEW DESIGN, not original foot.', 'walk':stripWalk,'relationToNewLeftPlanter':stripLeft,'sampleCount':len(stripSamples),'nonBlockedIntegerSamples':[v for v in stripSamples if v['terrain']!='#'],'purpose':'Source-informed continuous enclosure/base edge, not isolated plates at744514. A short stone face supported by this line can restore740508/744514; exact height/material geometry remains design/native validation.','joinRequirement':'This strip touches/overlaps the current planter base and must be deliberately constructed as one joined foundation/edge, not independent solid pieces claiming disjointness. Assign local support line; do not inherit planter front524.5 and swallow earlier-facing shoes.','notAccepted':'Suggestion only. Full 3D/visual join, pixel support, current local actor controls and potential lost source domains still require owner validation.'},'limitations':['Original736512 is a gray transition band, not uniquely certified wood/stone contact; 740508 is wall face;744514 is lower stone face above contact.','No bbox or whole sprite alpha used as physical ground.','The formal source has no inferred full platform hidden volume; compare its actual southern support trace only.']}
(out/'south-three-ground-domains-v2-and-left-source-band.json').write_text(json.dumps(r,indent=2)+'\n')
for name,z in pairs.items():print(name,z['groundDomainsDisjoint'],z['minimumDistance'])
print('OLD_NEGATIVE',negative['groundDomainsDisjoint'],negative['intersectingSegments'])
print('walk',{k:(v['intersectingSegments'],v['minimumDistance']) for k,v in walk.items()})
print('OBSERVED_BAND',observedWalk['intersectingSegments'],observedWalk['minimumDistance'])
print('STRIP',stripWalk['intersectingSegments'],stripWalk['minimumDistance'],'joinsLeft',not stripLeft['groundDomainsDisjoint'],'nonblocked',r['continuousLeftStoneBaseSuggestion']['nonBlockedIntegerSamples'])
