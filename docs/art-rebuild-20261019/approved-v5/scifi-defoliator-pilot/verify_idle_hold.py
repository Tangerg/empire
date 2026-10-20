from pathlib import Path
from PIL import Image
import json,hashlib,sys
import numpy as np
R=Path(__file__).resolve().parents[4];D=Path(__file__).parent
B=R/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/defoliator-pilot'
sys.path.insert(0,str(R/'tools/art'))
from assemble_approved_slice import alpha_report
qa={};review=Image.new('RGB',(64*3,64*3),'white')
for i,name in enumerate(['raw-rejected-v1.png','raw-v2.png','raw-v3.png']):
 p=B/'idle'/name;im=Image.open(p);a=np.array(im)[:,:,3];yy,xx=np.where(a>=128)
 row={'source':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'size':list(im.size),'opaqueBounds':[int(xx.min()),int(yy.min()),int(xx.max()),int(yy.max())]}
 try:row['sourceAlpha']=alpha_report(im)
 except ValueError as e:row['sourceAlphaError']=str(e)
 row['diagnosticProjections']={}
 for y in [12,11]:
  c=Image.new('RGBA',(64,64));c.paste(im.resize((52,52),Image.Resampling.LANCZOS),(9,y))
  data={'wholeCanvas':[9,y,61,y+52],'projectedLowestOpaqueY':float(y+yy.max()*52/1254)}
  try:data['alpha']=alpha_report(c)
  except ValueError as e:data['error']=str(e)
  row['diagnosticProjections'][str(y)]=data
  if y==11:
   c.save(D/f'idle-{name[:-4]}-native64-diagnostic.png')
   for j,bg in enumerate(['white','#171717','#777d56']):
    cell=Image.new('RGB',(64,64),bg);cell.paste(c,(0,0),c);review.paste(cell,(i*64,j*64))
 qa[name]=row
review.save(D/'idle-source-attempts-native64.png');review.resize((768,768),Image.Resampling.NEAREST).save(D/'idle-source-attempts-native64-4x.png')
(D/'idle-hold-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
print(json.dumps(qa,indent=2))
