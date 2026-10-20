from pathlib import Path
from PIL import Image
import json,hashlib
B=Path(__file__).parent
ORDER=['idle','walk-a','carry-or-work','walk-b']
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
R=json.loads((B/'registration-evidence.json').read_text());S=json.loads((B/'source-evidence.json').read_text());REJ=json.loads((B/'rejected-attempts.json').read_text())
review={
'idle':'Tired dark-haired tan adult man in patched deep teal coat, tan collared shirt, brown trousers and large brown boots. Exactly two wrists joined by thick ochre rope in front, two hands low at waist, same sealed tan scroll tube at screen-left hip. Both boots planted. Original tired facial shading retained; no invented injury or gear.',
'walk-a':'Corrected opposite contact: near screen-left boot advances toward viewer, broad sole visible and low; far screen-right boot is lifted and trails. Both bound wrists remain together at waist. Same identity, facing and tube side. Attempt 1 was rejected for duplicating walk-b contact and is retained unchanged.',
'carry-or-work':'Noncombat testimony role gesture: both bound wrists raised together toward lower chest, head slightly tilted up toward screen-right, mouth slightly open as if speaking. Two intact hands, coherent wrist rope, no struggle, weapon or new prop. Boots planted; scroll tube remains at hip.',
'walk-b':'Far screen-right boot advances heel-first while near screen-left boot trails back with heel lifted. This is the opposite leading leg from accepted walk-a. Both bound wrists remain together; tube remains at screen-left hip. No mirrored identity.'}
poses={}
for p in ORDER:
 d=B/p;req=json.loads((d/'request.json').read_text());refs=req['referenced_image_paths']
 roles=['Original identity/costume/restraints/tube reference','Approved name-registrar style only','Approved caravan-escort style only'] if p=='idle' else ['Approved idle identity, style, scale and proportions' if p!='walk-a' else 'Accepted walk-b edit target for opposite contact; fixed identity and scale']
 prov={'tool':'image_gen.imagegen','mode':'built-in','requestFile':'request.json','promptFile':'prompt.txt','transparentBackgroundRequested':True,'nativeOutputUntouched':True,'referenceRoles':roles,'references':[{'path':x,'sha256':sha(x)} for x in refs]};(d/'request-provenance.json').write_text(json.dumps(prov,indent=2)+'\n')
 q=json.loads((d/'alpha-qa.json').read_text())
 assert sha(d/'raw.png')==sha(d/'master.png')
 assert req['transparent_background'] is True
 assert req['prompt']==(d/'prompt.txt').read_text().rstrip('\n')
 assert all(sha(x['path'])==x['sha256'] for x in prov['references'])
 assert q['perimeterCountAlphaGreaterThan1']==0
 assert q['exteriorBeyond7PixelsFromOpaqueSilhouette']['pixelCountAlphaGreaterThan1']==0
 assert R['poses'][p]['wholeCanvasFitsProposedFrame']
 poses[p]={'raw':f'{p}/raw.png','master':f'{p}/master.png','exactPrompt':f'{p}/prompt.txt','request':f'{p}/request.json','requestProvenance':f'{p}/request-provenance.json','generationResult':f'{p}/generation-result.json','alphaQA':f'{p}/alpha-qa.json','sha256':sha(d/'master.png'),'nativeSize':q['nativeSize'],'sourceFoot':q['sourceFoot'],'registrationOffsetLogical':R['poses'][p]['registrationOffsetLogical'],'review':review[p]}
repo=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');atlas=Image.open(repo/S['sourceAtlas'])
assert sha(repo/S['sourceAtlas'])==S['sourceAtlasSha256']
assert Image.open(B/'source-evidence/original-highres-row.png').tobytes()==atlas.crop(tuple(S['rowCropXYXY'])).tobytes()
assert Image.open(B/'source-evidence/original-highres-idle.png').tobytes()==atlas.crop(tuple(S['idleCropXYXY'])).tobytes()
assert len(set(x['sha256'] for x in poses.values()))==4
assert R['canvasPaddingToleranceRequired']<=2
assert R['sharedScale']==44/942
rejected=B/'rejected/walk-a-attempt01';assert sha(rejected/'raw.png')==sha(rejected/'master.png')
manifest={
 'topicId':'C01-MISSION-DETAINED-WITNESS','assetId':'C01-FFV1-MISSION-DETAINED-WITNESS','label':'被押证人','category':'mission-unit',
 'status':'Complete native source quartet and QA; coordinator final review/export/integration pending','frameOrder':ORDER,
 'generationTool':'built-in image_gen.imagegen','generationAttempts':5,'rejectedAttempts':REJ,
 'nativeArtPolicy':'Every selected raw/master pair is byte-identical. No alpha cleanup, painting, recoloring, cropping, per-pose scaling, native-source padding or source pixel modification. Separate technical export and QA composites only.',
 'sourceEvidence':'source-evidence.json','registrationEvidence':'registration-evidence.json','qaReview':'qa-review.json','idleGateApproval':'idle-gate-approval.json',
 'originalLogicalRuntimeContract':{'png':'runtime/mission-unit/mission-detained-witness.png','width':128,'height':48,'frameWidth':32,'frameHeight':48,'frames':4,'frameOrder':ORDER,'anchor':[16,47],'footprint':[1,1]},
 'originalHDRuntimeContract':{'png':'runtime-hd/mission-unit/mission-detained-witness.png','width':256,'height':96,'pixelDensity':2},
 'classification':'catalog-only','consumerClassification':'catalog-only','actualConsumers':[],'domainActorCount':0,'scenePlacementCount':0,'liveGameAcceptance':False,
 'semanticContract':'idle/walk-a/carry-or-work/walk-b. Carry-or-work is a calm testimony gesture using existing bound hands. Catalog availability does not imply authored gameplay use.',
 'sourceAnomalyResolution':'Legacy source idle/work cells repeat effectively the same stance and both walk cells repeat the same contact. New work lifts both bound wrists together to chest for testimony; accepted walking poses have opposite leading boots. Original costume, restraint and document tube retained.',
 'palettePolicy':'Preserve tan man, dark brown hair and stubble, deep teal patched coat, tan shirt, brown belt/trousers/boots, ochre-golden rope joining wrists, tan cylindrical document tube with plain red seal. No faction recolor, metal manacles or invented injury/equipment.',
 'poses':poses}
(B/'bundle-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n')
qa={'status':'Native quartet passes identity, geometry, hand/rope/tube, opposite-step semantics and three-background visual QA; parent final integration pending','topicId':manifest['topicId'],'scale':R['sharedScale'],'bodyReferenceSourcePixels':942,'bodyTargetLogicalPixels':44,'rawMasterNativeHashesAllMatch':True,'allPromptsMatchRequests':True,'allRequestAndReferenceHashesVerified':True,'originalRowAndIdlePixelExactToBoard':True,'nativeAlphaNeverEdited':True,'alphaPerimeterGate':'All four native perimeters have max alpha 0.','exteriorHazeGate':'All four native files have zero alpha>1 pixels more than 7 pixels from alpha>127 silhouette. Native black/white/scene composites show no background, cast shadow or halo. Unmodified alpha1 trace pixels remain.','sourceCanvas':R['commonSourceCanvas'],'paddingTolerance':R['canvasPaddingToleranceRequired'],'maximumWholeCanvasRegistrationTranslationLogicalPixels':R['requiredMaximumTranslationLogicalPixels'],'sharedScaleUnchanged':True,'poseReviews':review,'generationAttempts':5,'rejections':REJ,'failures':[],'currentRuntimeRole':'Catalog-only; zero verified actors and scene placements. No deployed detained-witness binding.','runtimeOrRepositoryWrites':False,'visualQaFiles':['qa-native44-three-backgrounds.png','qa-native44-three-backgrounds-3x.png','qa-idle-shared-scale-3x.png','qa-native-montage-white.png','qa-native-montage-black.png','qa-native-montage-scene.png','qa-sequence.gif','qa-walking-loop.gif'],'perPoseFullNativeComposites':['qa-white.png','qa-black.png','qa-scene.png','qa-foot-measurements.png'],'logicalReviewExportMethod':'Same full-canvas 44/942 affine scale for every pose, with independently measured sole offsets. 4x supersampled antialias review then downsample to 104x64. No silhouette fitting.','scenePreviewMeaning':'Green backdrop is scene-tone QA only; it does not claim a live consumer or authored placement.'}
(B/'qa-review.json').write_text(json.dumps(qa,indent=2)+'\n')
(B/'pose-review.md').write_text('# Detained witness pose review\n\n'+'\n\n'.join(f'## {p}\n\n{review[p]}' for p in ORDER)+'\n\nAll four native source canvases are 1584×993. Parent-approved shared scale is 44/942. Maximum measured whole-canvas translation is '+str(R['requiredMaximumTranslationLogicalPixels'])+' logical pixels. No crop, per-pose fitting or pixel cleanup. The first walk-a attempt is retained as rejected because it duplicated walk-b contact.\n')
(B/'identity-audit.md').write_text('# Detained witness identity audit\n\nOriginal canonical, HD and high-resolution atlas row 4 were inspected. Original identity is a worried dark-haired adult man in a patched teal coat and brown trousers, wrists joined by thick golden rope and one sealed tan scroll tube at his left-screen hip. Original tired eye shading and stubble are preserved. No blood, wound, metal manacle, weapon, captor or invented gear was added.\n\nThe original work pose repeats idle and the original walk frames duplicate a contact. The new testimony gesture and opposite gait contacts resolve those source animation limitations while preserving the person, costume and restraint.\n\nThis topic is catalog-only: no verified domain actors or rendered scene placements. No live deployment acceptance is claimed.\n')
checks={str(p.relative_to(B)):sha(p) for p in sorted(B.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.json'}
(B/'SHA256SUMS.json').write_text(json.dumps(checks,indent=2)+'\n')
print(json.dumps({'verifiedFiles':len(checks),'poses':4,'rawMasterMatch':True,'requestsExact':True,'sourceCropsExact':True,'scale':R['sharedScale'],'padding':R['canvasPaddingToleranceRequired'],'maxRegistration':R['requiredMaximumTranslationLogicalPixels'],'allAlphaGatesPassed':True,'generationAttempts':5,'retainedRejectedAttempts':1},indent=2))
