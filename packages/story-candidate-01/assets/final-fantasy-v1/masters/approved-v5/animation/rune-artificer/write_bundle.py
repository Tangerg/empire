from pathlib import Path
import json,hashlib
b=Path(__file__).parent
poses=['idle','walk-a','attack','walk-b']
s=44/914
canonical,canvas,frame,origin=[802,971],[1586,992],[104,64],[8,4]
source=json.loads((b/'source-evidence.json').read_text())
identity=source['originalIdentity']
contract=('C01-UNIT-RUNE-ARTIFICER binds only c01.rune-artificer, with two c01-14 enemy actors, rune-rebel-2[15,7] and south-artificer[15,12]. '
 'Original idle/walk-a/attack/walk-b,32x48 frame,anchor[16,47],footprint1x1 remain the semantic contract. '
 'Short left-hand hammer matches blunt melee rune-hammer; right-hand open arched rune-tool supports range2 magic forge-burst. '
 'Both use the existing single attack slot, here a clear raised left-hand hammer strike/windup while retaining right-hand rune-tool. '
 'This shared-action limitation is documented, not silently fixed. Separate FX-04/07 remain outside character. '
 'Legacy near-idle attack and weak same-phase walks are reconstructed as meaningful action/opposite contacts; no rules changes or fifth frame.')
symbols=('Hat lens is filled lavender-gray glass; right-hand brass rune-tool center is a genuinely open transparent aperture, with three solid blue leaf pendants. '
 'Original small hammer angular-loop carving is simplified to the accepted angular diamond inset. Original orange-black belt plaque remains a separate geometric mark. '
 'No invented sacred meaning, faction symbol, rune alphabet or external glow.')
padding='All four selected native canvases are exactly1586x992; no transparent padding or source resizing required.'
reg={'status':'Measured whole-canvas proposal; coordinator export/binding pending','sharedScale':s,'scaleFraction':'44/914','bodyReferenceSourcePixels':914,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Idle main hat crown55 to exact two-sole mean969; flipped-up lens top44 excluded from body height but preserved.','canonicalSourceFoot':canonical,'commonSourceCanvas':canvas,'paddingPolicy':padding,'canvasPaddingToleranceRequired':0,'maximumPadRight':0,'maximumPadBottom':0,'proposedCommonFrameSize':frame,'proposedOrigin':origin,'registrationMethod':'Translate full native canvas at locked44/914. Foot x is mean of each boot bottom-four-row alpha>127 support band; y is lowest sole. Boot-only ROIs exclude gear/clothing. Exact two-sole midpoint retained.','poses':{}}
m={'asset':'C01-UNIT-RUNE-ARTIFICER','actualConsumers':['c01.rune-artificer'],'consumerStatus':'Campaign forge artificer; two verified c01-14 enemy actors','status':'Native quartet complete; coordinator final sequence review/export/binding pending','generationTool':'built-in image_gen','masterPolicy':'Selected master/raw images are byte-identical to native outputs. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.','sharedScale':s,'bodyReferenceSourcePixels':914,'bodyTargetHeightLogicalPixels':44,'frameOrder':poses,'commonSourceCanvas':canvas,'sourceEvidence':str(b/'source-evidence.json'),'registrationEvidence':str(b/'registration-evidence.json'),'identityContract':identity,'symbolContract':symbols,'semanticContract':contract,'selectedAttempts':{'idle':'first successful native output','walk-a':'first successful native output','attack':'first successful native output','walk-b':'attempt-03; attempts01/02 rejected duplicate walk contact and preserved'},'idleApproval':'Coordinator2026-10-19 18:29UTC approved identity,44/914 scale,1586x992 canvas and foot[802,971].','poses':{}}
reviews={}
for p in poses:
 d=b/p;q=json.loads((d/'alpha-qa.json').read_text());req=json.loads((d/'request.json').read_text());f=q['suggestedSourceFoot']
 reviews[p]=q['semanticReview']
 off=[(canonical[i]-f[i])*s for i in range(2)]
 full=[origin[0]+off[0],origin[1]+off[1],origin[0]+off[0]+canvas[0]*s,origin[1]+off[1]+canvas[1]*s]
 bounds=q['bboxAlphaGreaterThan127'];visible=[origin[i%2]+off[i%2]+bounds[i]*s for i in range(4)]
 fit=full[0]>=0 and full[1]>=0 and full[2]<=frame[0] and full[3]<=frame[1]
 reg['poses'][p]={'nativeSize':q['size'],'padRight':0,'padBottom':0,'sourceSupportAnchorEstimated':f,'bothBootSoleBandMidpoint':q['sourceFootMidpoint'],'registrationOffsetLogical':[round(v,9) for v in off],'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],'proposedLogicalVisibleBounds':[round(v,6) for v in visible],'proposedLogicalFullCanvasBounds':[round(v,6) for v in full],'wholeCanvasFitsProposedFrame':fit,'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterCountAlphaGreaterThan1']==0,'interpretation':'Native alpha1 specks retained, including walk-b bottom-edge alpha1; no silhouette alpha>1 touches any edge.'}}
 m['poses'][p]={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'exactPrompt':str(d/'prompt.txt'),'request':str(d/'request.json'),'exactToolCallPromptVerified':True,'exactPromptSha256':req['promptSha256'],'alphaQA':str(d/'alpha-qa.json'),'qaBlack':str(d/'qa-black.png'),'qaWhite':str(d/'qa-white.png'),'nativeSize':q['size'],'sourceFootAnchorSuggested':f,'visibleBoundsAlphaGreaterThan127':bounds,'sha256':q['sha256'],'generationSource':q['nativeSource'],'review':reviews[p],'openRuneApertureProbe':q['openRuneApertureProbe']}
 assert fit and q['size']==canvas and q['perimeterCountAlphaGreaterThan1']==0 and q['openRuneApertureProbe']['zeroPixels']>0
 assert hashlib.sha256((d/'master.png').read_bytes()).hexdigest()==q['sha256']==hashlib.sha256((d/'raw.png').read_bytes()).hexdigest()==hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
 assert hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest()==req['promptSha256']
reg['requiredMaximumTranslationLogicalPixels']=max(abs(v) for q in reg['poses'].values() for v in q['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels']=4
reg['translationBasis']='Maximum source foot shift occurs in walk-a: idle[802,971] minus walk-a[741,956]=[61,15], giving[2.936542670,0.722100656]logical at44/914. Existing limit4 suffices; all complete canvases fit104x64 at origin[8,4]. No independent pose fit.'
(b/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n');(b/'bundle-manifest.json').write_text(json.dumps(m,indent=2)+'\n')
qa={'status':'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export/binding pending','topic':m['asset'],'actualConsumers':m['actualConsumers'],'bodyScale':'44/914','selectedNativeCanvasSizes':{p:m['poses'][p]['nativeSize'] for p in poses},'commonSourceCanvas':canvas,'sourcePaddingRequired':padding,'nativeMastersUnmodified':True,'allRawMasterNativeHashesMatch':True,'allExactPromptsVerified':True,'alphaPerimeterGate':'All selected native perimeter alpha<=1; zero silhouette alpha>1 on edges; native alpha1 specks retained.','nativeAndWhiteBackgroundViewsInspected':poses,'blackAndWhiteCompositesSaved':poses,'identityPass':True,'originalLeftHammerRightRuneToolHandsPreserved':True,'runeToolApertureRealTransparencyVerifiedEveryPose':True,'threeBluePendantsAndBeltPlaqueRetainedEveryPose':True,'twoArmsAndTwoHandsOnly':True,'equipmentCompleteEveryPose':True,'walkContactsOppose':True,'walkCorrection':'Attempts01/02 of walk-b repeated right-down/left-raised phase and are rejected. Attempt03 from idle alone correctly shows left-down/right-raised. All attempts retained.','attackAnatomyReview':reviews['attack'],'semanticContract':contract,'symbolContract':symbols,'bothWeaponsShareExistingSingleActionSlot':True,'independentFXBakedIntoCharacter':False,'registrationEvidence':'registration-evidence.json','recommendedRegistrationLimitLogicalPixels':4,'proposedFullCanvasFit':all(q['wholeCanvasFitsProposedFrame'] for q in reg['poses'].values()),'runtimeOrRepositoryWrites':False,'weaponRulesChanged':False,'gameplayFixClaimed':False}
(b/'qa-review.json').write_text(json.dumps(qa,indent=2)+'\n')
for name in ['source-evidence.json','idle-review.json']:
 p=b/name;x=json.loads(p.read_text());x['status']='Native quartet complete; coordinator final sequence review/export/binding pending';x['quartetManifest']='bundle-manifest.json'
 if name=='idle-review.json':x['remainingPosesGenerated']=True
 p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
for name,r in source['references'].items():assert hashlib.sha256((b/'references'/name).read_bytes()).hexdigest()==r['sha256']
for attempt in ['attempt-01','attempt-02','attempt-03']:
 d=b/'walk-b'/attempt;q=json.loads((d/'alpha-qa.json').read_text());r=json.loads((d/'request.json').read_text())
 assert hashlib.sha256((d/'master.png').read_bytes()).hexdigest()==q['sha256']==hashlib.sha256((d/'raw.png').read_bytes()).hexdigest()==hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
 assert hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest()==r['promptSha256']
lines=['# Rune-artificer four-pose review','',contract,'','## Identity and symbols',identity,symbols,'','## Geometry',padding,reg['translationBasis'],'Shared44/914; canonical sourcefoot[802,971].','']
for p in poses:
 r=reg['poses'][p];lines+=['### '+p,reviews[p],f"Sourcefoot {r['sourceSupportAnchorEstimated']}; exact sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas offset {r['registrationOffsetLogical']}; source visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.",'']
lines+=['## Provenance and QA','Idle,walk-a,attack use first successful native output; walk-b uses corrected attempt03. Attempts01/02 repeated same foot phase and are retained rejected, not exported. Exact prompts and immutable native source files for every attempt retained. All raw/master/native hashes and prompt hashes match. Complete1672x941 original board, unresampled idle/face/gear crops and original HD/runtime strips preserved with hashes. Native and white QA views inspected; black/white composites saved. Each arch aperture has a7x7 all-alpha-zero probe. No repository/runtime writes.']
(b/'pose-review.md').write_text('\n'.join(lines)+'\n')
p=b/'source-contract-review.md';t=p.read_text();t=t.replace('remaining poses await idle review before generation.','Idle was approved18:29UTC; all four poses are now complete at the same locked scale.');p.write_text(t+'\nFinal quartet: see bundle-manifest.json and registration-evidence.json. Four selected canvases1586x992, no padding; sourceFeet[802,971],[741,956],[781,966],[791,974]. Walk-b attempts01/02 rejected for same-phase contact; attempt03 selected. All exact prompts/native files preserved.\n')
print(json.dumps({'manifest':str(b/'bundle-manifest.json'),'sourceFeet':{p:reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'maximumTranslationLogical':reg['requiredMaximumTranslationLogicalPixels'],'fullCanvasBounds':{p:reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses},'allChecks':'PASS'},indent=2))

