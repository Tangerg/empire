from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
B=Path(__file__).parent
POSES=['idle','walk-a','carry-or-work','walk-b']
BOOT_SPLITS={'idle':760,'walk-a':760,'carry-or-work':760,'walk-b':760}
COLORS={'white':(255,255,255),'black':(16,16,16),'scene':(114,128,86)}
for p in POSES:
 d=B/p
 if not (d/'master.png').exists():continue
 im=Image.open(d/'master.png').convert('RGBA');a=np.asarray(im)[:,:,3];ys,xs=np.where(a>127);box=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)];ys0,xs0=np.where(a>0)
 split=BOOT_SPLITS[p];feet=[]
 for x0,x1 in [(350,split),(split,1150)]:
  yy,xx=np.where(a[840:,x0:x1]>127);bot=int(yy.max()+840); yy,xx=np.where(a[bot-3:bot+1,x0:x1]>127);feet.append({'xRange':[x0,x1],'bottom':bot,'bottom4RowsBounds':[int(xx.min()+x0),bot-3,int(xx.max()+x0+1),bot+1],'soleCenterX':float((xx.min()+xx.max())/2+x0)})
 foot=[sum(f['soleCenterX'] for f in feet)/2,max(f['bottom'] for f in feet)]
 perimeter=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 q={'nativeSize':list(im.size),'bboxAlphaNonzero':[int(xs0.min()),int(ys0.min()),int(xs0.max()+1),int(ys0.max()+1)],'bboxAlphaGreaterThan127':box,'alphaZeroFraction':float((a==0).mean()),'alphaRange':[int(a.min()),int(a.max())],'perimeterMaxAlpha':int(perimeter.max()),'perimeterCountAlphaGreaterThan1':int((perimeter>1).sum()),'bootMeasurement':'alpha>127, separate left/right boot, bottom4rows; no source changes','measuredBootSoleBands':feet,'sourceFoot':foot,'sha256':hashlib.sha256((d/'master.png').read_bytes()).hexdigest(),'rawMatchesMaster':(d/'master.png').read_bytes()==(d/'raw.png').read_bytes(),'nativeAlphaUntouched':True}
 (d/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
 for name,col in COLORS.items():
  bg=Image.new('RGBA',im.size,col+(255,));bg.alpha_composite(im);bg.convert('RGB').save(d/f'qa-{name}.png')
 req=json.loads((d/'request.json').read_text());(d/'prompt.txt').write_text(req['prompt'])
print(json.dumps({p:json.loads((B/p/'alpha-qa.json').read_text()) for p in POSES if (B/p/'alpha-qa.json').exists()},indent=2))
