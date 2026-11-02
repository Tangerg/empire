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
rows={name:check(p) for name,p in feet.items()};p=feet['back-post-0'];proposals={}
for name,delta in [('forward1',[0,1]),('forward1p25',[0,1.25]),('sameCrossAxis1',[1,.5]),('sameCrossAxis1p5',[1.5,.75])]:proposals[name]={'translation':delta,'centre':(np.array(centres['back-post-0'])+delta).tolist(),'result':check((np.array(p)+delta).tolist())}
# Exact positive Y separation needed from unchanged observed orange front chain (continuous interpolation),
# using only the post's rear/upward edges where orange lies north of the visible front.
of=houses['orangeNewClosedBase']['polygon'][:-1];lower=[p[0],p[3],p[2]]
xs=sorted(set([v[0] for v in lower]+[v[0] for v in of if p[0][0]<=v[0]<=p[2][0]]))
def y_at(x,line):
 for a,b in zip(line,line[1:]):
  if min(a[0],b[0])-1e-10<=x<=max(a[0],b[0])+1e-10:
   if abs(b[0]-a[0])<1e-12:continue
   return a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0])
 raise ValueError(x)
diffs=[{'x':x,'orangeFrontY':y_at(x,of),'postRearY':y_at(x,lower),'requiredPositiveYToTouch':y_at(x,of)-y_at(x,lower)} for x in xs];critical=max(diffs,key=lambda v:v['requiredPositiveYToTouch'])
report={'ready':True,'scope':'Five actual finite front/side foot polygons read from source members plus explicitly provided NEW rear diamond corner. No centre-only inference; no production/GUI/engine/state changes.','sources':[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],'centres':centres,'hiddenRearOffset':[-.2,-2],'finiteFeet':rows,'backLeftSinglePostProposals':proposals,'backLeftVerticalSeparationThreshold':critical,'correctionScope':'Only the back-left post and the actual attached beam endpoint geometry/support need follow a chosen translation. Other four posts and whole trellis are not moved. Geometry changes here are proposals only.','limits':['The rear corner is explicitly NEW design; no source hidden-foot claim.','Exact vertical touch threshold is for frozen finite diamond and current new orange base; touching remains non-disjoint.','All support contours are the existing frozen Torren walk-a/b subset; no actual battle motion or all-role guarantee.','Changing one post may change beam axis/length; final finite members and support lines must match the selected new geometry.']}
(out/'five-actual-finite-post-feet-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'ready':True,'feet':{k:{'orange':v['relations']['orange'],'wallDist':v['relations']['stoneWall']['boundary']['minimumDistance'],'walkDist':v['walk']['minimumDistance']} for k,v in rows.items()},'critical':critical,'proposals':{k:{'centre':v['centre'],'orange':v['result']['relations']['orange']['boundary']['minimumDistance'],'orangeArea':v['result']['relations']['orange']['intersectionArea'],'orangeDisjoint':v['result']['relations']['orange']['disjoint'],'wall':v['result']['relations']['stoneWall']['boundary']['minimumDistance'],'walk':v['result']['walk']['minimumDistance']} for k,v in proposals.items()}},ensure_ascii=False,indent=2))
