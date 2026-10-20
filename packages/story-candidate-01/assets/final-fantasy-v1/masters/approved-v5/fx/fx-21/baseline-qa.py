from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json
r=Path(__file__).parent;q=r/'qa';q.mkdir(exist_ok=True);im=Image.open(r/'dust-impact/master.png');a=np.array(im.getchannel('A'));big=Image.new('RGB',(2508,1254));native=Image.new('RGB',(900,280),'#d6d2c9');d=ImageDraw.Draw(native);font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',15);rows=[]
for i,col in enumerate(['black','white']):
 bg=Image.new('RGBA',im.size,col);bg.alpha_composite(im);bg.convert('RGB').save(q/f'peak-source-{col}.png');big.paste(bg.convert('RGB'),(i*1254,0));d.text((20+i*450,15),'dust-impact / '+col,font=font,fill='black')
 for den,dx in [(1,0),(2,44)]:
  fr=Image.new('RGBA',(32*den,32*den));fr.paste(im.resize((28*den,28*den),Image.Resampling.LANCZOS),(2*den,2*den));out=Image.new('RGBA',fr.size,col);out.alpha_composite(fr);native.paste(out.convert('RGB'),(20+i*450+dx,50))
  aa=np.array(fr.getchannel('A'));pad=2*den;assert max(aa[:pad,:].max(),aa[-pad:,:].max(),aa[:,:pad].max(),aa[:,-pad:].max())==0
  if den==2:native.paste(out.resize((192,192),Image.Resampling.NEAREST).convert('RGB'),(20+i*450+135,50))
 e=np.array(bg.convert('RGB'));edge=np.concatenate([e[0],e[-1],e[:,0],e[:,-1]]);rows.append({'background':col,'perimeterDeviationMaximum':int(np.abs(edge.astype('int16')-(0 if col=='black' else 255)).max())})
big.save(q/'peak-source-black-white.png');native.save(q/'peak-native-black-white.png');(q/'peak-composite-proof.json').write_text(json.dumps(rows,indent=2)+'\n');print(rows)
