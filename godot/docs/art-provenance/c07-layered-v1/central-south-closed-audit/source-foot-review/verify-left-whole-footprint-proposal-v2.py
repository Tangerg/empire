from pathlib import Path
from PIL import Image
import numpy as np
import json,math
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
poly=[[733,509],[749,518],[758,514],[743,507]]
v2=json.load(open(out/'south-three-ground-domains-v2-and-left-source-band.json'));frozen=json.load(open(p/'central-east-planter-review/rear-white-planter-new-base-walk-corridor.json'));S=json.load(open(p/'central-platform-source.json'))['supportLine']
legal=[tuple(v) for v in frozen['canonicalLegalCells']];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];aa=[];bb=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    d=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']]
    aa.append([16*a[k]+8+d[k] for k in range(2)]);bb.append([16*b[k]+8+d[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
walk=scan(np.array(aa),np.array(bb),poly,meta);platform=scan(np.array(S[:-1],float),np.array(S[1:],float),poly,[{'supportSegment':i} for i in range(2)])
def segmentIntersection(a,b,c,d):
 u=np.array(b)-a;v=np.array(d)-c;w=np.array(c)-a;den=u[0]*v[1]-u[1]*v[0]
 if abs(den)<1e-12:return None
 t=(w[0]*v[1]-w[1]*v[0])/den;q=(w[0]*u[1]-w[1]*u[0])/den
 if -1e-9<=t<=1+1e-9 and -1e-9<=q<=1+1e-9:return [float(a[k]+t*u[k]) for k in range(2)]
intersections=[]
for a,b in zip(S,S[1:]):
 for c,d in zip(poly,poly[1:]+poly[:1]):
  q=segmentIntersection(a,b,c,d)
  if q:intersections.append({'point':q,'proposedEdge':[c,d],'platformSupportEdge':[a,b]})
# Exact convex polygon clipping gives all positive-area terrain allocations.
def clip(ps,axis,edge,sign):
 r=[]
 for a,b in zip(ps,ps[1:]+ps[:1]):
  ia=(a[axis]-edge)*sign>=-1e-9;ib=(b[axis]-edge)*sign>=-1e-9
  if ia:r.append(a)
  if ia!=ib:
   t=(edge-a[axis])/(b[axis]-a[axis]);r.append([a[k]+t*(b[k]-a[k]) for k in range(2)])
 return r
level=next(v for v in json.load(open(repo/'godot/data/grayflag-campaign-levels.json')) if v['id']=='c01-07');cells=[]
for y in range(31,33):
 for x in range(45,48):
  ps=[v[:] for v in poly]
  for axis,edge,sgn in [(0,16*x,1),(0,16*(x+1),-1),(1,16*y,1),(1,16*(y+1),-1)]:
   if ps:ps=clip(ps,axis,edge,sgn)
  area=abs(sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(ps,ps[1:]+ps[:1])))/2 if ps else 0
  if area>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'area':area})
source=Image.open(repo/'godot/assets/native/scenes/luojia.png')
marks=[]
for q,status in [([733,509],'visible lower gray band, exact stone/shadow contact uncertain'),([749,517],'dark near-corner contact band'),([749,518],'dark near-corner contact band'),([749,519],'paving immediately below near corner'),([758,513],'pale central low-column plinth upper-left material, NOT certified left-planter right foot'),([758,514],'same adjacent central plinth pale face')]:marks.append({'point':q,'rgb':list(source.getpixel(tuple(q))),'classification':status})
faces=[{'id':'stone-left','polygon':[[733,503],[749,512],[749,518],[733,509]]},{'id':'stone-right','polygon':[[749,512],[758,508],[758,514],[749,518]]}]
r={'ready':True,'scope':'One bounded whole-left-planter replacement ground proposal, not the earlier narrow joining edge. Static source+math only.','proposal':poly,'newDesignRightAndRear':[[758,514],[743,507]],'sixPixelStoneBodyFrontFaces':faces,'frozenStoneMaterialBearing':[{'sourcePoint':q,'faceOwners':[f['id'] for f in faces if inside(q,f['polygon'])]} for q in [[740,508],[744,514],[736,512]]],'sourceLandmarkStatus':marks,'sourceOriginalFeetConclusion':'Near749517..518 is supported. Left733509 is a lower-edge band, not exact isolated corner; right758513 is another owner/hidden join, so all three must not be called original planter feet.','terrain':{'positiveAreaCells':cells,'allBlocked':all(v['terrain']=='#' for v in cells)},'walk':walk,'platformActualSupport':platform,'platformSupportCrossings':intersections,'newRightPointInFrontOfPlatformBy':514-(491+(758-714)*27/53),'relations':{'newCentralPlinth':relation(poly,v2['centerDerivation']['completeGroundFootprint']),'newRightPlanter':relation(poly,v2['planters']['right']['groundHull'])},'decision':'Updated right758514 and rear743507 are explicit new design. Evaluate full results; original right758513 source label remains withdrawn. A geometric pass is not native body/depth acceptance.','boundedAdjustmentConstraint':{'rightX':758,'platformGroundFrontY':491+(758-714)*27/53,'requirement':'Any independently ground-standing right closure atx758 must stay in front of this true support boundary, with the entire connecting edge checked. This is a boundary, not an approved point or invented tolerance.'},'limitations':['Only local front support crossing is proven; no hidden full platform volume inferred from bbox.','Zero boots is not acceptance of wrong material ownership or partly stair-bearing object.','No production changes, GUI/engine/actions, or pixel edits.']}
(out/'left-source-whole-footprint-proposal-v2-review.json').write_text(json.dumps(r,indent=2)+'\n')
print('faceBearing',r['frozenStoneMaterialBearing']);print('terrain',cells);print('walk',walk['intersectingSegments'],walk['minimumDistance']);print('crossings',intersections);print('relations',{k:(v['groundDomainsDisjoint'],v['minimumDistance']) for k,v in r['relations'].items()})
