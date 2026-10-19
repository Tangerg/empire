from pathlib import Path
import json, hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
scale = 44 / 860
canonical = [843, 979]
canvas, frame, origin = [1586, 992], [104, 64], [8, 4]
source = json.loads((b / 'source-evidence.json').read_text())
identity = source['originalIdentity'] + ' ' + source['equipmentIdentity']
contract = ('C01-UNIT-SKELETON-GUARD binds only c01.skeleton-guard, with eleven initial enemy actors across c01-03, c01-11, c01-12 and c01-16. '
            'Retain original idle/walk-a/attack/walk-b slots,32x48 original frame,anchor[16,47],footprint[1,1]. '
            'Original third cell was near idle and both original walks had similar contact. Reconstructed opposite walks and an original-left-hand controlled spear sweep; original-right-hand plain wooden shield retained in all poses. '
            'Existing rule weapon c01.undead-blade/回声残刃 uses slash while original illustration consistently depicts spear/shield. No sword invented, rule remap, fifth frame or gameplay fix claimed. Independent C01-FX-01 remains separate.')
pending = ('Attack candidate approximate projected spear span1134px versus875idle,865walk-a,877walk-b after an imagegen-only correction of excessive first attack. '
           'Projected2Dspan is diagnostic, not proof of identical3Dlength. No independent source/weapon fit was performed. Coordinator shared-scale native sequence review must judge continuity before formal binding.')
padding = 'Native idle/walk-a/attack1586x992; walk-b1585x992. Preserve all native files; only1transparent right column is needed for walk-b common-source export. No resizing/cropping/alpha cleanup.'
reg = {'status': 'Measured whole-canvas registration proposal; native sequence review pending',
       'sharedScale': scale, 'scaleFraction': '44/860', 'bodyReferenceSourcePixels': 860,
       'bodyTargetHeightLogicalPixels': 44, 'bodyMeasurement': 'Approved idle main helmet crown114 to mean boot sole974; spear excluded from body denominator, whole canvas retained.',
       'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
       'paddingPolicy': padding, 'canvasPaddingToleranceRequired': 1, 'maximumPadRight': 1, 'maximumPadBottom': 0,
       'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
       'registrationMethod': 'Translate complete source canvas at locked44/860. Foot x averages the two boot bottom-four-row alpha>127 sole bands; y is lowest sole. Boot-only ROIs and exact average retained.',
       'poses': {}}
manifest = {'asset': 'C01-UNIT-SKELETON-GUARD', 'actualConsumers': ['c01.skeleton-guard'],
            'consumerStatus': source['consumerStatus'], 'status': 'Quartet/evidence complete; attack continuity and native shared-scale sequence review pending',
            'generationTool': 'built-in image_gen', 'masterPolicy': 'Native raw/master unchanged byte-for-byte. No programmatic painting,recoloring,cropping,resizing or alpha cleanup. White/black composites are QA only.',
            'sharedScale': scale, 'bodyReferenceSourcePixels': 860, 'bodyTargetHeightLogicalPixels': 44,
            'frameOrder': poses, 'commonSourceCanvas': canvas, 'sourceEvidence': str(b / 'source-evidence.json'),
            'registrationEvidence': str(b / 'registration-evidence.json'), 'identityContract': identity,
            'symbolContract': source['symbolReview'], 'semanticContract': contract,
            'continuityReviewRequired': pending,
            'selectedAttempts': {'idle': 'first successful native output', 'walk-a': 'first successful native output',
                                 'attack': 'attempt-02, targeted spear-length correction; attempt-01 preserved rejected for excessive length',
                                 'walk-b': 'first successful native output'},
            'idleApproval': 'Coordinator2026-10-19 19:08UTC native/2x idle accepted44/860,sourcefoot[843,979].',
            'attackCandidateReview': 'Coordinator19:14UTC accepted grip/anatomy/stance for current candidate; ordered no further generation before shared-scale quartet review, spear continuity still pending.',
            'poses': {}}

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()

for pose in poses:
    d = b / pose
    q = json.loads((d / 'alpha-qa.json').read_text())
    req = json.loads((d / 'request.json').read_text())
    foot = q['suggestedSourceFoot']
    off = [(canonical[i] - foot[i]) * scale for i in range(2)]
    full = [origin[0] + off[0], origin[1] + off[1], origin[0] + off[0] + canvas[0] * scale, origin[1] + off[1] + canvas[1] * scale]
    bounds = q['bboxAlphaGreaterThan127']
    visible = [origin[i % 2] + off[i % 2] + bounds[i] * scale for i in range(4)]
    pad = [canvas[i] - q['size'][i] for i in range(2)]
    fit = full[0] >= 0 and full[1] >= 0 and full[2] <= frame[0] and full[3] <= frame[1]
    reg['poses'][pose] = {'nativeSize': q['size'], 'padRight': pad[0], 'padBottom': pad[1],
                          'sourceSupportAnchorEstimated': foot, 'bothBootSoleBandMidpoint': q['sourceFootMidpoint'],
                          'registrationOffsetLogical': [round(v, 9) for v in off],
                          'nativeVisibleBoundsAlphaGreaterThan127': bounds, 'nativeAllNonzeroAlphaBounds': q['bboxAlphaNonzero'],
                          'proposedLogicalVisibleBounds': [round(v, 6) for v in visible],
                          'proposedLogicalFullCanvasBounds': [round(v, 6) for v in full],
                          'wholeCanvasFitsProposedFrame': fit,
                          'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0}}
    manifest['poses'][pose] = {'master': str(d / 'master.png'), 'raw': str(d / 'raw.png'),
                              'exactPrompt': str(d / 'prompt.txt'), 'request': str(d / 'request.json'),
                              'exactToolCallPromptVerified': True, 'exactPromptSha256': req['promptSha256'],
                              'alphaQA': str(d / 'alpha-qa.json'), 'qaBlack': str(d / 'qa-black.png'), 'qaWhite': str(d / 'qa-white.png'),
                              'nativeSize': q['size'], 'sourceFootAnchorSuggested': foot, 'visibleBoundsAlphaGreaterThan127': bounds,
                              'sha256': q['sha256'], 'generationSource': q['nativeSource'],
                              'review': q['semanticReview'], 'skeletalNegativeSpaceProbe': q['skeletalNegativeSpaceProbe'],
                              'spearProjectionReview': q['spearProjectionReview']}
    assert fit and max(pad) <= 1 and min(pad) >= 0 and q['perimeterCountAlphaGreaterThan1'] == 0
    assert q['skeletalNegativeSpaceProbe']['maxAlpha'] == 0
    assert sha(d / 'master.png') == q['sha256'] == sha(d / 'raw.png') == sha(q['nativeSource'])
    assert sha(d / 'prompt.txt') == req['promptSha256']
    for path, expected in req.get('referencedImageSha256', {}).items(): assert sha(path) == expected

reg['requiredMaximumTranslationLogicalPixels'] = max(abs(v) for q in reg['poses'].values() for v in q['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels'] = 8
reg['translationBasis'] = 'Attack sourcefoot[704,949] against idle[843,979] gives source delta[139,30],whole-canvas offset[7.111627907,1.534883721]logical. All padded complete canvases fit104x64 at origin[8,4],explicit limit8. No independent fit.'
qa = {'status': manifest['status'], 'topic': manifest['asset'], 'actualConsumers': manifest['actualConsumers'],
      'bodyScale': '44/860', 'selectedNativeCanvasSizes': {p: manifest['poses'][p]['nativeSize'] for p in poses},
      'commonSourceCanvas': canvas, 'sourcePaddingRequired': padding, 'nativeMastersUnmodified': True,
      'allRawMasterNativeHashesMatch': True, 'allExactPromptsVerified': True,
      'alphaPerimeterGate': 'All selected native perimeters have0pixels alpha>1. Idle/walk-a/attack max0; walk-b max1 specks retained, no cleanup.',
      'nativeAndWhiteBackgroundViewsInspected': poses, 'blackAndWhiteCompositesSaved': poses,
      'identityPreserved': True, 'originalEquipmentHandsPreserved': True, 'twoArmsAndTwoHandsOnly': True,
      'walkContactsOppose': True, 'skeletalNegativeSpaceNativeProbesAllZero': True,
      'socketAmberBoundedWithoutExternalGlow': True, 'semanticContract': contract,
      'symbolContract': source['symbolReview'], 'weaponContinuityReview': pending,
      'independentFXBakedIntoCharacter': False, 'registrationEvidence': 'registration-evidence.json',
      'recommendedRegistrationLimitLogicalPixels': 8,
      'proposedFullCanvasFit': all(p['wholeCanvasFitsProposedFrame'] for p in reg['poses'].values()),
      'runtimeOrRepositoryWrites': False, 'weaponRulesChanged': False, 'gameplayFixClaimed': False}

for filename, data in [('registration-evidence.json', reg), ('bundle-manifest.json', manifest), ('qa-review.json', qa)]:
    (b / filename).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
for filename in ['source-evidence.json', 'idle-review.json']:
    p = b / filename
    data = json.loads(p.read_text())
    data['status'] = manifest['status']
    data['quartetManifest'] = 'bundle-manifest.json'
    data['weaponContinuityReview'] = pending
    if filename == 'idle-review.json': data['remainingPosesGenerated'] = True
    p.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')

for filename, r in source['references'].items(): assert sha(b / 'references' / filename) == r['sha256']
for attempt in ['attempt-01', 'attempt-02']:
    d = b / 'attack' / attempt
    q, r = json.loads((d / 'alpha-qa.json').read_text()), json.loads((d / 'request.json').read_text())
    assert sha(d / 'master.png') == q['sha256'] == sha(d / 'raw.png') == sha(q['nativeSource'])
    assert sha(d / 'prompt.txt') == r['promptSha256']

lines = ['# Skeleton-guard four-pose review', '', contract, '', '## Identity and symbols', identity, source['symbolReview'], '', '## Geometry', padding, 'Shared44/860;canonical sourcefoot[843,979].', reg['translationBasis'], '']
for pose in poses:
    r, p = reg['poses'][pose], manifest['poses'][pose]
    lines += ['### ' + pose, p['review'], f"Sourcefoot {r['sourceSupportAnchorEstimated']}; exact sole midpoint {r['bothBootSoleBandMidpoint']}; full-canvas offset {r['registrationOffsetLogical']}; visible source bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.", '']
lines += ['## Pending native continuity gate', pending, 'Do not call exact physical spear length invariant from these2Dimages. Coordinator explicitly requested retaining corrected attack until native shared-scale quartet review. No further regeneration performed.', '', '## Provenance and alpha', 'Attack selects native attempt02; excessive-length attempt01 preserved with exact prompt/raw/QA. Other poses first native successes. Original complete board/strips and reference crops saved. Raw/master/native hashes and exact prompt hashes verified. Native and white views inspected; black and white composites saved. All4leg-gap7x7probes fully transparent; eye glow confined to dark sockets. All native perimeter alpha>1 counts zero. No repository writes.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}, 'integrityChecks': 'PASS', 'finalReview': 'coordinator native spear-length/body continuity review pending'}, indent=2))
