from pathlib import Path
import json,math,ast
import numpy as np
out=Path(__file__).resolve().parent;root=out.parents[2];repo=root/'empire-c01';wallpath=out.parent/'stone-v2-foot-study/v2-full-thin-wall-footing-review.json';wall=json.loads(wallpath.read_text());houses=json.loads((root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json').read_text());frozen=json.loads((root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json').read_text());level=next(v for v in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if v['id']=='c01-07')
s=root/'c07-static-preview/central-rear-west-bed-review/verify-rear-west-actual-four-leg-footing.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name in ['inside','cross','area','intersection','cc','ptseg']],type_ignores=[]),str(s),'exec'));s=out.parent/'stone-v2-foot-study/verify-thin-stone-wall.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name=='boundary_scan'],type_ignores=[]),str(s),'exec'))
poly=[[550,450],[582,434],[574,430],[542,446]];frontMiddle=[563,443.5]
A=[];B=[];meta=[]
for a,b in wall['directedEdges']:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    off=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']];A.append([16*a[k]+8+off[k] for k in range(2)]);B.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(A);B=np.array(B);walk=boundary_scan(A,B,poly,meta);lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);possible=np.flatnonzero((np.all(A>=lo,axis=1)&np.all(A<=hi,axis=1))|(np.all(B>=lo,axis=1)&np.all(B<=hi,axis=1)));ins=[int(i) for i in possible if inside(A[i],poly) or inside(B[i],poly)]
cells=[]
for y in range(int(lo[1]//16),int(hi[1]//16)+1):
 for x in range(int(lo[0]//16),int(hi[0]//16)+1):
  q=intersection(poly,[[x*16,y*16],[x*16+16,y*16],[x*16+16,y*16+16],[x*16,y*16+16]]);a=area(q)
  if a>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'area':a})
relations={}
for name,P in [('orange',houses['orangeNewClosedBase']['polygon']),('blue',houses['blueNewClosedBase']['polygon']),('stoneWall',wall['newThinGroundBase']['polygon'])]:
 Q=np.array(P);R=np.roll(Q,-1,axis=0);scan=boundary_scan(Q,R,poly,[{'otherBoundaryEdge':i} for i in range(len(Q))]);i1=[i for i,p in enumerate(poly) if inside(p,P)];i2=[i for i,p in enumerate(P) if inside(p,poly)];relations[name]={'boundary':scan,'gardenVerticesInsideOther':i1,'otherVerticesInsideGarden':i2,'disjointDeclaredGroundDomains':not i1 and not i2 and scan['intersectingSegments']==0}
report={'ready':True,'scope':'Latest strict-axis NEW garden base only. Earlier [-2,-8] draft deliberately not evaluated. No GUI/engine/action or production change.','declaredWholeBase':poly,'frontMiddleFoot':[563,443.5],'backOffset':[-8,-4],'postHeight':28,'newDesignNotSourceReconstruction':True,'area':area(poly),'positiveAreaCells':cells,'nonBlockedPositiveAreaCells':[v for v in cells if v['terrain']!='#'],'walkBoundary':walk,'walkEndpointInsideCount':len(ins),'relations':relations,'criticalScopeLimit':'The four vertices are treated as the OUTER complete physical domain for this mathematical result. If actual posts are centered on these vertices and have finite foot width extending outside it, those physical diamonds are not included here and must be checked from the final geometry; point-foot/base-centreline reasoning cannot certify them.','otherLimits':['The fixed28height frame intentionally differs from the original higher irregular wood canopy; no claim of original highest wood544390 reproduction.','Only frozen Torren walk support contours and inherited local canonical legal edges, not actual movement or all appearances.','House and wall rear domains are the same previously frozen NEW designs, not original hidden archaeological facts.']}
(out/'strict-axis-new-garden-base-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'ready':True,'area':area(poly),'cells':cells,'walk':walk,'inside':len(ins),'relations':relations},ensure_ascii=False,indent=2))
