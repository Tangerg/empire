from pathlib import Path
import json,hashlib

b=Path(__file__).parent
poses=['idle','walk-a','attack','walk-b']
scale=52/905.5
canonical,canvas,frame,origin=[828,970],[1586,992],[128,80],[14,6]
source=json.loads((b/'source-evidence.json').read_text())
status='Native quartet complete; coordinator shared-scale sequence review pending, especially attack spear projection'
contract=('C01-UNIT-WOLF-RIDER binds c01.wolf-rider, with six initial campaign enemies across c01-07 and c01-08 plus the experience-lab gray-wolf actor. '
          'Dedicated original four-frame board, order idle / walk-a / attack / walk-b. Original 32x48 frame, anchor [16,47], art footprint [1,1]; no rule footprint change. '
          'Mounted wolf and two-hand spear agree with wolf-spear rules: pierce 45, melee/mounted, range 1. '
          'Preserve the original rider face, cap/braid, shoulder plate, small round side shield, pale wrapped forward hand, wolf/harness and saddle blanket. '
          'The shoulder plate disappears in the original attack and walk-b; consistent idle armor is an intentional continuity repair, not exact frame replication. '
          'Opposite wolf diagonal steps and clear braced two-hand spear thrust replace the original subtle actions. Equipment icon and C01-FX-02 stay independent.')
scale_basis=('Coordinator accepted the complete mounted silhouette at 52 logical pixels. Idle cap top y56 to mean near-side hind/fore wolf sole y961.5 gives 905.5 native pixels. '
             'One locked 52/905.5 scale applies to all full canvases. The actual idle visible width 57.254555 logical was explicitly accepted without forcing the preliminary 60 lower bound. '
             'The rider is not independently resized, and the wolf volume is not separately fitted. The wolf paw anchor, not the rider boot, determines ground registration.')
padding='Idle, attack and walk-b are native 1586x992; walk-a is 1585x992. Exactly one transparent right column at export resolves the canvas discrepancy; native sources remain unmodified.'
variation=('Native visual butt-to-tip estimates give idle spear span 820.41 source pixels, walk-a 821.77, attack 1133.76 and walk-b 807.77. '
           'Attack is visibly about 38.2% longer in projected span than idle. It was requested at the same physical length but the output differs; no exact-length continuity claim, hidden rescale or automatic foreshortening justification is made. '
           'The braced stance plus extended spear gives attack total width 84.647156 logical, versus idle 57.254555 and walks 61.733849 / 61.791276. '
           'Coordinator native shared-scale sequence review must decide whether this projection variation is acceptable or needs a bounded weapon-only correction.')
reg={'status':status,'sharedScale':scale,'scaleFraction':'52/905.5','bodyReferenceSourcePixels':905.5,'bodyTargetHeightLogicalPixels':52,
     'bodyMeasurement':scale_basis,'canonicalSourceFoot':canonical,'commonSourceCanvas':canvas,'paddingPolicy':padding,'canvasPaddingToleranceRequired':1,
     'maximumPadRight':1,'maximumPadBottom':0,'proposedCommonFrameSize':frame,'proposedOrigin':origin,
     'registrationMethod':'Translate each complete native canvas at the locked shared scale. Separate paw ROIs measure alpha >127 sole bands; x averages four bottom-four-row paw centers and y is the lowest sole. No per-pose fit.','poses':{}}
manifest={'asset':'C01-UNIT-WOLF-RIDER','actualConsumers':['c01.wolf-rider'],'consumerStatus':source['consumerStatus'],'status':status,
          'generationTool':'built-in image_gen','masterPolicy':'All master/raw images are byte-identical native tool outputs. No painting, recoloring, cropping, resizing or alpha cleanup. White/black images are QA composites only.',
          'sharedScale':scale,'bodyReferenceSourcePixels':905.5,'bodyTargetHeightLogicalPixels':52,'speciesScaleContract':scale_basis,
          'frameOrder':poses,'commonSourceCanvas':canvas,'sourceEvidence':str(b/'source-evidence.json'),'registrationEvidence':str(b/'registration-evidence.json'),
          'identityContract':source['originalIdentity']+' '+source['equipmentIdentity'],'symbolContract':source['symbolReview'],'semanticContract':contract,
          'projectionVariation':variation,'selectedAttempts':{p:'first successful native output' for p in poses},
          'idleApproval':'Coordinator 2026-10-19 20:06 UTC accepted 52/905.5, foot [828,970], 1586x992 full canvas, candidate 128x80 / origin [14,6]; actual width 57.254555 accepted.','poses':{}}

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
for pose in poses:
 d=b/pose;q=json.loads((d/'alpha-qa.json').read_text());req=json.loads((d/'request.json').read_text());foot=q['suggestedSourceFoot']
 off=[(canonical[i]-foot[i])*scale for i in range(2)]
 full=[origin[0]+off[0],origin[1]+off[1],origin[0]+off[0]+canvas[0]*scale,origin[1]+off[1]+canvas[1]*scale]
 bb=q['bboxAlphaGreaterThan127'];visible=[origin[i%2]+off[i%2]+bb[i]*scale for i in range(4)];pad=[canvas[i]-q['size'][i] for i in range(2)]
 fit=full[0]>=0 and full[1]>=0 and full[2]<=frame[0] and full[3]<=frame[1]
 reg['poses'][pose]={'nativeSize':q['size'],'padRight':pad[0],'padBottom':pad[1],'sourceSupportAnchorEstimated':foot,'fourPawSoleBandMidpoint':q['sourceFootMidpoint'],
                     'registrationOffsetLogical':[round(v,9) for v in off],'nativeVisibleBoundsAlphaGreaterThan127':bb,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],
                     'logicalVisibleWidth':(bb[2]-bb[0])*scale,'proposedLogicalVisibleBounds':[round(v,6) for v in visible],
                     'proposedLogicalFullCanvasBounds':[round(v,6) for v in full],'wholeCanvasFitsProposedFrame':fit,
                     'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterCountAlphaGreaterThan1']==0}}
 manifest['poses'][pose]={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'exactPrompt':str(d/'prompt.txt'),'request':str(d/'request.json'),
                         'exactToolCallPromptVerified':True,'exactPromptSha256':req['promptSha256'],'alphaQA':str(d/'alpha-qa.json'),'qaBlack':str(d/'qa-black.png'),'qaWhite':str(d/'qa-white.png'),
                         'nativeSize':q['size'],'sourceFootAnchorSuggested':foot,'visibleBoundsAlphaGreaterThan127':bb,'sha256':q['sha256'],'generationSource':q['nativeSource'],
                         'review':q['semanticReview'],'negativeSpaceProbes':q['negativeSpaceProbes'],'spearProjectionMeasurement':q['spearProjectionMeasurement']}
 assert fit and max(pad)<=1 and min(pad)>=0 and q['perimeterCountAlphaGreaterThan1']==0
 assert all(p['maxAlpha']==0 and p['zeroPixels']==49 for p in q['negativeSpaceProbes'])
 assert sha(d/'master.png')==sha(d/'raw.png')==sha(q['nativeSource'])==q['sha256']
 assert sha(d/'prompt.txt')==req['promptSha256']
 for path,expected in req['referencedImageSha256'].items():assert sha(path)==expected
for filename,record in source['references'].items():assert sha(b/'references'/filename)==record['sha256']
reg['requiredMaximumTranslationLogicalPixels']=max(abs(v) for r in reg['poses'].values() for v in r['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels']=5
reg['translationBasis']='Attack foot [753,948] versus canonical [828,970] gives source delta [75,22], maximum logical x shift 4.3070127. Existing limit 4 is insufficient; explicit limit 5 is supported by these measured feet. Every full canvas fits 128x80 at origin [14,6].'
qa={'status':status,'topic':manifest['asset'],'actualConsumers':manifest['actualConsumers'],'bodyScale':'52/905.5','speciesScaleContract':scale_basis,
    'selectedNativeCanvasSizes':{p:manifest['poses'][p]['nativeSize'] for p in poses},'commonSourceCanvas':canvas,'sourcePaddingRequired':padding,
    'nativeMastersUnmodified':True,'allRawMasterNativeHashesMatch':True,'allExactPromptsVerified':True,
    'alphaPerimeterGate':'All selected native perimeters have zero pixels alpha >1. Idle and attack max 1 native specks retained; walk-a and walk-b max 0. No cleanup.',
    'nativeAndWhiteBackgroundViewsInspected':poses,'blackAndWhiteCompositesSaved':poses,'identityPreserved':True,
    'fourConnectedWolfLegsAndPawsVisuallyChecked':True,'oneWolfTailAndOneSeatedRider':True,'twoRiderHandsOnOneSpear':True,'originalSideShieldRetainedAllPoses':True,
    'walkContactsOppose':True,'allThreeNegativeSpaceProbesPerPoseZeroAlpha':True,'attackSemantics':'Two-hand forward spear thrust and four-paw braced wolf lunge; no independent FX.',
    'projectionVariationRequiresCoordinatorReview':variation,'semanticContract':contract,'symbolContract':source['symbolReview'],'independentFXBakedIntoCharacter':False,
    'registrationEvidence':'registration-evidence.json','recommendedRegistrationLimitLogicalPixels':5,'proposedFullCanvasFit':all(r['wholeCanvasFitsProposedFrame'] for r in reg['poses'].values()),
    'runtimeOrRepositoryWrites':False,'weaponRulesChanged':False,'gameplayFootprintChanged':False}
for filename,data in [('registration-evidence.json',reg),('bundle-manifest.json',manifest),('qa-review.json',qa)]:
 dest=b/filename
 if dest.exists():
  old=json.loads(dest.read_text())
  if 'coordinatorSequenceApproval' in old:data['coordinatorSequenceApproval']=old['coordinatorSequenceApproval'];data['status']=old['status']
 dest.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
for filename in ['source-evidence.json','idle-review.json']:
 p=b/filename;data=json.loads(p.read_text())
 if 'coordinatorSequenceApproval' not in data:data['status']=status
 data['quartetManifest']='bundle-manifest.json';data['remainingPosesGenerated']=True;data['sharedScaleContract']='52/905.5; foot [828,970]; full 1586x992 canvas; frame 128x80; origin [14,6]';p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
lines=['# Wolf-rider four-pose review','',contract,'','## Original identity and symbols',manifest['identityContract'],source['symbolReview'],'','## Shared mounted scale',scale_basis,'','## Geometry',padding,reg['translationBasis'],'','## Spear projection caveat',variation,'']
for pose in poses:
 r,p=reg['poses'][pose],manifest['poses'][pose]
 lines+=['### '+pose,p['review'],f"Source foot {r['sourceSupportAnchorEstimated']}; exact four-paw sole midpoint {r['fourPawSoleBandMidpoint']}; full-canvas offset {r['registrationOffsetLogical']}; native visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.",'']
lines+=['## Provenance and alpha','All four selected outputs are their first native successes, preserved byte-for-byte as raw/master. Exact prompts, requests and original/reference hashes verified. Native and white views inspected; black/white composites saved. Three 7x7 negative-space probes per pose are completely transparent. No programmatic art edits, alpha cleanup or repository changes.','', 'Coordinator shared-scale native sequence review is pending, with spear projection variation explicitly raised.']
(b/'pose-review.md').write_text('\n'.join(lines)+'\n')
print(json.dumps({'manifest':str(b/'bundle-manifest.json'),'sourceFeet':{p:reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'maximumTranslationLogical':reg['requiredMaximumTranslationLogicalPixels'],'fullCanvasBounds':{p:reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses},'integrityChecks':'PASS'},indent=2))
