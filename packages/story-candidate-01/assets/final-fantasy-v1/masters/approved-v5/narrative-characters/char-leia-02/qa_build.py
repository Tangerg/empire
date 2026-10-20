from pathlib import Path
from PIL import Image, ImageOps, ImageDraw
import json, hashlib
import numpy as np

BASE = Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-characters')
ROOT = Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
BG = ROOT / 'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/narrative/card-background/master.png'
SLUGS = ['char-leia-02', 'char-leia-03', 'char-roderick-02']
GENERATED = {
'char-leia-02': '/workspace/scratch/84c9fe948755/generated_images/exec-ccab4c65-8cbd-4571-b9b3-15e926282e04.png',
'char-leia-03': '/workspace/scratch/84c9fe948755/generated_images/exec-1da14d61-19d6-4404-883e-85b44086cf62.png',
'char-roderick-02': '/workspace/scratch/84c9fe948755/generated_images/exec-10cc803e-5386-490f-b764-75663b173b8a.png',
}
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
rgba_bg = Image.open(BG).convert('RGBA')
all_cards = Image.new('RGB',(900,600),'#e3e3e3')
draw = ImageDraw.Draw(all_cards)
transparent_sheet = Image.new('RGB',(1536,1600),'#d0d0d0')
td = ImageDraw.Draw(transparent_sheet)
summary = []
for i, slug in enumerate(SLUGS):
    folder = BASE/slug
    master = folder/'master.png'
    im = Image.open(master)
    assert im.size == (1024,1536) and im.mode == 'RGBA'
    a = np.array(im.getchannel('A'))
    edges = np.concatenate([a[0,:],a[-1,:],a[1:-1,0],a[1:-1,-1]])
    assert a.min() == 0 and a.max() >= 240 and edges.max() <= 1
    assert sha(master) == sha(folder/'raw.png') == sha(GENERATED[slug])
    ys,xs = np.where(a>1)
    alpha = {'mode':im.mode,'extrema':[int(a.min()),int(a.max())],
             'transparentPixels':int((a==0).sum()),'transparentFraction':float((a==0).mean()),
             'nearOpaquePixels':int((a>=240).sum()),'edgeMaxAlpha':int(edges.max()),
             'edgeNonzeroPixels':int((edges>0).sum()),'edgePixelsAbove1':int((edges>1).sum()),
             'alphaAbove1BoundsDiagnosticOnly':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]}
    native_pair = Image.new('RGB',(2048,1536),'white')
    for k,color in enumerate(['white','black','checker']):
        b = Image.new('RGBA',im.size,color if color!='checker' else '#eeeeee')
        if color=='checker':
            dr=ImageDraw.Draw(b)
            for y in range(0,1536,64):
                for x in range(0,1024,64):
                    if ((x//64)+(y//64))%2: dr.rectangle((x,y,x+63,y+63), fill='#c6c6c6')
        b.alpha_composite(im)
        b.convert('RGB').save(folder/f'qa-native-{color}.png')
        b.resize((512,768),Image.Resampling.LANCZOS).convert('RGB').save(folder/f'qa-{color}-preview.png')
        if k<2:
            native_pair.paste(b.convert('RGB'),(k*1024,0))
            transparent_sheet.paste(b.resize((512,768),Image.Resampling.LANCZOS).convert('RGB'),(i*512,24+k*800))
            td.text((i*512+8,4+k*800),slug+' / '+color,fill='black')
    native_pair.save(folder/'qa-native-black-white.png')
    card = ImageOps.fit(rgba_bg,(192,224),method=Image.Resampling.LANCZOS)
    card.alpha_composite(im.resize((144,216),Image.Resampling.LANCZOS),(24,4))
    assert card.getchannel('A').getextrema()==(255,255)
    card.save(folder/'card-preview.png')
    ui = {'desktop-dialogue': ImageOps.fit(card,(92,112),method=Image.Resampling.LANCZOS,centering=(.5,.18)),
          'mobile-dialogue': ImageOps.fit(card,(64,88),method=Image.Resampling.LANCZOS,centering=(.5,.18)),
          'roster': ImageOps.fit(card,(44,44),method=Image.Resampling.LANCZOS,centering=(.5,.18)),
          'hud': ImageOps.fit(card,(112,112),method=Image.Resampling.LANCZOS,centering=(.5,.5)).crop((8,0,104,112))}
    x=i*300+20
    draw.text((x,8),slug+' / native card',fill='black');all_cards.paste(card,(x,28))
    draw.text((x,270),'Desktop92x112 / mobile64x88',fill='black')
    all_cards.paste(ui['desktop-dialogue'],(x,292));all_cards.paste(ui['mobile-dialogue'],(x+116,292))
    draw.text((x,425),'Roster44x44 / HUD96x112',fill='black')
    all_cards.paste(ui['roster'],(x,448));all_cards.paste(ui['hud'],(x+76,448))
    for label,pic in ui.items(): pic.save(folder/f'qa-ui-{label}.png')
    req=json.load(open(folder/'request.json'))
    ra=json.load(open(folder/'reference-audit.json'))
    calls=3 if slug=='char-roderick-02' else 2
    references={p:sha(p) for p in req['referenced_image_paths']}
    refs1=ra['generationReferenceHashes']
    proof={'topicId':ra['topicId'],'tool':'built-in image_gen.imagegen','generatedNativePath':GENERATED[slug],
           'selectedAttempt':calls,'generationCalls':calls,'rejectedAttempts':json.load(open(folder/'rejections.json')),
           'nativeSize':list(im.size),'generatedNativeSha256':sha(GENERATED[slug]),'rawSha256':sha(folder/'raw.png'),
           'masterSha256':sha(master),'masterIsByteExactGeneratedSource':True,'transparentBackgroundRequested':True,
           'alphaRetainedUnmodified':True,'noAlphaCleanup':True,'noChromaKey':True,'noSourceResizeOrCrop':True,
           'noWarpFlipRecolorOrBBoxFit':True,'sourceAlpha':alpha,'selectedRequestReferenceHashes':references,
           'firstAttemptAuthorityReferenceHashes':refs1,'originalFullboard':ra['originalFullboard'],
           'originalFullboardSha256':ra['originalFullboardSha256'],'originalCell':ra['originalCell'],
           'originalCellSha256':ra['originalCellSha256'],'originalCropBox':ra['cropBoxXYXYExclusive'],
           'designedReconstruction':ra['designedReconstruction'],
           'cardComposition':{'inspectionOnly':True,'size':[192,224],'mode':'RGBA','alphaExtrema':[255,255],
                              'background':str(BG),'backgroundSha256':sha(BG),'backgroundCenterCover':True,
                              'characterWholeCanvasSize':[144,216],'origin':[24,4],'characterCropped':False,
                              'sha256':sha(folder/'card-preview.png')},
           'uiCrops':{'desktopDialogue':{'size':[92,112],'objectFit':'cover','objectPosition':[.5,.18]},
                      'mobileDialogue':{'size':[64,88],'objectFit':'cover','objectPosition':[.5,.18]},
                      'roster':{'size':[44,44],'objectFit':'cover','objectPosition':[.5,.18]},
                      'hud':{'size':[96,112],'imageViewport':[112,112],'x':-8,'y':0,'preserveAspectRatio':'xMidYMid slice'}},
           'verificationLimits':['Inspection-only source and mathematically modeled UI crop QA; no browser/GPU/live-game acceptance',
                                 'No repo/Git/exporter/binding/consumer edits','No stage selector changes','No runtime HD publication'],
           'visualReviewStatus':'awaiting final contact-sheet inspection'}
    (folder/'sourceproof.json').write_text(json.dumps(proof,indent=2,ensure_ascii=False)+'\n')
    (folder/'hash.sha256').write_text(sha(master)+'  master.png\n'+sha(folder/'raw.png')+'  raw.png\n')
    summary.append({'topicId':ra['topicId'],'master':str(master),'sha256':sha(master),'nativeAlpha':alpha,'selectedAttempt':calls})
all_cards.save(BASE/'char-leia-02/qa-three-card-and-ui-comparison.png')
transparent_sheet.save(BASE/'char-leia-02/qa-three-character-transparent-proof.png')
(BASE/'char-leia-02/three-character-delivery.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(summary,indent=2))
