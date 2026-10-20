from pathlib import Path
import json,hashlib
O=Path('/workspace/shared/empire-art-rebuild/approved-v5/scifi-skills');G=O/'c02-skill-five-world-combo'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rows={
'five-world-combo':('Exactly five planetary disks retain the verified positions and color identities: blue at top, green/blue at upper left, orange at upper right, beige/brown at lower left, and purple/gray at lower right. A single cyan outer ring joins all five, and a central blue/cyan hub has the original three spokes toward the top and two lower planets. Bounded accent diamonds/dots remain near the ring. Broad terrain patches and short flat highlights replace finer source texture. No extra planet, world label or invented connection route is added.','At actual 48px, all five planet colors, outer ring and central three-spoke hub remain legible on white, black and olive. Full-native white and source512 black/olive inspections show opaque planetary surfaces and clean transparent ring gaps. The shortest visible source margin is 45px, less than the prompt target but with no clipped subject; whole-canvas export retains a fully zero-alpha 2px border.'),
'maintainer-rebuild':('The complete reconstruction station is retained: a graphite segmented circular platform, exactly two bronze/graphite articulated arms with inward-pointing grippers, a dark-teal upright human silhouette bounded by cyan, and a few square cyan reconstruction pixels. The left gripper remains slightly higher than the right. The platform inset, figure and pixels are bounded material/graphic planes, without a broad projection cone or haze. No combat robot, face, medical equipment or extra robotic arm is invented.','At actual 48px, the two large arms, upright cyan-outlined figure and circular platform remain distinct on all three backgrounds. The narrow intentional opening between the figure’s right arm and torso contains 12 samples below alpha 240 in the supplemental 8px-eroded mask, minimum 108 at native coordinates near [714,715]. Native white inspection and retained diagnostic crops confirm these are cutout antialiasing samples, not a translucent material core. Other physical masses are near-opaque. No broad halo or clipped part is visible. The shortest source margin is 55px; final 2px border is zero alpha.'),
'vein-downgrade':('The original downgrade identity is retained: one large vertical downward cyan arrow, deep graphite backing, exactly eight cyan circular circuit terminals on four paths per side, and jagged purple fractured material behind it. Purple remains intentionally present in the source motif; it is not treated as chroma contamination to erase. Broad arrow bevel planes replace the original glossy diagonal stripes. No extra arrow, padlock, minus sign or device is invented.','At actual 48px, the downward arrow and branching terminals remain immediately recognizable on white, black and olive. Source512 composites and full-native white inspection show bounded opaque cyan routes over dark purple fractures with no broad haze. The narrowest visible source margin is 34px, below the prompt target but unclipped. The complete-canvas output has a fully transparent 2px border.'),
'zero-permission-breach':('The verified forced-access motif is retained: a shallow three-quarter graphite gate, damaged and separated pieces on the left, an intact three-bar cyan control panel on the right, an orange inner seal, and one sharp orange/yellow crossing impact starburst over the opening. The long principal rays preserve the original crossing diagonals. Flat opaque burst planes replace any broad light effect. No person, weapon, padlock, numeric zero or separate door is invented.','At actual 48px, the gate silhouette, right cyan panel, broken left edge and orange crossing impact remain readable on white, black and olive. Source512 and full-native inspection show opaque broad material planes and an angular bounded burst without flame, smoke or surrounding haze. Source margins are at least 56px, below the requested target but without clipping. The 2px output border is entirely zero alpha.')}
fragment={'schemaVersion':1,'assets':{}};delivery=[]
for slug,(semantic,visual) in rows.items():
 topic='c02-skill-'+slug;d=O/topic;s=json.loads((d/'source-reference.json').read_text());q=json.loads((d/'alpha-qa.json').read_text());g=json.loads((d/'generation-result.json').read_text());request=json.loads((d/'request.json').read_text());target=f'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/skills/{topic}/master.png'
 s['oldSourceCondition']='The original board has an opaque saturated magenta field. The legacy runtime was produced by the old crop/chroma/despill workflow and has softened colored boundaries; the paired-arm original particularly shows pale colored fringe. Intentional purple parts in the planet and downgrade motifs are not mislabeled as contamination. The verified grid crop contains the complete intended motif without observable clipping or neighboring-card bleed. Original board and runtime bytes are preserved; no old keyed pixels are reused in the generated master.'
 s['legacyBuilderEvidence']='The read-only build_final_scifi_v1.py chroma() function invokes remove_chroma_key.py, then additionally removes saturated magenta pixels; fit_transparent() crops to alpha bounds. This is historical provenance only. New previews use whole-canvas contain and preserve generated alpha.'
 (d/'source-reference.json').write_text(json.dumps(s,ensure_ascii=False,indent=2)+'\n')
 q.update({'visualQA':visual,'fullNativeAndSource512Inspected':True,'actual48ThreeBackgroundInspected':True,'sourceReady':True,'sourceReadyMeaning':'Ready for parent review and binding only; formal category status remains unchanged.','formalRuntimeIntegration':False,'runtimeReady':False,'liveGameVerified':False})
 (d/'alpha-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
 evidence={**s,'tool':'built-in image_gen','transparent_background':True,'rawGeneratedSource':g['generatedPath'],'nativeMaster':'master.png','immutableRaw':'raw-native-v1.png','promptFiles':['prompt.txt'],'requestFile':'request.json','toolInputReferencePaths':request['referenced_image_paths'],'semanticNotes':semantic,'native48Review':'diagnostic-native48/native48-white-black-olive.png','visualStyle':'Approved chunky C02 broad matte planes and bold contour; original skill motif plus approved cargo-crate style-only reference.','intendedSourcePathAfterParentCopy':target,'sourceReady':True,'formalRuntimeIntegration':False,'liveGameVerified':False,'countsTowardFormalCategory':False}
 (d/'source-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n');(d/'semantic-notes.txt').write_text(semantic+'\n'+visual+'\n')
 pos=s['sourceCellOneBased'];detail=f'The actual original 48px runtime was inspected first and visually matched to {Path(s["originalMaster"]).name}, row {pos["row"]}, column {pos["column"]} (one-based). Exact complete subject crop: {s["sourceCropBox"]}. Both the complete board and exact slot are retained. Topic catalog corroborates the identity; no sorted final-manifest index was used.'
 diag=q['diagnostic8pxErosion'];review=f'''# {topic} source review

## Verified identity and contract

Canonical label: {s['label']}. Existing C02 skill contract is one static 48×48 PNG, center anchor [24,24], with no HD entry. No authored deployment is inferred from catalog registration.

{detail}

No label/pixel conflict was found. The Chinese label establishes catalog identity; the verified original image establishes the complete functional motif. No extra narrative meaning is invented.

{s['oldSourceCondition']}

{s['legacyBuilderEvidence']}

{semantic}

## Generation and review

Exactly one built-in image_gen call used transparent_background=true for this topic. Its returned 1254×1254 RGBA bytes are preserved unchanged in raw-native-v1.png, raw.png and master.png. There were no failed or rejected image-generation attempts. Request, prompt, local reference copies, reference hashes, authority hashes and generated-path provenance are retained. No generated image was cropped, repainted, recolored, sharpened, keyed or cleaned. The source crop and narrow-gap diagnostic crop are reference/QA artifacts only and never replace the generated source.

The full native source, full native white composite, source512 black/olive composites, four-topic three-background contact sheet and actual 48px white/black/olive strip were inspected. Dark outlines are thick and stable, material planes are broad, and energy graphics remain bounded. All three full-resolution background composites are also preserved for independent parent review.

{visual}

## Measured transparency and export preview

Source alpha range is {q['alphaExtrema']}; maximum source perimeter alpha is {q['canvasEdgeMaxAlpha']}, with {q['canvasEdgeAlpha2PlusCount']} perimeter samples at alpha 2 or higher. The allowed alpha 1 residue, where present, is kept unchanged and shows no visible matte haze in composites. Near-opaque source pixel count at alpha 240 or higher: {q['nearOpaquePixelCountGE240']}. Supplemental 8px erosion reports minimum alpha {diag['minAlpha']} and {diag['pixelsBelow240']} samples below 240. This diagnostic is not an independent acceptance gate and is explained above where needed.

The complete source canvas is fitted using LANCZOS into 44×44, centered on 48×48 with exactly 2px padding. Source bounds are never inferred from alpha. The entire final 2px border has maximum alpha {q['actual48TwoPixelBorderMaxAlpha']}. The 6× nearest-neighbor view is diagnostic only. master.png remains byte-identical to raw generation.

## Handoff

Source-ready means ready for parent validation/binding at {target}. It does not change formal category counts or claim runtime/live-game integration. This worker made no repository, main, selector, gameplay, manifest or Git edits. All outputs are confined to the four assigned topic directories. Proposed override metadata is included for the sole integrator.
'''
 (d/'source-review.md').write_text(review)
 (d/'consumer-evidence.json').write_text(json.dumps({'topicId':topic,'catalogOnly':True,'existingAssetId':s['assetId'],'canonicalShape':[48,48],'anchor':[24,24],'noHdEntry':True,'authoredGameplayDeploymentVerified':False,'liveGameVerified':False,'formalRuntimeIntegrationByWorker':False,'evidence':'Existing C02 final asset manifest and topic catalog identity; no authored gameplay use is claimed.'},indent=2)+'\n')
 (d/'diagnostic-native48/three-background-review.json').write_text(json.dumps({'topicId':topic,'width':48,'height':48,'source':'master.png','wholeSourceCanvas':True,'fitSize':[44,44],'padding':2,'twoPixelBorderMaxAlpha':q['actual48TwoPixelBorderMaxAlpha'],'backgrounds':['white','black','olive'],'nativeSizeInspectionComplete':True,'visualAssessment':visual,'noAuthoredConsumerOrLiveProof':True,'mainRepoMutatedByWorker':False},ensure_ascii=False,indent=2)+'\n')
 (d/'final-verification.json').write_text(json.dumps({'topicId':topic,'sourceReady':True,'sourceContractPass':q['formalSourceContractPass'],'nativeSize':[1254,1254],'nativeMode':'RGBA','rawEqualsMaster':q['matchesRawGeneratedBytes'],'originalFullboardExactSlotAndRuntimeInspected':True,'sourceNative512AndActual48ThreeBackgroundInspected':True,'canonicalShape':[48,48],'anchor':[24,24],'noHdEntry':True,'generationAttempts':1,'generationFailures':0,'masterSha256':q['sha256'],'sourceEdgeMaxAlpha':q['canvasEdgeMaxAlpha'],'finalTwoPixelBorderMaxAlpha':q['actual48TwoPixelBorderMaxAlpha'],'formalRuntimeIntegrationByWorker':False,'liveGameVerified':False,'countsTowardFormalCategoryByWorker':False,'noMainOrGitEdits':True},indent=2)+'\n')
 override={'source':target,'sha256':q['sha256']};fragment['assets'][topic]=override
 (d/'proposed-source-overrides-fragment.json').write_text(json.dumps({'schemaVersion':1,'assets':{topic:override}},indent=2)+'\n')
 delivery.append({'topicId':topic,'sourceDirectory':str(d),'masterSha256':q['sha256'],'sourceReady':True,'formalRuntimeIntegrationByWorker':False,'intendedTarget':target,'sourceEdgeMaxAlpha':q['canvasEdgeMaxAlpha'],'finalTwoPixelBorderMaxAlpha':q['actual48TwoPixelBorderMaxAlpha']})
(G/'final-four-source-overrides-fragment.json').write_text(json.dumps(fragment,indent=2)+'\n')
(G/'final-four-delivery.json').write_text(json.dumps({'sourceReady':4,'formalRuntimeIntegrationByWorker':0,'categoryCountsChangedByWorker':False,'proposedFragment':'final-four-source-overrides-fragment.json','reviewSheet':'final-four-source-and-native48-review.png','items':delivery},indent=2)+'\n')
for slug in rows:
 topic='c02-skill-'+slug;d=O/topic
 files=[{'path':str(p.relative_to(d)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(d.rglob('*')) if p.is_file() and p.name not in ['bundle-manifest.json','integrity-manifest.sha256']]
 (d/'bundle-manifest.json').write_text(json.dumps({'topicId':topic,'sourceReady':True,'formalRuntimeIntegrationByWorker':False,'files':files},indent=2)+'\n')
 (d/'integrity-manifest.sha256').write_text('\n'.join(f'{sha(p)}  {p.relative_to(d)}' for p in sorted(d.rglob('*')) if p.is_file() and p.name!='integrity-manifest.sha256')+'\n')
 print(topic,len(files),'files plus manifests')
