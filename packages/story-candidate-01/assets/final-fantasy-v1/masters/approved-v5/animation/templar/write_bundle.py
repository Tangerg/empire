from pathlib import Path
import json
import hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
s = 44 / 944.5
canonical, canvas, frame, origin = [753, 973], [1586, 992], [104, 64], [8, 4]
reviews = {
    'idle': 'Accepted uncovered silver-gray swept-back hair and short gray beard/moustache, older tan face, ivory/gold armored surcoat and steel shoulder/arm/hip/knee/leg/boot plates. Large bare sword hand screen-left; tall cream shield with one gold vertical bar screen-right. Broad matte chunky head/body/hands/boots.',
    'walk-a': 'Front screen-right steel boot advances toe-up with heel contact and visible sole; rear screen-left boot remains flat and weight-bearing. Same sword and shield remain in original physical hands. Small arm/surcoat motion; no shield disappearance.',
    'walk-b': 'Opposite walking contact: front screen-right steel boot is flat and weight-bearing, rear screen-left heel is raised with toe contact. Same camera and sword/shield hands; not mirrored or repeated walk-a.',
    'attack': 'Clear planted sword thrust with front bent knee and rear extended bracing leg. Original screen-left connected arm and bare fist extend the compact sword horizontally in front of the shield. Original shield arm remains naturally occluded behind its full shield. One sword, one shield, two arms/hands total; no limb duplication, shield penetration or hand swap. Selected correction restores about330source-pixel blade length, matching the accepted short-sword identity.'
}
discrepancies = [
    {'kind': 'Original frame equipment continuity', 'status': 'Visual continuity repaired with coordinator approval', 'detail': 'Original board and HD idle/attack carry sword and shield; original walk-a/walk-b remove shield and introduce a second sword. Remake consistently retains the approved idle sword-and-shield identity in all four poses.'},
    {'kind': 'Rule weapon versus original illustration', 'status': 'Existing mismatch retained and documented, not a gameplay fix', 'detail': 'c01.templar rules list c01.legion-spear and c01.inquisitor-flame, while canonical artwork shows an uncovered gray-haired sword-and-shield veteran. No spear, new symbol or flame is invented, no topic remap occurs and no weapon rules are changed.'},
    {'kind': 'Single action slot', 'status': 'Existing shared-action limitation retained', 'detail': 'One original attack slot serves both rule weapons. The source-faithful sword thrust remains the shared illustration for those actions. No fifth pose or weapon-specific animation dispatch is introduced.'}
]
contract = 'C01-UNIT-TEMPLAR binds only c01.templar. Preserve original idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1 and uncovered gray-haired ivory/gold sword-and-shield identity. The shield has one long gold vertical bar, not a cross; the cross-bearing staff belongs to the distinct inquisitor row. Original equipment continuity and rule-to-art discrepancies are explicitly documented.'
padding = 'Native idle/walk-a/attack are1585x992 and each require one transparent right column in export preparation; walk-b is1586x992. Common1586x992. No native resizing, cropping, painting, recoloring or alpha cleanup; full canvas preserved.'
reg = {
    'status': 'Measured whole-canvas proposal; coordinator export/binding pending',
    'sharedScale': s, 'scaleFraction': '44/944.5', 'bodyReferenceSourcePixels': 944.5, 'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Accepted idle gray-hair crown y24 to exact two-sole mean y968.5; one body-derived scale applies to every complete native pose canvas.',
    'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
    'paddingPolicy': padding, 'canvasPaddingToleranceRequired': 1, 'maximumPadRight': 1, 'maximumPadBottom': 0,
    'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'Whole-canvas translation at locked44/944.5. Anchor x is mean of the two boot bottom-four-row alpha>127 support bands; y is lowest sole. Exact sole ROIs and two-sole midpoint preserved per pose.',
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-TEMPLAR', 'actualConsumers': ['c01.templar'],
    'status': 'Native quartet complete; coordinator final sequence review/export/binding pending',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'Selected master/raw files are byte-identical to native outputs. No programmatic image editing; black/white composites are QA only.',
    'sharedScale': s, 'bodyReferenceSourcePixels': 944.5, 'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses, 'commonSourceCanvas': canvas,
    'sourceEvidence': str(b / 'source-evidence.json'), 'registrationEvidence': str(b / 'registration-evidence.json'),
    'semanticContract': contract, 'sourceAndRulesDiscrepancies': discrepancies,
    'selectedAttempts': {'idle': 'attempt-01', 'walk-a': 'first successful native output', 'attack': 'attempt-02', 'walk-b': 'first successful native output'},
    'attackCorrection': 'First native attack made the blade about490source pixels long versus idle about315. Preserved attack/attempt-01. Selected native attempt-02 restores about330source-pixel compact blade while retaining attack body/lunge/shield. Exact edit-call input path snapshot is preserved and hashed in request.json because the selected root master later advances to attempt02.',
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
        'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0, 'interpretation': 'Native alpha=1 specks retained; no silhouette alpha>1 on any edge.'}
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
    for snap in req.get('inputSnapshots', []):
        assert hashlib.sha256(Path(snap.get('preservedSnapshot', snap['toolCallPath'])).read_bytes()).hexdigest() == snap['sha256']
reg['requiredMaximumTranslationLogicalPixels'] = max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels'] = 8
reg['translationBasis'] = 'Measured feet require attack whole-canvas offset[7.407093700,1.537321334] logical at unchanged scale. Recommend explicit limit8. This is not a hidden independent fit, and every complete canvas remains inside104x64.'
(b / 'registration-evidence.json').write_text(json.dumps(reg, indent=2) + '\n')
(b / 'bundle-manifest.json').write_text(json.dumps(m, indent=2) + '\n')
qa = {
    'status': 'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export/binding pending',
    'topic': m['asset'], 'actualConsumers': m['actualConsumers'], 'bodyScale': '44/944.5',
    'selectedNativeCanvasSizes': {p: m['poses'][p]['nativeSize'] for p in poses}, 'commonSourceCanvas': canvas,
    'sourcePaddingRequired': padding, 'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True,
    'allExactPromptsVerified': True, 'attackEditInputSnapshotsHashVerified': True,
    'alphaPerimeterGate': 'Native perimeter alpha<=1 in all selected poses; zero silhouette alpha>1 on edges, alpha=1 specks retained.',
    'nativeAndWhiteBackgroundViewsInspected': poses, 'blackAndWhiteCompositesSaved': poses,
    'identityPass': True, 'swordAndShieldHandsPreserved': True, 'shieldRetainedEveryPose': True, 'singleVerticalGoldBarNoCross': True,
    'walkContactsOppose': True, 'attackAnatomyReview': reviews['attack'], 'shortSwordCorrection': m['attackCorrection'],
    'semanticContract': contract, 'sourceAndRulesDiscrepancies': discrepancies,
    'registrationEvidence': 'registration-evidence.json', 'recommendedRegistrationLimitLogicalPixels': 8,
    'proposedFullCanvasFit': all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'runtimeOrRepositoryWrites': False, 'weaponRulesChanged': False, 'gameplayFixClaimed': False
}
(b / 'qa-review.json').write_text(json.dumps(qa, indent=2) + '\n')
for name in ['source-evidence.json', 'idle-review.json']:
    p = b / name
    x = json.loads(p.read_text())
    x['status'] = 'Native quartet complete; coordinator final sequence review/export/binding pending'
    x['quartetManifest'] = 'bundle-manifest.json'
    if name == 'source-evidence.json': x['sourceDiscrepancies'] = discrepancies
    if name == 'idle-review.json': x['remainingPosesGenerated'] = True
    p.write_text(json.dumps(x, ensure_ascii=False, indent=2) + '\n')
lines = ['# Templar four-pose review', '', contract, '', '## Identity', reviews['idle'], '', '## Geometry', padding,
         'Locked44/944.5 body scale, canonical foot[753,973]. Proposal104x64 at origin[8,4] preserves all complete canvases. Attack requires measured limit8; no native resizing or registration-driven regeneration.', '']
for p in poses:
    r = reg['poses'][p]
    lines += ['### ' + p, reviews[p], f"Source foot {r['sourceSupportAnchorEstimated']}; two-sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas logical offset {r['registrationOffsetLogical']}; native visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.", '']
lines += ['## Source and rule discrepancies']
for x in discrepancies: lines += [x['kind'] + ': ' + x['status'] + '. ' + x['detail'], '']
lines += ['## Provenance and QA', m['attackCorrection'], 'Idle and both walks are their first successful native outputs. Both attack attempts retain native/raw, exact prompt/request and alpha QA. Selected raw/master/native and prompt hashes all agree; edit input snapshots also verified. Native and white-background views inspected for every pose; black/white QA saved. Native alpha=1 specks retained. No repository/runtime writes.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}}, indent=2))
