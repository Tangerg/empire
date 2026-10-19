from pathlib import Path
import json, hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
scale, denominator, target = 46 / 946, 946, 46
canonical, canvas, frame, origin = [826, 971], [1586, 992], [104, 64], [8, 4]
source = json.loads((b / 'source-evidence.json').read_text())
identity = source['originalIdentity'] + ' ' + source['equipmentIdentity']
contract = ('C01-UNIT-WOODLAND-WALKER binds only c01.woodland-walker. Three initial c01-15 actors: allied silverwood-scout[4,14],enemy controlled-walker[17,7],east-walker[21,11]. '
            'Preserve actual tree-person source identity, original right-hand curled staff and empty left hand. Four original semantic slots idle/walk-a/attack/walk-b,32x48 original frame,anchor[16,47],footprint1x1. '
            'Walks have opposite root-foot contacts; attack is right staff aimed forward and left palm raised/open. '
            'Existing rules c01.ranger-bow and c01.border-blade conflict with original staff-only tree-person. One existing visual attack serves both weapons; no bow/sword invented, new gameplay, remap or fifth frame. '
            'Independent weapon icons and C01-FX-03/C01-FX-01 remain separate, no projectile or aura baked into character.')
padding = 'Native idle/walk-a/attack1586x992; walk-b1585x992. Only1transparent right column needed at common-canvas export; native sources retained unchanged.'
scale_basis = ('Original same-atlas tree full branch-crown height280px versus druid271px,ratio1.033. Coordinator approved46logical total including branch crown versus44logical human body. '
               'Idle highest branch11 to mean root sole957 gives946nativepx. Core bark-head crown approximately150 gives39.24logical, explicitly accepted in native/druid comparison19:28UTC rather than stretched to preliminary42.')
reg = {'status': 'Measured whole-canvas proposal; coordinator native sequence review/export pending',
       'sharedScale': scale, 'scaleFraction': '46/946', 'bodyReferenceSourcePixels': denominator,
       'bodyTargetHeightLogicalPixels': target, 'bodyMeasurement': scale_basis,
       'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas, 'paddingPolicy': padding,
       'canvasPaddingToleranceRequired': 1, 'maximumPadRight': 1, 'maximumPadBottom': 0,
       'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
       'registrationMethod': 'Translate full native canvas at locked46/946. Root-foot-only ROIs exclude staff; x averages both bottom-four-row alpha>127 root sole bands,y uses lowest sole. Exact mean retained.',
       'poses': {}}
manifest = {'asset': 'C01-UNIT-WOODLAND-WALKER', 'actualConsumers': ['c01.woodland-walker'],
            'consumerStatus': source['consumerStatus'], 'status': 'Native quartet/evidence complete; coordinator shared-scale sequence review pending',
            'generationTool': 'built-in image_gen', 'masterPolicy': 'Native raw/master byte-identical; no painting,recoloring,cropping,resizing or alpha cleanup. White/black composites are QA only.',
            'sharedScale': scale, 'bodyReferenceSourcePixels': denominator, 'bodyTargetHeightLogicalPixels': target,
            'speciesScaleContract': scale_basis, 'frameOrder': poses, 'commonSourceCanvas': canvas,
            'sourceEvidence': str(b / 'source-evidence.json'), 'registrationEvidence': str(b / 'registration-evidence.json'),
            'identityContract': identity, 'symbolContract': source['symbolReview'], 'semanticContract': contract,
            'selectedAttempts': {p: 'first successful native output' for p in poses},
            'idleApproval': 'Coordinator2026-10-19 19:28UTC native and druid comparison accepted46/946,[826,971],lower core height consistent with source crown ratio.',
            'scaleComparisonEvidence': str(b / 'idle/native-scale-comparison.png'), 'poses': {}}

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()

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
                          'sourceSupportAnchorEstimated': foot, 'bothRootSoleBandMidpoint': q['sourceFootMidpoint'],
                          'registrationOffsetLogical': [round(v, 9) for v in off],
                          'nativeVisibleBoundsAlphaGreaterThan127': bounds, 'nativeAllNonzeroAlphaBounds': q['bboxAlphaNonzero'],
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
reg['translationBasis'] = 'Largest offset from walk-a foot[754,969] against idle[826,971]:source delta[72,2],logical[3.501057082,0.097251586]. All complete common canvases fit104x64 at origin[8,4],limit4. No individual pose fit.'
qa = {'status': manifest['status'], 'topic': manifest['asset'], 'actualConsumers': manifest['actualConsumers'],
      'bodyScale': '46/946', 'speciesScaleContract': scale_basis,
      'selectedNativeCanvasSizes': {p: manifest['poses'][p]['nativeSize'] for p in poses}, 'commonSourceCanvas': canvas,
      'sourcePaddingRequired': padding, 'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True,
      'allExactPromptsVerified': True, 'alphaPerimeterGate': 'All selected native perimeters have0pixels alpha>1. Idle/attack/walk-b max0; walk-a max1 native specks retained. No cleanup.',
      'nativeAndWhiteBackgroundViewsInspected': poses, 'blackAndWhiteCompositesSaved': poses,
      'identityPreserved': True, 'originalEquipmentHandsPreserved': True, 'twoArmsAndTwoHandsOnly': True,
      'walkContactsOppose': True, 'threeNativeNegativeSpaceProbesPerPoseAllZero': True,
      'attackSemantics': 'Original-right arm extends same curled staff, original-left empty palm opens/raises; broad braced root stance. No bow/sword invention or external FX.',
      'semanticContract': contract, 'symbolContract': source['symbolReview'], 'independentFXBakedIntoCharacter': False,
      'registrationEvidence': 'registration-evidence.json', 'recommendedRegistrationLimitLogicalPixels': 4,
      'proposedFullCanvasFit': all(p['wholeCanvasFitsProposedFrame'] for p in reg['poses'].values()),
      'runtimeOrRepositoryWrites': False, 'weaponRulesChanged': False, 'gameplayFixClaimed': False}

comparison = b / 'idle/native-scale-comparison.png'
assert comparison.exists()
manifest['scaleComparisonSha256'] = sha(comparison)
for filename, data in [('registration-evidence.json', reg), ('bundle-manifest.json', manifest), ('qa-review.json', qa)]:
    (b / filename).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
for filename in ['source-evidence.json', 'idle-review.json']:
    p = b / filename
    data = json.loads(p.read_text())
    data['status'] = manifest['status']
    data['quartetManifest'] = 'bundle-manifest.json'
    if filename == 'idle-review.json': data['remainingPosesGenerated'] = True
    p.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
for filename, r in source['references'].items(): assert sha(b / 'references' / filename) == r['sha256']

lines = ['# Woodland-walker four-pose review', '', contract, '', '## Actual species and equipment', identity, source['symbolReview'], '', '## Species scale', scale_basis, 'Native/druid comparison saved at idle/native-scale-comparison.png.', '', '## Geometry', padding, reg['translationBasis'], '']
for pose in poses:
    r, p = reg['poses'][pose], manifest['poses'][pose]
    lines += ['### ' + pose, p['review'], f"Sourcefoot {r['sourceSupportAnchorEstimated']}; exact root sole midpoint {r['bothRootSoleBandMidpoint']}; full-canvas offset {r['registrationOffsetLogical']}; visible source bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.", '']
lines += ['## Provenance and native alpha', 'All four first native successes retained byte-for-byte. Original complete1254x1254atlas, first row and original strips preserved with hashes; native reference crops documented. Exact prompts/requests and native/raw/master hashes verified. Native and white views inspected, black/white composites saved. Three7x7probes per pose verify staff curl,leg gap andbranch gap all alpha0. No external eye aura or independent projectile baked into sprite. No repository or gameplay edits.', '', 'Coordinator shared-scale native quartet review remains pending.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}, 'integrityChecks': 'PASS'}, indent=2))
