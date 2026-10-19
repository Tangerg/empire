from pathlib import Path
import json
import hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
s = 44 / 873.5
canonical = [813, 949]
canvas = [1586, 993]
frame = [104, 64]
origin = [8, 4]
reviews = {
    'idle': 'Accepted clean-shaven brown-haired adult human in raised charcoal hood/cowl, blue-gray split tunic, olive-gray shoulder caps, brown gloves/bracers/boots, brass buckle, diagonal chest strap and hip quiver/scabbard. Original screen-left hand carries short blade; original screen-right hand holds short wooden bow.',
    'walk-a': 'Front screen-right boot advances toe-up with heel contact and visible sole; rear screen-left boot is flat and weight-bearing. Same original blade/bow hands and full costume, quiver and scabbard.',
    'walk-b': 'Opposite contact: front screen-right boot flat and weight-bearing; rear screen-left heel raised with toe contact. Same chunky head/body, original weapon hands, short blade, bow, quiver and scabbard.',
    'attack': 'Clear braced full-draw bow shot. Short blade is completely sheathed at original screen-left hip, with only hilt/guard/pommel visible. Original bow hand extends the compact short bow; original blade hand draws the string near the cheek. Exactly two connected gloved hands and one nocked arrow with one shaft, one steel head and one fletching group. V string connects bow tips to drawing fingers, partly occluded by the face. No hand holds a blade while drawing.'
}
limitation = {
    'status': 'Existing single-attack-slot limitation retained and documented',
    'detail': 'c01.bran has c01.ranger-bow and c01.border-blade, but the original four-frame strip supplies one attack slot. The accepted primary-bow shooting pose is shared by both weapon actions. No extra slot, gameplay change or weapon-specific animation dispatch is introduced or claimed.',
    'approvedVisualPlan': 'Idle and walks retain the carried short blade and bow in their original physical hands. Attack temporarily sheathes the blade to free its hand for drawing, while preserving the original bow hand.',
    'evidence': 'source-contract-review.md'
}
contract = ('C01-UNIT-RANGER is the actual c01.bran combat-unit artwork, distinct from equipment C01-EQUIP-RANGER and portrait C01-CHAR-BRAN-01. '
            'Retain idle / walk-a / attack / walk-b order, original 1x1 rules footprint, and source hooded human identity. '
            'Primary ranger-bow is a range-3 piercing arc projectile weapon; border-blade is the secondary slash/melee weapon. '
            'The one existing attack slot shows the primary bow at full draw with the short blade safely sheathed.')
reg = {
    'status': 'Measured whole-canvas proposal; no runtime writes', 'sharedScale': s, 'scaleFraction': '44/873.5',
    'bodyReferenceSourcePixels': 873.5, 'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Accepted idle hood crown y=75 to exact two-sole mean y=948.5. This one body-based scale applies to every complete pose canvas.',
    'canonicalSourceFoot': canonical, 'commonSourceCanvas': canvas,
    'paddingPolicy': 'Idle and walk-b: native1586x992, pad one transparent bottom row. Walk-a: native1585x992, pad one right column and one bottom row. Selected attack: native1584x993, pad two right columns. Padding happens only in export preparation; native master/raw files remain unchanged. No resize/crop/alpha cleanup or independent fitting.',
    'canvasPaddingToleranceRequired': 2, 'maximumPadRight': 2, 'maximumPadBottom': 1,
    'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'Translate the whole source canvas at locked44/873.5 scale. Anchor x is mean of the two boot bottom-four-row alpha>127 support bands; y is the lowest sole. Exact ROIs and both-sole midpoint are preserved in each alpha QA file.',
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-RANGER', 'actualConsumers': ['c01.bran'],
    'status': 'Native quartet complete; coordinator sequence review/export/binding pending',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'All selected master/raw files are byte-identical to native tool outputs. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.',
    'sharedScale': s, 'bodyReferenceSourcePixels': 873.5, 'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses, 'commonSourceCanvas': canvas, 'sourceEvidence': str(b / 'source-evidence.json'),
    'registrationEvidence': str(b / 'registration-evidence.json'), 'semanticContract': contract, 'existingActionLimitation': limitation,
    'selectedAttempts': {'idle': 'attempt-01', 'walk-a': 'first successful native output', 'attack': 'attempt-02', 'walk-b': 'first successful native output'},
    'attackCorrection': 'First attack had an over-tall bow. It is preserved in attack/attempt-01 with exact prompt/raw/QA. Selected native attempt-02 restores compact original short-bow size while retaining the human stance, sheathed blade and credible single-arrow draw. No programmatic image editing was used.',
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
        'proposedLogicalVisibleBounds': [round(x, 6) for x in visible], 'proposedLogicalFullCanvasBounds': [round(x, 6) for x in full],
        'wholeCanvasFitsProposedFrame': fit,
        'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0, 'interpretation': 'Native alpha=1 specks are retained; no silhouette alpha>1 crosses any edge.'}
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
reg['translationBasis'] = 'Measured boot sole support only. Maximum translation remains below the existing four-logical-pixel limit. Body scale is never changed to fit a frame.'
(b / 'registration-evidence.json').write_text(json.dumps(reg, indent=2) + '\n')
(b / 'bundle-manifest.json').write_text(json.dumps(m, indent=2) + '\n')
qa = {
    'status': 'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export/binding pending',
    'topic': m['asset'], 'actualConsumers': m['actualConsumers'], 'bodyScale': '44/873.5',
    'selectedNativeCanvasSizes': {p: m['poses'][p]['nativeSize'] for p in poses},
    'commonSourceCanvas': canvas, 'sourcePaddingRequired': reg['paddingPolicy'],
    'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True, 'allExactPromptsVerified': True,
    'alphaPerimeterGate': 'Native perimeter alpha<=1 in every selected frame; zero silhouette alpha>1 on edges. Alpha=1 specks retained unchanged.',
    'blackAndWhiteCompositesVisuallyInspected': poses, 'identityPass': True, 'bowHandPreserved': True,
    'idleAndWalkBladeHandPreserved': True, 'walkContactsOppose': True,
    'attackAnatomyReview': reviews['attack'], 'shortBowScaleCorrection': m['attackCorrection'],
    'semanticContract': contract, 'existingActionLimitation': limitation,
    'registrationEvidence': 'registration-evidence.json', 'proposedFullCanvasFit': all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'runtimeOrRepositoryWrites': False, 'weaponSpecificAnimationFixClaimed': False
}
(b / 'qa-review.json').write_text(json.dumps(qa, indent=2) + '\n')
for name in ['source-evidence.json', 'idle-review.json']:
    path = b / name
    x = json.loads(path.read_text())
    x['status'] = 'Native quartet complete; coordinator final sequence review/export/binding pending'
    x['quartetManifest'] = 'bundle-manifest.json'
    if name == 'idle-review.json':
        x['remainingPosesGenerated'] = True
    path.write_text(json.dumps(x, indent=2, ensure_ascii=False) + '\n')
lines = ['# Ranger four-pose review', '',
         'C01-UNIT-RANGER is protagonist c01.bran combat-unit art. Original full board, first-row/idle source crops and HD/runtime strips are preserved in references/. Equipment and portrait art were not substituted.', '',
         '## Identity', reviews['idle'], '',
         '## Geometry', 'Locked body scale44/873.5 from accepted idle hood crown75 to mean soles948.5. Canonical source foot[813,949]. Common canvas1586x993 requires only explicit maximum2-column/right and1-row/bottom transparent padding in export preparation. All complete registered canvases fit104x64 with origin[8,4]. No native image editing or independent silhouette fitting.', '']
for p in poses:
    r = reg['poses'][p]
    lines += ['### ' + p, reviews[p], f"Source foot anchor {r['sourceSupportAnchorEstimated']}; exact two-sole midpoint {r['bothBootSoleBandMidpoint']}; logical whole-canvas offset {r['registrationOffsetLogical']}; native visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}.", '']
lines += ['## Attack review and existing limitation', m['attackCorrection'], limitation['detail'], limitation['approvedVisualPlan'],
          'The selected attack has a single held nocked arrow, connected string, correct original bow hand and a safely sheathed blade. The upper string is partly occluded by the face, rather than disconnected. No extra projectile or effects image is introduced.', '',
          '## Provenance and QA', 'Idle and both walks are their first successful native outputs; selected attack is native attempt-02. Both attack attempts retain raw PNGs, exact prompts, requests and alpha QA. All selected native/master/raw hashes and prompt hashes agree. Native alpha=1 specks are retained, with no silhouette alpha>1 on source edges. Black/white QA and measured sole-band ROIs are saved. No runtime or repository writes.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'maximumTranslationLogical': reg['requiredMaximumTranslationLogicalPixels'], 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}}, indent=2))
