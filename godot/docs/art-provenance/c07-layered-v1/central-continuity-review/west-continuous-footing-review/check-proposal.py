from pathlib import Path
import json,math
import numpy as np
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';out=p/'west-continuous-footing-review'
frozen=json.load(open(p/'central-east-planter-review/rear-white-planter-new-base-walk-corridor.json'));white=json.load(open(p/'central-white-planter-actual-footing.json'));wood=json.load(open(p/'central-west-wood-planter-grounded-footing.json'));design=json.load(open(p/'central-white-planter-design.json'));wd=json.load(open(p/'central-west-wood-planter-design.json'));platform=json.load(open(p/'central-platform-source.json'))
# The current grounded west registration is explicitly frozen by latest task.
wd['worldAnchor']=[699.5,491]
feet=[{'texture':v['texture'],'source':[v['source'][0]-138,v['source'][1]-7]} for v in white['actualVisibleStoneFeet']]
base=[[x-138,y-7] for x,y in white['newConservativeBaseHull']]
connector=[[685,476],[699,480],[714,486],[714,491],[706,493],[699,493],[685,484],[680,480]]
def box(d):return [[d['worldAnchor'][k]+(q[k]-d['anchor'][k])*d['scale'] for k in range(2)] for q in d['stoneWholeBox']]
design['worldAnchor']=[696,473];design['hiddenBack']=[696,460]
wbox=box(design);woodbox=box(wd)
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def inside(q,poly):
 for a,b in zip(poly,poly[1:]+poly[:1]):
  if abs(cross(a,b,q))<1e-8 and min(a[0],b[0])-1e-8<=q[0]<=max(a[0],b[0])+1e-8 and min(a[1],b[1])-1e-8<=q[1]<=max(a[1],b[1])+1e-8:return True
 x,y=q;hit=False
 for a,b in zip(poly,poly[1:]+poly[:1]):
  if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:hit=not hit
 return hit
legal=[tuple(v) for v in frozen['canonicalLegalCells']];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1]
meta=[];aa=[];bb=[];s=frozen['scale']
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    delta=[(v[0]-f['anchor'][0])*s*(-1 if west else 1),(v[1]-f['anchor'][1])*s]
    aa.append([16*a[k]+8+delta[k] for k in range(2)]);bb.append([16*b[k]+8+delta[k] for k in range(2)])
    meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(aa);B=np.array(bb);AB=B-A
# vector point to segment distances, then proper crossings. Closed boundaries count as hits.
def cc(a,b):return a[:,0]*b[:,1]-a[:,1]*b[:,0]
def ptseg(P,Q,R):
 v=R-Q;t=np.clip(np.sum((P-Q)*v,axis=1)/np.sum(v*v,axis=1),0,1);N=Q+t[:,None]*v
 return np.sum((P-N)**2,axis=1),N,t

def scan(poly):
 dist=np.full(len(A),float('inf'));closest=[None]*len(A);hit=np.zeros(len(A),dtype=bool)
 # endpoint containment cheaply restricted by boundingbox
 lo=np.min(poly,axis=0);hi=np.max(poly,axis=0)
 for P in [A,B]:
  ids=np.where(np.all(P>=lo,axis=1)&np.all(P<=hi,axis=1))[0]
  for i in ids:
   if inside(P[i],poly):hit[i]=True
 for q,r in zip(poly,poly[1:]+poly[:1]):
  Q=np.broadcast_to(np.array(q,dtype=float),A.shape);R=np.broadcast_to(np.array(r,dtype=float),A.shape)
  o1=cc(AB,Q-A);o2=cc(AB,R-A);o3=cc(R-Q,A-Q);o4=cc(R-Q,B-Q)
  proper=(o1*o2<0)&(o3*o4<0);hit|=proper
  for P,X,Y,kind in [(A,Q,R,'start'),(B,Q,R,'end'),(Q,A,B,'fixed-edge-start'),(R,A,B,'fixed-edge-end')]:
   D,N,T=ptseg(P,X,Y);hit|=(D<1e-14)
   better=np.where(D<dist)[0]
   for i in better:closest[i]={'baseEdge':[q,r],'kind':kind,'fixedOrMovingPoint':P[i].tolist(),'nearestPoint':N[i].tolist(),'parameter':float(T[i])}
   dist=np.minimum(dist,D)
 ids=np.where(hit)[0];i=int(np.argmin(dist))
 return {'testedSoleSegments':len(A),'violationsCount':len(ids),'firstViolations':[dict(meta[int(j)],sourceSegment=[aa[int(j)],bb[int(j)]]) for j in ids[:10]],'minimumEuclideanClearance':0 if len(ids) else math.sqrt(float(dist[i])),'closest':dict(meta[i],sourceSegment=[aa[i],bb[i]],**closest[i])}
level=next(v for v in json.load(open(r/'empire-c01/godot/data/grayflag-campaign-levels.json')) if v['id']=='c01-07')
def ter(pt):return {'cell':[math.floor(pt[0]/16),math.floor(pt[1]/16)],'terrain':level['terrain'][math.floor(pt[1]/16)][math.floor(pt[0]/16)]}
# Physical body separation: source-projected solid-stone rectangles only, no foliage.
plat=[]
for m in platform['members']:
 boxp=[[min(q[k] for q in m['polygon']) for k in range(2)],[max(q[k] for q in m['polygon']) for k in range(2)]]
 dis=any(wbox[1][k]<boxp[0][k] or boxp[1][k]<wbox[0][k] for k in range(2));plat.append({'id':m['id'],'bounds':boxp,'whiteBoxDisjoint':dis})
# Samples every integer coordinate inside proposed connector, plus all vertices.
ipts=[[x,y] for y in range(476,494) for x in range(680,715) if inside([x,y],connector)]
nonblocked=[{'point':q,**ter(q)} for q in ipts if ter(q)['terrain']!='#']
rpt={'ready':True,'scope':'Read-only static math; no engine/GUI/actions. This registration and connector are new design, not original-foot reconstruction. Same frozen 175 Torren lower-support boot points, continuous segment sweep.','whiteDesign':design,'actualVisibleStoneFeet':feet,'whiteBaseHullWithAuthoredHiddenRear':base,'whiteWholeStoneBox':wbox,'currentWestWoodWholeStoneBox':woodbox,'whiteWoodWholeBoxDisjoint':any(wbox[1][k]<woodbox[0][k] or woodbox[1][k]<wbox[0][k] for k in range(2)),'whiteWoodVerticalBodyGap':woodbox[0][1]-wbox[1][1],'whitePlatform':plat,'whiteFeetNotBlocked':[dict(v,**ter(v['source'])) for v in feet if ter(v['source'])['terrain']!='#'],'whiteWalk':scan(base),'connectorProposal':connector,'connectorIntegerFootAreaSamples':len(ipts),'connectorNonBlockedSamples':nonblocked,'connectorWalk':scan(connector),'connectorFrozenSourceResidual':{'point':[706,490],'inside':inside([706,490],connector),'terrain':ter([706,490])},'woodFeetInsideConnectorCount':sum(inside(v['source'],connector) for v in wood['actualVisibleStoneFeet']),'woodFeetTotal':len(wood['actualVisibleStoneFeet']),'woodFootHull':wood['newConservativeBaseHull'],'limitations':['Stone wholebox is conservative projected material, not ground footprint.','New connector may be designed as joined base under wooden planter, but solid-body visual join still needs authoring/native view. A 2px raise must not be assumed to preserve current planter ground contact.','Only the frozen Torren support contours/2 phases/possible facing contract, not all appearances or live walk.','No claim original all50 cell foundations completed.']}
(out/'white-and-low-base-proposal-static-review.json').write_text(json.dumps(rpt,indent=2)+'\n')
for k in ['whiteWholeStoneBox','currentWestWoodWholeStoneBox','whiteWoodWholeBoxDisjoint','whiteWoodVerticalBodyGap','whiteFeetNotBlocked','whiteWalk','connectorIntegerFootAreaSamples','connectorNonBlockedSamples','connectorWalk','woodFeetInsideConnectorCount','woodFeetTotal']:print(k,json.dumps(rpt[k]))
