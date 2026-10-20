from pathlib import Path
from PIL import Image
from scipy.ndimage import label
import numpy as np,json
R=Path(__file__).parent;P=[p for p in ['short-rays','rising-fan','graylight-crest','residual-rays'] if (R/p/'master.png').exists()];a={p:np.array(Image.open(R/p/'master.png').convert('RGBA')) for p in P}
rois={'emitter-socket':[575,778,681,865],'front-parapet':[550,864,700,931],'front-door':[545,974,620,1069],'base-front':[490,1082,750,1146],'left-rubble':[385,1055,485,1168],'right-rubble':[775,1050,866,1165]};base=a['graylight-crest'];out={'referencePhase':'graylight-crest','method':'Fixed source-coordinate tower ROIs, alpha-weighted RGB absolute-difference search over +/-6 pixels. Largest alpha128 connected component isolates the physical tower/rays from floating stones; its region below y820 measures the registered lower tower. Diagnostic only, never image placement.','rois':rois,'phases':{}}
for p,z in a.items():
 d={'staticMaterialMatches':{}}
 for name,(x0,y0,x1,y1) in rois.items():
  ref=base[y0:y1,x0:x1,:3].astype(float);w=(base[y0:y1,x0:x1,3]>128)[...,None];best=None
  for dy in range(-6,7):
   for dx in range(-6,7):
    val=float((abs(z[y0+dy:y1+dy,x0+dx:x1+dx,:3].astype(float)-ref)*w).sum()/(w.sum()*3));item=(val,abs(dx)+abs(dy),dx,dy)
    if best is None or item<best:best=item
  d['staticMaterialMatches'][name]={'bestOffsetXY':list(best[2:]),'meanAbsoluteRgbError':round(best[0],3)}
 mask=z[:,:,3]>=128;ll,n=label(mask);counts=np.bincount(ll.flat);counts[0]=0;main=ll==counts.argmax();main[:820]=False;yy,xx=np.where(main);d['lowerTowerBoundsAlpha128']=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
 warm=(z[:,:,3]>=128)&(z[:,:,0]>170)&(z[:,:,1]>80)&(z[:,:,2]<190);warm[:778]=False;warm[865:]=False;warm[:,:575]=False;warm[:,681:]=False;yy,xx=np.where(warm);d['amberInteriorCentroid']=[float(xx.mean()),float(yy.mean())]
 out['phases'][p]=d
(R/'qa/material-registration-proof.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out,indent=2))
