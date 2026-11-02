from pathlib import Path
import json,ast,hashlib,math
import numpy as np
out=Path(__file__).resolve().parent;root=out.parents[1];repo=root/'empire-c01'
frozen=json.loads((root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json').read_text());level=next(v for v in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if v['id']=='c01-07')
s=root/'c07-static-preview/central-rear-west-bed-review/verify-rear-west-actual-four-leg-footing.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name in ['inside','cross','area','intersection','cc','ptseg']],type_ignores=[]),str(s),'exec'));s=root/'c07-readonly-review/right-garden-source-study/stone-v2-foot-study/verify-thin-stone-wall.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name=='boundary_scan'],type_ignores=[]),str(s),'exec'))
houses=json.loads((root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json').read_text());others={'blue':houses['blueNewClosedBase']['polygon'],'orange':houses['orangeNewClosedBase']['polygon']}
legal=[(x,y) for y in range(23,35) for x in range(16,31) if level['terrain'][y][x] not in '#_'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1]
A=[];B=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    off=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']];A.append([16*a[k]+8+off[k] for k in range(2)]);B.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(A);B=np.array(B)
def check(poly):
 lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);cells=[]
 for y in range(int(lo[1]//16),int(hi[1]//16)+1):
  for x in range(int(lo[0]//16),int(hi[0]//16)+1):
   p=intersection(poly,[[x*16,y*16],[x*16+16,y*16],[x*16+16,y*16+16],[x*16,y*16+16]]);v=area(p)
   if v>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'area':v})
 walk=boundary_scan(A,B,poly,meta);ids=np.flatnonzero((np.all(A>=lo,axis=1)&np.all(A<=hi,axis=1))|(np.all(B>=lo,axis=1)&np.all(B<=hi,axis=1)));ii=[int(i) for i in ids if inside(A[i],poly) or inside(B[i],poly)];relations={}
 for name,P in others.items():
  Q=np.array(P);scan=boundary_scan(Q,np.roll(Q,-1,axis=0),poly,[{'otherEdge':i} for i in range(len(Q))]);a=[i for i,p in enumerate(poly) if inside(p,P)];b=[i for i,p in enumerate(P) if inside(p,poly)];relations[name]={'minimumDistance':scan['minimumDistance'],'boundaryIntersections':scan['intersectingSegments'],'verticesInsideOther':a,'otherVerticesInside':b,'disjoint':not a and not b and scan['intersectingSegments']==0}
 return {'polygon':poly,'area':area(poly),'positiveAreaCells':cells,'walk':walk,'walkEndpointsInside':len(ii),'relations':relations}
polys={'longWallDefault4':[[300,454],[398,503],[402,501],[304,452]],'returnWallDefault4':[[398,503],[430,487],[426,485],[394,501]],'nearPier':[[392,500],[398,503],[404,500],[398,497]],'middlePier':[[352,480],[356,482],[360,480],[356,478]],'longWallMaxThickness6':[[300,454],[398,503],[404,500],[306,451]],'returnWallMaxThickness6':[[398,503],[430,487],[424,484],[392,500]]}
results={k:check(p) for k,p in polys.items()}
report={'ready':True,'scope':'One bounded NEW continuous L wall with finite near/middle pier ground volumes; no source-hidden-ground claim. No production/GUI/action.','frontGroundLine':[[300,454],[398,503],[430,487]],'thicknessDesign':{'defaultInsideLong':[4,-2],'defaultInsideReturn':[-4,-2],'defaultPerpendicularThickness':4/math.sqrt(1.25),'boundedRangeOffsetX':[3,6],'boundedPerpendicularThickness':[3/math.sqrt(1.25),6/math.sqrt(1.25)],'proof':'The maximum6 closed wall domains contain all offsets3..6; both max and default4 checked. Pillars are finite solids rather than centres.'},'results':results,'walk':{'legalCells':legal,'directedEdges':edges,'soleSegmentCount':len(A),'frames':[{k:f[k] for k in ['index','anchor','frameRegion']} for f in frozen['frames']],'scale':frozen['scale'],'source':'fixed rear-white-planter-new-base-walk-corridor.json sole arrays; actual path facing and both possible frame parities'} ,'limits':['NEW physical layout, not identical historic footprint or whole original asset fit.','Only the near L perimeter, not far rear wall, timber, roots, neighbouring pots or old far western tower.','Visible cap/face shape can be retained as design inspiration, but original pixel ownership/depth expectation is not imposed after physical relocation.','Local real frozen Torren walk-a/b boundary scope only, not all roles or played movement.','Wall and pier volumes intentionally join/overlap as one masonry system.','Hidden thickness is NEW; original foundation polygons are not the physical floor.']}
(out/'new-continuous-l-wall-full-footing-proposal.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'ready':True,'segments':len(A),'results':{k:{'cells':r['positiveAreaCells'],'walkGap':r['walk']['minimumDistance'],'walkCross':r['walk']['intersectingSegments'],'inside':r['walkEndpointsInside'],'other':r['relations']} for k,r in results.items()}},indent=2))
