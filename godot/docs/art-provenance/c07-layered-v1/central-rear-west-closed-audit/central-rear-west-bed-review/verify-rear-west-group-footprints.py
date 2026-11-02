from pathlib import Path
import json,math
import numpy as np
p=Path(__file__).resolve().parents[1]
repo=p.parent/"empire-c01"
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

def relation(a,b):
 A=np.array(a,float);B=np.roll(A,-1,axis=0);r=scan(A,B,b,[{'edgeIndex':i} for i in range(len(a))]);r['otherVerticesInside']=sum(inside(v,a) for v in b);r['groundDomainsDisjoint']=not r['intersectingSegments'] and not r['otherVerticesInside'];return r
bench=[[722,460],[740,452],[735,447],[717,455]]
bed=[[735,449],[744,454],[750,450],[741,445]]
alternative=[[741,450],[745,454],[750,451],[744,446]]
root=[743,449]
frozen=json.load(open(p/'central-east-planter-review/rear-white-planter-new-base-walk-corridor.json'));platform=json.load(open(p/'central-platform-source.json'));core=json.load(open(p/'central-landmark-footing-result.json'))['visibleFootSamples'];level=next(v for v in json.load(open(repo/'godot/data/grayflag-campaign-levels.json')) if v['id']=='c01-07')
def area(ps):return abs(sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(ps,ps[1:]+ps[:1])))/2 if ps else 0
def intersection(subject,clipper):
 ps=[list(q) for q in subject];signed=sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(clipper,clipper[1:]+clipper[:1]));sgn=1 if signed>0 else -1
 for q,r in zip(clipper,clipper[1:]+clipper[:1]):
  if not ps:break
  result=[]
  for a,b in zip(ps,ps[1:]+ps[:1]):
   ca=cross(q,r,a)*sgn;cb=cross(q,r,b)*sgn;ia=ca>=-1e-9;ib=cb>=-1e-9
   if ia:result.append(a)
   if ia!=ib:
    t=ca/(ca-cb);result.append([a[k]+t*(b[k]-a[k]) for k in range(2)])
  ps=result
 return ps
polys={'woodFourSupportHull':bench,'stoneBedOriginalProposal':bed,'stoneBedBoundedAlternative':alternative};legal=[tuple(v) for v in frozen['canonicalLegalCells']];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];aa=[];bb=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    d=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']]
    aa.append([16*a[k]+8+d[k] for k in range(2)]);bb.append([16*b[k]+8+d[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(aa);B=np.array(bb);rows={}
for name,poly in polys.items():
 cells=[]
 for y in range(27,29):
  for x in range(44,47):
   q=intersection(poly,[[16*x,16*y],[16*x+16,16*y],[16*x+16,16*y+16],[16*x,16*y+16]])
   if area(q)>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'positiveArea':area(q)})
 coreInside=[v for v in core if inside(v['source'],poly)];near=(float('inf'),None)
 for v in core:
  P=np.array([v['source']],float)
  for q,r in zip(poly,poly[1:]+poly[:1]):
   D,N,T=ptseg(P,np.array([q],float),np.array([r],float))
   if D[0]<near[0]:near=(float(D[0]),{'coreFoot':v,'nearestPoint':N[0].tolist(),'proposalEdge':[q,r]})
 overlaps=[]
 for m in platform['members']:
  q=intersection(poly,m['polygon'])
  if area(q)>1e-8:overlaps.append({'member':m['id'],'projectedArea':area(q),'intersectionPolygon':q})
 rows[name]={'polygon':poly,'terrain':cells,'allPositiveAreaBlocked':all(v['terrain']=='#' for v in cells),'walk':scan(A,B,poly,meta),'coreVisibleFeet':{'count':len(core),'insideCount':len(coreInside),'minimumDistance':math.sqrt(near[0]),'closest':near[1],'hiddenCoreGroundVolume':'Uncertified; no envelope used.'},'selectedPlatformProjectedMemberOverlaps':overlaps,'platformScope':'Actual member geometric polygons only. No invented closed ground hull from top surface or bbox. No projected overlap means not currently placed visibly atop an existing tread here; hidden rear-volume separation remains uncertified.'}
join=intersection(bench,bed);r=relation(bench,bed);r['supportHullIntersectionPolygon']=join;r['supportHullIntersectionArea']=area(join);r['fourBenchFootPointsInsideStone']=[v for v in bench if inside(v,bed)];r['meaning']='The bench quadrilateral is only a hull of four proposed supports unless a continuous plinth is explicitly intended. Positive hull area intersection is NOT itself proof solid legs collide. No finite leg footprint widths were supplied.'
# Closest actual supplied wood support point to the stone boundary.
near=(float('inf'),None)
for v in bench:
 P=np.array([v],float)
 for q,z in zip(bed,bed[1:]+bed[:1]):
  D,N,T=ptseg(P,np.array([q],float),np.array([z],float))
  if D[0]<near[0]:near=(float(D[0]),{'woodSupportPoint':v,'stoneEdge':[q,z],'nearestPoint':N[0].tolist()})
r['minimumPointSupportClearance']=math.sqrt(near[0]);r['closestPointSupport']=near[1]
altRelation=relation(bench,alternative)
report={'ready':True,'scope':'One bounded rear-west whole-group static footprint evaluation. No engine/GUI/action/save. Wood seat height8 does not change supplied ground foot domains.','sourceInputs':{'woodFrontFeet':[[722,460],[740,452]],'newWoodBackFeet':[[717,455],[735,447]],'stoneHiddenFullGroundProposal':bed,'flowerRoot':[743,449]},'domainResults':rows,'woodSupportHullVersusStone':r,'boundedIndependentAlternative':{'newStoneGroundPolygon':alternative,'flowerRootStillInside':inside(root,alternative),'woodRelation':altRelation,'reason':'Source-certified gray wall is only around743..748; original left reach735 was already hidden NEW DESIGN. A compact right-hand bed startingx741 preserves the fixed743449 root and visible-side neighbourhood without placing its foundation within the seat support hull. This does not claim original unseen foot restoration.','status':'One conditional geometric suggestion only, not native/visual acceptance. If a deliberately shared foundation is intended, its ownership must be explicit instead; do not call two overlapping solid bases disjoint.'},'limitations':['Core670 visible stone feet are not its full hidden base.','Actual platform support/material extent is not a bbox-derived foundation.','Original front wood contact bands do not certify four square leg widths or original floor elevation.','Need actual finite foot geometry before claiming0.194px point clearance is enough for the original candidate.']}
(out/'rear-west-group-footprint-proposal-math.json').write_text(json.dumps(report,indent=2)+'\n')
for k,v in rows.items():print(k,'walk',v['walk']['intersectingSegments'],v['walk']['minimumDistance'],'core',v['coreVisibleFeet']['insideCount'],v['coreVisibleFeet']['minimumDistance'],'platform',v['selectedPlatformProjectedMemberOverlaps'],'terrain',v['allPositiveAreaBlocked'])
print('JOIN',r);print('ALT',altRelation['groundDomainsDisjoint'],altRelation['minimumDistance'],'root',inside(root,alternative))
