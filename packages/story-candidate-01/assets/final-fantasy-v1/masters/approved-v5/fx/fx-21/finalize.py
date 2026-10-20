from pathlib import Path
import hashlib,json
r=Path(__file__).parent;sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();write=lambda n,x:(r/n).write_text(json.dumps(x,indent=2,ensure_ascii=False)+'\n')
qa=json.loads((r/'qa/qa-metrics.json').read_text());mo=json.loads((r/'qa/motion-and-registration-proof.json').read_text());ap=json.loads((r/'qa/apng-timing-verification.json').read_text());edge=json.loads((r/'qa/source-edge-composite-proof.json').read_text());phases=qa['phaseOrder'];entries=[]
for p in phases:
 d=r/p;assert sha(d/'raw.png')==sha(d/'master.png')
 for n in ['request.json','prompt.txt','provenance.json','hashes.json','source-evidence.json','reference-hashes.json','reference-snapshots.json']:assert(d/n).is_file()
 for ref in json.loads((d/'reference-snapshots.json').read_text()):assert sha(d/ref['snapshot'])==ref['sha256']
 prov=json.loads((d/'provenance.json').read_text());assert sha(Path(prov['generatedSource']))==sha(d/'master.png')
 entries.append({'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-21/{p}/master.png','sha256':sha(d/'master.png')})
status='accepted-source-static-phase-candidate';accept='Parent accepted full native and terminal sequence on 2026-10-20T06:39:54Z: stone/wood scatter, warm impact dust, then sparse rubble and settling dust.'
write('integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-21':{'kind':'fx-phases-v1','phases':entries,'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}}},'note':'Exact phase order and full source/static quartet accepted by parent. Source paths are proposed repository destinations. Preserve 12 fps, loop=false, normal blend and anchor [16,16]. Catalog-only: current structure attacks use weapon FX01–08, not FX21. No inferred 420/500 ms lifetime.'})
write('final-quartet.json',{'topicId':'C01-FX-21','status':status,'sources':[{'phase':e['phase'],'raw':str(r/e['phase']/'raw.png'),'master':str(r/e['phase']/'master.png'),'sha256':e['sha256'],'provenance':str(r/e['phase']/'provenance.json'),'proposedRepositoryDestination':e['source']}for e in entries],'independentlyAuthoredDrawings':4,'imagegenCalls':4,'rejectedAttempts':[]})
(r/'qa/PHASE-REVIEW.md').write_text('''# FX21 structure-damage phase review

The actual original atlas06 row1, full board and current HD runtime strip were viewed. The source shows gray stone and brown wood fragments, not a whole structure icon. Parent approved debris-lift → rubble-scatter → dust-impact → settling-dust and accepted the full native and terminal comparison on 2026-10-20 at 06:39:54 UTC.

1. Debris lift shows a compact group of gray angular stones, jagged ochre wood and chips with no dust.
2. Rubble scatter adds and redistributes fragments, including a larger lower foreground rock. Transparent gaps remain between pieces.
3. Dust impact introduces a localized warm beige-peach burst with mauve-brown contours behind the largest scattered fragments.
4. Settling dust removes most large debris, rounds and separates the dust into calmer puffs, and leaves small stones and a short wood fragment.

The original atlas is 1536×1024 with rectangular slots. All new sources are independently authored 1254-square canvases with one 28/1254 transform; no alpha fitting, shifting or whole-frame scaling was applied. Overall peak/terminal lower bounds are Y 1100 and 1102, a 0.045 logical-pixel difference. These bounds include fragments, so this does not establish pixel-identical dust contact. A warm-color diagnostic gives lower bounds 1100 and 1092, but can include warm fragment edges and is not perfect material segmentation. Native review found the lower impact region stable while the dust itself reshapes.

Source alpha masses are approximately 204,004, 264,446, 539,562 and 291,860 pixel equivalents. The first two phases have no drawn dust; the peak gains a localized dust body; the last loses most large fragments while rounded dust remains. Distinct hashes and alpha totals are diagnostics, not substitutes for native phase review. Not every stone and splinter is claimed to follow an exact ballistic trajectory across the original changing fragment distributions.

The original RGBA board contains an unwanted gray/white vignette, with outer-edge alpha up to 252. That original is retained as evidence only. New sources were generated on genuine transparency, never repaired with alpha cleanup. All new perimeter maxima are at most 1, actual black/white edge deviations are at most 1, and final logical 2px padding is alpha 0. Full-source black/white and native six-background composites were inspected; no old vignette or unrelated backing panel remains.

No literal current FX21 consumer was found. Structure attack events pass their weapon to playStrike/animateHit, and the candidate weapon map resolves FX01–08. Their generic 420 ms hit lifecycle is not an FX21 binding. FX21 stays catalog-only with no assumed hit/heal hold or removal rule. Four equal 1/12-second authored frames play once, nominal 1/3 second. All 28 APNGs and 112 decoded frames match their expected exports. Browser/live playback was not observed.
''')
review={'topicId':'C01-FX-21','label':'结构受损','status':status,'phaseOrder':phases,'phaseOrderApproval':'Parent approved exact phase order on 2026-10-20T06:31:20Z','parentFinalStaticPhaseAcceptance':accept,'method':'built-in image_gen','sourceCount':4,'imagegenCalls':4,'rejectedAttempts':[],'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'commonPlacement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]},'perPhaseFitCropShiftFadeAlphaCleanup':False,'sourceEdgeMaxima':[x['edgeAlphaMax']for x in edge],'sourceEdgeNonzeroSamples':[x['edgeAlphaNonzero']for x in edge],'conditionalAlpha1Proof':'qa/source-edge-composite-proof.json','originalBackingEvidence':'qa/original-backing-evidence.json','originalOuterEdgeAlphaMax':252,'allNativeLogical2pxPaddingAlpha0':True,'fullSourceBlackWhiteReviewed':True,'nativeFullAndTerminalReviewed':True,'nativeBackgrounds':['black','white','bright-stone','green-ground','dark-earth','checker'],'subjectPreserved':'Gray faceted stones, jagged ochre wood, small chips, localized warm beige/peach impact dust and settled rounded dust.','peakTerminalOverallBottomDifferenceLogicalPx':mo['peakTerminalOverallBottomDifferenceLogicalPx'],'phaseReview':'qa/PHASE-REVIEW.md','motionProof':'qa/motion-and-registration-proof.json','sourceHashProof':'final-quartet.json','semanticCaveats':mo['caveats'],'APNGVerification':{'proof':'qa/apng-timing-verification.json','files':len(ap['files']),'decodedFrames':sum(x['frameCount']for x in ap['files']),'exact12fpsFiles':14,'slowDiagnosticFiles':14,'everyDecodedFrameMatchesExpected':all(x['decodedFramesMatchExports']for x in ap['files']),'exactFrameSeconds':'1/12','nominalClipSeconds':'1/3','numPlays':1,'loop':False,'encodedFinalHold':False,'removalRule':None,'lifetimeOwner':'Future consumer; none currently bound'},'consumerAudit':'consumer-audit.json','authoredSceneConsumer':None,'currentStructureAttackBinding':'Weapon-selected FX01–08, not FX21','hit420msAssumed':False,'heal500msAssumed':False,'browserObserved':False,'browserBlocker':'Established browser/socket restriction; no repeated browser attempts were made.','liveGameObserved':False,'repoRuntimeGitWrites':False}
write('final-review.json',review);contract=json.loads((r/'generation-contract.json').read_text());contract['status']=status;contract['parentFinalStaticAcceptance']=accept;write('generation-contract.json',contract)
(r/'README.md').write_text('''# FX21 structure damage

Four independent 1254×1254 RGBA phase masters are ready with unchanged raw outputs, exact requests, input snapshots and hashes. Parent source/static review accepted all four phases and the terminal comparison.

- `integration-catalog.json`: intended repository paths and exact hashes
- `final-quartet.json`: verified staging source paths
- `final-review.json`: accepted gates and caveats
- `consumer-audit.json`: current structure-hit path and catalog-only distinction
- `qa/original-backing-evidence.json`: original RGBA vignette and edge-alpha 252 evidence
- `qa/full-and-terminal-labeled.png`: four-phase and terminal native inspection
- `qa/native-32-64-six-backgrounds.png`: native output on six backgrounds
- `qa/all-source-black-white.png`: actual full-source composites
- `qa/PHASE-REVIEW.md`: fragment/dust semantics and common impact region
- `qa/motion-and-registration-proof.json`: actual bounds and diagnostics
- `qa/apng-timing-verification.json`: 28 APNGs and 112 matching decoded frames

Use debris-lift → rubble-scatter → dust-impact → settling-dust. Preserve 12 fps, loop=false, normal blend, anchor [16,16] and the common 28/1254 transform at origin [2,2]. Source alpha-1 perimeter samples have clean composite evidence and final logical 2px padding is alpha 0. No original vignette, alpha cleanup, per-phase fitting or synthetic fade is used.

Catalog-only: structure events currently select weapon FX01–08. No FX21 420 ms or 500 ms lifecycle is claimed. Browser/live playback remains unobserved.
''')
files=[{'path':str(p.relative_to(r)),'bytes':p.stat().st_size,'sha256':sha(p)}for p in sorted(r.rglob('*'))if p.is_file()and p.name not in ['deliverable-manifest.json','link-verification.json']];write('deliverable-manifest.json',{'root':str(r),'status':status,'files':files,'selectedSources':entries})
links=[r/'final-review.json',r/'final-quartet.json',r/'integration-catalog.json',r/'consumer-audit.json',r/'qa/PHASE-REVIEW.md',r/'qa/motion-and-registration-proof.json',r/'qa/apng-timing-verification.json',r/'qa/source-edge-composite-proof.json',r/'qa/original-backing-evidence.json',r/'qa/full-and-terminal-labeled.png',r/'qa/native-32-64-six-backgrounds.png',r/'qa/all-source-black-white.png']+[r/e['phase']/'master.png'for e in entries]+[r/'qa'/f['file']for f in ap['files']];assert all(p.exists()for p in links);write('link-verification.json',{'allExistingStagingLinksVerified':True,'verifiedPaths':[str(p)for p in links],'proposedRepositoryPaths':'Not created by this worker; clearly marked integration destinations.'});print(json.dumps({'status':status,'files':len(files),'verifiedLinks':len(links),'sources':entries},indent=2))
