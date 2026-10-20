from pathlib import Path
import json,hashlib
from PIL import Image,ImageDraw
import numpy as np
root=Path(__file__).resolve().parents[5];doc=Path(__file__).parent
base=root/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/boarding-team'
paths={p:base/p/'master.png' for p in ['idle','walk-a','attack','walk-b']}
paths['blue-gait-v2']=base/'candidates/blue-gait-v2/walk-b.png';paths['blue-support-v3']=base/'candidates/blue-support-v3/walk-b.png'
rows=[];review=Image.new('RGB',(6*320,360),'#536048');d=ImageDraw.Draw(review)
for i,(p,path) in enumerate(paths.items()):
 im=Image.open(path).convert('RGBA');a=np.array(im)[:,:,3]
 bounds={}
 for threshold in [1,128]:
  yy,xx=np.where(a>=threshold);bounds[str(threshold)]=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
 rows.append({'pose':p,'source':str(path.relative_to(root)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'size':list(im.size),'alphaRange':[int(a.min()),int(a.max())],'boundsByMinimumAlpha':bounds,'edgeMaxAlpha':int(max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max()))})
 tile=Image.new('RGBA',im.size,'#536048');tile.alpha_composite(im);review.paste(tile.resize((314,314)),(i*320,25));d.text((i*320,5),p,fill='white')
review.save(doc/'full-source-comparison.png');(doc/'source-bounds.json').write_text(json.dumps(rows,indent=2)+'\n')
# Annotated source for review only; never a generation input or production asset.
im=Image.open(paths['blue-support-v3']).convert('RGBA');bg=Image.new('RGBA',im.size,'#536048');bg.alpha_composite(im);d=ImageDraw.Draw(bg)
boxes={'orange plant':(420,1090,660,1200),'blue plant':(895,1090,1125,1220),'orange lifted':(70,975,230,1155),'blue lifted':(1045,875,1160,1075)}
feet={}
a=np.array(im)[:,:,3]
for label,b in boxes.items():
 d.rectangle(b,outline='yellow',width=2);d.text((b[0],b[1]-15),label,fill='yellow');x0,y0,x1,y1=b;ys,xs=np.where(a[y0:y1,x0:x1]>=128);feet[label]={'roi':list(b),'soleY':int(ys.max()+y0),'bottomFourRowsMeanX':float((xs[ys>=ys.max()-3]+x0).mean())}
bg.save(doc/'source-foot-rois.png');(doc/'four-foot-evidence.json').write_text(json.dumps(feet,indent=2)+'\n')
print(json.dumps(rows[-1],indent=2));print(json.dumps(feet,indent=2))
