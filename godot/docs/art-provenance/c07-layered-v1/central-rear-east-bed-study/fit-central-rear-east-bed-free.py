from PIL import Image
from pathlib import Path
import numpy as np,json
p=Path('/workspace/scratch/84c9fe948755');a=np.array(Image.open(p/'generated_images/exec-35b3ff1d-189f-414b-ac74-6ebbd2434380.png'));xs=[];ys=[]
for x in range(328,1208):
 yy=np.where(a[470:,x,3]>127)[0]
 if len(yy):xs.append(x);ys.append(int(yy.max()+470))
xs=np.array(xs);ys=np.array(ys);foot=dict(zip(xs,ys));pts=np.array([[795,453],[799,455],[803,457],[807,459],[811,461],[815,465],[819,467]])
l=next(v for v in json.load(open(p/'empire-c01/godot/data/grayflag-campaign-levels.json')) if v['id']=='c01-07');blocked=np.array([[q=='#' for q in r] for r in l['terrain']]);valid=[];footOnly=[]
for scale in np.arange(.025,.0651,.0005):
 for tx in np.arange(750,791,.25):
  coords=[round((x-tx)/scale) for x,y in pts]
  if any(x not in foot for x in coords):continue
  yy=np.array([foot[x] for x in coords]);ty0=float(np.mean(pts[:,1]-yy*scale));ix=np.floor(tx+xs*scale).astype(int)//16
  for ty in np.arange(ty0-8,ty0+8.1,.5):
   iy=np.floor(ty+ys*scale).astype(int)//16
   if not blocked[iy,ix].all():continue
   uv=[(round((786-tx)/scale),round((y-ty)/scale)) for y in [419,420]];alphas=[int(a[v,u,3]) if 0<=u<a.shape[1] and 0<=v<a.shape[0] else 0 for u,v in uv];err=ty+yy*scale-pts[:,1];r={'scale':float(scale),'translation':[float(tx),float(ty)],'verticalDepartureFromEdgeFit':float(ty-ty0),'rmse':float(np.sqrt(np.mean(err**2))),'maxError':float(max(abs(err))),'fixedLeafAlpha':alphas,'allVisibleFeetBlocked':True};footOnly.append(r)
   if min(alphas)>127:valid.append(r)
valid.sort(key=lambda v:v['rmse']);footOnly.sort(key=lambda v:v['rmse']);result={'scope':'Exploratory FREE translation diagnostic; no source-geometry gate is relaxed. Rankings must disclose old-foot deviation and still require footprint/sole/actual render verification.','validCount':len(valid),'footOnlyCount':len(footOnly),'valid':valid[:10],'footOnly':footOnly[:5]};(p/'c07-static-preview/central-rear-east-bed-v3-free-fit.json').write_text(json.dumps(result,indent=2));print(result)
