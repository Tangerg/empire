from pathlib import Path
from PIL import Image
import hashlib,json,numpy as np
R=Path(__file__).parent
REPO=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
write=lambda p,o:p.write_text(json.dumps(o,indent=2,ensure_ascii=False)+'\n')
phases=['airborne-burst','falling-pieces','falling-remnants','settled-heap']
e=json.loads((R/'source-evidence.json').read_text());audit=e['sourceAuditRecord'];orig=REPO/'packages/story-candidate-01/assets/final-fantasy-v1/masters/fx/fx-atlas-02.png';board=Image.open(orig)
referenceChecks=[]
for i,s in enumerate(audit['slots']):
 p=R/f'reference/original-full-cell-f{i}.png';expected=board.crop(s['fullGridBoundsXYXY']);assert Image.open(p).tobytes()==expected.tobytes()
 referenceChecks.append({'phaseIndex':i,'boundsXYXY':s['fullGridBoundsXYXY'],'path':str(p.relative_to(R)),'decodedEqualOriginalBoardCell':True,'sha256':sha(p)})
p=R/'reference/original-board.png';assert Image.open(p).tobytes()==board.tobytes()
referenceChecks.append({'path':str(p.relative_to(R)),'repositorySource':str(orig.relative_to(REPO)),'repositoryFileSha256':sha(orig),'savedFileSha256':sha(p),'decodedPixelsExact':True})
for den,dr in [(1,'runtime'),(2,'runtime-hd')]:
 p=R/f'reference/legacy-runtime@{den}x.png';a=REPO/f'docs/art-rebuild-20261019/approved-v5/fx-source-audit/current-runtime/C01-FX-07@{den}x.png';assert Image.open(p).tobytes()==Image.open(a).tobytes()
 referenceChecks.append({'path':str(p.relative_to(R)),'sourceAuditSnapshot':str(a.relative_to(REPO)),'sha256':sha(p),'decodedPixelsExact':True})
 p=R/f'reference/current-runtime-at-worker-audit@{den}x.png';curr=REPO/f'packages/story-candidate-01/assets/final-fantasy-v1/{dr}/fx/fx-07.png'
 referenceChecks.append({'path':str(p.relative_to(R)),'sha256':sha(p),'repositorySource':str(curr.relative_to(REPO)),'matchesCurrentRepositoryRuntimeAtFinalization':Image.open(p).tobytes()==Image.open(curr).tobytes(),'snapshotContext':'Snapshot captured before worker generation; no claim that integrator has installed the new sources.'})
write(R/'reference/reference-verification.json',referenceChecks)
snaps=[]
for path,lo,hi in [('packages/story-candidate-01/src/presentation/candidate-01-bindings.ts',153,177),('packages/story-candidate-01/src/presentation/candidate-01-runtime.ts',232,269),('packages/game-ui/src/ui/board.ts',611,646),('packages/game-ui/src/art/svg-board-surface.ts',198,219),('packages/game-ui/src/art/svg-board-surface.ts',387,400),('packages/game-ui/src/ui/__tests__/candidate-fx-playback.test.ts',1,53)]:
 p=REPO/path;lines=p.read_text().splitlines();snaps.append({'path':path,'sha256':sha(p),'startLine':lo,'endLine':hi,'excerpt':'\n'.join(f'{i+1}: {lines[i]}' for i in range(lo-1,min(hi,len(lines))))})
write(R/'reference/consumer-source-snapshots.json',snaps)
e['sourceCanvasRegistration']='Full source canvas and pivot627,627 are the authority. Authored ground settles low in the canvas around y1120-1178. Alpha32 bottom differences are about one logical pixel across changing shapes, not a whole-image origin shift. No per-frame bbox, fit, crop or translate has been applied.'
e['actualRuntimeAnchor']=[16,16];e['sourceHistoricalAnchor']=[16,24];e['anchorNote']='Source audit preserves legacy metadata anchor16,24. Current manifest and actual target-hit consumer now use16,16; currentRuntimeRecord captures that actual record.'
e['sourceReferenceVerification']='reference/reference-verification.json';e['consumerCodeEvidence']='reference/consumer-source-snapshots.json';e['noAuthoredFade']='Final source is a compact opaque settled heap. Board removal at420ms is separate from intrinsic source-alpha fading.'
write(R/'source-evidence.json',e)
metrics=json.loads((R/'qa/qa-metrics.json').read_text());metrics['registrationNote']=e['sourceCanvasRegistration'];write(R/'qa/qa-metrics.json',metrics)
for i,phase in enumerate(phases):
 d=R/phase;write(d/'source-evidence.json',{'topicId':'C01-FX-07','phase':phase,'sourcePhaseIndex':i,'sourceBoard':'fx-atlas-02.png','sourceRow1Based':3,'sourceColumn1Based':i+1,'audit':'../source-evidence.json','auditSha256':sha(R/'source-evidence.json'),'referenceHashes':'reference-hashes.json'})
 a=np.array(Image.open(d/'master.png').getchannel('A'));row=metrics['frames'][i];row['opaquePixelCount']=int(np.count_nonzero(a==255));row['nearOpaque240to255Count']=int(np.count_nonzero(a>=240));row['boundsAlpha32Logical']=[2+x*28/1254 for x in row['boundsAlpha32']]
 for den in (1,2):
  aa=np.array(Image.open(R/f'qa/{phase}@{den}x.png').getchannel('A'));row['exports'][str(den)]['boundsAlpha32']=Image.fromarray((aa>=32).astype('uint8')).getbbox()
 write(R/f'qa/{phase}-detail-metrics.json',row)
write(R/'integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-07':{'kind':'fx-phases-v1','phases':[{'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-07/{p}/master.png','sha256':sha(R/p/'master.png')} for p in phases],'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}}}})
review={'topicId':'C01-FX-07','label':'攻城碎屑','subject':'siege wood and stone debris','selectedPhaseOrder':phases,'method':'built-in image_gen','sourceCount':4,'attemptCount':4,'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'perFrameCropFitShiftPaintCleanup':False,'actualCurrentRuntimeAnchor':[16,16],'acceptedSourceEdgeMaxima':[x['edgeAlphaMax'] for x in metrics['frames']],'acceptedSourceEdgeNonzeroSamples':[x['edgeAlphaNonzeroSamples'] for x in metrics['frames']],'sourceResidueDisclosure':'First two perimeters contain only alpha1 numerical residue,20 and52 samples. Last two perimeters are exactly zero. Exterior is visually clean on full1254 black/white/green composites; source files are untouched.','allNativeTwoPixelBordersAlpha0':True,'nativeBackgrounds':['black','white','bright-stone','green-ground','dark-earth'],'fullSourceBackgroundsActuallyInspected':['black','white','green-ground'],'semanticReview':'Dense airborne large brown timber and gray stones descend to fewer large pieces, then sparse small low remnants, then a compact solid settled heap. Progression follows original board2 row3 and is not reversed into growth. No symbols, panels or haze added.','registrationReview':e['sourceCanvasRegistration'],'nativeReadability':'Four silhouettes and downward reduction are distinct at32 and64; tiny last-remnant chips are intentionally faint at32, matching sparse source semantics. Settled heap remains a low compact readable cluster.','finalPhaseMaxAlpha':metrics['frames'][-1]['maxAlpha'],'finalPhaseOpaquePixels':int(np.count_nonzero(np.array(Image.open(R/phases[-1]/'master.png').getchannel('A'))==255)),'finalPhaseAlphaMassRelativeToSparseRemnants':metrics['frames'][-1]['alphaMass']/metrics['frames'][2]['alphaMass'],'finalPhaseDoesNotIntrinsicallyFade':True,'rejectedAttempts':[],'APNGVerification':'12 APNGs,48 decoded frames match exports,exact1/12+1/12+1/12+17/100 seconds=420ms,one play. Offline evidence only.','actualLiveGameObserved':False,'runtimeAndGitWrites':False,'reviewedBy':'native image inspection and deterministic source/alpha/transform/APNG checks'}
write(R/'final-review.json',review)
(R/'README.md').write_text('''# FX07 siege-debris source quartet

C01-FX-07 攻城碎屑: airborne-burst → falling-pieces → falling-remnants → settled-heap. Original identity is FX atlas02 row3: brown broken timber and gray stone fragments. The final phase is a solid settled heap, not a fade. All four independent selected sources were generated with the built-in image_gen tool.

## Source and actual consumer

source-evidence.json and reference/ preserve original full board, exact full-grid cells, row, inset crops, legacy-alpha references and32/64 runtime snapshots. Reference verification proves decoded equality to original repository board/cells and audit snapshots. Consumer source snapshots preserve actual bindings: c01.knight-charge, c01.satchel-charge, c01.forge-burst. The current manifest anchor is[16,16], unlike the historical original audit metadata[16,24]. candidate01FxPicture uses the current anchor and4 frames at12fps, nonlooping. BoardView.animateHit places the effect at target-cell center and removes it after420ms. The white circle and damage number remain separate renderer overlays, not baked into this source.

## Authoring and export

Each phase directory contains untouched byte-identical raw.png/master.png, prompt.txt, exact request.json, reference hashes, source evidence and hashes. Four generation attempts produced four accepted phases; no rejected generation or hidden cleanup. Reference crops are source audit inputs only.

One full-canvas transform:1254×1254 →28×28, origin[2,2] on32×32; sourcePivot[627,627] →logical[16,16].2x uses56×56 at[4,4] on64×64. No per-phase fit, crop, translation, painted repair, color-keying, alpha cleanup or opacity multiplication. Natural silhouette bounds and low settled ground vary about one logical pixel between phases and do not drive the transform. qa/registration-pivot-proof.png makes the common origin visible.

## Verified offline

- Full1254 black/white/green-ground composites inspected for every phase; native32/64 reviewed on black, white, bright stone, green ground and dark earth
- Source edge maxima are[1,1,0,0], with[20,52,0,0] nonzero perimeter samples. Alpha1 numerical residue in first two sources was visually reviewed and preserved. No matte panel or exterior haze
- All32/64 final2px borders are alpha0. First phase reads as a dense burst, second as descending large chunks, third sparse low chips, fourth a solid low wood/stone heap
- Last phase preserves opaque cores; runtime removal at420ms does not imply source fading
-12 APNGs each contain4 distinct decoded frames matching exports, durations1/12,1/12,1/12,17/100 seconds,420ms total, one play,170ms final hold. This is offline timing evidence, not live-game observation

## Integrator handoff

integration-catalog.json lists exact source paths/hashes and transform. final-review.json and final-quartet.json summarize acceptance; deliverable-manifest.json hashes every package file. Start visual review with qa/native-32-64-five-backgrounds.png and qa/registration-pivot-proof.png. Integrator owns runtime copy, binding, tests and Git. This worker made no repository/runtime/Git edits and claims no live-game test.
''')
write(R/'final-quartet.json',{'topicId':'C01-FX-07','readyForIntegrationReview':True,'phaseOrder':phases,'hashes':{p:json.loads((R/p/'hashes.json').read_text()) for p in phases},'review':'final-review.json','integrationCatalog':'integration-catalog.json'})
write(R/'failures.json',{'topicId':'C01-FX-07','generationAttempts':4,'selectedSources':4,'failedOrRejectedGenerations':[],'scopeNote':'No code/runtime validation executed by this source-only worker; integrator owns binding and live-game acceptance.'})
write(R/'deliverable-manifest.json',{'topicId':'C01-FX-07','root':str(R),'phaseOrder':phases,'method':'built-in image_gen','sourceCount':4,'attemptCount':4,'repoWrites':False,'files':[{'path':str(p.relative_to(R)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(R.rglob('*')) if p.is_file() and p.name!='deliverable-manifest.json']})
print(json.dumps({'ready':True,'files':len(json.loads((R/'deliverable-manifest.json').read_text())['files']),'phaseHashes':{p:sha(R/p/'master.png') for p in phases},'sourceAlphaMasses':[x['alphaMass'] for x in metrics['frames']]},indent=2))
