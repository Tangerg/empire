from PIL import Image, ImageOps, ImageDraw
from pathlib import Path
import json,hashlib
base=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-scenes')
for slug in ['scene-old-flag-homecoming','scene-refugee-bridgework']:
 b=base/slug; m=Image.open(b/'master.png'); crop=(4,2,1668,938); export=m.crop(crop).convert('RGBA'); export.save(b/'qa-source-native-1664x936.png'); rows=[]
 for w,h in [(512,288),(256,144)]:
  export.resize((w,h),Image.Resampling.LANCZOS).save(b/f'qa-native-{w}x{h}.png')
 for name,(w,h) in [('story-desktop',(960,534)),('story-mobile',(390,778)),('staging-inset',(280,245)),('staging-mobile',(366,220))]:
  s=max(w/export.width,h/export.height); sw=w/s; sh=h/s; box=[(export.width-sw)/2,(export.height-sh)/2,(export.width+sw)/2,(export.height+sh)/2]
  out=ImageOps.fit(export,(w,h),method=Image.Resampling.LANCZOS); out.save(b/f'qa-cover-{name}.png')
  rows.append({'view':name,'targetSize':[w,h],'input':'qa-source-native-1664x936.png','nativeExportCrop':list(crop),'sourceCropInPhysicalExport':box,'sourceAreaRetainedFraction':sw*sh/(export.width*export.height),'horizontalLossFraction':1-sw/export.width,'verticalLossFraction':1-sh/export.height,'objectPosition':[0.5,0.5],'actualConsumerReferenceProven':False,'cssSource':'packages/game-ui/src/styles/campaign.css:179-213,475-500,746-756','provenance':'Offline CSS-cover geometry simulation. Not browser/live evidence. Display filters, text and overlays are not simulated. Exact topic has no proven consumer, so cover simulations are hypothetical.'})
 (b/'viewport-qa.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
 card=Image.new('RGB',(1000,1100),'#e2ddd2'); d=ImageDraw.Draw(card)
 def put(file,x,y,label):
  d.text((x,y),label,fill='black'); i=Image.open(b/file); card.paste(i.convert('RGB'),(x,y+20))
 put('qa-native-512x288.png',12,10,'Native 512 x 288'); put('qa-native-256x144.png',540,10,'Native 256 x 144')
 put('qa-cover-story-mobile.png',12,310,'Modeled portrait story cover: 390 x 778')
 put('qa-cover-staging-inset.png',420,320,'Modeled staging inset: 280 x 245')
 put('qa-cover-staging-mobile.png',420,610,'Modeled mobile staging: 366 x 220')
 card.save(b/'qa-contact.png')
 print(slug,m.size,export.size,'alpha',export.getchannel('A').getextrema())
