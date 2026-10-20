from pathlib import Path
import hashlib,json
r=Path(__file__).parent;sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();write=lambda n,x:(r/n).write_text(json.dumps(x,indent=2,ensure_ascii=False)+'\n')
qa=json.loads((r/'qa/qa-metrics.json').read_text());mo=json.loads((r/'qa/motion-and-registration-proof.json').read_text());ap=json.loads((r/'qa/apng-timing-verification.json').read_text());edge=json.loads((r/'qa/source-edge-composite-proof.json').read_text());phases=qa['phaseOrder'];entries=[]
for p in phases:
 d=r/p;assert sha(d/'raw.png')==sha(d/'master.png')
 for n in ['request.json','prompt.txt','provenance.json','hashes.json','source-evidence.json','reference-hashes.json','reference-snapshots.json']:assert(d/n).is_file()
 for ref in json.loads((d/'reference-snapshots.json').read_text()):assert sha(d/ref['snapshot'])==ref['sha256']
 prov=json.loads((d/'provenance.json').read_text());assert sha(Path(prov['generatedSource']))==sha(d/'master.png')
 entries.append({'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-19/{p}/master.png','sha256':sha(d/'master.png')})
status='accepted-source-static-phase-candidate';accept='Parent accepted full native and terminal sequence on 2026-10-20T06:27:41Z: gray-green smoke rolls open, breaks apart and ends with leaves/small wisps.'
write('integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-19':{'kind':'fx-phases-v1','phases':entries,'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}}},'note':'Exact phase order and full source/static quartet accepted by parent. Source paths are proposed repository destinations. Preserve 12 fps, loop=false, normal blend and anchor [16,16]. Catalog-only; no hiding mechanism, visibility change, enemy marker or assumed consumer lifetime.'})
write('final-quartet.json',{'topicId':'C01-FX-19','status':status,'sources':[{'phase':e['phase'],'raw':str(r/e['phase']/'raw.png'),'master':str(r/e['phase']/'master.png'),'sha256':e['sha256'],'provenance':str(r/e['phase']/'provenance.json'),'proposedRepositoryDestination':e['source']} for e in entries],'independentlyAuthoredDrawings':4,'imagegenCalls':4,'rejectedAttempts':[]})
(r/'qa/PHASE-REVIEW.md').write_text('''# FX19 concealment phase review

The original atlas05 row3, full source board and current HD runtime strip were inspected. The original starts already dense, so no empty onset was invented. The parent approved dense-veil → curling-veil → veil-breakup → leaf-wisps and accepted the complete native and terminal static sequence on 2026-10-20 at 06:27:41 UTC.

- Dense veil is a broad low gray-green cloud with a high rolling curl, olive leaves and a few side puffs.
- Curling veil opens deeper transparent notches on both sides and reshapes the high and low rolls while keeping the same general emission region.
- Breakup is a narrower central smoky ribbon with separated lobes, puffs and leaves. Large empty regions replace the earlier solid cloud.
- Leaf wisps leaves only three olive leaves and four small disconnected gray wisps. No renewed cloud, actor silhouette, eye symbol, target marker or ground object appears.

The first two visible lower bounds are source Y 1149 and 1152, a difference of 0.067 logical pixels. Later fragments intentionally no longer form a stable contact silhouette because this is floating dissipating smoke, not a grounded object. All four images retain the same canvas and transform. No per-phase fitting or image shifts were applied.

Alpha mass relative to the dense source is 100%, 85.2%, 57.4% and 6.8%. Largest connected alpha>=128 component area is 100%, 83.7%, 35.9% and 1.4%. These metrics support the visible breakup but are not alone proof of animation quality. They include leaves and are not a perfect smoke-material segmentation. The third phase retains more total mass than the approximate prompt target, while its disconnected geometry visibly fulfils the source phase. Exact vector targets or continuous identity for every leaf are not claimed.

Full source black/white composites and native 32/64 outputs on six backgrounds were reviewed. Each source perimeter reaches only alpha 1, with actual edge composite deviation at most 1 and clean visible margins. All final logical 2px padding is alpha 0. The sources are byte-identical to their raw generated outputs; no opacity correction, alpha cleanup, crop, per-frame scale, translation or synthetic fade was used.

## Catalog and visibility boundary

No literal current FX19 consumer was found in package/app TypeScript, TSX or nonmanifest JSON. Existing visibility rules own unit visibility, the overlay projects hiddenUnits, and BoardView/backends honor it. Those rules were not changed and no asset marker was added at hidden enemy positions. The label concealment does not authorize a new gameplay mechanism.

The exported clip is four equal 1/12-second frames, nonloop, single-play APNG with nominal duration 1/3 second. No 420 ms hit or 500 ms heal lifetime, extra hold frame or removal rule is inferred. A future explicitly authorized consumer would own display/removal. All 28 clip/slow diagnostic APNGs verify every decoded frame against expected exported pixels. Browser/live playback was not observed.
''')
review={'topicId':'C01-FX-19','label':'隐蔽','status':status,'phaseOrder':phases,'phaseOrderApproval':'Parent approved exact phase order on 2026-10-20T06:14:25Z','parentFinalStaticPhaseAcceptance':accept,'method':'built-in image_gen','sourceCount':4,'imagegenCalls':4,'rejectedAttempts':[],'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'commonPlacement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]},'perPhaseFitCropShiftFadeAlphaCleanup':False,'sourceEdgeMaxima':[x['edgeAlphaMax'] for x in edge],'sourceEdgeNonzeroSamples':[x['edgeAlphaNonzero'] for x in edge],'conditionalAlpha1Proof':'qa/source-edge-composite-proof.json','allNativeLogical2pxPaddingAlpha0':True,'fullSourceBlackWhiteReviewed':True,'nativeFullAndTerminalReviewed':True,'nativeBackgrounds':['black','white','bright-stone','green-ground','dark-earth','checker'],'subjectPreserved':'Gray-green leafy smoke, initially dense, then rolling open and breaking into separated ribbons/puffs before three leaves and four small wisps remain.','alphaMassRelativeToDense':mo['alphaMassRelativeToDense'],'largestComponentAreaRelativeToDense':mo['largestComponentAreaRelativeToDense'],'firstPairBottomVariationLogicalPx':mo['firstPairBottomVariationLogicalPx'],'phaseReview':'qa/PHASE-REVIEW.md','motionProof':'qa/motion-and-registration-proof.json','sourceHashProof':'final-quartet.json','semanticCaveats':mo['caveats'],'APNGVerification':{'proof':'qa/apng-timing-verification.json','files':len(ap['files']),'decodedFrames':sum(x['frameCount'] for x in ap['files']),'exact12fpsFiles':14,'slowDiagnosticFiles':14,'everyDecodedFrameMatchesExpected':all(x['decodedFramesMatchExports'] for x in ap['files']),'exactFrameSeconds':'1/12','nominalClipSeconds':'1/3','numPlays':1,'loop':False,'encodedFinalHold':False,'removalRule':None,'lifetimeOwner':'Future consumer; none currently bound'},'consumerAudit':'consumer-audit.json','authoredSceneConsumer':None,'gameplayVisibilityChanged':False,'enemyMarkerOrActorSilhouetteAuthored':False,'hit420msAssumed':False,'heal500msAssumed':False,'browserObserved':False,'browserBlocker':'Established browser/socket restriction; no repeated browser attempts were made.','liveGameObserved':False,'repoRuntimeGitWrites':False}
write('final-review.json',review);contract=json.loads((r/'generation-contract.json').read_text());contract['status']=status;contract['parentFinalStaticAcceptance']=accept;write('generation-contract.json',contract)
(r/'README.md').write_text('''# FX19 concealment

Four independent 1254×1254 RGBA sources are ready with raw outputs, exact requests, reference snapshots and hashes. Parent source/static review accepted the complete dense-to-sparse sequence.

- `integration-catalog.json`: proposed repository destinations and exact hashes
- `final-quartet.json`: verified staging source paths
- `final-review.json`: acceptance state, proof and caveats
- `consumer-audit.json`: catalog-only search and existing visibility ownership
- `qa/full-and-terminal-labeled.png`: four phases and terminal comparison
- `qa/native-32-64-six-backgrounds.png`: native outputs on six backgrounds
- `qa/all-source-black-white.png`: actual full-source composites
- `qa/PHASE-REVIEW.md`: smoke semantics and registration review
- `qa/motion-and-registration-proof.json`: source geometry and dissipation metrics
- `qa/apng-timing-verification.json`: 28 APNGs and 112 matching decoded frames

Phase order: dense-veil → curling-veil → veil-breakup → leaf-wisps. Preserve 12 fps, loop=false, normal blend and anchor [16,16]. Use the same 28/1254 full-canvas transform at origin [2,2]. Final logical 2px padding is alpha 0, source alpha-1 perimeter is conditionally composite-verified, and raw/master bytes are identical. No alpha edits, per-frame fitting or synthesized motion were applied.

Catalog-only: no current literal consumer or specific display/removal lifetime. The artwork does not add a concealment mechanic or a marker at an unseen enemy. Browser/live playback remains unobserved.
''')
files=[{'path':str(p.relative_to(r)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(r.rglob('*')) if p.is_file() and p.name not in ['deliverable-manifest.json','link-verification.json']];write('deliverable-manifest.json',{'root':str(r),'status':status,'files':files,'selectedSources':entries})
links=[r/'final-review.json',r/'final-quartet.json',r/'integration-catalog.json',r/'consumer-audit.json',r/'qa/PHASE-REVIEW.md',r/'qa/motion-and-registration-proof.json',r/'qa/apng-timing-verification.json',r/'qa/source-edge-composite-proof.json',r/'qa/full-and-terminal-labeled.png',r/'qa/native-32-64-six-backgrounds.png',r/'qa/all-source-black-white.png']+[r/e['phase']/'master.png' for e in entries]+[r/'qa'/f['file'] for f in ap['files']]
assert all(p.exists() for p in links);write('link-verification.json',{'allExistingStagingLinksVerified':True,'verifiedPaths':[str(p) for p in links],'proposedRepositoryPaths':'Not created by this worker; clearly marked integration destinations.'});print(json.dumps({'status':status,'files':len(files),'verifiedLinks':len(links),'sources':entries},indent=2))
