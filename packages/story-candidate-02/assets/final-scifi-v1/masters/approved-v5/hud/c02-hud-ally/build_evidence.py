from pathlib import Path
from PIL import Image,ImageDraw
import json, hashlib, sys
import numpy as np
ROOT=Path('/workspace/shared/empire-art-rebuild/approved-v5/scifi-hud')
BACKGROUNDS={'white':(255,255,255,255),'black':(0,0,0,255),'olive':(107,112,66,255)}
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def composite(im,bg):
 c=Image.new('RGBA',im.size,bg);c.alpha_composite(im);return c.convert('RGB')
def run(topic):
 p=ROOT/f'c02-hud-{topic}';im=Image.open(p/'master.png');a=np.array(im)[:,:,3]
 assert im.mode=='RGBA'
 perimeter=np.concatenate([a[0,:],a[-1,:],a[:,0],a[:,-1]])
 edge=int(perimeter.max());assert edge<=1,edge
 contained=im.copy();contained.thumbnail((28,28),Image.Resampling.LANCZOS)
 runtime=Image.new('RGBA',(32,32),(0,0,0,0));offset=((32-contained.width)//2,(32-contained.height)//2);runtime.paste(contained,offset);runtime.save(p/'runtime.png')
 ar=np.array(runtime)[:,:,3];border=np.concatenate([ar[:2,:].ravel(),ar[-2:,:].ravel(),ar[:,:2].ravel(),ar[:,-2:].ravel()]);assert not border.any()
 qadir=p/'diagnostic-native32';qadir.mkdir(exist_ok=True)
 comp_strip=Image.new('RGB',(96,32));before_after=Image.new('RGB',(96,64))
 old=Image.open(p/'source-runtime.png').convert('RGBA')
 for idx,(name,color) in enumerate(BACKGROUNDS.items()):
  sourcecomp=composite(im,color);sourcecomp.save(p/f'qa-source-on-{name}.png');sourcecomp.resize((512,512),Image.Resampling.LANCZOS).save(p/f'qa-source512-on-{name}.png')
  c=composite(runtime,color);c.save(qadir/f'native32-on-{name}.png');comp_strip.paste(c,(idx*32,0));before_after.paste(composite(old,color),(idx*32,0));before_after.paste(c,(idx*32,32))
 comp_strip.save(qadir/'native32-white-black-olive.png');comp_strip.resize((768,256),Image.Resampling.NEAREST).save(qadir/'native32-white-black-olive-8x-nearest.png')
 before_after.save(qadir/'before-after-native32.png');before_after.resize((768,512),Image.Resampling.NEAREST).save(qadir/'before-after-native32-8x-nearest.png')
 qa={'source':{'mode':im.mode,'dimensions':im.size,'alphaMin':int(a.min()),'alphaMax':int(a.max()),'perimeterAlphaMax':edge,'perimeterNonzeroCount':int((perimeter>0).sum()),'alpha0PixelCount':int((a==0).sum()),'alpha255PixelCount':int((a==255).sum()),'alpha1PixelCount':int((a==1).sum()),'alpha2to254PixelCount':int(((a>=2)&(a<=254)).sum())},'runtime':{'size':[32,32],'anchor':[16,16],'hd':False,'resize':'Whole authored canvas Lanczos-contained into 28x28 without crop; centered on 32x32 transparent canvas','containedSize':contained.size,'offset':offset,'twoPixelMarginAlphaMax':int(border.max())},'integrity':{'rawSha256':sha(p/'raw.png'),'masterSha256':sha(p/'master.png'),'rawEqualsMaster':(p/'raw.png').read_bytes()==(p/'master.png').read_bytes(),'noAlphaCleanup':True,'noRecolor':True,'allowedProcessing':['unchanged raw copy','whole-canvas resize','transparent padding','diagnostic background compositing only']},'visualComposites':{'required':['white','black','olive'],'status':'Pending visual inspection'}}
 (p/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
 print(topic,qa)
for topic in sys.argv[1:]:run(topic)
