from pathlib import Path
import json, hashlib, struct, zlib
from PIL import Image, ImageDraw, ImageFont
import numpy as np
from scipy.ndimage import label
ROOT=Path(__file__).parent; OUT=ROOT/'qa';OUT.mkdir(exist_ok=True)
ORDER=['clustered-echoes','rising-echoes','dissolving-echoes','wisp-remnants'];PHASES=[p for p in ORDER if (ROOT/p/'master.png').exists()]
BGS={'black':'#000000','white':'#ffffff','bright-stone':'#eee5cf','green-ground':'#687047','dark-earth':'#322c27'}
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
exports={1:[],2:[]};metrics=[]
for phase in PHASES:
 p=ROOT/phase/'master.png';im=Image.open(p); assert im.mode=='RGBA' and im.size==(1254,1254)
 a=np.array(im.getchannel('A'));edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 row={'phase':phase,'sourceCanvas':list(im.size),'sha256':sha(p),'decodedPixelSha256':hashlib.sha256(im.tobytes()).hexdigest(),'rawMasterByteIdentical':sha(p)==sha(ROOT/phase/'raw.png'),'edgeAlphaMax':int(edge.max()),'edgeAlphaNonzeroSamples':int(np.count_nonzero(edge)),'edgeAlpha1ToleranceOnly':bool(edge.max()<=1),'bboxAlpha1':im.getchannel('A').getbbox(),'bboxAlpha128':Image.fromarray((a>=128).astype('uint8')).getbbox(),'alphaMass':float(a.sum()/255),'maxAlpha':int(a.max()),'exports':{}}
 yy,xx=np.where(a>=32)
 row['boundsAlpha32']=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
 row['boundsCenterAlpha32']=[float((xx.min()+xx.max()+1)/2),float((yy.min()+yy.max()+1)/2)]
 row['center100x100AlphaMax']=int(a[577:677,577:677].max())
 row['faintExteriorAlpha1to4Pixels']=int(np.count_nonzero((a>0)&(a<5)))
 for name,bg in BGS.items():
  comp=Image.new('RGBA',im.size,bg);comp.alpha_composite(im);comp.convert('RGB').save(OUT/f'{phase}-{name}-source-full.png')
 for density in (1,2):
  fit=im.resize((28*density,28*density),Image.Resampling.LANCZOS)
  cell=Image.new('RGBA',(32*density,32*density));cell.paste(fit,(2*density,2*density));cell.save(OUT/f'{phase}@{density}x.png');exports[density].append(cell)
  aa=np.array(cell.getchannel('A'));border=np.concatenate([aa[:2,:].flat,aa[-2:,:].flat,aa[:,:2].flat,aa[:,-2:].flat])
  row['exports'][str(density)]={'size':list(cell.size),'decodedPixelSha256':hashlib.sha256(cell.tobytes()).hexdigest(),'bboxAlpha1':cell.getchannel('A').getbbox(),'alphaMass':float(aa.sum()/255),'final2PixelBorderMax':int(border.max())}
 metrics.append(row)
 for name in ['source-evidence.json']:
  (ROOT/phase/name).write_text(json.dumps({'topicId':'C01-FX-08','phase':phase,'sourcePhaseIndex':ORDER.index(phase),'sourceBoard':'fx-atlas-02.png','sourceRow1Based':4,'sourceColumn1Based':ORDER.index(phase)+1,'audit':'../source-evidence.json','auditSha256':sha(ROOT/'source-evidence.json'),'referenceHashes':'reference-hashes.json'},indent=2)+'\n')
for density,frames in exports.items():
 strip=Image.new('RGBA',(len(PHASES)*32*density,32*density))
 for i,f in enumerate(frames):strip.paste(f,(i*32*density,0))
 strip.save(OUT/f'phase-strip@{density}x.png')
board=Image.new('RGB',(1230,1010),'#d6d2c9');d=ImageDraw.Draw(board);d.text((15,10),'FX08 offline QA: native 32px, native 64px, 32px enlarged 4x. Whole-source shared transform.',fill='black',font=font)
for b,(name,color) in enumerate(BGS.items()):
 y=45+b*190;d.text((10,y),name,fill='black',font=font)
 for i,phase in enumerate(PHASES):
  x=115+i*275;d.text((x,y),phase,fill='black',font=font)
  for density,dx in [(1,0),(2,42)]:
   c=Image.new('RGBA',(32*density,32*density),color);c.alpha_composite(exports[density][i]);board.paste(c.convert('RGB'),(x+dx,y+25))
   if density==1:board.paste(c.resize((128,128),Image.Resampling.NEAREST).convert('RGB'),(x+116,y+25))
board.save(OUT/'native-32-64-five-backgrounds.png')
# Rewrite only APNG timing fields to exact rational seconds, leaving every image-data byte intact.
def exact_timing(path):
 b=path.read_bytes();out=b[:8];pos=8;index=0;timings=[]
 while pos<len(b):
  n=struct.unpack('>I',b[pos:pos+4])[0];typ=b[pos+4:pos+8];data=b[pos+8:pos+8+n];pos+=12+n
  if typ==b'fcTL':
   num,den=(1,12) if index<3 else (17,100); data=data[:20]+struct.pack('>HH',num,den)+data[24:];timings.append([num,den]);index+=1
  out+=struct.pack('>I',len(data))+typ+data+struct.pack('>I',zlib.crc32(typ+data)&0xffffffff)
 path.write_bytes(out);return timings
apng=[]
if len(PHASES)==4:
 for density,frames in exports.items():
  for name,color in {'transparent':None,**BGS}.items():
   comps=[]
   for frame in frames:
    c=Image.new('RGBA',frame.size,color or (0,0,0,0));c.alpha_composite(frame);comps.append(c)
   p=OUT/f'12fps-420ms-heldfinal-{name}@{density}x.apng';comps[0].save(p,format='PNG',save_all=True,append_images=comps[1:],duration=[83,83,84,170],loop=1,disposal=0,blend=0)
   timings=exact_timing(p);apng.append({'path':p.name,'durationFractionsSeconds':timings,'durationMs':420,'numPlays':1,'sha256':sha(p)})
report={'topicId':'C01-FX-08','phaseOrder':PHASES,'sourceCanvas':[1254,1254],'commonTransform':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627],'logicalPivot':[16,16]},'registrationNote':'Memory echoes retain full source canvas registered to shared target pivot(627,627); common low wisp region near y1000..1080; source bounds measurements are diagnostic only, never used to fit or shift.','perPhaseFitCropShiftOrAlphaCleanup':False,'sourceEdgeGate':'alpha <= 1, actual composites and native borders separately reviewed','nativeFinal2pxBorderGate':0,'fps':12,'loop':False,'frameStartTimesMs':[0,1000/12,2000/12,250],'finalFrameHoldMs':170,'removalTimeMs':420,'runtimeConsumer':'BoardView.animateHit target cell center; candidate01FxPicture uses metadata [16,16] and HD strip; normal source-over','excludedRuntimeOverlays':['white circle','damage number'],'liveGameObserved':False,'frames':metrics,'apng':apng}
(OUT/'qa-metrics.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'phases':PHASES,'gates':[(x['phase'],x['edgeAlphaMax'],x['edgeAlphaNonzeroSamples'],x.get('mainFlameComponent'),[m['final2PixelBorderMax'] for m in x['exports'].values()]) for x in metrics],'apngCount':len(apng)},indent=2))
