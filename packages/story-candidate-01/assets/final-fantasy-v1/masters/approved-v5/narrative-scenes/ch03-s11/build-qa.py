from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib
D=Path(__file__).parent
im=Image.open(D/'master.png');w,h=im.size
square=D.name=='ch03-s11';logical=(128,128) if square else (256,144)
if square:
 assert w==h
 box=[0,0,w,h];formal_box=None;cw,ch=w,h
else:
 n=min(w//16,h//9);cw,ch=n*16,n*9;x=(w-cw)//2;y=(h-ch)//2;box=[x,y,x+cw,y+ch];formal_box=box
physical=im.crop(box).convert('RGBA');native_name=f'qa-source-native-{cw}x{ch}.png';physical.save(D/native_name)
physical.resize((512,512) if square else (512,288),Image.Resampling.LANCZOS).save(D/('qa-full-scene-512x512.png' if square else 'qa-exact-crop-512x288.png'))
physical.resize(logical,Image.Resampling.LANCZOS).save(D/f'qa-logical-{logical[0]}x{logical[1]}.png')
views=[('story-desktop',(960,534)),('story-mobile',(390,778)),('staging-inset',(280,245)),('staging-mobile',(366,220))]
rows=[];tiles=[]
for name,(tw,th) in views:
 scale=max(tw/cw,th/ch);sw=min(cw,tw/scale);sh=min(ch,th/scale);x0=(cw-sw)/2;y0=(ch-sh)/2;cb=[x0,y0,x0+sw,y0+sh]
 out=physical.resize((tw,th),Image.Resampling.LANCZOS,box=cb);out.save(D/f'qa-cover-{name}.png')
 rows.append({'view':name,'targetSize':[tw,th],'input':native_name,'nativeExportCrop':formal_box,'sourceCropInPhysicalExport':cb,'objectPosition':[0.5,0.5],'actualConsumerGeometry':False,'hypotheticalCompatibilityGeometry':True,'cssSource':'packages/game-ui/src/styles/campaign.css:180-213,475-499,746-755','provenance':'Offline CSS-cover geometry approximation from proposed source-native export. No direct current topic consumer found. Not browser/live evidence. Display filters, overlays and text are not simulated.'})
 tile=Image.new('RGB',(500,470),'#e0ded8');preview=out.copy();preview.thumbnail((480,425));tile.paste(preview,((500-preview.width)//2,35));ImageDraw.Draw(tile).text((10,10),f'{name} {tw}x{th}',fill='#151515');tiles.append(tile)
sheet=Image.new('RGB',(1000,940),'#e0ded8')
for i,tile in enumerate(tiles):sheet.paste(tile,((i%2)*500,(i//2)*470))
sheet.save(D/'qa-covers-contact-sheet.png')
(D/'viewport-qa.json').write_text(json.dumps(rows,indent=2)+'\n')
qa={'topicId':'C01-'+D.name.upper(),'rawMode':im.mode,'nativeSize':[w,h],'effectiveAlphaExtrema':list(im.convert('RGBA').getchannel('A').getextrema()),'rawEqualsNativeEqualsMaster':len({hashlib.sha256((D/f).read_bytes()).hexdigest() for f in ['raw.png','native.png','master.png']})==1,'sha256':hashlib.sha256((D/'master.png').read_bytes()).hexdigest(),'formalIntegerCrop':formal_box,'physicalExportSize':[cw,ch],'canonicalSize':list(logical),'exactDensity':cw/logical[0],'cropLossPx':{'left':box[0],'top':box[1],'right':w-box[2],'bottom':h-box[3]},'cropReason':'Full square native authored canvas retained, no crop and no upscale.' if square else 'Largest centered integer16:9 crop, removing only4/2/4/3 border pixels. Full authored native master preserved byte-for-byte.','opaqueSceneIntentional':True,'countsAsTransparentSprite':False,'programmaticArtworkChanges':False,'qaOnlyDerivatives':[native_name,'qa-logical-*','qa-full-scene-*','qa-exact-crop-*','qa-cover-*.png','qa-covers-contact-sheet.png'],'generationAttempts':1 if square else 2,'tool':'built-in image_gen','browserOrLiveVerification':False}
assert qa['rawEqualsNativeEqualsMaster'] and qa['effectiveAlphaExtrema']==[255,255]
assert cw/logical[0]==ch/logical[1] and cw<=w and ch<=h
(D/'source-qa.json').write_text(json.dumps(qa,indent=2)+'\n');print(json.dumps(qa,indent=2))
