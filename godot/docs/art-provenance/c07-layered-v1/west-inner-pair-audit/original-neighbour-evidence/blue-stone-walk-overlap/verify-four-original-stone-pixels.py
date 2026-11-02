from pathlib import Path
import json,hashlib,math
import numpy as np
from PIL import Image
out=Path(__file__).resolve().parent;root=out.parents[3];repo=root/'empire-c01'
fpath=root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json';frozen=json.loads(fpath.read_text())
level=next(x for x in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if x['id']=='c01-07')
assetpath=repo/'godot/assets/painted-units-v1/torren-stage01-v2.png';sprite=Image.open(assetpath).convert('RGBA');sourcepath=repo/'godot/assets/native/scenes/luojia.png';source=Image.open(sourcepath).convert('RGB')
legal=[(x,y) for y in range(28,32) for x in range(27,31) if level['terrain'][y][x] not in '#_'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1]
def position(cell,frame,p,west):
 return [16*cell[0]+8+(p[0]-frame['anchor'][0])*frozen['scale']*(-1 if west else 1),16*cell[1]+8+(p[1]-frame['anchor'][1])*frozen['scale']]
A=[];B=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for p in f['sole']:
    A.append(position(a,f,p,west));B.append(position(b,f,p,west));meta.append({'from':a,'to':b,'west':west,'frame':f['index'],'framePixel':p})
A=np.array(A,float);B=np.array(B,float);delta=B-A
points=[[450,484],[451,486],[452,487],[470,475]];counts=[]
for p in points:
 lo=np.zeros(len(A));hi=np.ones(len(A));valid=np.ones(len(A),bool)
 for k in range(2):
  moving=abs(delta[:,k])>1e-14;valid&=moving|((A[:,k]>=p[k])&(A[:,k]<p[k]+1))
  q=np.where(moving,delta[:,k],1.);ta=(p[k]-A[:,k])/q;tb=(p[k]+1-A[:,k])/q
  lo=np.where(moving,np.maximum(lo,np.minimum(ta,tb)),lo);hi=np.where(moving,np.minimum(hi,np.maximum(ta,tb)),hi)
 ids=np.flatnonzero(valid&(lo<hi));examples=[]
 for i in ids[:3]:
  t=float((lo[i]+hi[i])/2);examples.append({**meta[int(i)],'tInterval':[float(lo[i]),float(hi[i])],'t':t,'sourcePoint':(A[i]+delta[i]*t).tolist()})
 counts.append({'originalPixel':p,'materialConfidence':('lower stone/shadow mixed edge; not needed for the hard counterexample' if p==[452,487] else 'visually identified stone body/threshold material'),'RGB':list(source.getpixel(tuple(p))),'pixelDomain':[[p[0],p[1]],[p[0]+1,p[1]+1]],'piecewisePixelConvention':'Unit source texel cell [x,x+1) × [y,y+1). Only the four frozen source texels; 452,487 is a lower mixed edge, while the fixed counterexamples use the stronger 451,486 and470,475 material. No filled bbox or hidden foot inference.','intersectingSoleSegments':len(ids),'examples':examples})
# Freeze three robust, legible witnesses rather than choose more samples after inspecting output.
specs=[{'id':'blue-left-stone-east-walk-b','frame':3,'pixel':[108,552],'from':[28,30],'to':[29,30],'west':False,'targetAxis':0,'targetValue':451.5,'stonePixel':[451,486],'canonicalPathAllowingPhase':[[28,29],[28,30],[29,30]],'segmentIndex':1}, {'id':'blue-left-stone-west-walk-a','frame':1,'pixel':[367,552],'from':[29,30],'to':[28,30],'west':True,'targetAxis':0,'targetValue':451.5,'stonePixel':[451,486],'canonicalPathAllowingPhase':[[29,30],[28,30]],'segmentIndex':0}, {'id':'blue-right-threshold-south-retained-west-walk-b','frame':3,'pixel':[40,559],'from':[28,29],'to':[28,30],'west':True,'targetAxis':1,'targetValue':475.5,'stonePixel':[470,475],'canonicalPathAllowingPhase':[[29,29],[28,29],[28,30]],'segmentIndex':1}]
ws=[]
for w in specs:
 f=next(v for v in frozen['frames'] if v['index']==w['frame']);a=np.array(position(w['from'],f,w['pixel'],w['west']));b=np.array(position(w['to'],f,w['pixel'],w['west']));axis=w['targetAxis'];t=float((w['targetValue']-a[axis])/(b[axis]-a[axis]));p=(a+(b-a)*t).tolist();full=[f['frameRegion'][0]+w['pixel'][0],f['frameRegion'][1]+w['pixel'][1]]
 assert w['pixel'] in f['sole'];assert [math.floor(v) for v in p]==w['stonePixel'];assert all(level['terrain'][c[1]][c[0]] not in '#_' for c in w['canonicalPathAllowingPhase'])
 ws.append({**w,'frameName':'walk-a' if w['frame']==1 else 'walk-b','anchor':f['anchor'],'region':f['frameRegion'],'scale':frozen['scale'],'sheetPixel':full,'actorAlpha':sprite.getpixel(tuple(full))[3],'sourceStoneRGB':list(source.getpixel(tuple(w['stonePixel']))),'t':t,'sourcePoint':p,'pathTerrain':[level['terrain'][c[1]][c[0]] for c in w['canonicalPathAllowingPhase']],'meaning':'Static real-sole texel overlap on a canonical legal adjacent path with possible frame parity/facing. Not an executed or accepted battle move, and not a complete hidden ground-volume proof.'})
report={'ready':True,'scope':'Bounded original-blue-house stone texel / frozen real Torren shoe contour sweep. Original visual pixel material is checked independently; terrain p alone is not used as a defect. No GUI, engine or save.','inputs':{'frozenBoots':str(fpath),'frozenBootsSha256':hashlib.sha256(fpath.read_bytes()).hexdigest(),'source':str(sourcepath),'sourceSha256':hashlib.sha256(sourcepath.read_bytes()).hexdigest(),'actor':str(assetpath),'actorSha256':hashlib.sha256(assetpath.read_bytes()).hexdigest()},'animationSource':'godot/Presentation/BoardView.Animation.cs:197-221; walk-a/b chosen per segment parity, nonzero projected X changes facing, vertical retains it, actor stays at linearly interpolated contact with no whole-body bob.','sourceContactTransform':'16*cell+8 plus signed(framePixel-frameAnchor)*.0961538','legalCellBoundsInclusive':{'x':[27,30],'y':[28,31]},'legalCells':legal,'directedEdges':edges,'testedSoleSegments':len(A),'stoneTexelResults':counts,'fixedReproducibleWitnesses':ws,'conclusion':'Confirmed static actual shoe silhouette/solid-source-stone overlap for both original blue door jamb and right threshold. This is stronger than a nonblocked-cell assertion; later blue replacement may prioritize legal foot corridors rather than restore these conflicting old pixels. No full physical foundation or in-game action is certified.','limits':['Four frozen original material texels only; 452,487 is a mixed lower edge and is not needed for the two hard material counterexamples. Not an invented complete footprint.','Frozen front-support boot contour subset, not all shoes, poses, or units.','Linear texel-domain overlap is static geometry; native draw/pick and actual accepted movement were not run.','Canonical local paths prove direction/frame parity is possible in the renderer; no claim about current unit position, reachability, occupants, turn budget or saved battle state.']}
(out/'four-original-stone-pixels-walk-overlap.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'ready':True,'legalCells':len(legal),'directedEdges':len(edges),'segments':len(A),'counts':[(x['originalPixel'],x['intersectingSoleSegments']) for x in counts],'fixedWitnesses':ws},ensure_ascii=False,indent=2))
