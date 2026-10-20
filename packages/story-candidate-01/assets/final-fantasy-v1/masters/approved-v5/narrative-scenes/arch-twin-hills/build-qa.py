from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib
D=Path(__file__).parent
im=Image.open(D/'master.png');w,h=im.size
cw,ch=1664,936;x,y=4,2;box=[4,2,1668,938];assert w>=1668 and h>=938
physical=im.crop(box).convert('RGBA');physical.save(D/'qa-source-native-1664x936.png')
physical.resize((512,288),Image.Resampling.LANCZOS).save(D/'qa-exact-crop-512x288.png')
physical.resize((256,144),Image.Resampling.LANCZOS).save(D/'qa-logical-256x144.png')
views=[('story-desktop',(960,534),(.5,.5)),('story-mobile',(390,778),(.5,.5)),('staging-inset',(280,245),(.5,.5)),('staging-mobile',(366,220),(.5,.5))]
rows=[];tiles=[]
for name,(tw,th),(px,py) in views:
 scale=max(tw/cw,th/ch);sw=tw/scale;sh=th/scale;x0=(cw-sw)*px;y0=(ch-sh)*py;cb=[x0,y0,x0+sw,y0+sh]
 out=physical.crop(cb).resize((tw,th),Image.Resampling.LANCZOS);out.save(D/f'qa-cover-{name}.png')
 rows.append({'view':name,'targetSize':[tw,th],'input':'qa-source-native-1664x936.png','nativeExportCrop':box,'sourceCropIn1664x936':cb,'objectPosition':[px,py],'actualConsumerGeometry':False,'cssSource':'packages/game-ui/src/styles/campaign.css:180-213,475-490,746-752','provenance':'Offline hypothetical CSS-cover compatibility check from proposed source-native 1664x936 export. This catalog topic currently has no shipped consumer. Not browser/live evidence. Display filters, overlays and text are not simulated.'})
 tile=Image.new('RGB',(440,440),'#e0ded8');p=out.copy();p.thumbnail((420,400));tile.paste(p,((440-p.width)//2,30));ImageDraw.Draw(tile).text((10,10),name,fill='#151515');tiles.append(tile)
sheet=Image.new('RGB',(440*2,440*2),'#e0ded8')
for i,tile in enumerate(tiles):sheet.paste(tile,((i%2)*440,(i//2)*440))
sheet.save(D/'qa-covers-contact-sheet.png')
(D/'viewport-qa.json').write_text(json.dumps(rows,indent=2)+'\n')
a=json.loads((D/'audited-source.json').read_text())
qa={'topicId':a['topicId'],'rawMode':im.mode,'nativeSize':[w,h],'effectiveAlphaExtrema':list(im.convert('RGBA').getchannel('A').getextrema()),'rawEqualsNativeEqualsMaster':len({hashlib.sha256((D/f).read_bytes()).hexdigest() for f in ['raw.png','native.png','master.png']})==1,'sha256':hashlib.sha256((D/'master.png').read_bytes()).hexdigest(),'formalIntegerCrop':box,'croppedExportSize':[cw,ch],'cropLossPx':{'left':x,'top':y,'right':w-x-cw,'bottom':h-y-ch},'cropReason':'Explicit approved-family 16:9 crop [4,2,1668,938]; selected native width is 1671, so this removes 4 px left and 3 px right without resizing. Full authored native master preserved byte-for-byte.','opaqueSceneIntentional':True,'countsAsTransparentSprite':False,'programmaticArtworkChanges':False,'qaOnlyDerivatives':['qa-source-native-1664x936.png','qa-exact-crop-512x288.png','qa-logical-256x144.png','qa-cover-*.png','qa-covers-contact-sheet.png'],'generationAttempts':json.loads((D/'generation-result.json').read_text()).get('generationAttempts',1) if (D/'generation-result.json').exists() else 1,'tool':'built-in image_gen','browserOrLiveVerification':False}
(D/'source-qa.json').write_text(json.dumps(qa,indent=2)+'\n');print(json.dumps(qa,indent=2))
