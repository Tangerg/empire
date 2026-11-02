from pathlib import Path
import json,ast,hashlib,math
import numpy as np
out=Path(__file__).resolve().parent;root=out.parents[1]
basepath=out/'blue-v2-whole-new-base-and-orange-separation.json';whole=json.loads(basepath.read_text());base=whole['orangeNewClosedBase']
oldpath=root/'c07-readonly-review/west-inner-house-source-study/candidate-v2-foot-math/visible-foot-and-walk-result.json';old=json.loads(oldpath.read_text());regpath=Path(old['inputs']['registration']);assert hashlib.sha256(regpath.read_bytes()).hexdigest()==old['inputs']['registrationSha256']
frozen=json.loads(Path(old['inputs']['frozenSoleInput']).read_text());assert hashlib.sha256(Path(old['inputs']['frozenSoleInput']).read_bytes()).hexdigest()==old['inputs']['frozenSoleSha256']
script=oldpath.parent/'verify-visible-foot-and-walk.py';m=ast.parse(script.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name in ['inside','cc','ptseg','scan']],type_ignores=[]),str(script),'exec'))
AA=[];BB=[];meta=[]
for a,b in old['directedEdges']:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    off=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']];AA.append([16*a[k]+8+off[k] for k in range(2)]);BB.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(AA);B=np.array(BB);foot=np.array([base['right'],base['hiddenRearNewDesign'],base['left']]);rear=scan(A,B,meta);poly=base['polygon'];lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);possible=np.flatnonzero((np.all(A>=lo,axis=1)&np.all(A<=hi,axis=1))|(np.all(B>=lo,axis=1)&np.all(B<=hi,axis=1)));ids=[int(i) for i in possible if inside(A[i],poly) or inside(B[i],poly)]
report={'ready':True,'scope':'Add only the orange NEW hidden closing edges and full-base endpoint inclusion to the unchanged prior exact visible-front scan. No engine or production/state edit. Does not alter already closed blue/full-floor report.','closedWholeBaseReport':str(basepath),'cachedFrontScan':str(oldpath),'inputsHashVerifiedUnchanged':True,'legalCells':old['legalCells'],'directedEdges':old['directedEdges'],'testedSegments':len(A),'baseNewDesign':base,'frontBoundary':old['continuousWalkVsVisibleStoneFoot'],'hiddenBoundary':rear,'endpointInsideCount':len(ids),'insideExamples':[meta[i] for i in ids[:3]],'minimumBoundaryClearance':min(old['continuousWalkVsVisibleStoneFoot']['minimumDistance'],rear['minimumDistance']),'noSweepOrEndpointInsideOrangeNewBase':old['continuousWalkVsVisibleStoneFoot']['intersectingSoleSegments']==0 and rear['intersectingSoleSegments']==0 and not ids,'limitation':'Only the explicitly proposed new orange floor using actual wall-axis corner5211068, not recovered original rear volume. Frozen Torren front-support walk subset and bounded canonical legal edges; not gameplay or all-role guarantee.'}
(out/'orange-full-new-base-walk-addendum.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:report[k] for k in ['ready','testedSegments','hiddenBoundary','endpointInsideCount','minimumBoundaryClearance','noSweepOrEndpointInsideOrangeNewBase']},ensure_ascii=False,indent=2))
