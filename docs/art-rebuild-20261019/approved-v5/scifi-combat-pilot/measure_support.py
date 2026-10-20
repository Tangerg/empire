from pathlib import Path
from PIL import Image
import json
R=Path(__file__).resolve().parents[4];B=R/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/antler-knight';D=Path(__file__).parent
# Visually identified sole regions; exclude lifted feet from support centroid.
rois={
'idle': [('near-hind',[240,1140,360,1210],True),('far-hind',[530,1110,650,1190],True),('near-fore',[720,1150,820,1240],True),('far-fore',[990,1120,1110,1210],True)],
'walk-a':[('near-hind',[140,1090,240,1210],False),('far-hind',[530,1100,640,1190],True),('near-fore',[820,1130,960,1230],True),('far-fore',[1060,1030,1190,1150],False)],
'attack':[('near-hind',[40,1070,180,1170],True),('far-hind',[450,1060,600,1160],True),('near-fore',[710,1060,840,1170],True),('far-fore',[1030,1060,1170,1170],True)],
'walk-b':[('near-hind',[190,1110,320,1210],True),('far-hind',[550,1120,680,1210],True),('near-fore',[755,1020,850,1120],False),('far-fore',[1030,1130,1150,1230],True)]}
out={}
for n,feet in rois.items():
 im=Image.open(B/n/'master.png');a=im.getchannel('A'); rec=[]
 for name,box,planted in feet:
  mask=a.crop(box).point(lambda x:255 if x>=128 else 0);bb=mask.getbbox();y=box[1]+bb[3]-1
  xs=[x for yy in range(y-3,y+1) for x in range(box[0],box[2]) if a.getpixel((x,yy))>=128]
  rec.append({'foot':name,'roi':box,'planted':planted,'soleY':y,'bottomFourRowsMeanX':sum(xs)/len(xs)})
 contacts=[f for f in rec if f['planted']];out[n]={'feet':rec,'support':[sum(f['bottomFourRowsMeanX'] for f in contacts)/len(contacts),max(f['soleY'] for f in contacts)]}
(D/'measured-support.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out,indent=2))
