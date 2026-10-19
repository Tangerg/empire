from pathlib import Path
import json
import hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
s = 44 / 895
canonical, canvas, frame, origin = [770, 963], [1586, 992], [104, 64], [8, 4]
identity = ('Original mature stocky tan man, charcoal-gray full beard/moustache, close dark crown and brown headband with one brass-rimmed forehead lens. '
            'Dark segmented steel armor, rust-red leather bib apron, belt and brown boots. '
            'One original single-bladed axe with downward-flared steel edge, two-rivet socket, brown continuous haft, central metal band, butt cap and protruding wooden tip. '
            'Original screen-left rear grip and screen-right front grip retained. No shield. Approved chunky head/hands/boots and broad matte planes.')
contract = ('C01-UNIT-AXE-BREAKER is a canonical combat-unit catalog topic with no current campaign or built-in rule consumer/binding. '
            'No new binding or weapon rule is invented. Preserve original idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1. '
            'Original attack nearly repeats idle and both walks advance the same front foot. The new quartet reconstructs a clear two-handed downward chop and opposite contacts. '
            'The separate original C01-EQUIP-AXE-BREAKER icon is double-bitted while the original character holds a single-bladed axe. '
            'Preserve each original subject; the equipment topic does not remap or redesign this unit. No fifth pose or independent FX.')
padding = 'Idle native1586x992; other three native1585x992. Common1586x992 requires only one transparent right column for walk-a/attack/walk-b in export preparation. Native pixels and all original canvases remain unchanged.'
reg = {
    'status': 'Measured whole-canvas proposal; coordinator export/review pending',
    'sharedScale': s, 'scaleFraction': '44/895', 'bodyReferenceSourcePixels': 895, 'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Accepted idle crown y63 to exact two-sole mean y958. One body-derived scale applies to every complete native canvas.',
    'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
    'paddingPolicy': padding, 'canvasPaddingToleranceRequired': 1, 'maximumPadRight': 1, 'maximumPadBottom': 0,
    'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'Whole-canvas translation at locked44/895. Anchor x is the mean of both boot bottom-four-row alpha>127 support bands; y is the lowest sole. Manual boot-only ROIs exclude the lower axe blade and preserve exact midpoint evidence.',
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-AXE-BREAKER', 'actualConsumers': [],
    'consumerStatus': 'Catalog-only; no current campaign or built-in rule consumer/binding',
    'status': 'Native quartet complete; coordinator final sequence review/export pending',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'Every selected master/raw PNG is byte-identical to its native output. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.',
    'sharedScale': s, 'bodyReferenceSourcePixels': 895, 'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses, 'commonSourceCanvas': canvas,
    'sourceEvidence': str(b/'source-evidence.json'), 'registrationEvidence': str(b/'registration-evidence.json'),
    'identityContract': identity, 'semanticContract': contract,
    'equipmentTopicDifference': 'Original unit has one downward-flared cutting edge; distinct original equipment icon is double-bitted. Coordinator explicitly confirmed preserving both original subjects without silent remap.',
    'selectedAttempts': {p:'first successful native output' for p in poses},
    'idleApproval': 'Coordinator2026-10-19 17:49UTC viewed and accepted idle identity/framing,44/895 scale,1586x992 canvas and foot[770,963].',
    'poses': {}
}
reviews = {}
for p in poses:
    d=b/p
    q,req=json.loads((d/'alpha-qa.json').read_text()),json.loads((d/'request.json').read_text())
    reviews[p]=identity if p=='idle' else q['semanticReview']
    f=q['suggestedSourceFoot']; off=[(canonical[i]-f[i])*s for i in range(2)]
    full=[origin[0]+off[0],origin[1]+off[1],origin[0]+off[0]+canvas[0]*s,origin[1]+off[1]+canvas[1]*s]
    bounds=q['bboxAlphaGreaterThan127']; visible=[origin[i%2]+off[i%2]+bounds[i]*s for i in range(4)]
    fit=full[0]>=0 and full[1]>=0 and full[2]<=frame[0] and full[3]<=frame[1]
    reg['poses'][p]={
        'nativeSize':q['size'],'padRight':canvas[0]-q['size'][0],'padBottom':canvas[1]-q['size'][1],
        'sourceSupportAnchorEstimated':f,'bothBootSoleBandMidpoint':q['sourceFootMidpoint'],
        'registrationOffsetLogical':[round(x,9) for x in off],
        'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],
        'proposedLogicalVisibleBounds':[round(x,6) for x in visible],'proposedLogicalFullCanvasBounds':[round(x,6) for x in full],
        'wholeCanvasFitsProposedFrame':fit,
        'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterCountAlphaGreaterThan1']==0,'interpretation':'Native alpha=1 specks retained; no silhouette alpha>1 reaches any edge.'}
    }
    m['poses'][p]={
        'master':str(d/'master.png'),'raw':str(d/'raw.png'),'exactPrompt':str(d/'prompt.txt'),'request':str(d/'request.json'),
        'exactToolCallPromptVerified':True,'exactPromptSha256':req['promptSha256'],
        'alphaQA':str(d/'alpha-qa.json'),'qaBlack':str(d/'qa-black.png'),'qaWhite':str(d/'qa-white.png'),
        'nativeSize':q['size'],'sourceFootAnchorSuggested':f,'visibleBoundsAlphaGreaterThan127':bounds,
        'sha256':q['sha256'],'generationSource':q['nativeSource'],'review':reviews[p]
    }
    assert fit and q['perimeterCountAlphaGreaterThan1']==0
    assert hashlib.sha256((d/'master.png').read_bytes()).hexdigest()==q['sha256']==hashlib.sha256((d/'raw.png').read_bytes()).hexdigest()==hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
    assert hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest()==req['promptSha256']
    assert max(canvas[i]-q['size'][i] for i in range(2))<=1
reg['requiredMaximumTranslationLogicalPixels']=max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels']=8
reg['translationBasis']='Measured attack wide-brace sole anchor[624,935] differs from idle[770,963] by[146,28]source pixels. At44/895 this is x+7.177653631,y+1.376536313logical; recommend explicit limit8. All complete canvases fit104x64 at origin[8,4], without per-pose scale or silhouette fitting.'
(b/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
(b/'bundle-manifest.json').write_text(json.dumps(m,indent=2)+'\n')
qa={
    'status':'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export pending',
    'topic':m['asset'],'actualConsumers':[],'consumerStatus':m['consumerStatus'],'bodyScale':'44/895',
    'selectedNativeCanvasSizes':{p:m['poses'][p]['nativeSize'] for p in poses},'commonSourceCanvas':canvas,
    'sourcePaddingRequired':padding,'nativeMastersUnmodified':True,'allRawMasterNativeHashesMatch':True,'allExactPromptsVerified':True,
    'alphaPerimeterGate':'All native perimeter alpha<=1; zero silhouette alpha>1 on edges; native alpha=1 specks retained.',
    'nativeAndWhiteBackgroundViewsInspected':poses,'blackAndWhiteCompositesSaved':poses,
    'identityPass':True,'sameTwoHandsOnOneContinuousHaftEveryPose':True,'originalRearFrontGripOrderPreserved':True,
    'singleCuttingEdgeRetained':True,'equipmentCompleteEveryPose':True,'walkContactsOppose':True,'attackAnatomyReview':reviews['attack'],
    'semanticContract':contract,'equipmentTopicDifference':m['equipmentTopicDifference'],'independentFXBakedIntoCharacter':False,
    'registrationEvidence':'registration-evidence.json','recommendedRegistrationLimitLogicalPixels':8,
    'proposedFullCanvasFit':all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'runtimeOrRepositoryWrites':False,'weaponRulesChanged':False,'newConsumerBindingInvented':False,'gameplayFixClaimed':False
}
(b/'qa-review.json').write_text(json.dumps(qa,indent=2)+'\n')
for name in ['source-evidence.json','idle-review.json']:
    p=b/name;x=json.loads(p.read_text());x['status']='Native quartet complete; coordinator final sequence review/export pending';x['quartetManifest']='bundle-manifest.json'
    if name=='idle-review.json':x['remainingPosesGenerated']=True
    p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
e=json.loads((b/'source-evidence.json').read_text())
for name,r in e['references'].items():assert hashlib.sha256((b/'references'/name).read_bytes()).hexdigest()==r['sha256']
lines=['# Axe-breaker four-pose review','',contract,'','## Identity',identity,'','## Geometry',padding,reg['translationBasis'],'Shared44/895; canonical source foot[770,963].','']
for p in poses:
    r=reg['poses'][p]
    lines+=['### '+p,reviews[p],f"Source foot {r['sourceSupportAnchorEstimated']}; exact sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas offset {r['registrationOffsetLogical']}; source visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.",'']
lines+=['## Provenance and QA','All four are first successful native built-in image_gen outputs. Accepted idle was reviewed before the other poses. Every raw/master/native and exact prompt hash matches. Complete original board, first-row/idle crops and original HD/runtime four-frame strips are preserved. All native and white-background views inspected; black/white QA composites saved. No repository/runtime writes or invented consumer binding.']
(b/'pose-review.md').write_text('\n'.join(lines)+'\n')
print(json.dumps({'manifest':str(b/'bundle-manifest.json'),'sourceFeet':{p:reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'maximumTranslationLogical':reg['requiredMaximumTranslationLogicalPixels'],'fullCanvasBounds':{p:reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}},indent=2))
