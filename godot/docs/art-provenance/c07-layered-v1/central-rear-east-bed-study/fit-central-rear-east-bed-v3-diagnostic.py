from PIL import Image
import numpy as np,json,math
from pathlib import Path
p=Path('/workspace/scratch/84c9fe948755');a=np.array(Image.open(p/'generated_images/exec-35b3ff1d-189f-414b-ac74-6ebbd2434380.png'));foot={}
for x in range(328,1208):
 ys=np.where(a[470:1024,x,3]>127)[0]
 if len(ys):foot[x]=int(ys.max()+470)
pts=[(795,453),(799,455),(803,457),(807,459),(811,461),(815,465),(819,467)]
l=next(v for v in json.load(open(p/'empire-c01/godot/data/grayflag-campaign-levels.json')) if v['id']=='c01-07');best=[];valid=[];footOnly=[]
for scale in np.arange(.025,.0651,.0005):
 for tx in np.arange(750,791,.25):
  coords=[round((x-tx)/scale) for x,y in pts]
  if any(x not in foot for x in coords):continue
  ty=float(np.mean([y-foot[x]*scale for x,(_,y) in zip(coords,pts)]));errors=[ty+foot[x]*scale-y for x,(_,y) in zip(coords,pts)];rmse=float(np.sqrt(np.mean(np.array(errors)**2)))
  if rmse>20:continue
  samples=[]
  for x,y in [(786,419),(786,420)]:
   u,v=round((x-tx)/scale),round((y-ty)/scale);samples.append(int(a[v,u,3]) if 0<=u<a.shape[1] and 0<=v<a.shape[0] else 0)
  allblocked=all(l['terrain'][math.floor((ty+y*scale)/16)][math.floor((tx+x*scale)/16)]=='#' for x,y in foot.items())
  row={'scale':float(scale),'translation':[float(tx),ty],'rmse':rmse,'maxError':max(abs(v) for v in errors),'fixedLeafAlpha':samples,'allVisibleFeetBlocked':allblocked}
  best.append(row)
  if allblocked:footOnly.append(row)
  if min(samples)>127 and allblocked:valid.append(row)
footOnly.sort(key=lambda v:v['rmse']);best.sort(key=lambda v:v['rmse']);valid.sort(key=lambda v:v['rmse']);result={'scope':'Bounded uniform-registration search. Original lower-edge mixed bands approximated by midpoints; NOT exact original foot proof. Raw generated alpha used unchanged. No gameplay.','footRoi':[328,470,1208,1024],'footOnlyFits':footOnly[:12],'footOnlyCount':len(footOnly),'footColumns':len(foot),'topFits':best[:12],'validLeafAndBlockedFits':valid[:12],'validCount':len(valid),'examinedGoodEdgeFits':len(best)};(p/'c07-static-preview/central-rear-east-bed-v3-unbounded-diagnostic-fit.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result)[:2400])
