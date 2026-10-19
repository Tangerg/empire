from pathlib import Path
import json
import hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
s = 44 / 883.5
canonical, canvas, frame, origin = [753, 983], [1561, 1008], [104, 64], [8, 4]
reviews = {
    'idle': 'Accepted clean-shaven adult in rounded steel open-face helmet with cheek flaps and gold forehead teardrop, ivory coat/hood down, slate-blue-gray neck scarf/front panel, steel armor sections, brown diagonal strap/belt/pouch/boots and dark gloves. Screen-left rectangular shield with single gold vertical bar; screen-right wooden staff with gold four-arm-cross disk and open gray hook. Broad matte chunky body/head/hands/boots.',
    'walk-a': 'Front screen-right boot advances toe-up with heel contact and visible sole; rear screen-left boot is flat and weight-bearing. Original shield/staff hands and symbols preserved, complete hook/disk/shaft/butt retained.',
    'walk-b': 'Opposite walking contact: front screen-right boot flat and weight-bearing, rear screen-left heel raised with toe contact. Same camera and equipment hands; not mirrored or a repeated walk-a.',
    'attack': 'Original screen-right connected staff arm and glove extend forward at chest/shoulder height, presenting the tilted complete hooked cross-medallion staff in a clear braced casting gesture. Original screen-left shield arm raises the rectangular shield defensively. Both boots support the pose, exactly one staff/one shield and two arms/hands total. The staff cross rotates with its physical disk, not a new symbol. No external spell fire, aura, projectile or particles baked into the character.'
}
contract = ('C01-UNIT-INQUISITOR binds only c01.inquisitor, the campaign unit named 圣辉审判官. '
            'Keep original idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1 and exact third-row source identity. '
            'The source staff is compatible with the sole c01.inquisitor-flame range2 cross1-area magic/silence action. '
            'Original attack is visually near-idle and walk-b repeats walk-a; this quartet reconstructs a clear staff cast and opposite walking contacts. '
            'C01-EQUIP-INQUISITOR and C01-FX-06 remain separate topics; no independent spell FX is baked into this sprite and no rules change is claimed.')
padding = 'Idle/walk-a/walk-b native1561x1008. Attack native1561x1007 needs only one transparent bottom row in export preparation. Common1561x1008. Accepted idle was generated at this actual size despite an initial1586x992 request; native art was retained without resizing and remaining poses used its actual canvas.'
reg = {
    'status': 'Measured whole-canvas proposal; coordinator export/binding pending',
    'sharedScale': s, 'scaleFraction': '44/883.5', 'bodyReferenceSourcePixels': 883.5, 'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Accepted idle helmet crown y89 to exact two-sole mean y972.5; one body-derived scale applies to all full native pose canvases.',
    'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
    'paddingPolicy': padding, 'canvasPaddingToleranceRequired': 1, 'maximumPadRight': 0, 'maximumPadBottom': 1,
    'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'Whole-canvas translation at locked44/883.5. Anchor x is the mean of two boot bottom-four-row alpha>127 support bands; y is the lowest sole. Exact sole ROIs and two-sole midpoint are preserved per pose.',
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-INQUISITOR', 'actualConsumers': ['c01.inquisitor'],
    'status': 'Native quartet complete; coordinator final sequence review/export/binding pending',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'Every selected master/raw PNG is byte-identical to its native output. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.',
    'sharedScale': s, 'bodyReferenceSourcePixels': 883.5, 'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses, 'commonSourceCanvas': canvas,
    'sourceEvidence': str(b / 'source-evidence.json'), 'registrationEvidence': str(b / 'registration-evidence.json'),
    'semanticContract': contract,
    'symbolContract': 'Four-arm gold cross belongs only inside staff disk; shield keeps single narrow vertical gold bar, helmet keeps small teardrop. No symbol transplant, extra cross or rays.',
    'selectedAttempts': {p: 'first successful native output' for p in poses},
    'nativeCanvasAcceptance': 'Coordinator2026-10-19 17:15UTC accepted actual1561x1008 idle canvas and44/883.5 scale; no regeneration or resize solely to match initial nominal dimensions.',
    'poses': {}
}
for p in poses:
    d = b / p
    q, req = json.loads((d / 'alpha-qa.json').read_text()), json.loads((d / 'request.json').read_text())
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
        'proposedLogicalVisibleBounds': [round(x, 6) for x in visible], 'proposedLogicalFullCanvasBounds': [round(x, 6) for x in full],
        'wholeCanvasFitsProposedFrame': fit,
        'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0, 'interpretation': 'Native alpha=1 specks retained; no silhouette alpha>1 reaches any edge.'}
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
reg['recommendedRegistrationLimitLogicalPixels'] = 4
reg['translationBasis'] = 'Measured boot support yields maximum attack x offset3.984153933 logical, within existing limit4; attack y offset1.494057725. No independent fitting or pose scale changes. Every complete canvas fits104x64.'
(b / 'registration-evidence.json').write_text(json.dumps(reg, indent=2) + '\n')
(b / 'bundle-manifest.json').write_text(json.dumps(m, indent=2) + '\n')
qa = {
    'status': 'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export/binding pending',
    'topic': m['asset'], 'actualConsumers': m['actualConsumers'], 'bodyScale': '44/883.5',
    'selectedNativeCanvasSizes': {p: m['poses'][p]['nativeSize'] for p in poses}, 'commonSourceCanvas': canvas,
    'sourcePaddingRequired': padding, 'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True,
    'allExactPromptsVerified': True, 'alphaPerimeterGate': 'All native perimeter alpha<=1; zero silhouette alpha>1 on edges, alpha=1 specks retained.',
    'nativeAndWhiteBackgroundViewsInspected': poses, 'blackAndWhiteCompositesSaved': poses,
    'identityPass': True, 'shieldAndStaffHandsPreserved': True, 'equipmentCompleteEveryPose': True,
    'symbolContract': m['symbolContract'], 'walkContactsOppose': True, 'attackAnatomyReview': reviews['attack'],
    'semanticContract': contract, 'independentSpellFXBakedIntoCharacter': False,
    'registrationEvidence': 'registration-evidence.json', 'recommendedRegistrationLimitLogicalPixels': 4,
    'proposedFullCanvasFit': all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'runtimeOrRepositoryWrites': False, 'weaponRulesChanged': False, 'gameplayFixClaimed': False
}
(b / 'qa-review.json').write_text(json.dumps(qa, indent=2) + '\n')
for name in ['source-evidence.json', 'idle-review.json']:
    p = b / name
    x = json.loads(p.read_text())
    x['status'] = 'Native quartet complete; coordinator final sequence review/export/binding pending'
    x['quartetManifest'] = 'bundle-manifest.json'
    if name == 'idle-review.json': x['remainingPosesGenerated'] = True
    p.write_text(json.dumps(x, ensure_ascii=False, indent=2) + '\n')
lines = ['# Inquisitor four-pose review', '', contract, '', '## Identity and symbols', reviews['idle'], m['symbolContract'], '',
         '## Geometry', padding, 'Shared44/883.5 scale, canonical foot[753,983].104x64 frame at origin[8,4] retains every complete canvas. Measured maximum3.984153933logical translation is within limit4.', '']
for p in poses:
    r = reg['poses'][p]
    lines += ['### ' + p, reviews[p], f"Source foot {r['sourceSupportAnchorEstimated']}; exact two-sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas offset {r['registrationOffsetLogical']}; native visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.", '']
lines += ['## Provenance and QA', 'All four are first successful native built-in outputs. Every raw/master/native and exact prompt hash matches. Full source board, third-row/idle crops and original HD/runtime four-frame strips are preserved. All native and white-background pose views inspected; black/white QA composites saved. Native alpha=1 specks retained. No repository/runtime writes.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}}, indent=2))
