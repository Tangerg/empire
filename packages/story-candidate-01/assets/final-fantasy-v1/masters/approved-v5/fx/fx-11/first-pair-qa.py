from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label,find_objects
import numpy as np,json,hashlib
r=Path(__file__).parent;q=r/'qa';q.mkdir(exist_ok=True);phases=['gust-curl-a','gust-curl-b'];rows=[];sheet=Image.new('RGB',(900,500),'#d6d2c9');d=ImageDraw.Draw(sheet);font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
full=Image.new('RGB',(2508,2508))
for i,p in enumerate(phases):
 im=Image.open(r/p/'master.png');a=np.array(im.getchannel('A'));lab,n=label(a>=128);cc=[]
 for sl in find_objects(lab):
  y,x=sl
  if (x.stop-x.start)*(y.stop-y.start)>300:cc.append([x.start,y.start,x.stop,y.stop])
 rows.append({'phase':p,'componentsAlpha128':cc})
 for b,col in enumerate(['black','white']):
  bg=Image.new('RGBA',im.size,col);bg.alpha_composite(im);full.paste(bg.convert('RGB'),(1254*i,1254*b))
  x=10+i*450;y=20+b*240;d.text((x,y),p+' '+col,font=font,fill='black')
  for den,dx in [(1,0),(2,42)]:
   cell=Image.new('RGBA',(32*den,32*den));cell.paste(im.resize((28*den,28*den),Image.Resampling.LANCZOS),(2*den,2*den));back=Image.new('RGBA',cell.size,col);back.alpha_composite(cell);sheet.paste(back.convert('RGB'),(x+dx,y+30))
   if den==2:sheet.paste(back.resize((192,192),Image.Resampling.NEAREST).convert('RGB'),(x+130,y+30))
sheet.save(q/'first-pair-native-black-white.png');full.save(q/'first-pair-source-black-white.png');(r/'first-pair.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps(rows,indent=2))
