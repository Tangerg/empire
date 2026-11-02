from pathlib import Path
import json,math,ast,collections,hashlib
import numpy as np
from PIL import Image
out=Path(__file__).resolve().parent;root=out.parents[1];repo=root/'empire-c01';regpath=root/'c07-static-preview/west-inner-blue-v1-registration.json';d=json.loads(regpath.read_text());frozen=json.loads((root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json').read_text());level=next(v for v in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if v['id']=='c01-07')
oldscript=root/'c07-readonly-review/west-inner-house-source-study/candidate-v2-foot-math/verify-visible-foot-and-walk.py';m=ast.parse(oldscript.read_text());exec(compile(ast.Module(body=[n for n in m.body if isinstance(n,ast.FunctionDef) and n.name in ['inside','cc','ptseg','scan']],type_ignores=[]),str(oldscript),'exec'))
assetpath=repo/'godot/assets/native/c07-layered-v1/west-inner-blue-house-v1-candidate.png';im=np.array(Image.open(assetpath).convert('RGBA'));rawfoot=d['candidateVisibleStoneFoot'];declared=np.array([np.array(v)*d['scale']+d['worldOrigin'] for v in rawfoot]);stone=[]
for x,y in rawfoot:
 rows=np.flatnonzero((im[940:,x,3]>=200)&(im[940:,x,:3].mean(axis=1)>=45))+940;stone.append([x,int(rows[-1])])
# Supplemental color-interior trace only. Do not replace frozen parent outer-outline foot with this trace.
stoneworld=np.array(stone)*d['scale']+d['worldOrigin']
legal=[(x,y) for y in range(22,34) for x in range(22,34) if level['terrain'][y][x] not in '#_'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];aa=[];bb=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    off=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']];aa.append([16*a[k]+8+off[k] for k in range(2)]);bb.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(aa);B=np.array(bb)
def terrain_summary(ps):
 rows=[]
 for i,p in enumerate(ps):
  c=[math.floor(v/16) for v in p];rows.append({'index':i,'asset':rawfoot[i],'source':p.tolist(),'cell':c,'terrain':level['terrain'][c[1]][c[0]]})
 bad=[v for v in rows if v['terrain']!='#'];return {'counts':dict(collections.Counter(v['terrain'] for v in rows)),'nonBlockedFootSamples':bad}
results={}
for name,ps in [('initialParentOuterOutline',declared),('initialColoredStoneInterior',stoneworld),('candidateShiftNorth1',declared+[0,-1]),('candidateShiftNorth3',declared+[0,-3])]:
 foot=ps;results[name]={'terrain':terrain_summary(ps),'walkVsOpenVisibleFoot':scan(A,B,meta)};print(name,results[name]['terrain']['counts'],results[name]['walkVsOpenVisibleFoot']['intersectingSoleSegments'],results[name]['walkVsOpenVisibleFoot']['minimumDistance'],flush=True)
# A conventional rear closure is an explicit new design, inferred from parallelogram wall directions.
L=declared[0];F=np.array(d['worldAnchor']);R=declared[-1];H=L+R-F
# Parent outer front line plus rear close; no claim this reproduces original blue hidden floor.
bluepoly=declared.tolist()+[H.tolist()];orange=json.loads((root/'c07-static-preview/west-inner-house-v2-registration.json').read_text());orangefoot=np.array(orange['candidateVisibleStoneFoot'])*orange['scale']+orange['worldOrigin'];orangeInside=[{'index':i,'source':p.tolist()} for i,p in enumerate(orangefoot) if inside(p,bluepoly)]
# Boundary distance from all orange visible line segments to hypothetical blue full closure.
foot=np.array(bluepoly+[bluepoly[0]]);orangeBoundary=scan(orangefoot[:-1],orangefoot[1:],[{'orangeFootEdge':[i,i+1]} for i in range(len(orangefoot)-1)])
# Full-domain footwear intersections are not inferred from line distance alone.
for name,shift in [('initialParentOuterOutline',0),('candidateShiftNorth1',-1),('candidateShiftNorth3',-3)]:
 poly=(declared+[0,shift]).tolist()+[(H+[0,shift]).tolist()];plo=np.min(poly,axis=0);phi=np.max(poly,axis=0);possible=np.flatnonzero((np.all(A>=plo,axis=1)&np.all(A<=phi,axis=1))|(np.all(B>=plo,axis=1)&np.all(B<=phi,axis=1)));insideIds=[int(i) for i in possible if inside(A[i],poly) or inside(B[i],poly)];results[name]['hypotheticalClosedBaseWalkEndpointInsideCount']=len(insideIds);results[name]['hypotheticalClosedBaseInsideExamples']=[meta[i] for i in insideIds[:3]]
report={'ready':True,'scope':'Read-only initial-blue candidate foot math; no production edit, GUI, engine or real action. Parent 886 outer-outline samples preserved.','registration':{k:v for k,v in d.items() if k!='candidateVisibleStoneFoot'},'inputSha256':hashlib.sha256(regpath.read_bytes()).hexdigest(),'candidateSize':list(im.shape[1::-1]),'candidateSha256':hashlib.sha256(assetpath.read_bytes()).hexdigest(),'alphaAndMaterialObservation':{'anchorRGBA':im[1318,467].tolist(),'innerStone4671310RGBA':im[1310,467].tolist(),'innerStone4671313RGBA':im[1313,467].tolist(),'meaning':'The registered corner is opaque dark outline, not a white stone interior. This is a valid positional anchor but should not be described as an independently certified original physical point. The bottom stone face was manually inspected in the two grid crops.','supplementalColorTraceRule':'Only manually inspected base x150..1035,y>=940. Last pixel A>=200 and RGB mean>=45, to distinguish opaque black outline from stone. Supplemental trace is not substituted for the frozen outer silhouette and is not a universal physical-foot detector.'},'legalBoundsInclusive':{'x':[22,33],'y':[22,33]},'legalCells':legal,'directedEdges':edges,'results':results,'hypotheticalHiddenRearClosure':{'newDesign':True,'left':L.tolist(),'front':F.tolist(),'right':R.tolist(),'hidden':H.tolist(),'formula':'hidden = left + right - front; conventional parallelogram direction only. Original blue rear floor not exposed.','orangeActualVisibleFootSamplesInside':orangeInside,'orangeVisibleBoundaryVsBlueHypotheticalBoundary':orangeBoundary,'limitation':'Separation from the orange visible front line does not certify its hidden rear volume or original neighbour foundation. No arbitrary whole-building bbox used.'},'coloredStoneSamples':[{'asset':v,'source':p.tolist(),'rgba':im[v[1],v[0]].tolist()} for v,p in zip(stone,stoneworld)],'parentFootSamples':rawfoot,'limits':['Only real frozen Torren walk front-support boot contours; not full shoes or other roles.','Nonblocked texel assignment alone is not a navigation defect.','New hidden closure is a possible explicit design, not recovered source geometry.','North shifts are diagnostic proposals, not changed registration or approved rendering.']}
(out/'blue-initial-foot-math.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');(out/'blue-registration-frozen-input.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'ready':True,'legalCells':len(legal),'edges':len(edges),'segments':len(A),'results':{k:{'terrain':v['terrain']['counts'],'walk':v['walkVsOpenVisibleFoot'],'inside':v.get('hypotheticalClosedBaseWalkEndpointInsideCount')} for k,v in results.items()},'orangeVsHiddenClosure':report['hypotheticalHiddenRearClosure']},ensure_ascii=False,indent=2))
