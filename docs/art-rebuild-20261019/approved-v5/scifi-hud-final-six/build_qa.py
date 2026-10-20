from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib,shutil
import numpy as np
ROOT=Path(__file__).resolve().parents[4];DOC=Path(__file__).parent
requests=json.load(open(DOC/'requests.json'));native=Image.new('RGB',(96,32*6));review=Image.new('RGB',(900,340*6),'#dddddd')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for idx,item in enumerate(requests):
 p=Path(item['out']);im=Image.open(p/'master.png');a=np.array(im.getchannel('A'));edge=np.concatenate((a[0],a[-1],a[:,0],a[:,-1]));assert edge.max()<=1;assert a.min()==0 and a.max()==255
 small=im.copy();small.thumbnail((28,28),Image.Resampling.LANCZOS);rt=Image.new('RGBA',(32,32));rt.paste(small,((32-small.width)//2,(32-small.height)//2));rt.save(p/'runtime.png');diag=p/'diagnostic-native32';diag.mkdir(exist_ok=True);strip=Image.new('RGB',(96,32))
 for j,(name,bg) in enumerate([('white','#ffffff'),('black','#000000'),('olive','#6b7042')]):
  c=Image.new('RGBA',rt.size,bg);c.alpha_composite(rt);c=c.convert('RGB');c.save(diag/f'native32-on-{name}.png');strip.paste(c,(32*j,0))
  full=Image.new('RGBA',im.size,bg);full.alpha_composite(im);full.thumbnail((300,300),Image.Resampling.LANCZOS);review.paste(full.convert('RGB'),(j*300,idx*340+40))
 strip.save(diag/'native32-white-black-olive.png');strip.resize((768,256),Image.Resampling.NEAREST).save(diag/'native32-white-black-olive-8x-nearest.png');native.paste(strip,(0,idx*32));ImageDraw.Draw(review).text((10,idx*340+10),item['topicId'],fill='black')
 qa={'source':{'mode':im.mode,'dimensions':list(im.size),'alphaMin':int(a.min()),'alphaMax':int(a.max()),'perimeterAlphaMax':int(edge.max()),'alpha0PixelCount':int((a==0).sum())},'runtime':{'size':[32,32],'anchor':[16,16],'hd':False,'resize':'Whole authored canvas Lanczos-contained in 28x28 centered on 32x32; no crop'},'integrity':{'rawSha256':sha(p/'raw.png'),'masterSha256':sha(p/'master.png'),'rawEqualsMaster':sha(p/'raw.png')==sha(p/'master.png'),'noAlphaCleanup':True,'noRecolor':True},'visualComposites':{'required':['white','black','olive'],'status':'Pending visual review'}};(p/'alpha-qa.json').write_text(json.dumps(qa,indent=2))
review.save(DOC/'source-review-white-black-olive.jpg');native.save(DOC/'native32-white-black-olive.png');native.resize((768,1536),Image.Resampling.NEAREST).save(DOC/'native32-white-black-olive-8x.png')
