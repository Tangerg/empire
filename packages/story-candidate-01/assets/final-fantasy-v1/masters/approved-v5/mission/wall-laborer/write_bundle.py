from pathlib import Path
import json,hashlib,shutil
from PIL import Image
B=Path(__file__).parent
POSES=['idle','walk-a','carry-or-work','walk-b']
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,obj): p.write_text(json.dumps(obj,indent=2,ensure_ascii=False)+'\n')
reg=json.loads((B/'registration-evidence.json').read_text())
source=json.loads((B/'source-evidence.json').read_text())
reviews={
 'idle':'Both boots planted; screen-left glove wraps basket handle with stones contained; screen-right glove grips the trowel wooden handle.',
 'walk-a':'Screen-right boot advances heel-first with sole visible; screen-left boot remains planted. Basket and trowel keep original hand assignments.',
 'carry-or-work':'Heavy carry: bent screen-left arm holds stone basket raised against left waist/hip; right glove extends the trowel safely outward. Exactly two hands, one basket, one trowel.',
 'walk-b':'Screen-left boot advances toward the viewer with sole visible; screen-right boot remains planted. Opposite phase to walk-a; basket swings left, with no swapped props.'
}
originals={
 'idle':'/workspace/scratch/84c9fe948755/generated_images/exec-356b10a0-9581-41cf-a940-da7ff3b5a189.png',
 'walk-a':'/workspace/scratch/84c9fe948755/generated_images/exec-2b897c5b-3786-4007-9480-63705f2e41dd.png',
 'carry-or-work':'/workspace/scratch/84c9fe948755/generated_images/exec-d62988c6-3ab1-4bbc-968b-46d6d67c3990.png',
 'walk-b':'/workspace/scratch/84c9fe948755/generated_images/exec-89caae7b-f078-4dfe-91d8-0bf9ae780268.png'
}
manifest={
 'topicId':'C01-MISSION-WALL-LABORER','assetId':'C01-FFV1-MISSION-WALL-LABORER','category':'mission-unit',
 'status':'Native quartet and static QA complete; coordinator final review/export/integration pending',
 'frameOrder':POSES,'generationTool':'built-in image_gen.imagegen',
 'nativeArtPolicy':'Original generated source, native.png, raw.png and master.png are byte-identical for every selected pose. No source cleanup, crop, recolor, paint, per-pose fit or alpha changes. Technical padding and fixed scale/translation are proposals for export only; review composites are separate.',
 'sourceEvidence':'source-evidence.json','registrationEvidence':'registration-evidence.json','qaReview':'qa-review.json',
 'originalLogicalRuntimeContract':source['logicalRuntime'],'originalHDRuntimeContract':source['hdRuntime'],
 'actualConsumers':[],'domainActorCount':0,'scenePlacementCount':0,'consumerClassification':'catalog-only',
 'palettePolicy':'Original older warm-tan woman, gray hair and headscarf, ochre sleeveless tunic, gray sleeves, brown gloves/boots, gray-green trousers, wicker stone basket and gray triangular masonry trowel. No faction recolor.',
 'semanticContract':'idle/walk-a/carry-or-work/walk-b; purposeful action uses a braced heavy carry, not combat. Availability is not authored gameplay usage.',
 'sharedScale':reg['sharedScale'],'sourceAnchor':reg['canonicalSourceFoot'],'commonCanvas':reg['commonSourceCanvas'],'frameSize':reg['proposedCommonFrameSize'],'origin':reg['proposedOrigin'],
 'canvasPaddingToleranceRequired':reg['canvasPaddingToleranceRequired'],'maximumTranslationLogicalPixels':reg['requiredMaximumTranslationLogicalPixels'],
 'liveGameAcceptance':False,'rejectedAttempts':[],
 'unusedPlanningRequests':[{'path':'planning/carry-two-handed-not-requested','status':'Never submitted; changed before generation to keep basket left and trowel held right, matching coordinator direction.'}],
 'poses':{}
}
for pose in POSES:
 d=B/pose
 if not (d/'native.png').exists(): shutil.copy2(d/'raw.png',d/'native.png')
 q=json.loads((d/'alpha-qa.json').read_text());p=json.loads((d/'request-provenance.json').read_text());row=reg['poses'][pose]
 hashes={k:sha(Path(v)) for k,v in {'originalGenerated':originals[pose],'native':str(d/'native.png'),'raw':str(d/'raw.png'),'master':str(d/'master.png')}.items()}
 assert len(set(hashes.values()))==1
 p.update({'originalGeneratedPath':originals[pose],'originalNativeRawMasterHashes':hashes,'originalNativeRawMasterIdentical':True});write(d/'request-provenance.json',p)
 manifest['poses'][pose]={'native':f'{pose}/native.png','raw':f'{pose}/raw.png','master':f'{pose}/master.png','exactPrompt':f'{pose}/prompt.txt','request':f'{pose}/request.json','generationResult':f'{pose}/generation-result.json','requestProvenance':f'{pose}/request-provenance.json','alphaQA':f'{pose}/alpha-qa.json','sha256':hashes['raw'],'nativeSize':q['nativeSize'],'sourceFoot':row['sourceSupportAnchor'],'registrationOffsetLogical':row['registrationOffsetLogical'],'padRight':row['padRight'],'padBottom':row['padBottom'],'review':reviews[pose]}
write(B/'bundle-manifest.json',manifest)
qa={'status':'Worker visual and technical QA complete; coordinator final review pending','originalSourceIdentityPreserved':True,'styleFamilyComparedAt44LogicalPixels':['border-farmer','baker','forge-artisan','wall-laborer'],'inspectedFiles':['qa-family-native44-3x.png','qa-native-montage-white.png','qa-native-montage-black.png','qa-native44-three-backgrounds-3x.png'],'poseReviews':reviews,'trueAlpha':True,'allOriginalNativeRawMasterIdentical':True,'sourceArtModified':False,'fixedScale':reg['sharedScale'],'maximumTranslationLogicalPixels':reg['requiredMaximumTranslationLogicalPixels'],'allWholeCanvasesFit104x64':all(r['wholeCanvasFitsProposedFrame'] for r in reg['poses'].values()),'allNativePerimetersMaximumAlphaAtMost1':all(r['perimeterGate']['maxNativePerimeterAlpha']<=1 for r in reg['poses'].values()),'alphaQualification':'Very faint native alpha1 traces and alpha2-6 antialias fringe are retained without cleanup. No visible halos or matte on inspected black/white/olive composites.','animationQualification':'Two walk phases visibly reverse the advanced boot. No live-game acceptance or repo integration claimed.','rejectedGeneratedAttempts':0}
write(B/'qa-review.json',qa)
source['referenceFiles']=[{'path':str(p.relative_to(B)),'sha256':sha(p),'size':list(Image.open(p).size)} for p in sorted((B/'references').glob('*.png'))]
source['referenceQualification']='Original masterboard/row/idle crops preserve exact original RGB pixels and magenta reference background. canonical-* files are separate nearest-neighbor QA enlargements only; canonical-mission-unit-6x.png is a retained duplicate HD review made during evidence preparation.'
write(B/'source-evidence.json',source)
(B/'README.md').write_text('''# C01-MISSION-WALL-LABORER native quartet

Ready for coordinator final review and sole-integrator export. No repository or Git changes.

- Exact source: cook-doctor-wounded-wall masterboard row 4. Older woman, gray headscarf, ochre work tunic, gray sleeves, brown gloves and boots, stone basket left, masonry trowel right.
- Consumers: catalog-only. No domain actor, reinforcement, scene placement or population binding. Do not rebind bridge/forge laborers.
- Frame order: idle, walk-a, carry-or-work, walk-b. Two walking poses reverse the advanced boot; carry braces the raised basket against the left hip while retaining the right-hand trowel.
- Fixed scale: 44/941. Idle source foot [836.5,966]. Proposed common canvas 1586x993 with right/bottom technical padding at most2 pixels; frame104x64 and origin[8,4]. All source canvases fit. Largest measured translation0.549416 logical pixels.
- Native source, native.png, raw.png and master.png are byte-identical. All native perimeter alpha values are at most1. No cleanup, paint, crop, recolor, per-pose rescale, alpha edit or source padding.
- Review qa-native-montage-white.png, qa-native-montage-black.png, qa-native44-three-backgrounds-3x.png, qa-family-native44-3x.png and qa-walk-6.25fps.gif.
- Every submitted exact prompt/request, generated source location and hash, reference crop/hash, registration measurement and QA record is preserved. No rejected generated attempts. One never-submitted carry planning request is retained under planning/.
- Static QA only. No live-game acceptance claimed.
''')
hashes={str(p.relative_to(B)):sha(p) for p in sorted(B.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.json'}
write(B/'SHA256SUMS.json',hashes)
print(json.dumps({'poses':len(manifest['poses']),'filesHashed':len(hashes),'canvas':reg['commonSourceCanvas'],'scale':reg['sharedScale'],'maxTranslation':reg['requiredMaximumTranslationLogicalPixels'],'allOriginalNativeRawMasterIdentical':True},indent=2))
