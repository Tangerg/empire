import json,hashlib,sys
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np
root=Path(__file__).resolve().parents[5]
sys.path.insert(0,str(root/'tools/art'))
from export_frame_overrides import plan
base=root/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/boarding-team'
doc=Path(__file__).parent
def source(phase):
 return base/'candidates/blue-gait-v2/walk-b.png' if phase=='walk-b' else base/phase/'master.png'
rois={'idle':[(190,1090,375,1200),(500,1090,630,1170),(660,1160,825,1220),(960,1080,1220,1200)],'walk-a':[(190,1040,360,1140),(990,1080,1240,1190)],'attack':[(155,1080,345,1175),(455,1050,670,1140),(785,1150,947,1205),(1000,1080,1230,1180)],'walk-b':[(420,1090,595,1200),(600,1090,760,1200)]}
measure={}
for phase,boxes in rois.items():
 a=np.array(Image.open(source(phase)))[:,:,3]; feet=[]
 for x0,y0,x1,y1 in boxes:
  ys,xs=np.where(a[y0:y1,x0:x1]>=128); y=int(ys.max()+y0); near=xs[ys>=ys.max()-3]+x0;feet.append({'roi':[x0,y0,x1,y1],'soleY':y,'bottomFourRowsMeanX':float(near.mean()),'planted':True})
 measure[phase]={'feet':feet,'support':[sum(f['bottomFourRowsMeanX'] for f in feet)/len(feet),max(f['soleY'] for f in feet)]}
print(json.dumps(measure,indent=2))
(doc/'measured-support.json').write_text(json.dumps(measure,indent=2)+'\n')
offsets={'idle':[2,-1],'walk-a':[2,0],'attack':[2,0],'walk-b':[2,0]}; width=44;origin=[5,3]
frames={p:{'source':str(source(p).relative_to(root)),'sha256':hashlib.sha256(source(p).read_bytes()).hexdigest(),'registrationOffset':offsets[p]} for p in rois}
config={'schemaVersion':1,'assets':{'c02-unit-boarding-team':{'kind':'frames-v1','frames':frames,'placement':{'renderWidth':width,'origin':origin}}}}
(doc/'temporary-frame-overrides.json').write_text(json.dumps(config,indent=2)+'\n')
planned=plan(root,doc/'temporary-frame-overrides.json')
assert len(planned)==1,'Do not invent HD consumer'
target,sheet,report=planned[0]; sheet.save(doc/'canonical64x48-sheet.png')
review=Image.new('RGB',(288,192),'#222222');draw=ImageDraw.Draw(review)
for row,bg in enumerate(['#f0ece2','#202633','#647b4b']):
 for i,p in enumerate(rois):
  tile=Image.new('RGBA',(64,48),bg);tile.alpha_composite(sheet.crop((i*64,0,i*64+64,48)));review.paste(tile.convert('RGB'),(i*72,row*64+16));draw.text((i*72,row*64),p,fill='white')
review.save(doc/'native64x48-three-backgrounds.png')
# Magnified review only, not another consumer.
review.resize((864,576),Image.Resampling.NEAREST).save(doc/'native64x48-three-backgrounds-3x-review.png')
gif=[]
for p in ['idle','walk-a','walk-b','walk-a','walk-b','idle','attack','idle']:
 i=list(rois).index(p);tile=Image.new('RGBA',(64,48),'#647b4b');tile.alpha_composite(sheet.crop((i*64,0,i*64+64,48)));gif.append(tile.convert('RGB'))
gif[0].save(doc/'native64x48-motion.gif',save_all=True,append_images=gif[1:],duration=240,loop=0,disposal=2)
for p,m in measure.items():
 m['registrationOffset']=offsets[p];m['projectedSupport']=[origin[i]+offsets[p][i]+m['support'][i]*width/1254 for i in range(2)];m['canonicalAnchorError']=[m['projectedSupport'][0]-32,m['projectedSupport'][1]-47]
reg={'status':'Bounded registration candidate; actual scene contact review pending','frameSize':[64,48],'canonicalAnchor':[32,47],'commonSourceCanvas':[1254,1254],'renderWidth':width,'origin':origin,'registrationOffsets':offsets,'measuredSupport':measure,'method':'Bottom four rows alpha>=128 mean X per visually planted foot ROI, equal-weight mean of planted soles X, deepest sole Y; lifted boots excluded.','residualDriftXY':[max(v['projectedSupport'][i] for v in measure.values())-min(v['projectedSupport'][i] for v in measure.values()) for i in range(2)],'fullCanvasFit':True,'guardLimit':2,'masterPixelsModified':False,'formalRuntimeIntegration':False,'liveGamePlayback':'not performed'}
(doc/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n');(doc/'temporary-plan-verification.json').write_text(json.dumps({'passed':True,'outputCount':len(planned),'wouldWrite':str(target.relative_to(root)),'report':report,'runtimeWritesPerformed':False},indent=2)+'\n')
print(json.dumps(reg,indent=2))
