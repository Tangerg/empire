from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import hashlib,json,numpy as np
R=Path(__file__).parent;P=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');A=Path('/workspace/shared/empire-art-rebuild/approved-v5/fx-source-audit');PH=['clustered-echoes','rising-echoes','dissolving-echoes','wisp-remnants'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();write=lambda p,x:p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
metrics=json.loads((R/'qa/qa-metrics.json').read_text());assert metrics['phaseOrder']==PH
for p in PH:
 assert sha(R/p/'raw.png')==sha(R/p/'master.png')
 im=Image.open(R/p/'master.png');assert im.mode=='RGBA' and im.size==(1254,1254)
 for ref in json.loads((R/p/'reference-hashes.json').read_text()):assert sha(Path(ref['path']))==ref['sha256']
for row in metrics['frames']:
 assert row['edgeAlphaMax']<=1
 for x in row['exports'].values():assert x['final2PixelBorderMax']==0
assert len(set(x['sha256']for x in metrics['frames']))==4
assert sha(R/'clustered-echoes/master.png')=='c3d5fdac3e8b6a3f9671ab22e06f386bacfddeaaa8bcdc1ff38743f86fa57bd1'
assert sha(R/'rising-echoes/master.png')=='2fae7b5203c94d332b06fba00a71f90b2cb36a60335aeb278d54173bff1ca6a2'
e=json.loads((R/'source-evidence.json').read_text());orig=P/'packages/story-candidate-01/assets/final-fantasy-v1/masters/fx/fx-atlas-02.png';board=Image.open(orig);checks=[]
assert sha(orig)==sha(R/'reference/original-board.png')
checks.append({'path':'reference/original-board.png','source':str(orig.relative_to(P)),'byteExact':True,'sha256':sha(orig)})
for i,s in enumerate(e['sourceAuditRecord']['slots']):
 q=R/f'reference/original-full-cell-f{i}.png';assert Image.open(q).tobytes()==board.crop(s['fullGridBoundsXYXY']).tobytes();checks.append({'path':str(q.relative_to(R)),'boundsXYXY':s['fullGridBoundsXYXY'],'decodedEqualOriginalCell':True,'sha256':sha(q)})
 for stem in ['original','legacy-alpha']:
  src=A/f'source-crops/C01-FX-08/{stem}-f{i}.png';q=R/f'reference/{stem}-f{i}.png';assert sha(src)==sha(q);checks.append({'path':str(q.relative_to(R)),'byteExactAuditReference':True,'sha256':sha(q)})
q=R/'reference/original-row.png';assert Image.open(q).tobytes()==board.crop(e['sourceAuditRecord']['sourceRowBoundsXYXY']).tobytes();checks.append({'path':str(q.relative_to(R)),'decodedEqualOriginalRow':True,'sha256':sha(q)})
for den in (1,2):
 q=R/f'reference/legacy-runtime@{den}x.png';assert sha(q)==sha(A/f'current-runtime/C01-FX-08@{den}x.png');checks.append({'path':str(q.relative_to(R)),'byteExactAuditRuntimeSnapshot':True,'sha256':sha(q)})
write(R/'reference/reference-verification.json',checks)
e.update({'sourceReferenceVerification':'reference/reference-verification.json','consumerCodeEvidence':'reference/consumer-source-snapshots.json','semanticApprovedPhaseOrder':PH,'sourceCanvasRegistration':'One common entire1254 canvas, pivot627,627 to logical16,16. Original lower wisps vary as people overlap/dissolve; authored visible lower bounds1153,1196,1146,1101 differ naturally. Bounds never drive crop, fit, scale or translation.','anchorNote':'Historical source-audit metadata16,24 is preserved as history; actual current canonical anchor and target-hit pivot are16,16.','generationAttempts':4,'selectedSources':4,'generationPauseResolved':'First pair was paused and retained until parent confirmed remote backup main04bc18b7357e954d097dd61dbcaa54f6c9902cbf; only pending last two phases generated on resume.'});write(R/'source-evidence.json',e)
for i,p in enumerate(PH):
 write(R/p/'source-evidence.json',{'topicId':'C01-FX-08','phase':p,'sourcePhaseIndex':i,'sourceBoard':'fx-atlas-02.png','sourceRow1Based':4,'sourceColumn1Based':i+1,'audit':'../source-evidence.json','auditSha256':sha(R/'source-evidence.json'),'referenceHashes':'reference-hashes.json'})
 row=metrics['frames'][i];aa=np.array(Image.open(R/p/'master.png').getchannel('A'));row['nearOpaque240to255Count']=int(np.count_nonzero(aa>=240));row['opaquePixelCount']=int(np.count_nonzero(aa==255));row['boundsAlpha32Logical']=[2+x*28/1254 for x in row['boundsAlpha32']];write(R/f'qa/{p}-detail-metrics.json',row)
metrics['registrationNote']=e['sourceCanvasRegistration'];write(R/'qa/qa-metrics.json',metrics)
# Read-only sample evidence distinguishes authored transparent bodies from opaque paper cores.
im=np.array(Image.open(R/'dissolving-echoes/master.png'));samples=[]
for name,xy in [('central-head',(630,400)),('central-torso',(630,540)),('left-head',(440,620)),('left-torso',(440,740)),('right-head',(830,700)),('right-torso',(830,800)),('top-paper',(480,160)),('upper-right-paper',(980,330)),('left-wisp',(240,500))]:
 samples.append({'region':name,'xy':list(xy),'rgba':im[xy[1],xy[0]].tolist()})
assert all(0<s['rgba'][3]<240 for s in samples[:6]);assert all(s['rgba'][3]>=240 for s in samples[6:8])
write(R/'qa/authored-translucency-proof.json',{'phase':'dissolving-echoes','sourceSha256':sha(R/'dissolving-echoes/master.png'),'samples':samples,'conclusion':'All six reviewed head/torso samples have alpha194..220; paper cores253. Partial figure translucency is in generated source itself; no alpha multiplication, normalization, cleanup or painted repair occurred. Pale rims retain higher alpha and are not claimed uniformly translucent.'})
write(R/'integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-08':{'kind':'fx-phases-v1','phases':[{'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-08/{p}/master.png','sha256':sha(R/p/'master.png')}for p in PH],'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}}}})
review={'topicId':'C01-FX-08','label':'亡者记忆冲击','selectedPhaseOrder':PH,'method':'built-in image_gen','sourceCount':4,'attemptCount':4,'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'perFrameCropFitShiftPaintCleanup':False,'actualCurrentRuntimeAnchor':[16,16],'acceptedSourceEdgeMaxima':[x['edgeAlphaMax']for x in metrics['frames']],'acceptedSourceEdgeNonzeroSamples':[x['edgeAlphaNonzeroSamples']for x in metrics['frames']],'sourceResidueDisclosure':'All perimeters have onlyalpha1 numerical residue:2,16,75,26 perimeter samples. Source3 has more faint generated residue than the others but no visible halo/backing on full black/white or native colored composites. No alpha values were edited and no gate relaxed.','allNativeTwoPixelBordersAlpha0':True,'nativeBackgrounds':['black','white','bright-stone','green-ground','dark-earth'],'fullSourceBackgroundsActuallyInspected':['black','white'],'semanticReview':['Three overlapping ordinary gray human memories with pale rims, curled gray wisps and amber paper scraps','Five taller overlapping human memories emerge with a larger denser center and same low wisp region','Three smaller separated incomplete memories with genuine head/torso translucency, arms dissolving and amber papers still more opaque','No human figure remains; only five separated gray curls and amber paper scraps across an open center'],'identityPreserved':'Ordinary-person memories and name-paper vocabulary, muted gray/taupe, cream rims and ochre papers. No skull, cyan magic, weapon, legible inscription, generic blast or opaque panel.','registrationReview':e['sourceCanvasRegistration'],'nativeReadability':'Four silhouettes and substantial-to-sparse progression are distinct on all five backgrounds at32 and64. Individual head counts are clearest at64;32 reads a low clustered mass, taller denser mass, separated three remnants, then hollow sparse curls/papers.','authoredTranslucencyProof':'qa/authored-translucency-proof.json','sourceAlphaMasses':[x['alphaMass']for x in metrics['frames']],'noForcedGlobalFade':True,'excludedRuntimeOverlays':['white impact circle','damage number'],'rejectedAttempts':[],'APNGVerification':'12 APNGs;48 decoded frames exactly match exports; durations1/12,1/12,1/12,17/100 seconds total420ms; one play; final starts250ms and holds170ms. This is offline timing/frame evidence. APNG retains its last image on completion; actual BoardView node removal is established from code, not a live rendering observation.','actualLiveGameObserved':False,'runtimeAndGitWrites':False};write(R/'final-review.json',review)
write(R/'final-quartet.json',{'topicId':'C01-FX-08','readyForIntegrationReview':True,'phaseOrder':PH,'hashes':{p:json.loads((R/p/'hashes.json').read_text())for p in PH},'review':'final-review.json','integrationCatalog':'integration-catalog.json','APNGTimingProof':'qa/apng-timing-verification.json'})
write(R/'second-pair.json',{'topicId':'C01-FX-08','status':'remaining-pair-complete','phases':PH[2:],'hashes':{p:sha(R/p/'master.png')for p in PH[2:]},'acceptedFirstPairUnchanged':True,'fullQuartet':'final-quartet.json'})
write(R/'failures.json',{'topicId':'C01-FX-08','generationAttempts':4,'selectedSources':4,'failedOrRejectedGenerations':[],'operationalPause':'After2sources parent paused generation for remote auth recovery. Resumed only saved pending phases after verified backup. No duplicate calls.','scopeNote':'No main repository/runtime writes or Git operations by this worker. Live-game acceptance belongs to integrator.'})
h=json.loads((R/'paused-handoff.json').read_text());h['historicalRecord']=True;h['resolvedBy']='final-quartet.json';h['resumeConfirmedBackup']='04bc18b7357e954d097dd61dbcaa54f6c9902cbf';write(R/'paused-handoff.json',h)
(R/'README.md').write_text('''# FX08 dead-memory impact source quartet

C01-FX-08 亡者记忆冲击: clustered-echoes → rising-echoes → dissolving-echoes → wisp-remnants. Four independent built-in image_gen outputs preserve original FX atlas02 row4:3 ordinary-person memories,5 taller overlapping echoes,3 separated dissolving echoes, then gray curls and amber name papers only. No skull substitution or generic blast.

## Provenance and registration

Each phase contains exact request.json/prompt.txt, reference hashes, source evidence and untouched byte-identical raw.png/master.png. Common original row and exact phase cell are reference inputs. Original full board, exact row/cells,4legacy-alpha frames,32/64legacy runtime and actual consumer snapshots are retained and verified under reference/. Historical manifest anchor16,24 remains explicitly historical; actual current target hit pivot is16,16.

One whole-canvas transform for every phase:1254square →28square at2,2 on32square. Source pivot627,627 maps16,16.2x uses56square at4,4 on64square. No per-frame crop, fit, translation, alpha cleanup, opacity multiplier or hand-painted fix. Authored changing bounds do not own alignment.

## Visual and alpha review

- Full source black/white composites inspected for all4sources; native32/64 inspected on black, white, bright stone, green ground and dark earth
- Source edge maxima1,1,1,1; nonzero perimeter samples2,16,75,26 are alpha1 residue, preserved untouched. Final32/64 two-pixel borders are all0
- Broad gray/taupe humans, cream rims and amber papers retain identity. Source3 uses true partial body alpha194..220 at6read-only head/torso samples, while paper samples253 remain near opaque. See authored-translucency-proof.json
- Native32 distinguishes low cluster, denser rising mass, three separated remnants and final open curls/papers.64retains more head detail. Final phase contains no human heads or bodies
- No matte panel, cyan aura, legible inscription, glow field or baked runtime damage overlay

## Actual timing and consumers

c01.ghost-touch and c01.colossus-sweep use this target-centered effect. Current canonical record is4frames at12fps, nonloop, normal source-over; BoardView.animateHit removes its node at420ms. Starts are0,83⅓,166⅔,250ms; last phase holds170ms. White impact circle and damage number remain separate renderer overlays.

12APNGs cover transparent plus5backgrounds at1x/2x. Their48decoded frames equal QA exports exactly, rational frame durations1/12,1/12,1/12,17/100 total420ms, one play. APNG retains its last image afterward; actual node removal is source-code evidence, not live-game observation. No live-game playback was performed by this worker.

## Handoff

Start with qa/native-32-64-five-backgrounds.png, qa/registration-pivot-proof.png, final-review.json and qa/apng-timing-verification.json. integration-catalog.json gives exact repository-target source paths/hashes and shared transform. final-quartet.json is complete; deliverable-manifest.json hashes all files. First pair was safely paused for remote-auth restoration and is byte-identical after resuming only the pending pair. paused-handoff.json is historical. Parent owns integration, tests, runtime writes and Git.
''')
write(R/'deliverable-manifest.json',{'topicId':'C01-FX-08','root':str(R),'status':'complete-4of4','phaseOrder':PH,'complete':True,'sourceCount':4,'attemptCount':4,'repoWrites':False,'files':[{'path':str(p.relative_to(R)),'bytes':p.stat().st_size,'sha256':sha(p)}for p in sorted(R.rglob('*'))if p.is_file() and p.name!='deliverable-manifest.json']})
print(json.dumps({'ready':True,'phaseHashes':{p:sha(R/p/'master.png')for p in PH},'files':len(json.loads((R/'deliverable-manifest.json').read_text())['files']),'sourceEdgeMaxima':review['acceptedSourceEdgeMaxima'],'sourceEdgeCounts':review['acceptedSourceEdgeNonzeroSamples']},indent=2))
