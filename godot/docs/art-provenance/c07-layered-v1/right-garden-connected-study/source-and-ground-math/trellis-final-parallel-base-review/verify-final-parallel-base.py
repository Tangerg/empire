from pathlib import Path
import json,math,ast,hashlib
import numpy as np
out=Path(__file__).resolve().parent;root=out.parents[2];repo=root/'empire-c01';sp=root/'c07-static-preview';files=[out/'front-source-frozen-input.json',out/'back-source-frozen-input.json'];sources=[json.loads(p.read_text()) for p in files];houses=json.loads((root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json').read_text());wall=json.loads((out.parent/'stone-v2-foot-study/v2-full-thin-wall-footing-review.json').read_text());frozen=json.loads((root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json').read_text());level=next(v for v in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if v['id']=='c01-07')
s=root/'c07-static-preview/central-rear-west-bed-review/verify-rear-west-actual-four-leg-footing.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name in ['inside','cross','area','intersection','cc','ptseg']],type_ignores=[]),str(s),'exec'));s=out.parent/'stone-v2-foot-study/verify-thin-stone-wall.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name=='boundary_scan'],type_ignores=[]),str(s),'exec'))
A=[];B=[];meta=[]
for a,b in wall['directedEdges']:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    off=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']];A.append([16*a[k]+8+off[k] for k in range(2)]);B.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(A);B=np.array(B)
others={'orange':houses['orangeNewClosedBase']['polygon'],'blue':houses['blueNewClosedBase']['polygon'],'stoneWall':wall['newThinGroundBase']['polygon']}
def check(poly):
 lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);cells=[]
 for y in range(int(lo[1]//16),int(hi[1]//16)+1):
  for x in range(int(lo[0]//16),int(hi[0]//16)+1):
   p=intersection(poly,[[x*16,y*16],[x*16+16,y*16],[x*16+16,y*16+16],[x*16,y*16+16]]);v=area(p)
   if v>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'area':v})
 walk=boundary_scan(A,B,poly,meta);ids=np.flatnonzero((np.all(A>=lo,axis=1)&np.all(A<=hi,axis=1))|(np.all(B>=lo,axis=1)&np.all(B<=hi,axis=1)));insideIds=[int(i) for i in ids if inside(A[i],poly) or inside(B[i],poly)];relations={}
 for name,P in others.items():
  Q=np.array(P);R=np.roll(Q,-1,axis=0);scan=boundary_scan(Q,R,poly,[{'otherBoundaryEdge':i} for i in range(len(Q))]);i1=[i for i,p in enumerate(poly) if inside(p,P)];i2=[i for i,p in enumerate(P) if inside(p,poly)];cut=intersection(P,poly)
  relations[name]={'boundary':scan,'postVerticesInsideOther':i1,'otherVerticesInsidePost':i2,'intersectionArea':area(cut),'intersectionPolygon':cut,'disjoint':not i1 and not i2 and scan['intersectingSegments']==0}
 return {'footPolygon':poly,'positiveAreaCells':cells,'walk':walk,'walkEndpointsInside':len(insideIds),'relations':relations}
feet={};centres={}
for d in sources:
 members={v['id']:v for v in d['members']}
 for name,v in members.items():
  if '-post-' in name and name.endswith('-front'):
   prefix=name[:-6];fp=v['polygon'];side=members[prefix+'-side']['polygon'];c=[fp[2][0]-1,fp[2][1]];poly=[fp[3],fp[2],side[2],[c[0]-.2,c[1]-2]];feet[prefix]=poly;centres[prefix]=c

old=json.loads((out.parent/'trellis-finite-feet-review/five-actual-finite-post-feet-review.json').read_text())
rows={}
for name,p in feet.items():
 if name.startswith('front-'):
  assert p==old['finiteFeet'][name]['footPolygon'],name
  rows[name]=old['finiteFeet'][name]
 else:rows[name]=check(p)
assert centres['back-post-0']==[543,446.5]
assert centres['back-post-1']==[575,430.5]
base=[[550,450],[582,434],[575,430.5],[543,446.5]]
whole=check(base)
report={'ready':True,'scope':'Only final parallel-axis rear-row shift (+1,+0.5). Front three finite-foot results reused only after exact geometry equality. Both new rear diamonds and declared whole NEW garden base recalculated. Static math, no engine or production changes.','sources':[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],'centres':centres,'finiteFeet':rows,'frontResultsReusedFrom':str(out.parent/'trellis-finite-feet-review/five-actual-finite-post-feet-review.json'),'newWholeGardenBase':whole,'walkSegments':len(A),'limits':['Whole garden base is an explicitly NEW design domain, not source hidden foundation.','Finite rear corners are NEW [-0.2,-2] offsets; visible three corners derived from actual member polygons.','Original 0.01px separation was not overlap; final change improves robustness without claiming a collision repair.','Walk validation uses frozen actual Torren walk-a/b sole points, continuous applicable-facing local legal edges, not actual gameplay or all-role proof.']}
(out/'final-parallel-rear-row-footing-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
def summary(v):
 return {'cells':v['positiveAreaCells'],'walkGap':v['walk']['minimumDistance'],'walkCross':v['walk']['intersectingSegments'],'walkInside':v['walkEndpointsInside'],'other':{k:{'gap':r['boundary']['minimumDistance'],'disjoint':r['disjoint'],'area':r['intersectionArea']} for k,r in v['relations'].items()}}
print(json.dumps({'ready':True,'backLeft':summary(rows['back-post-0']),'backRight':summary(rows['back-post-1']),'whole':summary(whole),'walkSegments':len(A)},ensure_ascii=False,indent=2))
