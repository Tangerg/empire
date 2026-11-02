import sys
from pathlib import Path
from PIL import Image
import json,math,hashlib
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
sourcePath=Path(sys.argv[1]) if len(sys.argv)>1 else p/'central-rear-west-group-source.json';sourceBytes=sourcePath.read_bytes();d=json.loads(sourceBytes);legs=d['actualWoodLegFeet'];bed=d['bedHiddenFootDesign'];polys={**legs,'bed':bed};frozen=json.load(open(p/'central-east-planter-review/rear-white-planter-new-base-walk-corridor.json'));platform=json.load(open(p/'central-platform-source.json'));core=json.load(open(p/'central-landmark-footing-result.json'))['visibleFootSamples'];level=next(v for v in json.load(open(repo/'godot/data/grayflag-campaign-levels.json')) if v['id']=='c01-07')
legal=[tuple(v) for v in frozen['canonicalLegalCells']];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];aa=[];bb=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    offset=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']]
    aa.append([16*a[k]+8+offset[k] for k in range(2)]);bb.append([16*b[k]+8+offset[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(aa);B=np.array(bb);rows={}
for name,poly in polys.items():
 cells=[]
 for y in range(27,29):
  for x in range(44,47):
   q=intersection(poly,[[16*x,16*y],[16*x+16,16*y],[16*x+16,16*y+16],[16*x,16*y+16]])
   if area(q)>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'area':area(q)})
 coreHits=[v for v in core if inside(v['source'],poly)];near=(float('inf'),None)
 for v in core:
  P=np.array([v['source']],float)
  for q,r in zip(poly,poly[1:]+poly[:1]):
   dist,N,T=ptseg(P,np.array([q],float),np.array([r],float))
   if dist[0]<near[0]:near=(float(dist[0]),{'coreFoot':v,'closestPoint':N[0].tolist(),'domainEdge':[q,r]})
 overlaps=[]
 for m in platform['members']:
  q=intersection(poly,m['polygon'])
  if area(q)>1e-8:overlaps.append({'member':m['id'],'projectedArea':area(q)})
 rows[name]={'groundFootprint':poly,'positiveAreaCells':cells,'allBlocked':all(v['terrain']=='#' for v in cells),'walk':scan(A,B,poly,meta),'coreVisibleFootSamples':{'count':len(core),'insideCount':len(coreHits),'minimumDistance':math.sqrt(near[0]),'closest':near[1]},'platformActualMemberProjectedOverlaps':overlaps}
pairs={}
names=list(polys)
for i,a in enumerate(names):
 for b in names[i+1:]:pairs[a+'__'+b]=relation(polys[a],polys[b])
parts={v['id']:v for v in d['parts']};bedtop=[parts['bed-top-0']['polygon'][0],parts['bed-top-0']['polygon'][1],parts['bed-top-1']['polygon'][1],parts['bed-top-2']['polygon'][1]];soil=parts['bed-soil']['polygon'];root=d['foliageRoot'];groundRoot=d['foliageGroundRoot'];fol=parts['bed-foliage']['source']['members'][0]
# Source carrier is affine; independently solve UV->source and invert declared root.
uv=np.array(fol['uv'],float);xy=np.array(fol['polygon'],float);transform=np.linalg.lstsq(np.column_stack([uv,np.ones(len(uv))]),xy,rcond=None)[0];residual=float(np.max(np.abs(np.column_stack([uv,np.ones(len(uv))])@transform-xy)));uvroot=np.linalg.solve(transform[:2].T,np.array(root)-transform[2]);im=Image.open(repo/'godot/assets/native/c07-layered-v1/central-front-wood-planter-v1-candidate.png');rp=[math.floor(float(v)+1e-8) for v in uvroot];alpha=im.getpixel(tuple(rp))[3]
r={'ready':True,'scope':'Final bounded static foot-domain audit of actual four finite2px wooden legs, not support points or a filled support hull. Fresh source JSON, no GUI/engine/action/save.','sourceFile':str(sourcePath),'sourceSha256':hashlib.sha256(sourceBytes).hexdigest(),'domainResults':rows,'finiteDomainPairRelations':pairs,'roots':{'declaredProjectedRoot':root,'declaredGroundRoot':groundRoot,'groundRootInsideBedFootprint':inside(groundRoot,bed),'projectedRootInsideBedTop':inside(root,bedtop),'projectedRootInsideActualSoil':inside(root,soil),'actualSoilPolygon':soil,'heightDifference':groundRoot[1]-root[1],'bedTopEqualsGroundShiftUp6':all(abs((q[0]-f[0]))<1e-8 and abs(q[1]+6-f[1])<1e-8 for q,f in zip(bedtop,bed)),'carrierAffineResidual':residual,'inverseRootUv':uvroot.tolist(),'sampledUvPixel':rp,'nativeAlphaAtRoot':alpha,'meaning':'Declared new root/carrier lands on the supported top; not original hidden botanical root certification.'},'frozenSourceWoodEndsInsideActualFeet':{'left':inside([722,460],legs['left']),'right':inside([740,452],legs['right'])},'limitations':['Core comparison uses670 visible feet only; hidden core base not globally certified and no bbox was used.','Platform comparison uses actual selected member geometry; absence of projected overlap does not invent a complete hidden platform foundation.','Fixed175 real Torren lower-support boot points, continuous324legal-edge sweep; no all-appearance or actual motion claim.','No visual acceptance of seat/backrest silhouette or native target/depth granted by math.']}
r['mathPassed']=all(v['allBlocked'] and v['walk']['intersectingSegments']==0 and v['coreVisibleFootSamples']['insideCount']==0 and not v['platformActualMemberProjectedOverlaps'] for v in rows.values()) and all(v['groundDomainsDisjoint'] for v in pairs.values()) and r['roots']['groundRootInsideBedFootprint'] and r['roots']['projectedRootInsideBedTop'] and r['roots']['projectedRootInsideActualSoil'] and alpha>127 and all(r['frozenSourceWoodEndsInsideActualFeet'].values())
if len(sys.argv)<2:(out/'rear-west-actual-four-leg-input-source.json').write_bytes(sourceBytes)
(Path(sys.argv[2]) if len(sys.argv)>2 else out/'rear-west-actual-four-leg-footing-final.json').write_text(json.dumps(r,indent=2)+'\n')
print('PASSED',r['mathPassed']);print('legbed',[(k,v['minimumDistance']) for k,v in pairs.items() if k.endswith('__bed')]);print('walk',[(k,v['walk']['intersectingSegments'],v['walk']['minimumDistance']) for k,v in rows.items()]);print('roots',r['roots']);print('ends',r['frozenSourceWoodEndsInsideActualFeet'])

assert all(v["groundDomainsDisjoint"] for v in pairs.values()), "Finite wooden leg and stone bed domains must remain disjoint"
assert r["mathPassed"], "Rear-west full foot-domain and connected root contract failed"
