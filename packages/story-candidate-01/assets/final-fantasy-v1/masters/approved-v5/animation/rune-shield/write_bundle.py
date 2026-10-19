from pathlib import Path
import json
import hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
s = 44 / 904.5
canonical, canvas, frame, origin = [780, 973], [1586, 993], [104, 64], [8, 4]
reviews = {
    'idle': 'Accepted clean-shaven tan adult with uncovered black ponytail, broad dark steel segmented armor/lamellar skirt, square breastplate with gray diamond stud, brown gloves/belt/boots. Original screen-left short-handled rectangular hammer and screen-right octagonal shield. Large head, hands and boots; broad matte cel planes.',
    'walk-a': 'Front screen-right boot advances toe-up with heel contact and visible sole; rear screen-left boot is flat and weight-bearing. Original low hammer hand and octagonal shield arm retained.',
    'walk-b': 'Opposite walking contact: front screen-right boot flat and weight-bearing; rear screen-left heel raised, toe down. Same camera, identity, hammer and shield hands. Not mirrored or repeated walk-a.',
    'attack': 'Clear braced raised-hammer phase of a downward melee strike: original screen-left connected arm raises the compact rectangular hammer high beside the head, retaining its short wooden handle and round face insets. Screen-right shield arm keeps the complete shield defensively forward. Two arms total; hammer glove visible and shield hand naturally concealed behind shield. No independent impact, aura or rune FX.'
}
contract = ('C01-UNIT-RUNE-SHIELD binds only c01.rune-shield, the campaign heavy forge guard. '
            'Preserve original idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1 and fourth-row source identity. '
            'The original compact hammer/shield art matches the sole c01.rune-hammer blunt melee rule. '
            'Original attack is near-idle and original walking contacts are weak; this quartet reconstructs a clear raised-hammer melee action and opposite contacts. '
            'Shared equipment C01-EQUIP-RUNE-ARTIFICER does not remap this unit to distinct c01.rune-artificer. '
            'C01-FX-04 remains separate. No rules change or fifth pose.')
symbols = ('Accepted corrected idle is the exact quartet basis: octagonal dark shield with one central gold open elongated diamond and upper/lower crossing open forks. '
           'The tiny upper inset and dark gray center/lower panel recess details present in that accepted basis are retained consistently. '
           'No additional closed gold lower diamond, symbol transplant, new cross or external glow. '
           'The rune rotates as part of the physical shield face.')
padding = ('Idle/walk-b native1586x992; walk-a/attack native1584x993. Common1586x993 needs only one transparent bottom row for idle/walk-b '
           'and two transparent right columns for walk-a/attack during export preparation. Native masters remain unchanged; no resizing or independent fitting.')
reg = {
    'status': 'Measured whole-canvas proposal; coordinator export/binding pending',
    'sharedScale': s, 'scaleFraction': '44/904.5', 'bodyReferenceSourcePixels': 904.5, 'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Accepted idle crown y64 to exact two-sole mean y968.5. One body-derived scale applies to all complete native canvases.',
    'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
    'paddingPolicy': padding, 'canvasPaddingToleranceRequired': 2, 'maximumPadRight': 2, 'maximumPadBottom': 1,
    'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'Whole-canvas translation at locked44/904.5. Anchor x is mean of both boot bottom-four-row alpha>127 support bands; y is lowest sole. Exact sole ROIs and midpoint preserved per pose.',
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-RUNE-SHIELD', 'actualConsumers': ['c01.rune-shield'],
    'status': 'Native quartet complete; coordinator final sequence review/export/binding pending',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'Every selected master/raw PNG is byte-identical to native output. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.',
    'sharedScale': s, 'bodyReferenceSourcePixels': 904.5, 'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses, 'commonSourceCanvas': canvas,
    'sourceEvidence': str(b/'source-evidence.json'), 'registrationEvidence': str(b/'registration-evidence.json'),
    'semanticContract': contract, 'symbolContract': symbols,
    'selectedAttempts': {'idle': 'attempt-02 targeted source-rune correction; both attempts preserved', 'walk-a': 'first successful native output', 'attack': 'first successful native output', 'walk-b': 'first successful native output'},
    'idleApproval': 'Coordinator2026-10-19 17:31UTC viewed and accepted corrected idle identity, full1586x992 canvas and44/904.5 scale.',
    'poses': {}
}
for p in poses:
    d = b/p
    q, req = json.loads((d/'alpha-qa.json').read_text()), json.loads((d/'request.json').read_text())
    f = q['suggestedSourceFoot']
    off = [(canonical[i]-f[i])*s for i in range(2)]
    full = [origin[0]+off[0], origin[1]+off[1], origin[0]+off[0]+canvas[0]*s, origin[1]+off[1]+canvas[1]*s]
    bounds = q['bboxAlphaGreaterThan127']
    visible = [origin[i%2]+off[i%2]+bounds[i]*s for i in range(4)]
    fit = full[0]>=0 and full[1]>=0 and full[2]<=frame[0] and full[3]<=frame[1]
    reg['poses'][p] = {
        'nativeSize': q['size'], 'padRight': canvas[0]-q['size'][0], 'padBottom': canvas[1]-q['size'][1],
        'sourceSupportAnchorEstimated': f, 'bothBootSoleBandMidpoint': q['sourceFootMidpoint'],
        'registrationOffsetLogical': [round(x,9) for x in off],
        'nativeVisibleBoundsAlphaGreaterThan127': bounds, 'nativeAllNonzeroAlphaBounds': q['bboxAlphaNonzero'],
        'proposedLogicalVisibleBounds': [round(x,6) for x in visible], 'proposedLogicalFullCanvasBounds': [round(x,6) for x in full],
        'wholeCanvasFitsProposedFrame': fit,
        'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1']==0, 'interpretation': 'Native alpha=1 specks retained; no silhouette alpha>1 reaches any edge.'}
    }
    m['poses'][p] = {
        'master': str(d/'master.png'), 'raw': str(d/'raw.png'), 'exactPrompt': str(d/'prompt.txt'), 'request': str(d/'request.json'),
        'exactToolCallPromptVerified': True, 'exactPromptSha256': req['promptSha256'],
        'alphaQA': str(d/'alpha-qa.json'), 'qaBlack': str(d/'qa-black.png'), 'qaWhite': str(d/'qa-white.png'),
        'nativeSize': q['size'], 'sourceFootAnchorSuggested': f, 'visibleBoundsAlphaGreaterThan127': bounds,
        'sha256': q['sha256'], 'generationSource': q['nativeSource'], 'review': reviews[p]
    }
    assert fit and q['perimeterCountAlphaGreaterThan1']==0
    assert hashlib.sha256((d/'master.png').read_bytes()).hexdigest()==q['sha256']==hashlib.sha256((d/'raw.png').read_bytes()).hexdigest()==hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
    assert hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest()==req['promptSha256']
    assert max(canvas[i]-q['size'][i] for i in range(2))<=2
reg['requiredMaximumTranslationLogicalPixels'] = max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels'] = 4
reg['translationBasis'] = f"Measured boot support requires maximum {reg['requiredMaximumTranslationLogicalPixels']} logical translation, within existing limit4. Every complete padded canvas fits104x64 at origin[8,4]. No pose-dependent scaling."
(b/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
(b/'bundle-manifest.json').write_text(json.dumps(m,indent=2)+'\n')
qa = {
    'status': 'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export/binding pending',
    'topic': m['asset'], 'actualConsumers': m['actualConsumers'], 'bodyScale': '44/904.5',
    'selectedNativeCanvasSizes': {p:m['poses'][p]['nativeSize'] for p in poses}, 'commonSourceCanvas': canvas,
    'sourcePaddingRequired': padding, 'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True,
    'allExactPromptsVerified': True, 'alphaPerimeterGate': 'All native perimeter alpha<=1; zero silhouette alpha>1 on edges. Native alpha=1 specks retained.',
    'nativeAndWhiteBackgroundViewsInspected': poses, 'blackAndWhiteCompositesSaved': poses,
    'identityPass': True, 'shieldAndHammerHandsPreserved': True, 'equipmentCompleteEveryPose': True,
    'symbolContract': symbols, 'walkContactsOppose': True, 'attackAnatomyReview': reviews['attack'],
    'semanticContract': contract, 'independentFXBakedIntoCharacter': False,
    'registrationEvidence': 'registration-evidence.json', 'recommendedRegistrationLimitLogicalPixels': 4,
    'proposedFullCanvasFit': all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'runtimeOrRepositoryWrites': False, 'weaponRulesChanged': False, 'gameplayFixClaimed': False
}
(b/'qa-review.json').write_text(json.dumps(qa,indent=2)+'\n')
for name in ['source-evidence.json','idle-review.json']:
    p=b/name; x=json.loads(p.read_text()); x['status']='Native quartet complete; coordinator final sequence review/export/binding pending'; x['quartetManifest']='bundle-manifest.json'
    if name=='idle-review.json': x['remainingPosesGenerated']=True
    p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
lines = ['# Rune-shield four-pose review','',contract,'','## Identity and symbols',reviews['idle'],symbols,'','## Geometry',padding,reg['translationBasis'],'Shared44/904.5; canonical source foot[780,973].','']
for p in poses:
    r=reg['poses'][p]
    lines += ['### '+p,reviews[p],f"Source foot {r['sourceSupportAnchorEstimated']}; exact sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas offset {r['registrationOffsetLogical']}; visible source bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.",'']
lines += ['## Provenance and QA','Idle attempt02 targeted the source rune after attempt01 added gold detailing; both native attempts, exact prompts and references are preserved. Selected corrected idle was reviewed before the remaining poses. All other poses are first successful native built-in outputs. All raw/master/native and prompt hashes match. Complete original board, fourth-row/idle/shield crops and original HD/runtime strips are preserved. All native and white-background views inspected; black/white composites saved. No repository/runtime writes.']
(b/'pose-review.md').write_text('\n'.join(lines)+'\n')
print(json.dumps({'manifest':str(b/'bundle-manifest.json'),'sourceFeet':{p:reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'maximumTranslationLogical':reg['requiredMaximumTranslationLogicalPixels'],'fullCanvasBounds':{p:reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}},indent=2))
