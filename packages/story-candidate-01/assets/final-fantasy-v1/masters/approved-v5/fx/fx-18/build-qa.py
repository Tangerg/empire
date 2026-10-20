from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label,find_objects
from fractions import Fraction
import json,hashlib,struct,zlib
import numpy as np
R=Path(__file__).parent;Q=R/'qa';Q.mkdir(exist_ok=True)
P=['scattered-bands','aligning-ring','closed-restraints','settled-restraints']
B={'black':'#000000','white':'#ffffff','bright-stone':'#eee5cf','green-ground':'#687047','dark-earth':'#322c27','checker':None}
F=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def bg(size,name):
 im=Image.new('RGBA',size,B[name] or '#dddddd')
 if name=='checker':
  d=ImageDraw.Draw(im);s=max(4,size[0]//16)
  for y in range(0,size[1],s):
   for x in range(0,size[0],s):
    if (x//s+y//s)%2:d.rectangle((x,y,x+s-1,y+s-1),fill='#999999')
 return im
def comp(im,name):
 c=bg(im.size,name);c.alpha_composite(im);return c
exports={1:[],2:[]};rows=[]
for phase in P:
 p=R/phase/'master.png';im=Image.open(p);assert im.mode=='RGBA' and im.size==(1254,1254)
 a=np.array(im.getchannel('A'));ed=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 lab,n=label(a>=128);cc=[]
 for sl in find_objects(lab):
  y,x=sl
  if (x.stop-x.start)*(y.stop-y.start)>1500:cc.append([x.start,y.start,x.stop,y.stop])
 row={'phase':phase,'sha256':sha(p),'rawMasterByteIdentical':sha(p)==sha(R/phase/'raw.png'),'sourceCanvas':list(im.size),'mode':im.mode,'edgeAlphaMax':int(ed.max()),'edgeNonzeroSamples':int(np.count_nonzero(ed)),'alphaMax':int(a.max()),'alphaMass':float(a.sum()/255),'boundsAlpha128':Image.fromarray((a>=128).astype('uint8')).getbbox(),'visibleComponentsAlpha128':cc,'exports':{}}
 assert ed.max()<=1
 for name in B:comp(im,name).convert('RGB').save(Q/f'{phase}-{name}-source-full.png')
 for density in (1,2):
  cell=Image.new('RGBA',(32*density,32*density));cell.paste(im.resize((28*density,28*density),Image.Resampling.LANCZOS),(2*density,2*density));cell.save(Q/f'{phase}@{density}x.png');exports[density].append(cell)
  aa=np.array(cell.getchannel('A'));pad=2*density;border=np.concatenate([aa[:pad,:].flat,aa[-pad:,:].flat,aa[:,:pad].flat,aa[:,-pad:].flat]);assert border.max()==0
  row['exports'][str(density)]={'sha256':sha(Q/f'{phase}@{density}x.png'),'decodedSha256':hashlib.sha256(cell.tobytes()).hexdigest(),'paddingPixels':pad,'paddingAlphaMax':0,'alphaMass':float(aa.sum()/255)}
 rows.append(row)
for density,frames in exports.items():
 for name,order in [('phase-strip',[0,1,2,3]),('terminal-restart-comparison',[2,3,0,1])]:
  strip=Image.new('RGBA',(128*density,32*density))
  for i,index in enumerate(order):strip.paste(frames[index],(i*32*density,0))
  strip.save(Q/f'{name}@{density}x.png')
# Native32+64 and zoom; native composites remain unscaled.
board=Image.new('RGB',(1230,1200),'#d6d2c9');d=ImageDraw.Draw(board);d.text((12,8),'FX18 offline catalog QA: native32, native64, 4x zoom. Exact whole-source shared placement.',font=F,fill='black')
for b,name in enumerate(B):
 y=40+b*190;d.text((10,y),name,font=F,fill='black')
 for i,phase in enumerate(P):
  x=115+i*275;d.text((x,y),phase,font=F,fill='black')
  for den,dx in [(1,0),(2,42)]:board.paste(comp(exports[den][i],name).convert('RGB'),(x+dx,y+25))
  board.paste(comp(exports[1][i],name).resize((128,128),Image.Resampling.NEAREST).convert('RGB'),(x+116,y+25))
board.save(Q/'native-32-64-six-backgrounds.png')
# Full and3-4-1-2 seam strips at readable zoom, no altered pixels except nearest-neighbor presentation.
board=Image.new('RGB',(1150,610),'#d6d2c9');d=ImageDraw.Draw(board)
for row,(title,order) in enumerate([('Full 1-2-3-4',[0,1,2,3]),('Terminal / restart comparison, NOT runtime loop',[2,3,0,1])]):
 y=row*305;d.text((10,y+8),title,font=F,fill='black')
 for col,i in enumerate(order):
  x=10+col*285;d.text((x,y+30),P[i],font=F,fill='black');board.paste(comp(exports[2][i],'white').resize((256,256),Image.Resampling.NEAREST).convert('RGB'),(x,y+50))
board.save(Q/'full-and-terminal-comparison-labeled.png')
def timing(p,num=1,den=12):
 b=p.read_bytes();out=b[:8];pos=8
 while pos<len(b):
  n=struct.unpack('>I',b[pos:pos+4])[0];t=b[pos+4:pos+8];data=b[pos+8:pos+8+n];pos+=12+n
  if t==b'fcTL':data=data[:20]+struct.pack('>HH',num,den)+data[24:]
  out+=struct.pack('>I',len(data))+t+data+struct.pack('>I',zlib.crc32(t+data)&0xffffffff)
 p.write_bytes(out)
apng=[]
for density,frames in exports.items():
 for name in ['transparent',*B]:
  fs=frames if name=='transparent' else [comp(f,name) for f in frames]
  for mode,num,den in [('12fps-once',1,12),('slow-diagnostic-repeat',1,3)]:
   p=Q/f'{mode}-{name}@{density}x.apng';fs[0].save(p,format='PNG',save_all=True,append_images=fs[1:],duration=83 if mode=='12fps-once' else 333,loop=1 if mode=='12fps-once' else 0,disposal=0,blend=0);timing(p,num,den)
   data=p.read_bytes();pos=8;times=[]
   while pos<len(data):
    n=struct.unpack('>I',data[pos:pos+4])[0];t=data[pos+4:pos+8];b=data[pos+8:pos+8+n];pos+=12+n
    if t==b'acTL':count,plays=struct.unpack('>II',b)
    if t==b'fcTL':times.append(Fraction(*struct.unpack('>HH',b[20:24])))
   assert count==4 and plays==(1 if mode=='12fps-once' else 0) and times==[Fraction(num,den)]*4
   dec=Image.open(p);hashes=[]
   for i in range(4):
    dec.seek(i);f=dec.convert('RGBA');assert f.tobytes()==fs[i].tobytes();hashes.append(hashlib.sha256(f.tobytes()).hexdigest())
   apng.append({'file':p.name,'sha256':sha(p),'numPlays':plays,'frameCount':4,'frameDurationsSeconds':[str(t) for t in times],'sequenceSeconds':str(sum(times)),'decodedFramesMatchExports':True,'decodedFrameHashes':hashes})
report={'topicId':'C01-FX-18','phaseOrder':P,'sourceCanvas':[1254,1254],'commonTransform':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627],'logicalPivot':[16,16]},'fps':12,'loop':False,'frameStartTimesMs':[0,1000/12,2000/12,250],'sequenceMs':1000/3,'equalFrameDurationSeconds':'1/12','encodedFinalFrameHold':False,'afterOnePlay':'APNG retains its final image; scene lifetime is not established without a consumer','removalRule':None,'authoredSceneConsumer':None,'liveGameObserved':False,'sourceProcessing':'byte-identical raw/master; no alpha cleanup, cropping, per-frame fitting, shifting, fading or synthesized motion','frames':rows,'apng':apng}
(Q/'qa-metrics.json').write_text(json.dumps(report,indent=2)+'\n');(Q/'apng-timing-verification.json').write_text(json.dumps({'verified':True,'method':'acTL/fcTL exact rational timing plus every decoded RGBA frame compared with expected source export','files':apng},indent=2)+'\n')
print(json.dumps({'phases':[(r['phase'],r['edgeAlphaMax'],r['alphaMass'])for r in rows],'APNGs':len(apng),'verifiedFrames':len(apng)*4},indent=2))
