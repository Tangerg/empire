from pathlib import Path
from PIL import Image
import hashlib,json,numpy as np
R=Path(__file__).parent
REPO=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
write=lambda p,o:p.write_text(json.dumps(o,indent=2,ensure_ascii=False)+'\n')
phases=['low-ring','rising-ring','sealed-ring','fading-fragments']
# Reference crops only; no generated artwork modified.
e=json.loads((R/'source-evidence.json').read_text());orig=REPO/e['originalBoard']['source'];board=Image.open(orig)
referenceChecks=[]
for i,s in enumerate(e['originalTopic']['slots']):
 p=R/f'reference/original-full-cell-f{i}.png';board.crop(s['fullGridBoundsXYXY']).save(p)
 referenceChecks.append({'phaseIndex':i,'boundsXYXY':s['fullGridBoundsXYXY'],'path':str(p.relative_to(R)),'decodedEqualOriginalBoardCell':Image.open(p).tobytes()==board.crop(s['fullGridBoundsXYXY']).tobytes(),'sha256':sha(p)})
p=R/'reference/original-board.png'
referenceChecks.append({'path':str(p.relative_to(R)),'repositorySource':str(orig.relative_to(REPO)),'repositoryFileSha256':sha(orig),'savedFileSha256':sha(p),'decodedPixelsExact':Image.open(p).tobytes()==Image.open(orig).tobytes()})
auditRoot=Path('/workspace/shared/empire-art-rebuild/approved-v5/fx-source-audit')
for density,dr in [(1,'runtime'),(2,'runtime-hd')]:
 legacy=R/f'reference/legacy-runtime@{density}x.png';old=auditRoot/f'current-runtime/C01-FX-06@{density}x.png'
 current=R/f'reference/current-runtime-at-worker-audit@{density}x.png';repoCurrent=REPO/f'packages/story-candidate-01/assets/final-fantasy-v1/{dr}/fx/fx-06.png'
 referenceChecks.append({'path':str(legacy.relative_to(R)),'sourceAuditSnapshot':str(old),'sourceAuditGitHead':'79904dc748b7e7e6ede3d3ae1e8038b45741b0fd','savedFileSha256':sha(legacy),'decodedPixelsExact':Image.open(legacy).tobytes()==Image.open(old).tobytes(),'matchesNowIntegratedRuntime':Image.open(legacy).tobytes()==Image.open(repoCurrent).tobytes(),'note':'Legacy original audit snapshot intentionally differs from newly integrated remake runtime.'})
 referenceChecks.append({'path':str(current.relative_to(R)),'repositorySource':str(repoCurrent.relative_to(REPO)),'repositoryFileSha256':sha(repoCurrent),'savedFileSha256':sha(current),'decodedPixelsExact':Image.open(current).tobytes()==Image.open(repoCurrent).tobytes(),'matchesWholeCanvasQaStrip':Image.open(current).tobytes()==Image.open(R/f'qa/phase-strip@{density}x.png').tobytes(),'note':'Integrator updated runtime during source package completion; this snapshot is the integrated remake, not the legacy original.'})
write(R/'reference/reference-verification.json',referenceChecks)
sourceSnapshots=[]
for name,start,end in [('packages/story-candidate-01/src/presentation/candidate-01-bindings.ts',153,177),('packages/story-candidate-01/src/presentation/candidate-01-runtime.ts',239,269),('packages/game-ui/src/ui/board.ts',611,645)]:
 p=REPO/name;lines=p.read_text().splitlines();sourceSnapshots.append({'path':name,'fileSha256':sha(p),'startLine':start,'endLine':end,'excerpt':'\n'.join(f'{i+1}: {lines[i]}' for i in range(start-1,end))})
write(R/'reference/consumer-source-snapshots.json',sourceSnapshots)
manifest=REPO/'packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json';current=next(x for x in json.loads(manifest.read_text())['assets'] if x['topicId']=='C01-FX-06')
e['currentRuntimeMetadata']={'path':str(manifest.relative_to(REPO)),'fileSha256':sha(manifest),'record':current};e['sourceReferenceVerification']='reference/reference-verification.json';e['consumerCodeEvidence']='reference/consumer-source-snapshots.json';e['registrationNote']='Full source canvases and shared pivot are preserved. Natural asymmetric fragment bounds have different centers; they are diagnostic, not a crop, fit or shift instruction. All native central4x4 logical pixels remain alpha0.'
write(R/'source-evidence.json',e)
for i,phase in enumerate(phases):
 d=R/phase;write(d/'source-evidence.json',{'topicId':'C01-FX-06','phase':phase,'sourcePhaseIndex':i,'sourceBoard':'fx-atlas-02.png','sourceRow1Based':2,'sourceColumn1Based':i+1,'audit':'../source-evidence.json','auditSha256':sha(R/'source-evidence.json'),'referenceHashes':'reference-hashes.json'})
 metrics=json.loads((R/'qa/qa-metrics.json').read_text());row=metrics['frames'][i];a=np.array(Image.open(d/'master.png').getchannel('A'))
 row['opaquePixelCount']=int(np.count_nonzero(a==255));row['centralSource100x100AlphaCounts']={str(k):int(v) for k,v in zip(*np.unique(a[577:677,577:677],return_counts=True))}
 for den in (1,2):
  aa=np.array(Image.open(R/f'qa/{phase}@{den}x.png').getchannel('A'));row['exports'][str(den)]['central4x4LogicalAlphaMax']=int(aa[14*den:18*den,14*den:18*den].max())
 write(R/f'qa/{phase}-detail-metrics.json',row)
write(R/'integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-06':{'kind':'fx-phases-v1','phases':[{'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-06/{p}/master.png','sha256':sha(R/p/'master.png')} for p in phases],'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}}}})
metrics=json.loads((R/'qa/qa-metrics.json').read_text());final=metrics['frames'][-1];peak=metrics['frames'][2]
review={'topicId':'C01-FX-06','selectedPhaseOrder':phases,'method':'built-in image_gen','sourceCount':4,'attemptCount':5,'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'perFrameCropFitShiftPaintCleanup':False,'actualCurrentRuntimeAnchor':[16,16],'acceptedSourceEdgeMaxima':[x['edgeAlphaMax'] for x in metrics['frames']],'acceptedSourceEdgeNonzeroSamples':[x['edgeAlphaNonzeroSamples'] for x in metrics['frames']],'sourceResidueDisclosure':'Peak and final perimeter contain only alpha1 numerical residue, visually clean on black,white and colored composites. Central100x100 source holes contain 1-3 alpha1 pixels; native central4x4 logical regions are fully alpha0. Raw sources retained unchanged.','allNativeTwoPixelBordersAlpha0':True,'allNativeCentral4x4LogicalRegionsAlpha0':True,'nativeBackgrounds':['black','white','bright-stone','green-ground','dark-earth'],'semanticReview':'Low broken oval rises into an assembling angular ring, peaks as thicker interlocking bars with top/bottom diamond lozenges, then disperses into sparse dim angular and diamond fragments. Original geometric symbols retained; no readable runes invented.','finalPhaseMaxAlpha':final['maxAlpha'],'finalPhaseOpaquePixels':0,'finalAlphaMassFractionOfPeak':final['alphaMass']/peak['alphaMass'],'rejectedAttempts':['fading-fragments-rejected-v1'],'APNGVerification':'12 APNGs,48 decoded frames match exports,exact1/12+1/12+1/12+17/100 seconds=420ms,one play. Offline evidence only.','actualLiveGameObserved':False,'runtimeAndGitWrites':False,'reviewedBy':'native image inspection and deterministic source/alpha/transform/APNG checks'}
write(R/'final-review.json',review)
(R/'README.md').write_text('''# FX06 amber oath-fire source quartet

Completed source staging: low-ring → rising-ring → sealed-ring → fading-fragments. Canonical identity is original atlas02 row2: angular golden oath-ring segments with diamond lozenges and an open center. It is distinct from ordinary orange FX05 fire. No letters or readable runes were invented.

## Exact source and consumer audit

`source-evidence.json` preserves the canonical topic audit, original four cell bounds, current metadata, binding and placement evidence. `reference/` includes original full board, full row, four full-grid cells, original/legacy-alpha inset crops, and legacy32/64 strips. Decoded-pixel checks confirm the original board and cells against repository originals, and legacy32/64 strips against the original audit snapshot at79904dc748b7e7e6ede3d3ae1e8038b45741b0fd. The integrator updated runtime during final packaging; separate current-runtime-at-worker-audit snapshots exactly match the new QA strips, while the intentionally preserved legacy strips differ. Actual current metadata anchor is[16,16], four frames at12fps, nonlooping. Four weapon consumers are c01.gray-oath, c01.oath-dispel, c01.inquisitor-flame and c01.oath-bolt. Board animateHit places effect at target-cell center and removes it after420ms. White burst and damage number remain separate runtime overlays.

## Source/export contract

All four phases were separately authored through built-in image_gen. Each selected phase retains byte-identical raw/master1254×1254 RGBA PNG, exact prompt/request, reference hashes and provenance. One final-phase correction was generated because the initial fragments remained too opaque; the rejected source and its exact request/provenance are preserved in fading-fragments-rejected-v1.

One common full-canvas export uses scale28/1254, origin[2,2], sourcePivot[627,627] → logical[16,16]. No individual fit, crop, shift, paint, alpha cleanup, opacity normalization or opacity multiplication. Diagnostic bounds centers differ naturally as fragments assemble; they never drive placement. The canvas and pivot are the registration authority.

## Verified offline

- Native32/64 review on black, white, bright stone, green ground and dark earth shows the distinct four-state growth/closure/breakup/decay and clear empty centers
- All native2px borders and central4×4 logical-pixel regions are alpha0
- First two source perimeters are alpha0. Peak/final contain20/3 alpha1 perimeter samples only, visually clean on black/white/color composites. Each source central100×100 region has1–3 alpha1 samples; these disappear at native size. Nothing was cleaned or erased
- Final phase has genuine generated translucency, maxalpha152, no opaque pixel, and6.65% of peak source alpha mass. It is intentionally faint during the170ms final hold
- All12 APNGs decode into four distinct matching frames with exact durations1/12,1/12,1/12,17/100 seconds,420ms total, one play. These are offline timing evidence, not a live-game claim

## Review and integration

Start with qa/native-32-64-five-backgrounds.png, qa/qa-metrics.json, qa/apng-timing-verification.json and final-review.json. Full-size five-background composites are also preserved. Integration-catalog.json specifies exact phase paths/hashes and placement. The integrator must explicitly register this phase order and retain the existing consumer contract.

No repository, runtime or Git changes were made by this worker. The parent integrator confirmed runtime integration and decoded-pixel equality during packaging. Live-game observation is not asserted by this worker.
''')
write(R/'final-quartet.json',{'topicId':'C01-FX-06','readyForBackup':True,'phaseOrder':phases,'hashes':{p:json.loads((R/p/'hashes.json').read_text()) for p in phases},'review':'final-review.json','integrationCatalog':'integration-catalog.json'})
write(R/'deliverable-manifest.json',{'topicId':'C01-FX-06','root':str(R),'phaseOrder':phases,'method':'built-in image_gen','sourceCount':4,'attemptCount':5,'repoWrites':False,'files':[{'path':str(p.relative_to(R)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(R.rglob('*')) if p.is_file() and p.name!='deliverable-manifest.json']})
print(json.dumps({'ready':True,'files':len(json.loads((R/'deliverable-manifest.json').read_text())['files']),'phaseHashes':{p:sha(R/p/'master.png') for p in phases},'finalAlphaMassFractionOfPeak':review['finalAlphaMassFractionOfPeak']},indent=2))
