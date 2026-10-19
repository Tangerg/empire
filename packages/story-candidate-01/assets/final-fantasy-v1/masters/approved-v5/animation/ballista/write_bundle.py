from pathlib import Path
import hashlib
import json

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
s = 76 / 1509
canonical = [922, 983]
canvas = [1586, 992]
frame = [104, 64]
origin = [8, 4]
reviews = {
    'idle': 'Accepted complete four-wheel wooden ballista and one large-headed imperial operator. Steel kettle helmet/cheek flaps, navy/red armor, gold cord, huge bare hands and brown boots; thick timber carriage, metal caps/brackets, disk wheels, rope string and loaded steel-tipped bolt aimed right.',
    'walk-a': 'Operator leans forward pushing both original rear controls. Exposed rearward leg has heel raised/toe contact; opposite boot bears weight forward behind the rear wheel. Diagonal disk-wheel plank seams show rolling. Loaded bolt and complete chassis retained.',
    'walk-b': 'Opposite pushing contact: the forward boot is toe-up/heel-contact beside the rear wheel, opposite boot trails with heel raised. Disk-wheel seam rotation differs from walk-a. Same operator, mechanical proportions, four wheels and loaded right-facing bolt.',
    'attack': 'Unmistakable post-release firing moment: loaded bolt has left the guide, rope string is forward, bow assembly has relaxed and operator pulls the rear control up while bracing. Empty guide and visible released string distinguish attack from idle. Four wheels, complete permanent chassis and operator identity retained. No detached projectile or external effects are added.'
}
contract = ('Exact C01-UNIT-BALLISTA combat-unit contract: idle, walk-a, attack, walk-b. '
            'The original source includes the operator; neither equipment-only art nor the separate deployment platform substitutes for the unit. '
            'Gameplay remains heavy siege/ranged, piercing range 2-4, no move-and-attack. '
            'Walk frames are opposite pushing phases; attack is the post-release shot. '
            'The consumed bolt is absent only in attack, while the whole permanent vehicle remains intact. '
            'The existing separate projectile contract is not changed or duplicated here.')
reg = {
    'status': 'Measured whole-canvas proposal; no runtime writes',
    'sharedScale': s, 'scaleFraction': '76/1509',
    'scaleReference': 'Accepted idle complete-unit visible alpha>127 width 1509 source pixels mapped to 76 logical pixels. Not a humanoid 44-high fit.',
    'visibleUnitReferenceWidthSourcePixels': 1509, 'visibleUnitTargetWidthLogicalPixels': 76,
    'operatorReference': 'Idle helmet-to-exposed-boot comparison is approximately 41.198 logical pixels in an operating stance. Parent reviewed beside 44-high standing infantry; no forced body-height scale.',
    'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
    'paddingPolicy': 'Attack is 1586x992. Idle, walk-a and walk-b are 1585x992 and require exactly one transparent column at the right during export preparation only. Preserve every native master/raw byte. No resize/crop/alpha cleanup or independent fitting.',
    'canvasPaddingToleranceRequired': 1, 'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'For each pose, x is the mean of three visible wheel bottom-four-row alpha>127 support bands; y is the lowest near/front-wheel contact. Explicit wheel ROIs and contact measurements are in each alpha QA file. Fourth far-rear contact is occluded and not invented. Translate the complete source canvas at the single shared scale.',
    'originalRulesFootprint': [2, 2], 'originalRuntimeFrameSize': [64, 48], 'originalRuntimeAnchor': [32, 47],
    'rulesFootprintChanged': False,
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-BALLISTA', 'actualConsumers': ['c01.ballista', 'ballista (built-in)'],
    'status': 'Native quartet visually accepted; formal export/binding pending coordinator',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'All master/raw files are byte-identical to native tool outputs. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.',
    'sharedScale': s, 'scaleFraction': '76/1509', 'visibleUnitReferenceWidthSourcePixels': 1509, 'visibleUnitTargetWidthLogicalPixels': 76,
    'frameOrder': poses, 'commonSourceCanvas': canvas, 'originalRulesFootprint': [2, 2], 'originalRuntimeFrameSize': [64, 48],
    'sourceEvidence': str(b / 'source-evidence.json'), 'registrationEvidence': str(b / 'registration-evidence.json'),
    'semanticContract': contract,
    'acceptanceEvidence': 'Parent 2026-10-19 16:11 UTC accepted idle width/operator mass beside 44-high swordsman; parent 16:19 UTC accepted native quartet opposite pushing contacts and post-release empty-guide attack.',
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
        'sourceSupportAnchorEstimated': f, 'visibleWheelContactMidpoint': q['sourceGroundContactMidpoint'], 'wheelContacts': q['wheelContacts'],
        'registrationOffsetLogical': [round(x, 9) for x in off],
        'nativeVisibleBoundsAlphaGreaterThan127': bounds, 'nativeAllNonzeroAlphaBounds': q['bboxAlphaNonzero'],
        'proposedLogicalVisibleBounds': [round(x, 6) for x in visible], 'proposedLogicalFullCanvasBounds': [round(x, 6) for x in full],
        'wholeCanvasFitsProposedFrame': fit,
        'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0, 'interpretation': 'Native alpha=1 specks retained; no silhouette alpha>1 crosses a source edge.'}
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
reg['translationBasis'] = 'Measured wheel-ground contacts only; maximum shift is below one logical pixel. No shared-scale changes.'
(b / 'registration-evidence.json').write_text(json.dumps(reg, indent=2) + '\n')
(b / 'bundle-manifest.json').write_text(json.dumps(m, indent=2) + '\n')
qa = {
    'status': 'Native quartet visually accepted; all source integrity and geometry gates pass; formal export/binding pending coordinator',
    'topic': m['asset'], 'actualConsumers': m['actualConsumers'], 'scale': '76/1509',
    'scaleBasis': reg['scaleReference'], 'operatorReference': reg['operatorReference'],
    'selectedNativeCanvasSizes': {p: m['poses'][p]['nativeSize'] for p in poses},
    'sourcePaddingRequired': 'Idle, walk-a and walk-b: one transparent right column only. Attack: none.',
    'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True, 'allExactPromptsVerified': True,
    'alphaPerimeterGate': 'All native perimeter alpha<=1; zero silhouette alpha>1 on edges. Native alpha=1 specks retained unchanged.',
    'blackAndWhiteCompositesVisuallyInspected': poses, 'originalOperatorIdentityRetained': True, 'fourWheelChassisRetained': True,
    'mechanicalAndHumanVolumeConsistent': True, 'walkContactsOppose': True, 'wheelDiskRotationChanges': True,
    'attackSemanticsPass': reviews['attack'], 'semanticContract': contract,
    'originalRulesFootprint': [2, 2], 'rulesFootprintChanged': False, 'originalRuntimeFrameSize': [64, 48],
    'displayFrameQualification': 'Proposed 104x64 display frame preserves full native canvases and does not redefine rules occupancy.',
    'registrationEvidence': 'registration-evidence.json', 'proposedFullCanvasFit': all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'runtimeOrRepositoryWrites': False
}
(b / 'qa-review.json').write_text(json.dumps(qa, indent=2) + '\n')
for name in ['source-evidence.json', 'idle-review.json']:
    path = b / name
    x = json.loads(path.read_text())
    x['status'] = 'Native quartet complete and visually accepted; formal export/binding pending coordinator'
    x['quartetManifest'] = 'bundle-manifest.json'
    if name == 'idle-review.json':
        x['remainingPosesGenerated'] = True
        x['nextGate'] = 'Coordinator formal full-canvas export, runtime sequence review and binding'
    path.write_text(json.dumps(x, indent=2, ensure_ascii=False) + '\n')
lines = ['# Ballista four-pose review', '',
         'C01-UNIT-BALLISTA is the actual character-plus-machine combat unit bound to c01.ballista and built-in ballista. The original board, first-row/idle crops and runtime-HD/runtime strips are preserved in references/.', '',
         '## Scale and rules contract', reg['scaleReference'], reg['operatorReference'],
         'Canonical wheel-ground anchor [922,983]. Common canvas 1586x992; three native 1585x992 frames need one transparent right column in export only. The old rules footprint remains 2x2. The old 64x48 frame is evidence of the source contract, not a crop limit for the new art. A 104x64 display frame with origin [8,4] contains every complete registered native canvas.', '']
for p in poses:
    r = reg['poses'][p]
    lines += ['### ' + p, reviews[p], f"Wheel-ground anchor {r['sourceSupportAnchorEstimated']}; measured three-contact midpoint {r['visibleWheelContactMidpoint']}; logical whole-canvas offset {r['registrationOffsetLogical']}; native visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}.", '']
lines += ['## Action semantics', contract,
          'Operator feet are animation evidence, not the registration pivot. Registration uses the same three observable wheel supports; the occluded fourth contact is never invented. A changed wheel-disk seam orientation signals rolling without adding spokes or changing diameter.', '',
          '## Provenance and QA', 'All four selected masters are first successful native built-in image-generation outputs. Exact prompts, requests, raw PNGs, source references, wheel measurement ROIs and black/white QA are saved. Idle is additionally retained under attempt-01. Every native/master/raw hash and exact prompt hash was verified. No generated master was resized, cropped, recolored, repainted or alpha-cleaned; no runtime or repository write was made.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}}, indent=2))
