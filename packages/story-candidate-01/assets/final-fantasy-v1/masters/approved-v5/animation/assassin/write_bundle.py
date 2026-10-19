from pathlib import Path
import json
import hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
s = 44 / 942.5
canonical = [805, 975]
canvas = [1586, 992]
frame = [104, 64]
origin = [8, 4]
reviews = {
    'idle': 'Accepted exposed short black hair, close black jaw/chin beard and stern adult face. Charcoal blue-gray raised-collar long coat with cream edge/lapels, cream neck/chest and wrist wraps, brown fingerless gloves/belt/pouch/boots and olive-brown trousers. Screen-left curved dagger held down-right, screen-right upswept dagger held up-right. Broad matte chunky head/hands/boots retained.',
    'walk-a': 'Front screen-right boot advances toe-up with heel contact and visible sole; rear screen-left boot is flat and weight-bearing. Modest arm and coat-tail motion preserves both original dagger hands and full identity.',
    'walk-b': 'Opposite walking contact: front screen-right boot is flat and weight-bearing; rear screen-left heel is lifted with toe contact. Same camera and weapon hands, not a mirrored or repeated walk-a.',
    'attack': 'Clear planted forward lunge: front knee bent and rear leg braced. Original screen-right hand thrusts its own upswept dagger at chest height; original screen-left hand advances its own lower curved dagger near waist. Two connected arms, two hands and two separate grips/guards/blades are readable. Blade tips complete; no third limb, crossed arms, merged weapon, poison haze or new equipment.'
}
contract = ('C01-UNIT-ASSASSIN is the actual art of built-in rogue only, with no c01 campaign protagonist consumer. '
            'Canonical order idle/walk-a/attack/walk-b, original anchor[16,47] and 1x1 rules footprint remain documented. '
            'rogue_blades is piercing melee with a poisoned status effect. The source attack is visually near-idle and its walk-b repeats the first walking phase; '
            'this quartet reconstructs clear dual-dagger melee action and opposite walking contacts while preserving original face, costume and two physical weapon hands. '
            'No external poison effect or rules change is introduced.')
reg = {
    'status': 'Measured whole-canvas proposal; coordinator export/binding pending',
    'sharedScale': s, 'scaleFraction': '44/942.5',
    'bodyReferenceSourcePixels': 942.5, 'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Accepted idle black hair crown y25 to exact two-sole mean y967.5; same body-derived scale applies to all complete native canvases.',
    'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
    'paddingPolicy': 'Walk-a native1585x992 requires one transparent right column in export preparation only. Other native poses are1586x992. All native master/raw pixels remain unchanged; no resize, crop, alpha cleanup or independent fitting.',
    'canvasPaddingToleranceRequired': 1, 'maximumPadRight': 1, 'maximumPadBottom': 0,
    'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'Translate each complete native canvas at locked44/942.5 scale. Anchor x is the mean of the two boot bottom-four-row alpha>127 support bands; y is the lowest sole. Exact ROIs and two-sole midpoint are preserved per pose.',
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-ASSASSIN', 'actualConsumers': ['rogue'],
    'status': 'Native quartet complete; coordinator final sequence review/export/binding pending',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'All selected master/raw PNGs are byte-identical to native outputs. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.',
    'sharedScale': s, 'bodyReferenceSourcePixels': 942.5, 'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses, 'commonSourceCanvas': canvas,
    'sourceEvidence': str(b / 'source-evidence.json'), 'registrationEvidence': str(b / 'registration-evidence.json'),
    'semanticContract': contract,
    'selectedAttempts': {p: 'first successful native output' for p in poses},
    'sourceDetailCaveat': 'The original second-row source visibly has small muted reddish crossed/hatched marks on pale wrist wraps. They are retained as source cloth details; no faction, religious or magical meaning is assigned.',
    'poses': {}
}
for p in poses:
    d = b / p
    q = json.loads((d / 'alpha-qa.json').read_text())
    req = json.loads((d / 'request.json').read_text())
    f = q['suggestedSourceFoot']
    off = [(canonical[i] - f[i]) * s for i in range(2)]
    full = [origin[0] + off[0], origin[1] + off[1], origin[0] + off[0] + canvas[0] * s, origin[1] + off[1] + canvas[1] * s]
    bounds = q['bboxAlphaGreaterThan127']
    visible = [origin[i % 2] + off[i % 2] + bounds[i] * s for i in range(4)]
    fit = full[0] >= 0 and full[1] >= 0 and full[2] <= frame[0] and full[3] <= frame[1]
    reg['poses'][p] = {
        'nativeSize': q['size'], 'padRight': canvas[0] - q['size'][0], 'padBottom': canvas[1] - q['size'][1],
        'sourceSupportAnchorEstimated': f, 'bothBootSoleBandMidpoint': q['sourceFootMidpoint'],
        'registrationOffsetLogical': [round(x, 9) for x in off],
        'nativeVisibleBoundsAlphaGreaterThan127': bounds, 'nativeAllNonzeroAlphaBounds': q['bboxAlphaNonzero'],
        'proposedLogicalVisibleBounds': [round(x, 6) for x in visible],
        'proposedLogicalFullCanvasBounds': [round(x, 6) for x in full],
        'wholeCanvasFitsProposedFrame': fit,
        'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0, 'interpretation': 'Native alpha=1 specks are retained unchanged; no silhouette alpha>1 reaches any canvas edge.'}
    }
    m['poses'][p] = {
        'master': str(d / 'master.png'), 'raw': str(d / 'raw.png'), 'exactPrompt': str(d / 'prompt.txt'), 'request': str(d / 'request.json'),
        'exactToolCallPromptVerified': True, 'exactPromptSha256': req['promptSha256'],
        'alphaQA': str(d / 'alpha-qa.json'), 'qaBlack': str(d / 'qa-black.png'), 'qaWhite': str(d / 'qa-white.png'),
        'nativeSize': q['size'], 'sourceFootAnchorSuggested': f, 'visibleBoundsAlphaGreaterThan127': bounds,
        'sha256': q['sha256'], 'generationSource': q['nativeSource'], 'review': reviews[p]
    }
    assert fit and q['perimeterCountAlphaGreaterThan1'] == 0
    assert hashlib.sha256((d / 'master.png').read_bytes()).hexdigest() == q['sha256'] == hashlib.sha256((d / 'raw.png').read_bytes()).hexdigest() == hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
    assert hashlib.sha256((d / 'prompt.txt').read_bytes()).hexdigest() == req['promptSha256']
reg['requiredMaximumTranslationLogicalPixels'] = max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels'] = 7
reg['translationBasis'] = 'Measured boot support. Attack shifts the complete canvas +6.442440318 logical x and +0.466843501 y at unchanged scale; explicit limit7 is proposed, not a hidden silhouette fit. All registered full canvases remain inside104x64.'
(b / 'registration-evidence.json').write_text(json.dumps(reg, indent=2) + '\n')
(b / 'bundle-manifest.json').write_text(json.dumps(m, indent=2) + '\n')
qa = {
    'status': 'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export/binding pending',
    'topic': m['asset'], 'actualConsumers': m['actualConsumers'], 'bodyScale': '44/942.5',
    'selectedNativeCanvasSizes': {p: m['poses'][p]['nativeSize'] for p in poses},
    'commonSourceCanvas': canvas, 'sourcePaddingRequired': reg['paddingPolicy'],
    'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True, 'allExactPromptsVerified': True,
    'alphaPerimeterGate': 'All native perimeter alpha<=1; zero silhouette alpha>1 on edges. Native alpha=1 specks retained.',
    'nativeAndWhiteBackgroundViewsInspected': poses, 'blackAndWhiteCompositesSaved': poses,
    'identityPass': True, 'bothPhysicalDaggerHandsPreserved': True, 'walkContactsOppose': True,
    'attackAnatomyReview': reviews['attack'], 'sourceWristMarks': m['sourceDetailCaveat'],
    'semanticContract': contract, 'registrationEvidence': 'registration-evidence.json',
    'proposedFullCanvasFit': all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'recommendedRegistrationLimitLogicalPixels': 7, 'runtimeOrRepositoryWrites': False, 'gameplayFixClaimed': False
}
(b / 'qa-review.json').write_text(json.dumps(qa, indent=2) + '\n')
for name in ['source-evidence.json', 'idle-review.json']:
    path = b / name
    x = json.loads(path.read_text())
    x['status'] = 'Native quartet complete; coordinator final sequence review/export/binding pending'
    x['quartetManifest'] = 'bundle-manifest.json'
    if name == 'source-evidence.json':
        x['sourceWristMarksReview'] = m['sourceDetailCaveat']
    if name == 'idle-review.json':
        x['remainingPosesGenerated'] = True
    path.write_text(json.dumps(x, indent=2, ensure_ascii=False) + '\n')
lines = ['# Assassin four-pose review', '', contract, '', '## Identity', reviews['idle'], m['sourceDetailCaveat'], '',
         '## Geometry', 'Locked shared scale44/942.5, canonical foot[805,975], common1586x992 canvas. Walk-a alone needs one transparent right column in export preparation. Proposal104x64 with origin[8,4] preserves every complete source canvas. Attack requires measured limit7; no native art is resized or regenerated for registration.', '']
for p in poses:
    r = reg['poses'][p]
    lines += ['### ' + p, reviews[p], f"Source foot {r['sourceSupportAnchorEstimated']}; exact two-sole midpoint {r['bothBootSoleBandMidpoint']}; logical whole-canvas offset {r['registrationOffsetLogical']}; native visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; complete logical canvas bounds {r['proposedLogicalFullCanvasBounds']}.", '']
lines += ['## Provenance and QA', 'All four selected poses are first successful native built-in outputs. Each preserves raw/master, exact prompt/request hashes, native source path, alpha QA and both black/white composites. Native and white-background views inspected for every pose. All native/raw/master and exact prompt hashes agree. Perimeter passes with alpha=1 specks retained. No repository/runtime writes; coordinator owns sequence review, export and formal binding.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}}, indent=2))
