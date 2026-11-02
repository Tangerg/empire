from pathlib import Path
import json,hashlib,math
import numpy as np
from PIL import Image
out=Path(__file__).resolve().parent;root=out.parents[1];repo=root/'empire-c01'
fpath=root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json';frozen=json.loads(fpath.read_text())
level=next(x for x in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if x['id']=='c01-07')
assetpath=repo/'godot/assets/painted-units-v1/torren-stage01-v2.png';sprite=Image.open(assetpath).convert('RGBA');sourcepath=repo/'godot/assets/native/scenes/luojia.png';source=Image.open(sourcepath).convert('RGB')
legal=[(x,y) for y in range(28,33) for x in range(17,29) if level['terrain'][y][x]=='p'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1]
def position(cell,frame,p,west):
 return [16*cell[0]+8+(p[0]-frame['anchor'][0])*frozen['scale']*(-1 if west else 1),16*cell[1]+8+(p[1]-frame['anchor'][1])*frozen['scale']]
A=[];B=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for p in f['sole']:
    A.append(position(a,f,p,west));B.append(position(b,f,p,west));meta.append({'from':a,'to':b,'west':west,'frame':f['index'],'framePixel':p})
A=np.array(A,float);B=np.array(B,float);delta=B-A
points=[[318,474],[332,481],[337,483]];counts=[]
for p in points:
 lo=np.zeros(len(A));hi=np.ones(len(A));valid=np.ones(len(A),bool)
 for k in range(2):
  moving=abs(delta[:,k])>1e-14;valid&=moving|((A[:,k]>=p[k])&(A[:,k]<p[k]+1))
  q=np.where(moving,delta[:,k],1.);ta=(p[k]-A[:,k])/q;tb=(p[k]+1-A[:,k])/q
  lo=np.where(moving,np.maximum(lo,np.minimum(ta,tb)),lo);hi=np.where(moving,np.minimum(hi,np.maximum(ta,tb)),hi)
 ids=np.flatnonzero(valid&(lo<hi));examples=[]
 for i in ids[:3]:
  t=float((lo[i]+hi[i])/2);examples.append({**meta[int(i)],'tInterval':[float(lo[i]),float(hi[i])],'t':t,'sourcePoint':(A[i]+delta[i]*t).tolist()})
 counts.append({'originalPixel':p,'materialConfidence':('lower stone/shadow mixed edge; not needed for the hard counterexample' if p==[452,487] else 'visually identified west-long-wall lower stone material'),'RGB':list(source.getpixel(tuple(p))),'pixelDomain':[[p[0],p[1]],[p[0]+1,p[1]+1]],'piecewisePixelConvention':'Unit source texel cell [x,x+1) × [y,y+1). Only the three manually identified west-long-wall lower-stone texels. No filled bbox or hidden foot inference.','intersectingSoleSegments':len(ids),'examples':examples})

(out/'bounded-long-wall-foot-pixel-walk-review.json').write_text(json.dumps({'scope':'Only three original visible lower-stone texels and local canonical legal adjacent edges, static not gameplay','legalCells':legal,'directedEdges':edges,'segments':len(A),'points':counts},indent=2)+'\n');print(json.dumps(counts,indent=2))
