from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from fractions import Fraction
import json,struct,zlib,hashlib
r=Path(__file__).parent;q=r/'qa';P=json.loads((q/'qa-metrics.json').read_text())['phaseOrder'];B={'black':'#000000','white':'#ffffff','bright-stone':'#eee5cf','green-ground':'#687047','dark-earth':'#322c27','checker':None};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();F=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
def bg(size,name):
 im=Image.new('RGBA',size,B[name] or '#dddddd')
 if name=='checker':
  d=ImageDraw.Draw(im);s=max(4,size[0]//16)
  for y in range(0,size[1],s):
   for x in range(0,size[0],s):
    if (x//s+y//s)%2:d.rectangle((x,y,x+s-1,y+s-1),fill='#999999')
 return im
def comp(im,name):
 b=bg(im.size,name);b.alpha_composite(im);return b
records=[];durations=[Fraction(1,12)]*3+[Fraction(1,4),Fraction(1,12)]
for den in [1,2]:
 fs=[Image.open(q/f'{p}@{den}x.png').convert('RGBA') for p in P];fs.append(Image.new('RGBA',fs[0].size))
 for name in ['transparent',*B]:
  expected=fs if name=='transparent' else [comp(f,name) for f in fs];out=q/f'consumer-500ms-removal-{name}@{den}x.apng';expected[0].save(out,format='PNG',save_all=True,append_images=expected[1:],duration=[83,83,83,250,83],loop=1,disposal=0,blend=0)
  b=out.read_bytes();new=b[:8];pos=8;frame=0
  while pos<len(b):
   n=struct.unpack('>I',b[pos:pos+4])[0];kind=b[pos+4:pos+8];data=b[pos+8:pos+8+n];pos+=12+n
   if kind==b'fcTL':
    t=durations[frame];frame+=1;data=data[:20]+struct.pack('>HH',t.numerator,t.denominator)+data[24:]
   new+=struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
  out.write_bytes(new);pos=8;actual=[]
  while pos<len(new):
   n=struct.unpack('>I',new[pos:pos+4])[0];kind=new[pos+4:pos+8];data=new[pos+8:pos+8+n];pos+=12+n
   if kind==b'acTL':count,plays=struct.unpack('>II',data)
   if kind==b'fcTL':actual.append(Fraction(*struct.unpack('>HH',data[20:24])))
  assert count==5 and plays==1 and actual==durations
  im=Image.open(out);hashes=[]
  for i in range(count):
   im.seek(i);a=im.convert('RGBA');assert a.tobytes()==expected[i].tobytes();hashes.append(hashlib.sha256(a.tobytes()).hexdigest())
  records.append({'file':out.name,'sha256':sha(out),'numPlays':plays,'frameCount':count,'authoredSourceCount':4,'lastSample':'Empty effect at 500 ms, diagnostic removal only; not a fifth authored phase','frameStartsSeconds':['0','1/12','1/6','1/4','1/2'],'frameDurationsSeconds':[str(t) for t in actual],'effectLifetimeSeconds':'1/2','includingRemovalObservationSeconds':str(sum(actual)),'decodedFramesMatchExports':True,'decodedFrameHashes':hashes})
# Static samples are the unaltered 64px exports shown at nearest-neighbor 2x inspection zoom.
samples=[('0 ms',0),('83.34 ms',1),('166.67 ms',2),('250 ms',3),('333.34 ms',3),('420 ms',3),('499 ms',3),('500 ms',4)]
sheet=Image.new('RGB',(1200,440),'#d6d2c9');d=ImageDraw.Draw(sheet);d.text((12,10),'FX17 consumer diagnostic: final phase held from 250 to 500 ms, then removed. 2x inspection zoom. No amount overlay.',font=F,fill='black')
for row,name in enumerate(['black','white']):
 for col,(time,index) in enumerate(samples):
  x=col*150+10;y=40+row*195;d.text((x,y),time,font=F,fill='black');f=Image.open(q/f'{P[index]}@2x.png') if index<4 else Image.new('RGBA',(64,64));c=comp(f,name);sheet.paste(c.resize((128,128),Image.Resampling.NEAREST).convert('RGB'),(x,y+25))
sheet.save(q/'consumer-500ms-hold-removal-samples.png')
proof={'topicId':'C01-FX-17','type':'Source/export consumer-lifetime illustration, not a runtime test or live playback observation','normalMotionConsumer':'BoardView.animateHeal','effectLifetimeMs':500,'hit420msApplied':False,'authoredSourceCount':4,'sourceClipFps':12,'sourceClipLoop':False,'sourceClipNominalSeconds':'1/3','consumerDisplayedDurationsSeconds':['1/12','1/12','1/12','1/4'],'terminalSampleStartMs':250,'terminalSampleTotalVisibleMs':250,'removalAtMs':500,'removalDiagnosticAddsAuthoredPhase':False,'blankObservationSeconds':'1/12','numberOverlayOmitted':True,'expectedTimestampSamples':[{'atMs':t,'authoredFrameIndex':min(int(t*12/1000),3) if t<500 else None,'effectPresent':t<500}for t in [0,83.34,166.67,250,333.34,419,420,499,500]],'files':records,'verifiedFrames':sum(x['frameCount'] for x in records),'parentReportedRuntimeRegression':'Parent reported on 2026-10-20T06:03:50Z that actual BoardView heal 500 ms removal, final frame 250–499 ms hold and separate hit 420 ms regressions passed. New source masters were not yet bound at that time.','newSourcesIntegrated':False,'browserObserved':False};(q/'consumer-lifecycle-verification.json').write_text(json.dumps(proof,indent=2,ensure_ascii=False)+'\n');print(json.dumps({'files':len(records),'decodedFrames':proof['verifiedFrames'],'effectLifetimeMs':500,'terminalVisibleMs':250},indent=2))
