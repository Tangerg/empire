from pathlib import Path
import json, math, hashlib, collections
import numpy as np
from PIL import Image
out=Path(__file__).resolve().parent
root=out.parents[2]
repo=root/'empire-c01'
# All production files are read only; only this external report directory is written.
regpath=root/'c07-static-preview/west-inner-house-v2-registration.json'
d=json.loads(regpath.read_text()); frozenpath=root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json';frozen=json.loads(frozenpath.read_text())
level=next(v for v in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if v['id']=='c01-07')
audit=json.loads((repo/'godot/docs/art-provenance/luojia-geometry.json').read_text())
imagepath=repo/'godot/assets/native/c07-layered-v1/west-inner-house-v2-candidate.png';im=Image.open(imagepath).convert('RGBA')
def inside(q,poly):
 x,y=q;hit=False
 for a,b in zip(poly,poly[1:]+poly[:1]):
  c=(b[0]-a[0])*(y-a[1])-(b[1]-a[1])*(x-a[0])
  if abs(c)<1e-9 and min(a[0],b[0])-1e-9<=x<=max(a[0],b[0])+1e-9 and min(a[1],b[1])-1e-9<=y<=max(a[1],b[1])+1e-9:return True
  if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:hit=not hit
 return hit
feet=[]
relevant=[v for v in audit['blockedPolygons'] if v['id'] in ['west-inner-house-foundation','west-inner-blue-house-foundation','west-inner-east-wall','west-inner-side-garden','west-inner-door-pot']]
for v in d['candidateVisibleStoneFoot']:
 p=[d['worldOrigin'][i]+d['scale']*v[i] for i in range(2)];cell=[math.floor(z/16) for z in p]
 feet.append({'asset':v,'source':p,'alpha':im.getpixel(tuple(v))[3],'cell':cell,'terrain':level['terrain'][cell[1]][cell[0]],'coarsePolygonResponsibilities':[q['id'] for q in relevant if inside(p,q['polygonPixels'])],'coarseCellResponsibilities':[q['id'] for q in relevant if cell in q['blockedCells']]})
foot=np.array([v['source'] for v in feet])
# Bounded neighborhood: every canonical cell in this window, then all legal adjacent edges within it.
# Every relevant close approach is inside; report does not claim all character classes or hidden back feet.
legal=[(x,y) for y in range(23,32) for x in range(27,37) if level['terrain'][y][x] not in '#_']
edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1]
A=[];B=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for p in f['sole']:
    offset=[(p[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(p[1]-f['anchor'][1])*frozen['scale']]
    A.append([16*a[k]+8+offset[k] for k in range(2)]);B.append([16*b[k]+8+offset[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':p})
A=np.array(A,float);B=np.array(B,float)
def cc(a,b):return a[:,0]*b[:,1]-a[:,1]*b[:,0]
def ptseg(P,Q,R):
 v=R-Q;den=np.sum(v*v,axis=1);t=np.clip(np.sum((P-Q)*v,axis=1)/den,0,1);N=Q+t[:,None]*v
 return np.sum((P-N)**2,axis=1),N,t

def scan(A,B,meta):
 hit=np.zeros(len(A),bool);best=(float('inf'),None)
 for k,(q,r) in enumerate(zip(foot,foot[1:])):
  Q=np.broadcast_to(q,A.shape);R=np.broadcast_to(r,A.shape)
  o1=cc(B-A,Q-A);o2=cc(B-A,R-A);o3=cc(R-Q,A-Q);o4=cc(R-Q,B-Q)
  hit|=(o1*o2<0)&(o3*o4<0)
  for P,X,Y,kind in [(A,Q,R,'actor-start'),(B,Q,R,'actor-end'),(Q,A,B,'stone-start'),(R,A,B,'stone-end')]:
   D,N,T=ptseg(P,X,Y);hit|=D<1e-16;i=int(np.argmin(D))
   if D[i]<best[0]:best=(float(D[i]),{'actor':meta[i],'actorSweep':[A[i].tolist(),B[i].tolist()],'closestKind':kind,'point':P[i].tolist(),'nearestPoint':N[i].tolist(),'footEdgeIndices':[k,k+1],'footEdge':[q.tolist(),r.tolist()],'parameter':float(T[i])})
 return {'testedSoleSegments':len(A),'intersectingSoleSegments':int(hit.sum()),'minimumDistance':0.0 if hit.any() else math.sqrt(best[0]),'closestWitness':best[1],'firstViolations':[meta[int(i)] for i in np.flatnonzero(hit)[:5]]}
walk=scan(A,B,meta)
# Exact site/approach edge, both directed horizontal movement facings.
sel=[i for i,m in enumerate(meta) if set([tuple(m['fromCell']),tuple(m['toCell'])])==set([(33,30),(34,30)])]
entry=scan(A[sel],B[sel],[meta[i] for i in sel])
# Visible threshold profile only has source evidence over x501..515; endpoints outside remain unproven extension.
old=[[501,458],[507,456],[515,454]];new=d['stepWorldSamples']
def y_at(x,pts):
 for a,b in zip(pts,pts[1:]):
  if a[0]<=x<=b[0]:return a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0])
 return None
threshold={'originalApproximateContactBand':old,'originalMixedEdgeUncertaintyPixels':[1,2],'newMeasuredThreshold':new,'landmarkPairDeltasNotOuterCornerFit':[[new[i][j]-old[i][j] for j in range(2)] for i in range(3)],'sameXDifferences':[{'x':x,'originalY':y_at(x,old),'newY':y_at(x,new),'deltaY':y_at(x,new)-y_at(x,old)} for x in [501,507,509,515]],'unprovedExtensions':{'leftSourcePixels':501-new[0][0],'rightSourcePixels':new[-1][0]-515},'meaning':'Local threshold slopes are about 1.3–1.6 source pixels farther back at overlapping x. New end extensions are not source-certified jamb/outer-feet restoration.'}
resp={q['id']:{'polygonCount':sum(q['id'] in f['coarsePolygonResponsibilities'] for f in feet),'cellCount':sum(q['id'] in f['coarseCellResponsibilities'] for f in feet),'polygonSampleRange':([v['source'] for v in feet if q['id'] in v['coarsePolygonResponsibilities']][:1]+[v['source'] for v in feet if q['id'] in v['coarsePolygonResponsibilities']][-1:])} for q in relevant}
report={'ready':True,'scope':'Read-only mathematical review of parent supplied 794 visible stone-foot pixels and frozen Torren front-support walk contours. No engine, actual movement, or whole hidden-footprint certification.','registration':{k:v for k,v in d.items() if k!='candidateVisibleStoneFoot'},'inputs':{'registration':str(regpath),'registrationSha256':hashlib.sha256(regpath.read_bytes()).hexdigest(),'candidate':str(imagepath),'candidateSha256':hashlib.sha256(imagepath.read_bytes()).hexdigest(),'frozenSoleInput':str(frozenpath),'frozenSoleSha256':hashlib.sha256(frozenpath.read_bytes()).hexdigest()},'candidateSize':list(im.size),'visibleFootCount':len(feet),'alphaRange':[min(v['alpha'] for v in feet),max(v['alpha'] for v in feet)],'footSourceBounds':[foot.min(axis=0).tolist(),foot.max(axis=0).tolist()],'terrainCounts':dict(collections.Counter(v['terrain'] for v in feet)),'cellCounts':[{'cell':list(k),'count':v,'terrain':level['terrain'][k[1]][k[0]]} for k,v in sorted(collections.Counter(tuple(v['cell']) for v in feet).items())],'thresholdComparison':threshold,'neighborResponsibilityBands':resp,'responsibilityLimit':'Coarse responsibility polygon/cell overlap is not proof of physical interpenetration. Original blue-house hidden foundation and right garden ownership must be resolved from visible material, not filled/baked by inference.','legalSearchBoundsInclusive':{'x':[27,36],'y':[23,31]},'legalCells':legal,'directedEdges':edges,'walkFacingContract':'Horizontal follows deltaX; vertical may retain east or west. Both frozen walk frames checked on each applicable directed edge; conservative phase coverage, not an engine run.','frames':frozen['frames'],'torrenScale':frozen['scale'],'continuousWalkVsVisibleStoneFoot':walk,'canonicalSiteApproachPath':entry,'visibleFootSamples':feet,'limitations':['Only original threshold contact band is visibly source-certified; left/right outer bases are new design.','794 footline samples and their continuous connections do not certify hidden rear volume, full ground-footprint, blue-house separation, or total original responsibility coverage.','No ground-floor inferred from roof alpha or coarse foundation polygon.','Only frozen manually inspected Torren walk-a/walk-b front-support sole contours; not all footwear or all units.']}
(out/'visible-foot-and-walk-result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
(out/'registration-frozen-input.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ['ready','visibleFootCount','terrainCounts','neighborResponsibilityBands','thresholdComparison','continuousWalkVsVisibleStoneFoot','canonicalSiteApproachPath']},ensure_ascii=False,indent=2))
