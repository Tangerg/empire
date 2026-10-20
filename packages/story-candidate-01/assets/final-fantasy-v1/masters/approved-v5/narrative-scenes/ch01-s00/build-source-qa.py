from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageChops
import json,hashlib,shutil
O=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-scenes/ch01-s00')
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
N=Path('/workspace/scratch/84c9fe948755/generated_images/exec-0bedb77a-9c93-4823-816b-609db7623c36.png')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(p,v): p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
for name in ['raw.png','native.png','master.png']:shutil.copy2(N,O/name)
im=Image.open(N)
im.resize((627,627),Image.Resampling.LANCZOS).save(O/'qa-full-plate-627.png')
im.convert('RGBA').resize((128,128),Image.Resampling.LANCZOS).save(O/'qa-canonical-128.png')
im.resize((256,256),Image.Resampling.LANCZOS).save(O/'qa-square-256.png')
views=[]
for name,w,h in [('story-desktop',960,534),('story-mobile',390,778),('staging-inset',280,245),('staging-mobile',366,220)]:
 scale=max(w/im.width,h/im.height);cw=w/scale;ch=h/scale;x=(im.width-cw)/2;y=(im.height-ch)/2
 box=[x,y,x+cw,y+ch]
 ImageOps.fit(im,(w,h),method=Image.Resampling.LANCZOS,centering=(.5,.5)).save(O/f'qa-cover-{name}.png')
 views.append({'view':name,'size':[w,h],'objectPosition':[.5,.5],'sourceCrop':box,'status':'diagnostic-only; no current S00 consumer','note':'Centered CSS cover stress test; selected square master unchanged. Not an acceptance claim for non-square UI.'})
dump(O/'viewport-qa.json',views)
# QA layout only, with exact-size canonical sample and derived cover panels.
canvas=Image.new('RGB',(1290,1130),(239,240,242));draw=ImageDraw.Draw(canvas)
def panel(name,p,x,y,maxsz=None):
 v=Image.open(O/p).convert('RGB')
 if maxsz:v.thumbnail(maxsz,Image.Resampling.LANCZOS)
 draw.text((x,y),name,fill=(20,25,30));canvas.paste(v,(x,y+22))
panel('Full square plate: 627px QA','qa-full-plate-627.png',16,12)
panel('Canonical exact 128x128 (1x)','qa-canonical-128.png',670,12)
panel('Square 256x256','qa-square-256.png',940,12)
panel('Portrait cover 390x778: diagnostic only','qa-cover-story-mobile.png',670,185,(245,488))
panel('Inset cover 280x245: diagnostic only','qa-cover-staging-inset.png',960,315)
panel('Desktop cover: clips head tops/low hand','qa-cover-story-desktop.png',16,680,(620,345))
panel('Mobile landscape cover: diagnostic only','qa-cover-staging-mobile.png',670,720,(550,331))
canvas.save(O/'qa-contact-sheet.png')
legacy=R/'packages/story-candidate-01/assets/final-fantasy-v1/runtime/narrative-static/ch01-s00.png'
shutil.copy2(legacy,O/'legacy-runtime-128.png')
refs=json.loads((O/'reference-hashes.json').read_text())
q={'topicId':'C01-CH01-S00','selectedAttempt':2,'tool':'built-in image_gen','originalGeneratedPath':str(N),'sha256':sha(N),'nativeSize':list(im.size),'mode':im.mode,'effectiveAlphaExtrema':list(im.convert('RGBA').getchannel('A').getextrema()),'fullyOpaque':im.mode=='RGB' or im.getchannel('A').getextrema()==(255,255),'nativeRawMasterByteIdentical':len({sha(N),sha(O/'raw.png'),sha(O/'native.png'),sha(O/'master.png')})==1,'masterProcessing':'none; byte-for-byte copies of selected native imagegen output. Horizontal reversal/anatomy repair performed by imagegen, never programmatic pixel editing.','sourceCropBox':None,'logicalSize':[128,128],'physicalSize':[1254,1254],'pixelDensity':1254/128,'attempt01Retained':True,'promptSha256':sha(O/'prompt.txt'),'requestSha256':sha(O/'request.json'),'referenceHashesMatch':all(sha(Path(x['path']))==x['sha256'] for x in refs),'canonicalQAExactlyMatchesWholeNativeLanczos':Image.open(O/'qa-canonical-128.png').tobytes()==im.convert('RGBA').resize((128,128),Image.Resampling.LANCZOS).tobytes(),'provenanceComplete':True,'currentConsumers':[],'liveGameChecked':False,'repositoryOrGitChanges':False,'status':'selected native source ready for coordinator visual review; formal integration pending'}
dump(O/'source-qa.json',q);dump(O/'generation-provenance.json',q)
print(json.dumps(q,indent=2))
