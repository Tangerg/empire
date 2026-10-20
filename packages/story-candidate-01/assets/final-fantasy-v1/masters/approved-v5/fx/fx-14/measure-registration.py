from pathlib import Path
from PIL import Image
import numpy as np,json
R=Path(__file__).parent
P=[p for p in ['low-fumes','rising-fumes','plume-crest','settling-wisps'] if (R/p/'master.png').exists()]
a={p:np.array(Image.open(R/p/'master.png').convert('RGBA')) for p in P}
# Fixed material regions, deliberately separate from the animated fumes and airborne papers.
rois={'left-ground-paper':[250,920,480,1045],'right-ground-paper':[800,915,1020,1030],'grass':[220,680,410,810],'left-rock':[250,880,360,930],'center-rock':[480,915,565,965],'front-rock':[620,980,725,1030]}
base=a['rising-fumes'];out={'referencePhase':'rising-fumes','method':'Fixed source-coordinate material ROIs, alpha-weighted RGB absolute-difference search over +/-6 pixels. Diagnostic measurement only; no source or export movement.','rois':rois,'phases':{}}
for p,z in a.items():
 d={'staticMaterialMatches':{},'soilExtrema':{}}
 for name,(x0,y0,x1,y1) in rois.items():
  ref=base[y0:y1,x0:x1,:3].astype(float);w=(base[y0:y1,x0:x1,3]>128)[...,None];best=None
  for dy in range(-6,7):
   for dx in range(-6,7):
    val=float((abs(z[y0+dy:y1+dy,x0+dx:x1+dx,:3].astype(float)-ref)*w).sum()/(w.sum()*3))
    item=(val,abs(dx)+abs(dy),dx,dy)
    if best is None or item<best:best=item
  d['staticMaterialMatches'][name]={'bestOffsetXY':list(best[2:]),'meanAbsoluteRgbError':round(best[0],3)}
 mask=z[:,:,3]>=128;mask[:740]=False
 for name,(x0,y0,x1,y1) in {'leftTip':[0,850,160,1000],'rightTip':[1100,850,1254,1000],'bottomTip':[500,1080,800,1200]}.items():
  yy,xx=np.where(mask[y0:y1,x0:x1]);d['soilExtrema'][name]=[int(xx.min()+x0),int(yy.min()+y0),int(xx.max()+x0+1),int(yy.max()+y0+1)]
 out['phases'][p]=d
(R/'qa/material-registration-proof.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out,indent=2))
