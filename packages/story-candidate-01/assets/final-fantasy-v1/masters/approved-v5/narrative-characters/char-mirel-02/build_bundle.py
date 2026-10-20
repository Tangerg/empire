from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
import json,hashlib,shutil,numpy as np
ROOT=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
BASE=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-characters')
SELF=BASE/'char-mirel-02'
AUDIT=ROOT/'docs/art-rebuild-20261019/approved-v5/remaining-character-card-audit/audit.json'
BG=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/narrative/card-background/master.png'
STAGE1=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/narrative/char-mirel-01/master.png'
PROP=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-props/prop-ada-medicine/master.png')
NAMES=['char-mirel-02','char-ada-01','char-ada-02']
paths=json.loads((SELF/'generation-paths.json').read_text())
audit=json.loads(AUDIT.read_text())
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
write=lambda p,d:Path(p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')

def excerpt(rel,start,end):
 p=ROOT/rel;lines=p.read_text().splitlines()
 return {'path':rel,'sha256':sha(p),'lines':[start,end],'exactText':'\n'.join(f'{i+1}: {lines[i]}' for i in range(start-1,min(end,len(lines))))}

shared_evidence=[excerpt('docs/story-candidates/candidate-01/deep-customized/09-static-assets.md',53,61),excerpt('docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md',48,54)]
mirelle_evidence=shared_evidence+[excerpt('docs/story-candidates/candidate-01/deep-novel-v2/06-war-of-seven-towers-novel.md',205,227),excerpt('docs/story-candidates/candidate-01/deep-novel-v2/07-throne-without-king-novel.md',375,383),excerpt('docs/story-candidates/candidate-01/deep-novel-v2/07-throne-without-king-novel.md',545,547)]
ada_evidence=shared_evidence+[excerpt('docs/story-candidates/candidate-01/deep-customized/00-creative-bible.md',46,52),excerpt('docs/story-candidates/candidate-01/deep-customized/01-border-fire.md',43,59),excerpt('docs/story-candidates/candidate-01/deep-novel-v2/01-border-fire-novel.md',259,269),excerpt('docs/story-candidates/candidate-01/deep-novel-v2/01-border-fire-novel.md',343,357),excerpt('docs/story-candidates/candidate-01/deep-novel-v2/01-border-fire-novel.md',733,735),excerpt('docs/story-candidates/candidate-01/deep-novel-v2/07-throne-without-king-novel.md',113,137)]
summary=[]
card_sheet=Image.new('RGB',(1050,610),'#e3e3e3');cd=ImageDraw.Draw(card_sheet)
native_sheet=Image.new('RGB',(1536,1600),'#e3e3e3');nd=ImageDraw.Draw(native_sheet)
ui_contract={'desktopDialogue':{'size':[92,112],'objectFit':'cover','objectPosition':[.5,.18]},'mobileDialogue':{'size':[64,88],'objectFit':'cover','objectPosition':[.5,.18]},'roster':{'size':[44,44],'objectFit':'cover','objectPosition':[.5,.18]},'hud':{'size':[96,112],'imageViewport':[112,112],'x':-8,'y':0,'preserveAspectRatio':'xMidYMid slice'}}
for idx,name in enumerate(NAMES):
 d=BASE/name;topic='C01-'+name.upper();old=next(v for v in audit['remainingCards'] if v['topicId']==topic)
 im=Image.open(d/'master.png');arr=np.array(im.getchannel('A'));edge=np.concatenate((arr[0,:],arr[-1,:],arr[1:-1,0],arr[1:-1,-1]));ys,xs=np.where(arr>1)
 assert im.mode=='RGBA' and im.size==(1024,1536) and arr.min()==0 and arr.max()>=240 and edge.max()<=1
 assert sha(d/'raw.png')==sha(d/'master.png')==sha(paths[name]['selected'])
 alpha={'mode':im.mode,'extrema':[int(arr.min()),int(arr.max())],'transparentPixels':int((arr==0).sum()),'transparentFraction':float((arr==0).mean()),'nearOpaquePixels':int((arr>=240).sum()),'edgeMaxAlpha':int(edge.max()),'edgeNonzeroPixels':int((edge>0).sum()),'edgePixelsAbove1':int((edge>1).sum()),'alphaAbove1BoundsDiagnosticOnly':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]}
 refs=d/'references';refs.mkdir(exist_ok=True)
 origboard=Path(old['original']['board']['path']);origcell=ROOT/'docs/art-rebuild-20261019/approved-v5/remaining-character-card-audit/original-cells'/f'{topic.lower()}-original-cell.png'
 refs_in=[('original-board.png',origboard),('original-cell.png',origcell),('approved-mirelle-stage01.png',STAGE1),('independent-card-background.png',BG)]
 if name.startswith('char-ada'):refs_in.append(('approved-ada-medicine.png',PROP))
 if name=='char-ada-02':refs_in.append(('ada-age8-selected-master.png',BASE/'char-ada-01/master.png'))
 reference_hashes=[]
 for dest,src in refs_in:
  shutil.copy2(src,refs/dest);reference_hashes.append({'role':dest,'sourcePath':str(src),'preservedPath':str(refs/dest),'sha256':sha(src)})
 old['auditCheckpointMeaning']='Read-only source audit before this remake; runtime/consumer entries are historical checkpoint evidence, not a claim of current integration.'
 write(d/'reference-audit.json',{'topicId':topic,'originalFullboard':str(origboard),'originalFullboardSha256':sha(origboard),'originalCell':str(origcell),'originalCellSha256':sha(origcell),'cropBoxXYXYExclusive':old['original']['cropBoxXYXYExclusive'],'catalogSlotZeroBased':old['original']['catalogSlotZeroBased'],'originalAuditSource':str(AUDIT),'originalAuditSha256':sha(AUDIT),'originalCardAudit':old,'references':reference_hashes})
 decision={'topicId':topic,'decision':'Explicit story-led designed reconstruction, not exact source restoration','originalBoardIsProvenanceNotFinalIdentityAuthority':True,'approval':'Parent approved source/lore reconciliation2026-10-20; preserve named identity and ages, approved single cracked blue-thread bottle with two abstract mark systems, plain functional gloves/seal tools, no invented faction emblem','style':'Broad matte chunky shapes, strong outlines and readable face/hands','sourceEvidence':mirelle_evidence if name=='char-mirel-02' else ada_evidence,'consumerScope':'Catalog-only card. No stage selector, runtime HD, gameplay or consumer change.'}
 if name=='char-mirel-02':
  decision.update({'identity':'Same approved middle-aged female Mirelle stage01, dark bun, warm mature face, charcoal-cream healer/keeper clothes','laterStage':'Founder of Last Oath, open name ledger, lantern, three separately held plain belt seal-tools and individually removable name tags','reconciliation':'Static brief asks purification seal split into public tools; novel has her relinquish absolute purification seal and make voluntary limited independent interfaces. The plain independent belt tools visualize limited public work, never an intact commanding authority seal. Number3 and plain shapes are composition details, not asserted canon or faction symbols.'})
 else:
  decision.update({'identity':'Named Ada Cole, same dark brown eyes, small rounded nose, warm complexion, dark brown swept fringe across age8 bob and age17 low bun','age':8 if name=='char-ada-01' else 17,'reconciliation':'Old character board depicts toy boat/cloth at8 and generic multiple bottles at17; an earlier creative bible delays learning bilingual labels until ch02. Explicit deep-customized ch01 has the cracked bottle and retained two national labels, and deep-novel-v2 ch01 lines261/345/735 already has the age8 blue-thread bilingual bottle. These explicit scene passages plus the newly approved single-bottle prop resolve the conflict in favor of one saved cracked bottle at both stages. Abstract angular/curved marks represent scripts; no readable invented language.','costume':'Fully clothed patched mustard/teal child travel outfit' if name=='char-ada-01' else 'Fully clothed cream/teal practical young medic, brown satchel and ordinary functional soft healing gloves; simple cuff fastening is non-canonical functional design, no emblem'})
 write(d/'identity-decision.json',decision)
 history=[]
 for attempt,prefix,native in [(1,d/'attempt-01',paths[name]['first']),(2,d,paths[name]['selected'])]:
  req=json.loads((prefix/'request.json').read_text());raw=prefix/'raw.png';a=Image.open(raw)
  h={'attempt':attempt,'selected':attempt==2,'tool':'built-in image_gen.imagegen','generatedNativePath':native,'rawCopy':str(raw),'nativeSize':list(a.size),'mode':a.mode,'rawSha256':sha(raw),'masterOrRawByteExact':sha(raw)==sha(native),'transparentBackgroundRequested':req['transparent_background'],'requestPath':str(prefix/'request.json'),'requestSha256':sha(prefix/'request.json'),'promptPath':str(prefix/'prompt.txt'),'promptSha256':sha(prefix/'prompt.txt'),'referenceHashes':{p:sha(p) for p in req['referenced_image_paths']}}
  if attempt==1:h['disposition']=json.loads((prefix/'rejection.json').read_text())
  history.append(h)
  if attempt==1:write(prefix/'generation-provenance.json',h)
 prov={'tool':'built-in image_gen.imagegen','generatedNativePath':paths[name]['selected'],'rawSha256':sha(d/'raw.png'),'masterSha256':sha(d/'master.png'),'generationCalls':2,'selectedAttempt':2,'generationHistory':history,'noPostGenerationSourceModification':True,'sourceproof':'sourceproof.json','finalPrompt':'prompt.txt','request':'request.json'}
 write(d/'generation-provenance.json',prov)
 write(d/'rejections.json',[history[0]['disposition']])
 (d/'hash.sha256').write_text(f"{sha(d/'master.png')}  master.png\n{sha(d/'raw.png')}  raw.png\n")
 pair=Image.new('RGB',(2048,1536),'white')
 for j,color in enumerate(['white','black','checker']):
  bg=Image.new('RGBA',im.size,color if color!='checker' else '#eeeeee')
  if color=='checker':
   draw=ImageDraw.Draw(bg)
   for y in range(0,1536,64):
    for x in range(0,1024,64):
     if (x//64+y//64)%2:draw.rectangle((x,y,x+63,y+63),fill='#c6c6c6')
  bg.alpha_composite(im);rgb=bg.convert('RGB');rgb.save(d/f'qa-native-{color}.png');rgb.resize((512,768),Image.Resampling.LANCZOS).save(d/f'qa-{color}-preview.png')
  if j<2:
   pair.paste(rgb,(j*1024,0));native_sheet.paste(rgb.resize((512,768),Image.Resampling.LANCZOS),(idx*512,24+j*800));nd.text((idx*512+8,4+j*800),f'{name} / {color}',fill='black')
 pair.save(d/'qa-native-black-white.png')
 card=ImageOps.fit(Image.open(BG).convert('RGBA'),(192,224),method=Image.Resampling.LANCZOS);card.alpha_composite(im.resize((144,216),Image.Resampling.LANCZOS),(24,4));card.save(d/'card-preview.png')
 assert card.getchannel('A').getextrema()==(255,255)
 ui={k:ImageOps.fit(card,size,method=Image.Resampling.LANCZOS,centering=(.5,.18)) for k,size in [('desktop-dialogue',(92,112)),('mobile-dialogue',(64,88)),('roster',(44,44))]}
 ui['hud']=ImageOps.fit(card,(112,112),method=Image.Resampling.LANCZOS).crop((8,0,104,112))
 for key,pic in ui.items():pic.save(d/f'qa-ui-{key}.png')
 x=idx*350+20;cd.text((x,8),f'{name} / native192x224 card',fill='black');card_sheet.paste(card,(x,28));cd.text((x,265),'Desktop92x112 / mobile64x88',fill='black');card_sheet.paste(ui['desktop-dialogue'],(x,289));card_sheet.paste(ui['mobile-dialogue'],(x+116,289));cd.text((x,420),'Roster44x44 / HUD96x112',fill='black');card_sheet.paste(ui['roster'],(x,444));card_sheet.paste(ui['hud'],(x+76,444));cd.text((x,578),'CATALOG ONLY / modeled crops',fill='black')
 proof={'topicId':topic,'tool':'built-in image_gen.imagegen','generatedNativePath':paths[name]['selected'],'selectedAttempt':2,'generationCalls':2,'generationHistory':history,'nativeSize':[1024,1536],'generatedNativeSha256':sha(paths[name]['selected']),'rawSha256':sha(d/'raw.png'),'masterSha256':sha(d/'master.png'),'masterIsByteExactGeneratedSource':True,'transparentBackgroundRequested':True,'alphaRetainedUnmodified':True,'noAlphaCleanup':True,'noChromaKey':True,'noSourceResizeOrCrop':True,'noWarpFlipRecolorOrBBoxFit':True,'sourceAlpha':alpha,'selectedRequestReferenceHashes':history[-1]['referenceHashes'],'allPreservedReferences':reference_hashes,'originalFullboard':str(origboard),'originalFullboardSha256':sha(origboard),'originalCell':str(origcell),'originalCellSha256':sha(origcell),'originalCropBox':old['original']['cropBoxXYXYExclusive'],'cardComposition':{'inspectionOnly':True,'size':[192,224],'mode':'RGBA','alphaExtrema':[255,255],'background':str(BG),'backgroundSha256':sha(BG),'backgroundCenterCover':True,'characterWholeCanvasSize':[144,216],'origin':[24,4],'characterCropped':False,'sha256':sha(d/'card-preview.png')},'uiCrops':ui_contract,'consumerStatus':'catalog-only-no-selected-consumer','stageSelectorExists':False,'liveGameVerified':False,'verificationLimits':['Source and mathematically modeled UI-crop QA only; no browser/GPU/live-game acceptance','No main repo, exporter, binding, consumer, gameplay, stage-selector or Git writes','No runtime HD publication'],'visualReviewStatus':'pending final comparison review','identityDecision':'identity-decision.json'}
 if (d/'visual-review.json').exists():
  proof['visualReviewStatus']='passed source and modeled consumer QA';proof['visualReview']=json.loads((d/'visual-review.json').read_text())
 write(d/'sourceproof.json',proof)
 (d/'README.txt').write_text(f'{topic}: independent transparent native1024x1536 portrait\nSelected attempt2. Raw and master are byte-exact copies of built-in image_gen output; no source pixel edits. Original and rejected/superseded attempt preserved.\nCanonical opaque192x224 card proof uses separate approved background with whole1024x1536 canvas resized144x216 at24,4. Other QA files model current desktop/mobile/roster/HUD crops without publishing consumer changes.\nCatalog-only. No live game, GPU, stage-selection or HD publication claim.\nSee identity-decision.json for exact story evidence and explicit designed reconciliation; sourceproof.json and generation-provenance.json preserve hashes, requests and attempts.\n')
 summary.append({'topicId':topic,'master':str(d/'master.png'),'raw':str(d/'raw.png'),'sha256':sha(d/'master.png'),'nativeAlpha':alpha,'selectedAttempt':2,'consumerStatus':'catalog-only-no-selected-consumer'})
card_sheet.save(SELF/'qa-three-card-and-ui-comparison.png');native_sheet.save(SELF/'qa-three-character-transparent-proof.png')
continuity=Image.new('RGB',(1024,430),'#eeeeee');sd=ImageDraw.Draw(continuity)
for i,(label,path) in enumerate([('Mirelle approved stage01',STAGE1),('Mirelle oath-builder02',BASE/'char-mirel-02/master.png'),('Ada age8 bridge child',BASE/'char-ada-01/master.png'),('Ada age17 capital medic',BASE/'char-ada-02/master.png')]):
 im=Image.open(path).convert('RGBA').resize((256,384),Image.Resampling.LANCZOS);bg=Image.new('RGBA',(256,384),'white');bg.alpha_composite(im);continuity.paste(bg.convert('RGB'),(i*256,24));sd.text((i*256+8,4),label,fill='black')
sd.text((8,414),'Full canvases normalized for review, not a physical-height chart',fill='black');continuity.save(SELF/'qa-stage-continuity-comparison.png')
write(SELF/'three-character-delivery.json',summary)
print(json.dumps(summary,indent=2))
