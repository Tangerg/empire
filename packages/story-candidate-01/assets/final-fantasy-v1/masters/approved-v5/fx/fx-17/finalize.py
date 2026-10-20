from pathlib import Path
import hashlib,json
r=Path(__file__).parent;sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();write=lambda n,x:(r/n).write_text(json.dumps(x,indent=2,ensure_ascii=False)+'\n')
qa=json.loads((r/'qa/qa-metrics.json').read_text());mo=json.loads((r/'qa/motion-and-registration-proof.json').read_text());ap=json.loads((r/'qa/apng-timing-verification.json').read_text());edge=json.loads((r/'qa/source-edge-composite-proof.json').read_text());life=json.loads((r/'qa/consumer-lifecycle-verification.json').read_text());phases=qa['phaseOrder'];entries=[]
for p in phases:
 d=r/p;assert sha(d/'raw.png')==sha(d/'master.png')
 for n in ['request.json','prompt.txt','provenance.json','hashes.json','source-evidence.json','reference-hashes.json','reference-snapshots.json']:assert(d/n).is_file()
 for ref in json.loads((d/'reference-snapshots.json').read_text()):assert sha(d/ref['snapshot'])==ref['sha256']
 prov=json.loads((d/'provenance.json').read_text());assert sha(Path(prov['generatedSource']))==sha(d/'master.png')
 entries.append({'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-17/{p}/master.png','sha256':sha(d/'master.png')})
status='accepted-source-static-phase-candidate'
write('integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-17':{'kind':'fx-phases-v1','phases':entries,'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}}},'note':'Exact phase order and full source/static quartet accepted by parent. Paths are proposed repository destinations. Preserve 12 fps, loop=false, normal blend, anchor [16,16], and existing BoardView heal lifetime of 500 ms. Do not apply the separate 420 ms hit rule. Runtime integration is owned by the parent.'})
write('final-quartet.json',{'topicId':'C01-FX-17','status':status,'sources':[{'phase':e['phase'],'raw':str(r/e['phase']/'raw.png'),'master':str(r/e['phase']/'master.png'),'sha256':e['sha256'],'provenance':str(r/e['phase']/'provenance.json'),'proposedRepositoryDestination':e['source']} for e in entries],'independentlyAuthoredDrawings':4,'imagegenCalls':4,'rejectedAttempts':[]})
(r/'qa/PHASE-REVIEW.md').write_text('''# FX17 healing phase review

The original runtime strip and atlas05 row1 were inspected directly. The four authored phases are healing-onset → leaf-spiral → healing-peak → orbit-dissipation. The parent accepted the peak baseline first, then the complete native and terminal comparison on 2026-10-20 at 06:11:02 UTC.

1. Onset has a compact thin sage orbit, the olive medical plus, two crossed beige bandage pairs and sparse leaves.
2. Leaf spiral opens and develops the inner ribbons, with more leaves and outward bandage positions. Its outer envelope is already close to peak width, but its ribbons are more open and total alpha mass is lower.
3. Peak has the fullest nested sage orbit, still the same central medical plus and beige bandages, with more leaves around the arc.
4. Dissipation removes the plus and both bandage pairs. Five olive leaves and separate thin broken arcs remain. The central source region formerly occupied by the plus has zero pixels at alpha >=128. There is no leaf bed, fire, ground patch, filled backing disc or renewed pulse.

The first three plus centers are (627.5,736.5), (627,720.5), and (627.5,707.5) source pixels. The maximum displacement is 29 source pixels, or 0.6475 logical pixels, mostly upward. The parent accepted this asset-specific native static variation. No pixels were shifted or normalized to conceal it. Leaf identities are not tracked as exact continuous trajectories; the evidence shows changing authored orbit shapes and poses, not proof that every leaf follows an exact path.

Total source alpha mass rises from 147,078.5 to 308,704.6 to 380,909.5 pixel equivalents, then falls to 69,986.9. This supports visibly different geometry; distinct hashes or alpha totals alone are not a motion acceptance criterion. All four full source black/white composites and native outputs on six backgrounds were viewed. The alpha-1 source perimeter samples are conditionally accepted with actual clean composites and edge deviation at most 1. Native logical 2px padding remains alpha 0.

## Actual heal consumer

The heal event resolves the healed target and calls BoardView.animateHeal. The presentation binding is C01-FX-17, and the adapter selects the HD source while retaining 32-square logical frames and anchor [16,16]. The strip starts automatically and plays four frames at 12 fps without looping. Frame 4 starts at 250 ms and remains until the normal-motion 500 ms tween completes and removes the drawing. Its total dwell is 250 ms, including 166⅔ ms after the nominal 333⅓ ms clip completion. The independently animated +amount text is not baked into the source; the consumer does not fade or scale the strip itself. The separate 420 ms hit lifecycle does not apply.

The ordinary clip APNGs contain four equal 1/12-second authored frames and play once. Separate consumer diagnostics use visible durations 1/12,1/12,1/12,1/4 seconds and then a blank observation sample beginning at 500 ms. The blank depicts removal and is not a fifth authored phase. It lasts 1/12 second only for inspection. All decoded pixels match the selected exports or the explicitly empty removal sample.

The parent reported that actual BoardView heal hold/removal and separate hit timing regression tests passed before new-source binding. This worker does not claim those source masters are already integrated. Browser/live playback was not observed. Reduced-motion and cancellation paths are documented in consumer-audit.json and are distinct from the normal-motion 500 ms contract.
''')
review={'topicId':'C01-FX-17','label':'治疗','status':status,'phaseOrder':phases,'phaseOrderApproval':'Parent approved nonloop healing phase design on 2026-10-20T05:58:28Z and confirmed exact phase names were added to the exporter contract by 06:03:50Z.','peakBaselineApproval':'Parent accepted actual source black/white and native32/64 baseline on 2026-10-20T06:03:50Z, retaining the observed center near source y710.','parentFinalStaticPhaseAcceptance':'Accepted on 2026-10-20T06:11:02Z after four-phase native and terminal review; 0.65 logical-pixel center variation explicitly accepted for this asset.','method':'built-in image_gen','sourceCount':4,'imagegenCalls':4,'rejectedAttempts':[],'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'commonPlacement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]},'perPhaseFitCropShiftFadeAlphaCleanup':False,'sourceEdgeMaxima':[x['edgeAlphaMax'] for x in edge],'sourceEdgeNonzeroSamples':[x['edgeAlphaNonzero'] for x in edge],'conditionalAlpha1Proof':'qa/source-edge-composite-proof.json','allNativeLogical2pxPaddingAlpha0':True,'fullSourceBlackWhiteReviewed':True,'nativeFullAndTerminalReviewed':True,'consumerHoldRemovalSamplesReviewed':True,'nativeBackgrounds':['black','white','bright-stone','green-ground','dark-earth','checker'],'subjectPreserved':'Olive medical plus, beige crossed bandages, sage orbital ribbons and olive leaves; terminal leaves and broken arcs after the plus and bandages disappear.','plusCentersSource':mo['plusCentersSource'],'plusCenterMaximumPairDistanceLogicalPx':mo['plusCenterMaximumPairDistanceLogicalPx'],'phaseReview':'qa/PHASE-REVIEW.md','motionProof':'qa/motion-and-registration-proof.json','sourceHashProof':'final-quartet.json','semanticCaveats':mo['caveats'],'APNGVerification':{'proof':'qa/apng-timing-verification.json','files':len(ap['files']),'decodedFrames':sum(x['frameCount'] for x in ap['files']),'exact12fpsFiles':14,'slowDiagnosticFiles':14,'everyDecodedFrameMatchesExpected':all(x['decodedFramesMatchExports'] for x in ap['files']),'exactAuthoredFrameSeconds':'1/12','nominalClipSeconds':'1/3','numPlays':1,'loop':False,'encodedFinalHoldInAuthoredClip':False},'consumerLifecycleVerification':{'proof':'qa/consumer-lifecycle-verification.json','diagnosticFiles':len(life['files']),'decodedFrames':life['verifiedFrames'],'effectLifetimeMs':500,'terminalFrameStartMs':250,'terminalVisibleMs':250,'removalAtMs':500,'blankIsDiagnosticOnly':True,'hit420msApplied':False,'independentAmountOverlayBakedIntoArt':False,'parentReportedRuntimeRegression':life['parentReportedRuntimeRegression'],'newSourceBindingClaimed':False},'browserObserved':False,'browserBlocker':'Established browser/socket restriction; no repeated browser attempts were made.','authoredSceneConsumer':'Existing C01-FX-17 heal presentation binding; new source registration owned by parent','liveGameObserved':False,'repoRuntimeGitWrites':False}
write('final-review.json',review)
contract=json.loads((r/'generation-contract.json').read_text());contract['status']=status;contract['parentFinalStaticAcceptance']=review['parentFinalStaticPhaseAcceptance'];write('generation-contract.json',contract)
(r/'README.md').write_text('''# FX17 healing

Four independent 1254×1254 RGBA phase masters are ready, with unchanged raw outputs, exact requests, reference snapshots and hashes. The parent accepted the peak baseline and the complete source/static quartet.

- `integration-catalog.json`: intended repository paths and exact hashes
- `final-quartet.json`: verified current staging paths
- `final-review.json`: accepted gates and remaining boundaries
- `consumer-audit.json`: current heal event, binding, adapter and 500 ms lifetime code excerpts
- `qa/full-and-terminal-labeled.png`: full sequence and terminal dissipation
- `qa/native-32-64-six-backgrounds.png`: native outputs on six backgrounds
- `qa/all-source-black-white.png`: full source transparency composites
- `qa/PHASE-REVIEW.md`: medical/orbit semantics and measured registration caveat
- `qa/motion-and-registration-proof.json`: actual plus centers and authored phase diagnostics
- `qa/apng-timing-verification.json`: 28 single-play clip/slow diagnostics, 112 exact decoded frames
- `qa/consumer-lifecycle-verification.json`: 14 consumer diagnostics, 70 exact decoded frames
- `qa/consumer-500ms-hold-removal-samples.png`: displayed final phase from 250 through 499 ms, empty at 500 ms

Use healing-onset → leaf-spiral → healing-peak → orbit-dissipation. Preserve 12 fps, loop=false, normal blend and anchor [16,16]. The existing heal consumer owns the 500 ms lifetime, with a 250 ms final-frame dwell. The 420 ms hit rule is separate. The additional blank in consumer diagnostics depicts removal and is not an authored fifth phase.

All sources use one full-canvas transform, 28/1254 at origin [2,2]. Raw/master bytes match, no alpha edits or per-frame fitting were applied, and final logical 2px padding is alpha 0. The 0.65 logical-pixel medical-plus variation is an explicit asset-specific accepted static caveat. All 182 APNG decoded samples match their exports or the empty removal sample.

Existing heal runtime tests were reported passed by the parent before new-source binding. This package alone does not claim deployment or observed browser/live playback.
''')
files=[{'path':str(p.relative_to(r)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(r.rglob('*')) if p.is_file() and p.name not in ['deliverable-manifest.json','link-verification.json']]
write('deliverable-manifest.json',{'root':str(r),'status':status,'files':files,'selectedSources':entries})
links=[r/'final-review.json',r/'final-quartet.json',r/'integration-catalog.json',r/'consumer-audit.json',r/'qa/PHASE-REVIEW.md',r/'qa/motion-and-registration-proof.json',r/'qa/apng-timing-verification.json',r/'qa/consumer-lifecycle-verification.json',r/'qa/source-edge-composite-proof.json',r/'qa/full-and-terminal-labeled.png',r/'qa/native-32-64-six-backgrounds.png',r/'qa/all-source-black-white.png',r/'qa/consumer-500ms-hold-removal-samples.png']+[r/e['phase']/'master.png' for e in entries]+[r/'qa'/f['file'] for f in ap['files']+life['files']]
assert all(p.exists() for p in links);write('link-verification.json',{'allExistingStagingLinksVerified':True,'verifiedPaths':[str(p) for p in links],'proposedRepositoryPaths':'Not created by this worker; clearly marked integration destinations.'});print(json.dumps({'status':status,'files':len(files),'verifiedLinks':len(links),'sources':entries},indent=2))
