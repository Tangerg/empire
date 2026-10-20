from pathlib import Path
from PIL import Image
import numpy as np,json
r=Path(__file__).parent
phases=['neutral-standard','capture-spiral','blue-standard-peak','settled-standard']
results=[]
for phase in phases:
 p=r/phase/'master.png'
 if not p.exists():continue
 a=np.array(Image.open(p)).astype('int16');red,green,blue,alpha=a.transpose(2,0,1)
 y,x=np.indices(alpha.shape)
 gold=(red>green+10)&(red>blue+70)&(green>blue+25)&(alpha>=128)&(x>=500)&(x<=675)&(y>=70)&(y<=920)&((y<300)|(y>830))
 yy,xx=np.nonzero(gold)
 bbox=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
 results.append({'phase':phase,'goldLandmarkBounds':bbox})
baseline=results[0]['goldLandmarkBounds']
for row in results:
 row['goldBoundsDeltaSourcePixels']=[a-b for a,b in zip(row['goldLandmarkBounds'],baseline)]
 row['maxGoldBoundsDeltaLogicalPixels']=max(map(abs,row['goldBoundsDeltaSourcePixels']))*28/1254
report={'method':'Threshold gold finial/collar and foot (exclude energy-bearing middle) within fixed source ROI; compare bounding extrema to unchanged neutral. Diagnostic only, no pixel manipulation.','baseline':phases[0],'sharedScale':28/1254,'frames':results,'visualReviewRequired':True}
(r/'qa'/'landmark-registration.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
