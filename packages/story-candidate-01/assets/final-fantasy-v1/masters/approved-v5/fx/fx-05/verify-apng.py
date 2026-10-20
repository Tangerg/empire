from pathlib import Path
from PIL import Image
from fractions import Fraction
import json,struct,hashlib
r=Path(__file__).parent;q=r/'qa'; phases=['ignite','rise','flare','embers'];bgs={'transparent':None,'black':'#000000','white':'#ffffff','bright-stone':'#eee5cf','green-ground':'#687047','dark-earth':'#322c27'}
rows=[]
for density in (1,2):
 for bg,color in bgs.items():
  p=q/f'12fps-420ms-heldfinal-{bg}@{density}x.apng';data=p.read_bytes();pos=8;times=[];plays=None
  while pos<len(data):
   n=struct.unpack('>I',data[pos:pos+4])[0];typ=data[pos+4:pos+8];b=data[pos+8:pos+8+n];pos+=12+n
   if typ==b'acTL': count,plays=struct.unpack('>II',b)
   if typ==b'fcTL': times.append(Fraction(*struct.unpack('>HH',b[20:24])))
  assert times==[Fraction(1,12)]*3+[Fraction(17,100)]
  assert sum(times)==Fraction(21,50) and plays==1 and count==4
  im=Image.open(p);assert im.n_frames==4; hashes=[]
  for i,phase in enumerate(phases):
   im.seek(i);f=im.convert('RGBA');expected=Image.new('RGBA',f.size,color or (0,0,0,0));expected.alpha_composite(Image.open(q/f'{phase}@{density}x.png'))
   assert f.tobytes()==expected.tobytes();hashes.append(hashlib.sha256(f.tobytes()).hexdigest())
  assert len(set(hashes))==4
  rows.append({'path':p.name,'density':density,'background':bg,'frameCount':count,'numPlays':plays,'durationFractionsSeconds':[str(t) for t in times],'durationMs':420,'lastPhaseStartMs':250,'lastPhaseHoldMs':170,'decodedFramesEqualWholeCanvasQaExports':True,'frameHashes':hashes})
(q/'apng-timing-verification.json').write_text(json.dumps({'verified':True,'files':rows,'verification':'APNG chunks and all decoded frames; no real-time browser observation asserted'},indent=2)+'\n');print('PASS: 12 APNGs; 48 frames; exact 420ms duration with 170ms last hold; one play; exact RGBA decode matches.')
