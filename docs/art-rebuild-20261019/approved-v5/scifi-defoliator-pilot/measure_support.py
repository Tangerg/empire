from pathlib import Path
from PIL import Image
import numpy as np,json
R=Path(__file__).resolve().parents[4];D=Path(__file__).parent;B=R/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/defoliator-pilot'
rois=json.loads((D/'support-rois.json').read_text()); reg=json.loads((D/'registration.json').read_text());results={}
for name,regions in rois.items():
 a=np.array(Image.open(B/name/'master.png'))[:,:,3];feet=[]
 for label,box in regions.items():
  x0,y0,x1,y1=box;yy,xx=np.where(a[y0:y1,x0:x1]>=128);bottom=int(yy.max()+y0); centers=[]
  for y in range(bottom-3,bottom+1):
   xs=np.where(a[y,x0:x1]>=128)[0]
   if len(xs):centers.append(float(xs.mean()+x0))
  feet.append({'name':label,'roi':box,'bottomY':bottom,'bottomFourRowCentroidX':sum(centers)/len(centers)})
 x=sum(f['bottomFourRowCentroidX'] for f in feet)/len(feet);y=max(f['bottomY'] for f in feet);dx,dy=reg['offsets'][name];w=reg['renderWidth'];ox,oy=reg['origin'];px=ox+dx+x*w/a.shape[1];py=oy+dy+y*w/a.shape[0];bx,by=px-16,py-32
 for f in feet:
  fx=ox+dx+f['bottomFourRowCentroidX']*w/a.shape[1]-16;fy=oy+dy+f['bottomY']*w/a.shape[0]-32
  f['boardSupport']=[fx,fy];f['shadowNormalizedDistance']=((fx-16)/12.5)**2+((fy-29.2)/4.2)**2
 results[name]={'feet':feet,'sourceSupport':[x,y],'projectedSupport':[px,py],'anchorError':[px-32,py-63],'boardSupport':[bx,by],'shadowNormalizedDistance':((bx-16)/12.5)**2+((by-29.2)/4.2)**2}
(D/'measured-support.json').write_text(json.dumps(results,indent=2)+'\n');print(json.dumps(results,indent=2))
