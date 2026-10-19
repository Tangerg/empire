from pathlib import Path
import json, hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
scale, denominator, target = 56 / 950.5, 950.5, 56
canonical, canvas, frame, origin = [994, 983], [1586, 992], [128, 80], [14, 6]
source = json.loads((b / 'source-evidence.json').read_text())
identity = source['originalIdentity']
contract = ('C01-UNIT-IVRA-GROWTH binds only c01.ivra. Eight initial campaign appearances: neutral caged-ivra in c01-09 and allied campaign-ivra in c01-10 through c01-16. '
            'The original second atlas row is one juvenile copper-red dragon across idle / walk-a / attack / walk-b, not four age stages. '
            'Preserve four legs, two wings, gray natural belly plates and horn crown, gold eye, and the small white branching left-neck scar. '
            'Walks show opposite diagonal paw contacts. Attack is a braced forward head and open-mouth breath-ready gesture; independent C01-FX-05 remains separate. '
            'Grounded source gait is retained despite the existing flying movement rule; no flight slot or rule change. '
            'Original 64x64 frame, anchor [32,63] and footprint [2,2] remain original ART metadata. They do not establish or introduce multi-cell gameplay occupancy. Narrative age cards and equipment icons remain separate.')
padding = 'Native idle and walk-a are 1586x992; attack and walk-b are 1585x992. Export may add exactly one transparent right column to those two canvases. All native masters remain unchanged.'
scale_basis = ('Coordinator accepted a 56 logical-pixel young dragon versus 44 logical-pixel humans. Selected idle attempt-02 horn tip y24 to mean near-side hind/fore sole y974.5 gives 950.5 source pixels. '
               'The shared scale is 56/950.5 for every pose. Idle visible width is 1140 source pixels, or 67.164650 logical pixels after the approved bounded tail curl. '
               'Source contact anchor x averages four visible paw sole-band centers; y is the lowest paw sole. The partially hidden idle far-hind sole is explicitly an estimate from its visible part.')
variation = ('Attack projected width is 1437 source pixels / 84.662809 logical pixels, versus idle 1140 / 67.164650, walk-a 1171 / 68.991057 and walk-b 1147 / 67.577065. '
             'The wider attack includes spread wings, a lowered forward-reaching head and a braced stance. It remains at exactly the same export scale; exact silhouette-size invariance is not claimed. '
             'Coordinator shared-scale native sequence review must judge this variation before formal binding.')
status = 'Native quartet and evidence complete; coordinator shared-scale sequence review pending'
reg = {'status': status, 'sharedScale': scale, 'scaleFraction': '56/950.5', 'bodyReferenceSourcePixels': denominator,
       'bodyTargetHeightLogicalPixels': target, 'bodyMeasurement': scale_basis, 'canonicalSourceFoot': canonical,
       'commonSourceCanvas': canvas, 'paddingPolicy': padding, 'canvasPaddingToleranceRequired': 1,
       'maximumPadRight': 1, 'maximumPadBottom': 0, 'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
       'registrationMethod': 'Translate the complete native canvas at locked 56/950.5. Each paw is measured in a separate ROI at alpha >127; x is the average of four bottom-four-row sole-band centers, y is the lowest sole. Exact four-paw averages are retained.',
       'projectionVariation': variation, 'poses': {}}
manifest = {'asset': 'C01-UNIT-IVRA-GROWTH', 'actualConsumers': ['c01.ivra'], 'consumerStatus': source['consumerStatus'],
            'status': status, 'generationTool': 'built-in image_gen',
            'masterPolicy': 'Native raw/master are byte-identical; no painting, recoloring, cropping, resizing or alpha cleanup. White/black composites are QA only.',
            'sharedScale': scale, 'bodyReferenceSourcePixels': denominator, 'bodyTargetHeightLogicalPixels': target,
            'speciesScaleContract': scale_basis, 'frameOrder': poses, 'commonSourceCanvas': canvas,
            'sourceEvidence': str(b / 'source-evidence.json'), 'registrationEvidence': str(b / 'registration-evidence.json'),
            'identityContract': identity, 'symbolContract': source['symbolReview'], 'semanticContract': contract,
            'projectionVariation': variation,
            'selectedAttempts': {'idle': 'attempt-02: approved bounded tail curl', 'walk-a': 'first successful native output', 'attack': 'first successful native output', 'walk-b': 'first successful native output'},
            'supersededAttempts': [{'pose': 'idle', 'directory': 'idle/attempt-01', 'reason': 'Excessive 78.087 logical-pixel width; coordinator requested a bounded tail-only inward curl, preserving the body scale.'}],
            'idleApproval': 'Coordinator 2026-10-19 19:46 UTC approved corrected attempt-02, shared 56/950.5, foot [994,983], frame 128x80 and origin [14,6].', 'poses': {}}

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()

for pose in poses:
    d = b / pose
    q, req = json.loads((d / 'alpha-qa.json').read_text()), json.loads((d / 'request.json').read_text())
    foot = q['suggestedSourceFoot']
    off = [(canonical[i] - foot[i]) * scale for i in range(2)]
    full = [origin[0] + off[0], origin[1] + off[1], origin[0] + off[0] + canvas[0] * scale, origin[1] + off[1] + canvas[1] * scale]
    bounds = q['bboxAlphaGreaterThan127']
    visible = [origin[i % 2] + off[i % 2] + bounds[i] * scale for i in range(4)]
    pad = [canvas[i] - q['size'][i] for i in range(2)]
    fit = full[0] >= 0 and full[1] >= 0 and full[2] <= frame[0] and full[3] <= frame[1]
    reg['poses'][pose] = {'nativeSize': q['size'], 'padRight': pad[0], 'padBottom': pad[1],
                          'sourceSupportAnchorEstimated': foot, 'fourPawSoleBandMidpoint': q['sourceFootMidpoint'],
                          'registrationOffsetLogical': [round(v, 9) for v in off],
                          'nativeVisibleBoundsAlphaGreaterThan127': bounds, 'nativeAllNonzeroAlphaBounds': q['bboxAlphaNonzero'],
                          'logicalVisibleWidth': (bounds[2]-bounds[0])*scale,
                          'proposedLogicalVisibleBounds': [round(v, 6) for v in visible],
                          'proposedLogicalFullCanvasBounds': [round(v, 6) for v in full], 'wholeCanvasFitsProposedFrame': fit,
                          'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0}}
    manifest['poses'][pose] = {'master': str(d / 'master.png'), 'raw': str(d / 'raw.png'), 'exactPrompt': str(d / 'prompt.txt'),
                              'request': str(d / 'request.json'), 'exactToolCallPromptVerified': True, 'exactPromptSha256': req['promptSha256'],
                              'alphaQA': str(d / 'alpha-qa.json'), 'qaBlack': str(d / 'qa-black.png'), 'qaWhite': str(d / 'qa-white.png'),
                              'nativeSize': q['size'], 'sourceFootAnchorSuggested': foot, 'visibleBoundsAlphaGreaterThan127': bounds,
                              'sha256': q['sha256'], 'generationSource': q['nativeSource'], 'review': q['semanticReview'],
                              'negativeSpaceProbes': q['negativeSpaceProbes']}
    assert fit and max(pad) <= 1 and min(pad) >= 0 and q['perimeterCountAlphaGreaterThan1'] == 0
    assert all(p['maxAlpha'] == 0 and p['zeroPixels'] == 49 for p in q['negativeSpaceProbes'])
    assert sha(d / 'master.png') == q['sha256'] == sha(d / 'raw.png') == sha(q['nativeSource'])
    assert sha(d / 'prompt.txt') == req['promptSha256']
    for path, expected in req.get('referencedImageSha256', {}).items(): assert sha(path) == expected

reg['requiredMaximumTranslationLogicalPixels'] = max(abs(v) for q in reg['poses'].values() for v in q['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels'] = 4
reg['translationBasis'] = 'Largest offset is walk-a: foot [1018,967] against canonical [994,983], source delta [-24,16], logical [-1.413992635,0.942661757]. Existing limit 4 is sufficient. Every complete common canvas fits 128x80 at origin [14,6]. No independent pose fit.'
qa = {'status': status, 'topic': manifest['asset'], 'actualConsumers': manifest['actualConsumers'], 'bodyScale': '56/950.5',
      'speciesScaleContract': scale_basis, 'selectedNativeCanvasSizes': {p: manifest['poses'][p]['nativeSize'] for p in poses},
      'commonSourceCanvas': canvas, 'sourcePaddingRequired': padding, 'nativeMastersUnmodified': True,
      'allRawMasterNativeHashesMatch': True, 'allExactPromptsVerified': True,
      'alphaPerimeterGate': 'All selected native perimeters have zero pixels alpha >1. Idle, attack and walk-b max 0; walk-a max 1 native specks retained. No cleanup.',
      'nativeAndWhiteBackgroundViewsInspected': poses, 'blackAndWhiteCompositesSaved': poses,
      'identityPreserved': True, 'fourConnectedLegsAndTwoWingsVisuallyChecked': True, 'walkContactsOppose': True,
      'allNegativeSpaceProbesZeroAlpha': True,
      'attackSemantics': 'Four paws braced, head forward, open mouth with contained tongue and fangs; original pair of wings spread. No independent breath plume or aura.',
      'projectionVariationRequiresCoordinatorReview': variation, 'semanticContract': contract, 'symbolContract': source['symbolReview'],
      'independentFXBakedIntoCharacter': False, 'registrationEvidence': 'registration-evidence.json',
      'recommendedRegistrationLimitLogicalPixels': 4, 'proposedFullCanvasFit': all(p['wholeCanvasFitsProposedFrame'] for p in reg['poses'].values()),
      'runtimeOrRepositoryWrites': False, 'weaponRulesChanged': False, 'gameplayMultiCellOccupancyClaimed': False,
      'gameplayFixClaimed': False}

for filename, data in [('registration-evidence.json', reg), ('bundle-manifest.json', manifest), ('qa-review.json', qa)]:
    # Keep a later coordinator decision if this evidence builder is rerun.
    dest = b / filename
    if dest.exists():
        old = json.loads(dest.read_text())
        if 'coordinatorSequenceApproval' in old:
            data['coordinatorSequenceApproval'] = old['coordinatorSequenceApproval']
            data['status'] = old['status']
    dest.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
for filename in ['source-evidence.json', 'idle-review.json']:
    path = b / filename
    data = json.loads(path.read_text())
    if 'coordinatorSequenceApproval' not in data: data['status'] = status
    data['quartetManifest'] = 'bundle-manifest.json'
    data['remainingPosesGenerated'] = True
    data['sharedScaleContract'] = '56/950.5; canonical foot [994,983]; full 1586x992 common canvas; frame 128x80; origin [14,6]'
    data['coordinatorIdleApproval'] = manifest['idleApproval']
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
for filename, record in source['references'].items(): assert sha(b / 'references' / filename) == record['sha256']
for attempt in ['attempt-01', 'attempt-02']:
    d = b / 'idle' / attempt
    q = json.loads((d / 'alpha-qa.json').read_text())
    assert sha(d / 'raw.png') == sha(d / 'master.png') == q['sha256'] == sha(q['nativeSource'])
    req = json.loads((d / 'request.json').read_text())
    assert sha(d / 'prompt.txt') == req['promptSha256']

lines = ['# Ivra-growth four-pose review', '', contract, '', '## Identity and symbols', identity, source['symbolReview'],
         '', '## Species scale', scale_basis, '', '## Full-canvas registration', padding, reg['translationBasis'],
         '', '## Projection caveat for native sequence review', variation, '']
for pose in poses:
    r, p = reg['poses'][pose], manifest['poses'][pose]
    lines += ['### ' + pose, p['review'], f"Source foot {r['sourceSupportAnchorEstimated']}; exact four-paw sole midpoint {r['fourPawSoleBandMidpoint']}; full-canvas offset {r['registrationOffsetLogical']}; visible source bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.", '']
lines += ['## Provenance and native alpha', 'All selected masters and raw files match native tool outputs byte-for-byte. Exact prompts, requests and reference hashes verified. Idle attempt-01 is retained, superseded only for excessive tail width; attempt-02 was accepted by the coordinator before the three remaining poses. Four idle and three probes per other pose verify real transparent wing, limb and tail gaps. No programmatic art edits, alpha cleanup, repository edits or gameplay changes.', '', 'Coordinator shared-scale native quartet review remains pending.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}, 'integrityChecks': 'PASS'}, indent=2))
