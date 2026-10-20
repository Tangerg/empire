from pathlib import Path
from PIL import Image, ImageOps, ImageDraw
import json,hashlib,numpy as np,shutil
BASE=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-characters/char-severin')
ROOT=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
PACK=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1'
BG=PACK/'masters/approved-v5/narrative/card-background/master.png'
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
im=Image.open(BASE/'attempt-01/raw.png');a=np.array(im.getchannel('A'));edges=np.concatenate((a[0,:],a[-1,:],a[1:-1,0],a[1:-1,-1]));ys,xs=np.where(a>1)
assert im.mode=='RGBA' and im.size==(1024,1536) and int(a.min())==0 and int(a.max())>=240 and int(edges.max())<=1
for color in ['black','white','checker']:
 canvas=Image.new('RGBA',im.size,color if color!='checker' else '#eee')
 if color=='checker':
  dr=ImageDraw.Draw(canvas)
  for y in range(0,im.height,64):
   for x in range(0,im.width,64):
    if ((x//64)+(y//64))%2:dr.rectangle((x,y,x+63,y+63),fill='#c6c6c6')
 canvas.alpha_composite(im);canvas.convert('RGB').save(BASE/f'qa-native-{color}.png');canvas.convert('RGB').resize((512,768),Image.Resampling.LANCZOS).save(BASE/f'qa-{color}-preview.png')
pair=Image.new('RGB',(2048,1536));pair.paste(Image.open(BASE/'qa-native-black.png'),(0,0));pair.paste(Image.open(BASE/'qa-native-white.png'),(1024,0));pair.save(BASE/'qa-native-black-white.png')
bg=Image.open(BG).convert('RGBA')
card=ImageOps.fit(bg,(192,224),method=Image.Resampling.LANCZOS);card.alpha_composite(im.resize((144,216),Image.Resampling.LANCZOS),(24,4));assert card.getchannel('A').getextrema()==(255,255);card.save(BASE/'card-preview.png')
ui={k:ImageOps.fit(card,size,method=Image.Resampling.LANCZOS,centering=(.5,.18)) for k,size in [('desktop-dialogue',(92,112)),('mobile-dialogue',(64,88)),('roster',(44,44))]}
ui['hud']=ImageOps.fit(card,(112,112),method=Image.Resampling.LANCZOS).crop((8,0,104,112))
for key,pic in ui.items():pic.save(BASE/f'qa-ui-{key}.png')
sheet=Image.new('RGB',(1100,640),'#ddd');dr=ImageDraw.Draw(sheet)
dr.text((10,10),'SEVERIN: historical / selected full-canvas card / approved style; modeled UI crops',fill='black')
old=Image.open(PACK/'runtime/narrative-static/char-severin.png');sheet.paste(old,(10,40));sheet.paste(card,(240,40))
style=Image.open(PACK/'masters/approved-v5/narrative/char-mirel-01/master.png');sc=ImageOps.fit(bg,(192,224),method=Image.Resampling.LANCZOS);sc.alpha_composite(style.resize((144,216),Image.Resampling.LANCZOS),(24,4));sheet.paste(sc,(470,40))
roderick=Image.open(PACK/'masters/approved-v5/narrative/char-roderick-01/master.png');rc=ImageOps.fit(bg,(192,224),method=Image.Resampling.LANCZOS);rc.alpha_composite(roderick.resize((144,216),Image.Resampling.LANCZOS),(24,4));sheet.paste(rc,(700,40))
for x,label in [(10,'Historical source (ornate defects)'),(240,'Severin: plain regent correction'),(470,'Mirel: style only'),(700,'Roderick: distinct knight identity')]:dr.text((x,280),label,fill='black')
x=10
for key,pic in ui.items():
 dr.text((x,325),key+' '+str(pic.size),fill='black');sheet.paste(pic,(x,350));sheet.paste(pic.resize((pic.width*2,pic.height*2),Image.Resampling.NEAREST),(x+105,350));x+=250
sheet.save(BASE/'qa-card-and-ui-comparison.png')
alpha={'mode':'RGBA','extrema':[int(a.min()),int(a.max())],'transparentPixels':int((a==0).sum()),'transparentFraction':float((a==0).mean()),'nearOpaquePixels':int((a>=240).sum()),'edgeMaxAlpha':int(edges.max()),'edgeNonzeroPixels':int((edges>0).sum()),'edgePixelsAbove1':int((edges>1).sum()),'alphaAbove1BoundsDiagnosticOnly':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)],'alphaAbove1MarginsDiagnosticOnly':[int(xs.min()),int(ys.min()),int(im.width-xs.max()-1),int(im.height-ys.max()-1)]}
request=json.load(open(BASE/'attempt-01/request.json'));audit=json.load(open(BASE/'reference-audit.json'));provenance=json.load(open(BASE/'attempt-01/generation-provenance.json'))
proof={'topicId':'C01-CHAR-SEVERIN','tool':'built-in image_gen.imagegen','generatedNativePath':provenance['generatedNativePath'],'selectedAttempt':1,'generationCalls':1,'rejectedAttempts':[],'nativeSize':list(im.size),'generatedNativeSha256':sha(provenance['generatedNativePath']),'rawSha256':sha(BASE/'attempt-01/raw.png'),'masterSha256':sha(BASE/'attempt-01/raw.png'),'masterIsByteExactGeneratedSource':True,'transparentBackgroundRequested':True,'alphaRetainedUnmodified':True,'noAlphaCleanup':True,'noChromaKey':True,'noSourceResizeOrCrop':True,'noWarpFlipRecolorOrBBoxFit':True,'sourceAlpha':alpha,'selectedRequestReferenceHashes':{p:sha(p) for p in request['referenced_image_paths']},'originalFullboard':audit['original']['board']['path'],'originalFullboardSha256':audit['original']['board']['sha256'],'originalCell':audit['original']['exactCrop']['path'],'originalCellSha256':audit['original']['exactCrop']['sha256'],'originalCropBox':audit['original']['cropBoxXYXYExclusive'],'designedReconstruction':audit['designedReconstruction'],'crownReconciliation':audit['crownReconciliation'],'cardComposition':{'inspectionOnly':True,'size':[192,224],'mode':'RGBA','alphaExtrema':[255,255],'background':str(BG),'backgroundSha256':sha(BG),'backgroundCenterCover':True,'backgroundSourceCropBox':[89.57142857142856,0,1164.4285714285716,1254],'characterWholeCanvasSize':[144,216],'origin':[24,4],'characterCropped':False,'sha256':sha(BASE/'card-preview.png')},'uiCrops':{'desktopDialogue':{'size':[92,112],'objectFit':'cover','objectPosition':[.5,.18]},'mobileDialogue':{'size':[64,88],'objectFit':'cover','objectPosition':[.5,.18]},'roster':{'size':[44,44],'objectFit':'cover','objectPosition':[.5,.18]},'hud':{'size':[96,112],'imageViewport':[112,112],'x':-8,'y':0,'preserveAspectRatio':'xMidYMid slice'}},'consumerStatus':'catalog-only-no-selected-consumer','stageSelectorExists':False,'liveGameVerified':False,'verificationLimits':['Source and mathematically modeled UI-crop QA only; no browser/GPU/live-game acceptance','No repository, exporter, binding, consumer, gameplay, stage-selector or Git writes','No runtime HD publication'],'visualReviewStatus':'awaiting native black/white and UI crop review'}
(BASE/'sourceproof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(alpha,indent=2))
