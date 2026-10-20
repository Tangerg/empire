from pathlib import Path
import hashlib,json
r=Path(__file__).parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
write=lambda n,x:(r/n).write_text(json.dumps(x,indent=2,ensure_ascii=False)+'\n')
qa=json.loads((r/'qa/qa-metrics.json').read_text());mo=json.loads((r/'qa/motion-and-registration-proof.json').read_text());ap=json.loads((r/'qa/apng-timing-verification.json').read_text());edge=json.loads((r/'qa/source-edge-composite-proof.json').read_text());phases=qa['phaseOrder'];entries=[]
acceptance=json.loads((r/'parent-static-acceptance.json').read_text())
assert acceptance['accepted'] is True
for p in phases:
 d=r/p;assert sha(d/'raw.png')==sha(d/'master.png')
 for n in ['request.json','prompt.txt','provenance.json','hashes.json','source-evidence.json','reference-hashes.json','reference-snapshots.json']:assert(d/n).is_file()
 for ref in json.loads((d/'reference-snapshots.json').read_text()):assert sha(d/ref['snapshot'])==ref['sha256']
 prov=json.loads((d/'provenance.json').read_text());assert sha(Path(prov['generatedSource']))==sha(d/'master.png')
 entries.append({'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-23/{p}/master.png','sha256':sha(d/'master.png')})
status='accepted-source-static-phase-candidate'
write('integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-23':{'kind':'fx-phases-v1','phases':entries,'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}}},'note':'Exact source/static quartet accepted by parent. Source paths are proposed repository destinations. Preserve 12 fps, loop=false, normal blend and anchor [16,16]. Catalog-only: no current FX23 consumer found. No inferred hit/heal lifetime or new network mechanic.'})
write('final-quartet.json',{'topicId':'C01-FX-23','status':status,'sources':[{'phase':e['phase'],'raw':str(r/e['phase']/'raw.png'),'master':str(r/e['phase']/'master.png'),'sha256':e['sha256'],'provenance':str(r/e['phase']/'provenance.json'),'proposedRepositoryDestination':e['source']}for e in entries],'independentlyAuthoredDrawings':4,'imagegenCalls':4,'rejectedAttempts':[]})
(r/'qa/PHASE-REVIEW.md').write_text('''# FX23 tower-link disconnection phase review

The actual original atlas06 row3, full board, first slot and current HD strip were inspected. The source shows two persistent gray-stone beacons with gold caps and base rings, cyan diamond cores, and a connection that disintegrates. Both cores remain present in the terminal source. This is not a generic disconnected-line UI icon.

1. Intact link has one straight cyan-edged cream beam between round bright endpoint nodes.
2. Unstable link changes the beam into a connected zigzag and adds square sparks.
3. Link breakup leaves two separate short angular fragments and square particles, with large transparent gaps. A small round tip remains on the left fragment, markedly smaller than the previous endpoint node.
4. Disconnected beacons has four separate square particles and no line or endpoint circles. Both complete beacons and cyan diamond cores remain.

All four sources are independently drawn 1254-square canvases. One full-canvas 28/1254 transform at origin [2,2] and pivot [627,627] is used throughout. No per-phase fit, crop, shift, scale or fade was applied. Both base bottom rows measure 981, 983, 982 and 981 (exclusive bounds), a maximum variation of 2 source pixels or 0.045 logical pixels. Lower-base alpha-centroid variation is below 0.025 logical pixels on either axis. These are measurements of the actual outputs, not a claim of pixel-identical towers. Native full and terminal comparisons show the two beacons planted while the connection changes.

Central energy area at alpha >=128 in x445..809, y480..779 is 19,228, 26,578, 7,460 and 3,797 source pixels. The unstable link is larger due to its zigzag and sparks; breakup then residue progressively remove the connected shape. The mask excludes tower cores and is diagnostic only. Distinct hashes and areas do not prove motion, and the square particles are not claimed to have continuously tracked identities.

The original RGBA atlas has unwanted gray/white and blue backing, with perimeter alpha up to 252. Its raw row crop also contains unrelated clipped flag tops from the neighboring fourth row. These are retained only as evidence and were not copied. No source pixels were alpha-cleaned. New source perimeter maxima are [1,0,0,0], with actual clean black/white composite proof for the 28 alpha-1 perimeter samples in the first frame. Final logical 2px padding is alpha 0. Full-source black/white and native black, white, bright-stone, green-ground, dark-earth and checker composites were inspected.

No literal current FX23 consumer was found. Generic catalog resolution does not establish a consumer, and command_node maps to a separate structure asset. No network gameplay state, hit 420 ms lifetime, or heal 500 ms lifetime was introduced. The catalog clip is four equal 1/12-second frames, single play, nominal 1/3 second. All 28 APNGs and 112 decoded frames match expected exports. Future consumers own hold/removal. Browser and live game playback were not observed.
''')
review={'topicId':'C01-FX-23','label':'塔网失联','status':status,'phaseOrder':phases,'phaseOrderApproval':'Parent approved exact phase order on 2026-10-20T06:45:41Z','parentFinalStaticPhaseAcceptance':acceptance,'method':'built-in image_gen','sourceCount':4,'imagegenCalls':4,'rejectedAttempts':[],'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'commonPlacement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]},'perPhaseFitCropShiftFadeAlphaCleanup':False,'sourceEdgeMaxima':[x['edgeAlphaMax']for x in edge],'sourceEdgeNonzeroSamples':[x['edgeAlphaNonzero']for x in edge],'conditionalAlpha1Proof':'qa/source-edge-composite-proof.json','originalBackingEvidence':'qa/original-backing-evidence.json','originalOuterEdgeAlphaMax':252,'allNativeLogical2pxPaddingAlpha0':True,'fullSourceBlackWhiteReviewed':True,'nativeFullAndTerminalReviewed':True,'nativeBackgrounds':['black','white','bright-stone','green-ground','dark-earth','checker'],'subjectPreserved':'Two gray/gold/cyan-core stone beacons; straight link, unstable zigzag, disconnected fragments, sparse square particles. Both beacon cores remain.','baseBottomMaxVariationLogicalPx':mo['baseBottomMaxVariationLogicalPx'],'baseCentroidMaxAxisVariationLogicalPx':mo['baseCentroidMaxAxisVariationLogicalPx'],'phaseReview':'qa/PHASE-REVIEW.md','motionProof':'qa/motion-and-registration-proof.json','sourceHashProof':'final-quartet.json','semanticCaveats':mo['caveats'],'APNGVerification':{'proof':'qa/apng-timing-verification.json','files':len(ap['files']),'decodedFrames':sum(x['frameCount']for x in ap['files']),'exact12fpsFiles':14,'slowDiagnosticFiles':14,'everyDecodedFrameMatchesExpected':all(x['decodedFramesMatchExports']for x in ap['files']),'exactFrameSeconds':'1/12','nominalClipSeconds':'1/3','numPlays':1,'loop':False,'encodedFinalHold':False,'removalRule':None,'lifetimeOwner':'Future consumer; none currently bound'},'consumerAudit':'consumer-audit.json','authoredSceneConsumer':None,'hit420msAssumed':False,'heal500msAssumed':False,'newNetworkMechanic':False,'browserObserved':False,'browserBlocker':'Established browser/socket restriction; no repeated browser attempts were made.','liveGameObserved':False,'repoRuntimeGitWrites':False}
write('final-review.json',review);contract=json.loads((r/'generation-contract.json').read_text());contract['status']=status;contract['parentFinalStaticAcceptance']=acceptance;write('generation-contract.json',contract)
(r/'README.md').write_text('''# FX23 tower-link disconnection

Four independent 1254×1254 RGBA masters are ready with byte-identical raw outputs, exact requests, input snapshots and hashes. Parent source/static review accepted the full and terminal sequence.

- `integration-catalog.json`: intended repository paths and exact hashes
- `final-quartet.json`: verified staging source paths
- `final-review.json`: accepted gates and caveats
- `consumer-audit.json`: catalog-only distinction
- `qa/original-backing-evidence.json`: original RGBA vignette and edge-alpha 252 evidence
- `qa/full-and-terminal-labeled.png`: native four-phase and terminal inspection
- `qa/native-32-64-six-backgrounds.png`: native output on six backgrounds
- `qa/all-source-black-white.png`: actual full-source composites
- `qa/PHASE-REVIEW.md`: connection semantics and stable beacons
- `qa/motion-and-registration-proof.json`: actual base registration and energy diagnostics
- `qa/apng-timing-verification.json`: 28 APNGs and 112 matching decoded frames

Use intact-link → unstable-link → link-breakup → disconnected-beacons. Preserve 12 fps, loop=false, normal blend, anchor [16,16] and shared transform 28/1254 at origin [2,2]. The two cyan-core beacons remain in every phase; only the connection breaks into sparse square particles. No original backing or neighboring flag pieces were copied. No alpha cleanup or per-phase fitting was used.

No current FX23 consumer or specific lifetime was found. No new network mechanic is implied. Browser/live playback remains unobserved.
''')
files=[{'path':str(p.relative_to(r)),'bytes':p.stat().st_size,'sha256':sha(p)}for p in sorted(r.rglob('*'))if p.is_file()and p.name not in ['deliverable-manifest.json','link-verification.json']];write('deliverable-manifest.json',{'root':str(r),'status':status,'files':files,'selectedSources':entries})
links=[r/'final-review.json',r/'final-quartet.json',r/'integration-catalog.json',r/'consumer-audit.json',r/'qa/PHASE-REVIEW.md',r/'qa/motion-and-registration-proof.json',r/'qa/apng-timing-verification.json',r/'qa/source-edge-composite-proof.json',r/'qa/original-backing-evidence.json',r/'qa/full-and-terminal-labeled.png',r/'qa/native-32-64-six-backgrounds.png',r/'qa/all-source-black-white.png']+[r/e['phase']/'master.png'for e in entries]+[r/'qa'/f['file']for f in ap['files']];assert all(p.exists()for p in links);write('link-verification.json',{'allExistingStagingLinksVerified':True,'verifiedPaths':[str(p)for p in links],'proposedRepositoryPaths':'Not created by this worker; clearly marked integration destinations.'});print(json.dumps({'status':status,'files':len(files),'verifiedLinks':len(links),'sources':entries},indent=2))
