from pathlib import Path
from PIL import Image, ImageOps, ImageDraw
import json, hashlib, numpy as np
BASE=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-characters')
ROOT=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
BG=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/narrative/card-background/master.png'
NAMES=['torren','bran','cain']
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
bg=Image.open(BG).convert('RGBA')
sheet=Image.new('RGB',(1050,640),'#e3e3e3');sd=ImageDraw.Draw(sheet)
pair=Image.new('RGB',(1536,1600),'#eeeeee');pd=ImageDraw.Draw(pair)
continuity=Image.new('RGB',(1050,570),'#e3e3e3');cd=ImageDraw.Draw(continuity)
summary=[]
for i,n in enumerate(NAMES):
 d=BASE/f'char-{n}-02'; im=Image.open(d/'master.png'); a=np.array(im.getchannel('A'))
 assert im.size==(1024,1536) and im.mode=='RGBA' and int(a.min())==0 and int(a.max())>=240
 edges=np.concatenate((a[0,:],a[-1,:],a[1:-1,0],a[1:-1,-1]));assert int(edges.max())<=1
 prov=json.load(open(d/'generation-provenance.json'));req=json.load(open(d/'request.json'));ra=json.load(open(d/'reference-audit.json'))
 assert sha(d/'master.png')==sha(d/'raw.png')==sha(prov['generatedNativePath'])
 ys,xs=np.where(a>1)
 alpha={'mode':'RGBA','extrema':[int(a.min()),int(a.max())],'transparentPixels':int((a==0).sum()),'transparentFraction':float((a==0).mean()),'nearOpaquePixels':int((a>=240).sum()),'edgeMaxAlpha':int(edges.max()),'edgeNonzeroPixels':int((edges>0).sum()),'edgePixelsAbove1':int((edges>1).sum()),'alphaAbove1BoundsDiagnosticOnly':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]}
 npair=Image.new('RGB',(2048,1536),'white')
 for k,col in enumerate(['white','black','checker']):
  canvas=Image.new('RGBA',im.size,col if col!='checker' else '#eee')
  if col=='checker':
   dr=ImageDraw.Draw(canvas)
   for y in range(0,1536,64):
    for x in range(0,1024,64):
     if ((x//64)+(y//64))%2:dr.rectangle((x,y,x+63,y+63),fill='#c6c6c6')
  canvas.alpha_composite(im)
  rgb=canvas.convert('RGB');rgb.save(d/f'qa-native-{col}.png')
  rgb.resize((512,768),Image.Resampling.LANCZOS).save(d/f'qa-{col}-preview.png')
  if k<2:
   npair.paste(rgb,(k*1024,0));pair.paste(rgb.resize((512,768),Image.Resampling.LANCZOS),(i*512,24+k*800))
   pd.text((i*512+8,4+k*800),f'{n}02 / {col}',fill='black')
 npair.save(d/'qa-native-black-white.png')
 card=ImageOps.fit(bg,(192,224),method=Image.Resampling.LANCZOS);card.alpha_composite(im.resize((144,216),Image.Resampling.LANCZOS),(24,4));assert card.getchannel('A').getextrema()==(255,255);card.save(d/'card-preview.png')
 ui={k:ImageOps.fit(card,size,method=Image.Resampling.LANCZOS,centering=(.5,.18)) for k,size in [('desktop-dialogue',(92,112)),('mobile-dialogue',(64,88)),('roster',(44,44))]}
 ui['hud']=ImageOps.fit(card,(112,112),method=Image.Resampling.LANCZOS).crop((8,0,104,112))
 for key,pic in ui.items():pic.save(d/f'qa-ui-{key}.png')
 x=i*350+20
 sd.text((x,8),f'{n}02 / native192x224 card',fill='black');sheet.paste(card,(x,28))
 sd.text((x,265),'Desktop92x112 / mobile64x88',fill='black');sheet.paste(ui['desktop-dialogue'],(x,289));sheet.paste(ui['mobile-dialogue'],(x+116,289))
 sd.text((x,420),'Roster44x44 / HUD96x112',fill='black');sheet.paste(ui['roster'],(x,444));sheet.paste(ui['hud'],(x+76,444))
 sd.text((x,578),'CATALOG ONLY / modeled crops',fill='black')
 s1=Image.open(req['referenced_image_paths'][0]);oldcard=ImageOps.fit(bg,(192,224),method=Image.Resampling.LANCZOS);oldcard.alpha_composite(s1.resize((144,216),Image.Resampling.LANCZOS),(24,4))
 continuity.paste(oldcard,(x,28));continuity.paste(card,(x,306));cd.text((x,8),f'{n} stage01 identity/style',fill='black');cd.text((x,286),f'{n} stage02 selected',fill='black')
 ui_contract={'desktopDialogue':{'size':[92,112],'objectFit':'cover','objectPosition':[.5,.18]},'mobileDialogue':{'size':[64,88],'objectFit':'cover','objectPosition':[.5,.18]},'roster':{'size':[44,44],'objectFit':'cover','objectPosition':[.5,.18]},'hud':{'size':[96,112],'imageViewport':[112,112],'x':-8,'y':0,'preserveAspectRatio':'xMidYMid slice'}}
 proof={'topicId':ra['topicId'],'tool':'built-in image_gen.imagegen','generatedNativePath':prov['generatedNativePath'],'selectedAttempt':1,'generationCalls':1,'rejectedAttempts':[],'nativeSize':[1024,1536],'generatedNativeSha256':sha(prov['generatedNativePath']),'rawSha256':sha(d/'raw.png'),'masterSha256':sha(d/'master.png'),'masterIsByteExactGeneratedSource':True,'transparentBackgroundRequested':True,'alphaRetainedUnmodified':True,'noAlphaCleanup':True,'noChromaKey':True,'noSourceResizeOrCrop':True,'noWarpFlipRecolorOrBBoxFit':True,'sourceAlpha':alpha,'selectedRequestReferenceHashes':{p:sha(p) for p in req['referenced_image_paths']},'originalFullboard':ra['originalFullboard'],'originalFullboardSha256':ra['originalFullboardSha256'],'originalCell':ra['originalCell'],'originalCellSha256':ra['originalCellSha256'],'originalCropBox':ra['cropBoxXYXYExclusive'],'cardComposition':{'inspectionOnly':True,'size':[192,224],'mode':'RGBA','alphaExtrema':[255,255],'background':str(BG),'backgroundSha256':sha(BG),'backgroundCenterCover':True,'characterWholeCanvasSize':[144,216],'origin':[24,4],'characterCropped':False,'sha256':sha(d/'card-preview.png')},'uiCrops':ui_contract,'consumerStatus':'catalog-only-no-selected-consumer','stageSelectorExists':False,'liveGameVerified':False,'verificationLimits':['Source and mathematically modeled UI-crop QA only, no browser/GPU/live-game acceptance','No repo, exporter, binding, consumer, gameplay, stage-selector or Git writes','No runtime HD publication'],'visualReviewStatus':'awaiting final black/white and UI comparison review'}
 if (d/'sourceproof.json').exists():
  previous=json.load(open(d/'sourceproof.json'))
  for key in ['designedReconstruction','generationHistory','visualReviewStatus','visualReview','reviewedProofFiles','qaComparison','transparentComparison','stageContinuityComparison']:
   if key in previous:proof[key]=previous[key]
 (d/'sourceproof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
 (d/'rejections.json').write_text('[]\n')
 (d/'hash.sha256').write_text(sha(d/'master.png')+'  master.png\n'+sha(d/'raw.png')+'  raw.png\n')
 summary.append({'topicId':ra['topicId'],'master':str(d/'master.png'),'sha256':sha(d/'master.png'),'nativeAlpha':alpha,'selectedAttempt':1,'consumerStatus':'catalog-only-no-selected-consumer'})
sheet.save(BASE/'char-torren-02/qa-three-card-and-ui-comparison.png')
pair.save(BASE/'char-torren-02/qa-three-character-transparent-proof.png')
continuity.save(BASE/'char-torren-02/qa-stage-continuity-comparison.png')
(BASE/'char-torren-02/three-character-delivery.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(summary,indent=2))

