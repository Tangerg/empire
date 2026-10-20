from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
import json,hashlib,datetime
D=Path(__file__).parent
im=Image.open(D/'master.png');w,h=im.size
n=min(w//16,h//9);cw,ch=n*16,n*9;x=(w-cw)//2;y=(h-ch)//2;box=[x,y,x+cw,y+ch]
hd=im.crop(box).resize((512,288),Image.Resampling.LANCZOS).convert('RGBA'); hd.save(D/'qa-exact-crop-512x288.png')
lo=im.crop(box).resize((256,144),Image.Resampling.LANCZOS).convert('RGBA');lo.save(D/'qa-logical-256x144.png')
views=[('story-desktop',(960,534),(.5,.5),True),('story-mobile',(390,778),(.5,.5),True),('staging-inset',(280,245),(.5,.5),True),('staging-mobile',(366,220),(.5,.5),True),('title-mobile-stress',(390,844),(.5,.46),False)]
rows=[];tiles=[]
for name,(tw,th),(px,py),actual in views:
 scale=max(tw/512,th/288);sw=tw/scale;sh=th/scale;x0=(512-sw)*px;y0=(288-sh)*py;cb=[x0,y0,x0+sw,y0+sh]
 out=hd.crop(cb).resize((tw,th),Image.Resampling.LANCZOS);out.save(D/f'qa-cover-{name}.png')
 rows.append({'view':name,'targetSize':[tw,th],'input':'qa-exact-crop-512x288.png','nativeExportCrop':box,'sourceCropIn512x288':cb,'objectPosition':[px,py],'actualConsumerGeometry':actual,'cssSource':'packages/game-ui/src/styles/campaign.css:180-213,475-490,746-752' if actual else 'packages/game-ui/src/styles/app.css:774-785; stress only, topic is NOT title consumer','provenance':'Offline CSS-cover geometry inspection from exact proposed 512x288 export. Not browser/live evidence. Display filters, overlays and text are not simulated.'})
 tile=Image.new('RGB',(440,440),'#e0ded8');p=out.copy();p.thumbnail((420,400));tile.paste(p,((440-p.width)//2,30));ImageDraw.Draw(tile).text((10,10),name+(' (stress only)' if not actual else ''),fill='#151515');tiles.append(tile)
sheet=Image.new('RGB',(440*3,440*2),'#e0ded8')
for i,tile in enumerate(tiles):sheet.paste(tile,((i%3)*440,(i//3)*440))
sheet.save(D/'qa-covers-contact-sheet.png')
(D/'viewport-qa.json').write_text(json.dumps(rows,indent=2)+'\n')
qa={'topicId':'C01-CH01-S02','rawMode':im.mode,'nativeSize':[w,h],'effectiveAlphaExtrema':list(im.convert('RGBA').getchannel('A').getextrema()),'rawEqualsNativeEqualsMaster':len({hashlib.sha256((D/f).read_bytes()).hexdigest() for f in ['raw.png','native.png','master.png']})==1,'sha256':hashlib.sha256((D/'master.png').read_bytes()).hexdigest(),'formalIntegerCrop':box,'croppedExportSize':[cw,ch],'cropLossPx':{'left':x,'top':y,'right':w-x-cw,'bottom':h-y-ch},'cropReason':'Largest centered integer 16:9 crop, tiny explicit edge removal only for deterministic exporter. Full authored native master preserved byte-for-byte.','opaqueSceneIntentional':True,'countsAsTransparentSprite':False,'programmaticArtworkChanges':False,'qaOnlyDerivatives':['qa-exact-crop-512x288.png','qa-logical-256x144.png','qa-cover-*.png','qa-covers-contact-sheet.png'],'generationAttempts':2,'tool':'built-in image_gen','generatedOriginal':'/workspace/scratch/84c9fe948755/generated_images/exec-7d29c273-c7e0-4ba6-9b9e-fbf0a10d98b1.png','browserOrLiveVerification':False}
(D/'source-qa.json').write_text(json.dumps(qa,indent=2)+'\n');print(json.dumps(qa,indent=2))
