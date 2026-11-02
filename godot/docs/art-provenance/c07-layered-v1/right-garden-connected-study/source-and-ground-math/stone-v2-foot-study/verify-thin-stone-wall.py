from pathlib import Path
import json,math,hashlib,ast
import numpy as np
from PIL import Image
out=Path(__file__).resolve().parent;root=out.parents[2];repo=root/'empire-c01';regpath=root/'c07-static-preview/west-inner-right-garden-stone-v2-registration.json';reg=json.loads(regpath.read_text());rawpath=root/'generated_images/exec-7d6219ab-ee72-49a8-a915-673252dc671a.png';im=Image.open(rawpath).convert('RGBA');frozen=json.loads((root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json').read_text());houses=json.loads((root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json').read_text());level=next(v for v in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if v['id']=='c01-07')
s=root/'c07-static-preview/central-rear-west-bed-review/verify-rear-west-actual-four-leg-footing.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name in ['inside','cross','area','intersection','cc','ptseg']],type_ignores=[]),str(s),'exec'))
def world(p):return (np.array(p)*reg['scale']+reg['worldOrigin']).tolist()
foot=np.array([world(p) for p in reg['visibleStoneFoot']]);hiddenAsset=[[1277,313],[899,565],[270,808]];poly=foot.tolist()+[world(p) for p in hiddenAsset]
# These explicit hidden anchors form a NEW thin common base, not the source audit17cell polygon.
# Near: H=L+R-F from visible side feet145859,270927,395876.
# Middle: visible right base1008616/front890670 plus cap left-axis displacement(-109,-51)
# gives NEW left781619 and hidden899565. Far: visible near return1350428/right1417366,
# cap left-axis continuation produces NEW hidden-left1210375, hence rear1277313.
legal=[(x,y) for y in range(23,32) for x in range(31,40) if level['terrain'][y][x] not in '#_'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];aa=[];bb=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    off=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']];aa.append([16*a[k]+8+off[k] for k in range(2)]);bb.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(aa);B=np.array(bb)
def boundary_scan(A,B,P,meta):
 # Exact continuous segment distances/intersections. AABB pruning uses a witnessed upper bound,
 # not a geometry tolerance. Excluded pairs cannot improve it or intersect.
 mn=np.minimum(A,B);mx=np.maximum(A,B);best=float('inf');witness=None;hit=np.zeros(len(A),bool)
 for q in P[::max(1,len(P)//12)]:
  Q=np.broadcast_to(q,A.shape);D,N,T=ptseg(Q,A,B);i=int(np.argmin(D))
  if D[i]<best:best=float(D[i]);witness={'actor':meta[i],'point':Q[i].tolist(),'nearestPoint':N[i].tolist(),'kind':'fixed-boundary-sample'}
 for k,(q,r) in enumerate(zip(P,P[1:]+P[:1])):
  lo=np.minimum(q,r);hi=np.maximum(q,r);gap=np.maximum(np.maximum(lo-mx,mn-hi),0);ids=np.flatnonzero(np.sum(gap*gap,axis=1)<=best+1e-14)
  if len(ids)==0:continue
  a=A[ids];b=B[ids];Q=np.broadcast_to(q,a.shape);R=np.broadcast_to(r,a.shape);o1=cc(b-a,Q-a);o2=cc(b-a,R-a);o3=cc(R-Q,a-Q);o4=cc(R-Q,b-Q);crossings=(o1*o2<0)&(o3*o4<0);hit[ids[crossings]]=True
  for pp,xx,yy,kind in [(a,Q,R,'actor-start'),(b,Q,R,'actor-end'),(Q,a,b,'base-edge-start'),(R,a,b,'base-edge-end')]:
   D,N,T=ptseg(pp,xx,yy);hit[ids[D<1e-16]]=True;j=int(np.argmin(D))
   if D[j]<best:
    i=int(ids[j]);best=float(D[j]);witness={'actor':meta[i],'actorSweep':[A[i].tolist(),B[i].tolist()],'kind':kind,'point':pp[j].tolist(),'nearestPoint':N[j].tolist(),'baseEdgeIndex':k,'baseEdge':[q,r],'parameter':float(T[j])}
 return {'testedSegments':len(A),'intersectingSegments':int(hit.sum()),'minimumDistance':0 if hit.any() else math.sqrt(best),'closestWitness':witness,'firstViolations':[meta[i] for i in np.flatnonzero(hit)[:3]]}
walk=boundary_scan(A,B,poly,meta);lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);possible=np.flatnonzero((np.all(A>=lo,axis=1)&np.all(A<=hi,axis=1))|(np.all(B>=lo,axis=1)&np.all(B<=hi,axis=1)));insideIds=[int(i) for i in possible if inside(A[i],poly) or inside(B[i],poly)]
cells=[]
for y in range(math.floor(lo[1]/16),math.floor(hi[1]/16)+1):
 for x in range(math.floor(lo[0]/16),math.floor(hi[0]/16)+1):
  cut=intersection(poly,[[16*x,16*y],[16*x+16,16*y],[16*x+16,16*y+16],[16*x,16*y+16]]);v=area(cut)
  if v>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'area':v})
relations={}
for name,key in [('orange','orangeNewClosedBase'),('blue','blueNewClosedBase')]:
 hp=houses[key]['polygon'];HA=np.array(hp);HB=np.roll(HA,-1,axis=0);scan=boundary_scan(HA,HB,poly,[{'houseBoundaryEdge':i} for i in range(len(HA))]);winside=[i for i,p in enumerate(poly) if inside(p,hp)];hinside=[i for i,p in enumerate(hp) if inside(p,poly)];relations[name]={'boundary':scan,'wallVerticesInsideHouse':winside,'houseVerticesInsideWall':hinside,'disjointNewDomains':scan['intersectingSegments']==0 and not winside and not hinside}
heights=[]
for p in [[270,145],[270,139],[270,927],[145,859],[395,876],[890,664],[1350,421],[500,500]]:heights.append({'asset':p,'source':world(p),'RGBA':list(im.getpixel(tuple(p)))})
report={'ready':True,'scope':'V2 uniform registration, full NEW thin-stone boundary base and unchanged real Torren foot contours. Read-only math; no GUI/engine/action or production modification. Timber/foliage remain separate and not certified here.','inputs':{'registration':str(regpath),'registrationSha256':hashlib.sha256(regpath.read_bytes()).hexdigest(),'raw':str(rawpath),'rawSha256':hashlib.sha256(rawpath.read_bytes()).hexdigest(),'houses':str(root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json')},'uniformRegistration':{k:v for k,v in reg.items() if k!='visibleStoneFoot'},'visibleFootCount':len(foot),'visibleFootAlphaRange':[min(im.getpixel(tuple(v))[3] for v in reg['visibleStoneFoot']),max(im.getpixel(tuple(v))[3] for v in reg['visibleStoneFoot'])],'heightAndAlphaSamples':heights,'nearHeightMeaning':'Near cap stone around raw270145 to foot927 gives29.716sourcepx at.038; opaque outline top139 gives29.944. Source same-owner conservative stone top~539432..542430 to foot461..464 is approximately30–34, not leaves/timber. V1 uniform shape could not achieve this near height and the span simultaneously.','newThinGroundBase':{'polygon':poly,'hiddenRearAssetAnchors':hiddenAsset,'hiddenRearWorldAnchors':[world(p) for p in hiddenAsset],'derivation':{'near':'VisibleL145859 + visibleR395876 - front270927 = hidden270808.','middle':'Visible front890670/right1008616; cap-axis displacement defines hidden left781619; rear=899565. Left/rear are new design, not a source-proven foot.','far':'Visible near return1350428/right1417366; cap/side-axis continuation defines hidden left1210375; rear1277313. Curved endcap rear is approximate new design, not measured hidden material.'},'limits':'Whole common rear chain is explicitly NEW, narrow and continuous. The generated piece has a local jog/rounded end; this is not a rigorously reconstructed single straight 3D masonry prism. No17cell box or garden interior fill.'},'canonicalPositiveAreaCells':cells,'wholeBaseArea':area(poly),'nonBlockedPositiveAreaCells':[v for v in cells if v['terrain']!='#'],'legalBoundsInclusive':{'x':[31,39],'y':[23,31]},'legalCells':legal,'directedEdges':edges,'completeWalkBoundary':walk,'walkEndpointInsideCount':len(insideIds),'walkEndpointInsideExamples':[meta[i] for i in insideIds[:3]],'wholeFloorRelationsToNewHouseDomains':relations,'limitations':['Only this explicit new rear-thickness design is evaluated; not any arbitrary hidden base.','1273 outer stone/outline samples are unchanged; no filtering or sample shift to obtain the result.','Frozen Torren walk-a/b support-boot contours and legal local edges only, not every unit/pose or gameplay.','Alpha0 colored RGB haze is not opaque material; e.g500500 is reported directly.','Stone shape native appearance/depth still belongs to parent static renderer checks. No timber/foliage support completion is implied.']}
(out/'v2-full-thin-wall-footing-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');(out/'registration-frozen-input.json').write_text(json.dumps(reg,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'ready':True,'area':area(poly),'cells':cells,'walk':walk,'inside':len(insideIds),'relations':relations},ensure_ascii=False,indent=2))
