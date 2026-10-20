from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,numpy as np
r=Path(__file__).parent;order=['clustered-echoes','rising-echoes','dissolving-echoes','wisp-remnants'];phases=[p for p in order if(r/p/'master.png').exists()];q=r/'qa'
f=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
im=Image.new('RGB',(1280,440),'#d6d2c9');d=ImageDraw.Draw(im);d.text((20,10),'Shared full canvas and target pivot: no per-frame crop, fit or shift; overlay lines are QA only.',font=f,fill='black')
for i,p in enumerate(phases):
 x=20+i*315;d.text((x,40),p,font=f,fill='black');src=Image.open(q/f'{p}@2x.png');comp=Image.new('RGBA',src.size,'#322c27');comp.alpha_composite(src);comp=comp.resize((256,256),Image.Resampling.NEAREST);im.paste(comp.convert('RGB'),(x,75));d.line((x+128,75,x+128,331),fill='#21d5d7');d.line((x,203,x+256,203),fill='#21d5d7');d.rectangle((x+16,91,x+239,314),outline='#e98951',width=1);d.text((x,345),'cyan: target16,16; orange:28px canvas',font=f,fill='black')
im.save(q/'registration-pivot-proof.png')
print('Updated common-pivot overlay proof only; source files unchanged')
