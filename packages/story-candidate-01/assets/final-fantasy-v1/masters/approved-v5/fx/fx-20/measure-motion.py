from pathlib import Path
from PIL import Image
from scipy.ndimage import label,find_objects
import numpy as np,json,hashlib
R=Path(__file__).parent;Q=R/'qa';P=['bare-crescent','contact-spark','spark-spray','residual-sparks'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
A={p:np.array(Image.open(R/p/'master.png')).astype(int)for p in P};ref=A[P[0]]
def blue(a):return (a[:,:,3]>=128)&(a[:,:,2]>a[:,:,0]+8)&(a[:,:,2]>a[:,:,1]+3)
def gold(a):return (a[:,:,3]>=128)&(a[:,:,0]>a[:,:,2]+30)&(a[:,:,1]>a[:,:,2]+10)&(a[:,:,0]>100)
def bbox(m):
 y,x=np.where(m);return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)] if len(x) else None
rois={'upper-crescent':[285,170,620,440],'lower-crescent':[580,850,770,1142]};frames={}
for p,a in A.items():
 b=blue(a);g=gold(a);lab,n=label(g);cs=[]
 for k,s in enumerate(find_objects(lab)):
  z=lab[s]==k+1
  if z.sum()>100:
   yy,xx=np.where(z);cs.append({'pixels':int(z.sum()),'bounds':[s[1].start,s[0].start,s[1].stop,s[0].stop],'centroid':[float(xx.mean()+s[1].start),float(yy.mean()+s[0].start)]})
 reg={}
 for name,(x0,y0,x1,y1) in rois.items():
  m=blue(ref)[y0:y1,x0:x1];target=ref[y0:y1,x0:x1,:3];best=None
  for dy in range(-6,7):
   for dx in range(-6,7):
    part=a[y0+dy:y1+dy,x0+dx:x1+dx,:3];err=float(np.abs(part-target)[m].mean())
    if best is None or err<best[0]:best=(err,dx,dy)
  reg[name]={'roi':rois[name],'comparison':'Mean absolute RGB error on fixed bare-crescent blue-material mask; diagnostic offsets only, never applied to pixels.','bestOffsetXY':[best[1],best[2]],'meanAbsoluteRGBError':best[0]}
 roots=[]
 for y in range(590,631):
  xs=np.where(g[y,680:780])[0]
  if len(xs):roots.append([int(xs.min()+680),y])
 frames[p]={'sourceSHA256':sha(R/p/'master.png'),'sourceAlphaMass':float(a[:,:,3].sum()/255),'blueMaterialBounds':bbox(b),'blueMaterialPixels':int(b.sum()),'goldMaterialBounds':bbox(g),'goldMaterialPixels':int(g.sum()),'goldComponentsOver100Pixels':cs,'detectedGoldComponents':len(cs),'fixedMaterialROIs':reg,'contactRootAtRows590Through630':roots,'medianContactRootX':float(np.median(np.array(roots)[:,0])) if roots else None}
m=np.array([frames[p]['sourceAlphaMass']for p in P]);bb=np.array([frames[p]['blueMaterialBounds']for p in P]);root1=np.array(frames['contact-spark']['contactRootAtRows590Through630']);root2=np.array(frames['spark-spray']['contactRootAtRows590Through630']);assert np.array_equal(root1[:,1],root2[:,1]);delta=np.abs(root1[:,0]-root2[:,0]);proof={'topicId':'C01-FX-20','phaseOrder':P,'method':'Blue/gold color-and-alpha diagnostic masks separate fixed crescent from authored sparks. Alpha128 blue mask uses B>R+8 and B>G+3. Gold mask uses R>B+30, G>B+10 and R>100. Masks are measurements only; no generated pixels are modified.','frames':frames,'blueMaterialBoundsSpreadSourcePx':np.ptp(bb,axis=0).tolist(),'maxBlueBoundsSpreadLogicalPx':float(np.ptp(bb,axis=0).max()*28/1254),'maxMaterialBestOffsetSourcePx':max(abs(v)for f in frames.values() for r in f['fixedMaterialROIs'].values()for v in r['bestOffsetXY']),'contactRootComparison':{'rows':[590,630],'interpretation':'The leftmost gold boundary across the contact-height cross-section is stable. This bounds attachment to the crescent, not a claim that the entire burst silhouette or centroid is fixed.','medianDeltaSourcePx':float(np.median(delta)),'maxDeltaSourcePx':int(delta.max()),'maxDeltaLogicalPx':float(delta.max()*28/1254)},'adjacentSourceAlphaMassChangesPercent':(np.diff(m)/m[:-1]*100).tolist(),'noPerFrameFitOrShift':True,'loop':False,'livePlaybackObserved':False};(Q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
color=Image.new('RGB',(1600,1600))
for i,p in enumerate(P):color.paste(Image.open(Q/f'{p}-green-ground-source-full.png').resize((800,800),Image.Resampling.LANCZOS),(i%2*800,i//2*800))
color.save(Q/'all-source-green-ground-overview.png')
print(json.dumps({k:proof[k] for k in ['blueMaterialBoundsSpreadSourcePx','maxBlueBoundsSpreadLogicalPx','maxMaterialBestOffsetSourcePx','contactRootComparison','adjacentSourceAlphaMassChangesPercent']},indent=2));print({p:frames[p]['detectedGoldComponents']for p in P})
