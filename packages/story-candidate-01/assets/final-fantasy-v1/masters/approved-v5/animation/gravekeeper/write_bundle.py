from pathlib import Path
import hashlib
import json

b = Path(__file__).parent
s = 44 / 897
poses = ['idle', 'walk-a', 'attack', 'walk-b']
canonical = [832, 979]
canvas = [1586, 992]
frame = [104, 64]
origin = [8, 4]
reviews = {
    'idle': 'Accepted older living human with swept-back gray hair and short beard, broad charcoal/olive tabard, cream sleeves, blue-gray scarf, huge bare hands and boots. Screen-left hand holds the complete long spade; screen-right hand carries the separate candle lantern. Wooden tags, pouch and silver vial retained.',
    'walk-a': 'Front screen-right boot advances toe-up with heel contact and visible sole; rear screen-left boot stays flat. Same spade/lantern hands, head, tabard, scarf, tags, pouch and vial. Gentle walking sway without external effects.',
    'walk-b': 'Opposite contacts: front screen-right boot is flat and weight-bearing; rear screen-left heel is raised with toe contact. Same physical equipment hands and identity. The native image is one column narrower; preserve it unchanged and pad only in export.',
    'attack': 'Clear raised forward lantern cast/dispel: original screen-right bare hand extends the intact upright lantern toward the target at chest/face height, focused face and wide braced boots. Original screen-left hand retains the complete spade. Candle warmth stays inside the lantern cage; no external glow or added effects.'
}
contract = ('Existing idle / walk-a / attack / walk-b contract for C01-UNIT-GRAVEKEEPER. '
            'Range-2 magic/arcane support permits the attack slot to present a forward lantern cast/dispel while retaining the practical shovel. '
            'Heal continues to share the existing action animation contract; no fifth pose is introduced.')
mismatch = {
    'status': 'Pre-existing naming/visual discrepancy documented; no binding or gameplay change',
    'detail': 'The c01-02 actor named bridge-cleric is c01.gravekeeper, not the separate built-in cleric. The gameplay weapon is called lantern-staff, while the exact original artwork carries a separate long spade and candle lantern. Preserve the canonical source identity and physical equipment hands.',
    'evidence': 'source-contract-review.md',
    'gameplaySemanticFixClaimed': False,
    'identityRemapPerformed': False
}
reg = {
    'status': 'Measured proposal only; no runtime writes',
    'sharedScale': s,
    'bodyReferenceSourcePixels': 897,
    'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Accepted idle hair crown y=76 to exact mean boot sole y=973. Preserve this one body-based scale for every pose.',
    'canonicalSourceFoot': canonical,
    'commonSourceCanvas': canvas,
    'paddingPolicy': 'Idle and walk-a are 1586x992. Attack and walk-b are 1585x992 and need exactly one transparent column at the right in the export preparation only. Native master/raw files remain unchanged. No resize, crop, alpha cleanup or independent pose fitting.',
    'canvasPaddingToleranceRequired': 1,
    'proposedCommonFrameSize': frame,
    'proposedOrigin': origin,
    'registrationMethod': 'Translate the whole source canvas using measured boot sole support at locked scale 44/897. Anchor x is the mean of each boot bottom-four-row alpha>127 band; anchor y is the lowest sole. Both-sole midpoint and explicit ROIs are preserved in each alpha QA file. Shovel and lantern pixels are excluded from the foot ROIs.',
    'poses': {}
}
m = {
    'asset': 'C01-UNIT-GRAVEKEEPER',
    'actualConsumers': ['c01.gravekeeper', 'c01.mirelle'],
    'status': 'Native quartet complete; formal export/binding pending coordinator',
    'generationTool': 'built-in image_gen',
    'masterPolicy': 'All master/raw files are byte-identical to selected native tool outputs. No painting, recoloring, cropping, resizing or alpha cleanup. Black/white composites are QA only.',
    'sharedScale': s,
    'bodyReferenceSourcePixels': 897,
    'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses,
    'commonSourceCanvas': canvas,
    'sourceEvidence': str(b / 'source-evidence.json'),
    'registrationEvidence': str(b / 'registration-evidence.json'),
    'semanticContract': contract,
    'semanticMismatch': mismatch,
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
        'perimeterGate': {'maxNativePerimeterAlpha': q['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': q['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': q['perimeterCountAlphaGreaterThan1'] == 0, 'interpretation': 'Native alpha=1 specks retained unchanged; no silhouette alpha>1 crosses any edge.'}
    }
    m['poses'][p] = {
        'master': str(d / 'master.png'), 'raw': str(d / 'raw.png'), 'exactPrompt': str(d / 'prompt.txt'),
        'request': str(d / 'request.json'), 'exactToolCallPromptVerified': True, 'exactPromptSha256': req['promptSha256'],
        'alphaQA': str(d / 'alpha-qa.json'), 'qaBlack': str(d / 'qa-black.png'), 'qaWhite': str(d / 'qa-white.png'),
        'nativeSize': q['size'], 'sourceFootAnchorSuggested': f, 'visibleBoundsAlphaGreaterThan127': bounds,
        'sha256': q['sha256'], 'generationSource': q['nativeSource'], 'review': reviews[p]
    }
    assert fit and q['perimeterCountAlphaGreaterThan1'] == 0
    assert hashlib.sha256((d / 'master.png').read_bytes()).hexdigest() == q['sha256'] == hashlib.sha256((d / 'raw.png').read_bytes()).hexdigest() == hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
    assert hashlib.sha256((d / 'prompt.txt').read_bytes()).hexdigest() == req['promptSha256']

reg['requiredMaximumTranslationLogicalPixels'] = max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels'] = 4
reg['translationBasis'] = 'Measured boot support only. Maximum translation is below the existing four-logical-pixel registration allowance. Shared scale never changes.'
(b / 'registration-evidence.json').write_text(json.dumps(reg, indent=2) + '\n')
(b / 'bundle-manifest.json').write_text(json.dumps(m, indent=2) + '\n')
qa = {
    'status': 'Native quartet visual/geometry QA passes; coordinator export/binding pending',
    'topic': m['asset'], 'actualConsumers': m['actualConsumers'], 'bodyScale': '44/897',
    'selectedNativeCanvasSizes': {p: m['poses'][p]['nativeSize'] for p in poses},
    'sourcePaddingRequired': 'Attack and walk-b: exactly one transparent column on the right in export only. Idle and walk-a: none.',
    'nativeMastersUnmodified': True, 'allRawMasterNativeHashesMatch': True, 'allExactPromptsVerified': True,
    'alphaPerimeterGate': 'All perimeter alpha<=1; zero silhouette alpha>1 on edges; native alpha=1 specks retained unchanged.',
    'blackAndWhiteCompositesVisuallyInspected': poses, 'identityPass': True, 'equipmentHandsPreserved': True,
    'walkContactsOppose': True, 'attackSemanticsPass': reviews['attack'], 'semanticContract': contract,
    'semanticMismatch': mismatch, 'originalArtIdentityRetainedWithoutRemapping': True, 'gameplaySemanticFixClaimed': False,
    'registrationEvidence': 'registration-evidence.json', 'proposedFullCanvasFit': all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),
    'runtimeOrRepositoryWrites': False
}
(b / 'qa-review.json').write_text(json.dumps(qa, indent=2) + '\n')
for filename in ['idle-review.json', 'source-evidence.json']:
    path = b / filename
    x = json.loads(path.read_text())
    x['status'] = 'Idle accepted; native quartet complete' if filename.startswith('idle') else 'Native quartet complete; originals and exact source crops preserved'
    x['quartetManifest'] = 'bundle-manifest.json'
    path.write_text(json.dumps(x, indent=2, ensure_ascii=False) + '\n')
lines = ['# Gravekeeper four-pose review', '',
         'C01-UNIT-GRAVEKEEPER maps to c01.gravekeeper and c01.mirelle. The c01-02 bridge-cleric actor at cell (2,9) uses c01.gravekeeper. Original source board, native source-cell crop and HD/runtime strips are preserved under references/.', '',
         '## Source identity', reviews['idle'], '',
         '## Geometry', 'One locked body scale, 44/897, from accepted idle hair crown y=76 to mean soles y=973. Canonical source foot [832,979]. Common source canvas 1586x992. Attack and walk-b need one transparent column on the right in export only; no native master is edited. All complete source canvases fit a 104x64 export frame with origin [8,4].', '']
for p in poses:
    r = reg['poses'][p]
    lines += ['### ' + p, reviews[p], f"Source foot anchor {r['sourceSupportAnchorEstimated']}; exact two-sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas logical offset {r['registrationOffsetLogical']}; native visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}.", '']
lines += ['## Action contract', contract, mismatch['detail'],
          'No remapping or gameplay-semantic fix is performed or claimed. The separate spade and lantern retain their original physical hands. The lantern flame remains fully contained, with no added magic-effect image.', '',
          '## Provenance and QA', 'All four selected masters are first successful native outputs. Idle is additionally retained under attempt-01. Exact prompts, request metadata, original source references, raw PNGs, measured sole bands and black/white QA are preserved. Every master/raw/native hash agrees, and every exact prompt hash agrees with its request metadata. No generated art was painted, recolored, resized, cropped or alpha-cleaned. No runtime/repository edits.']
(b / 'pose-review.md').write_text('\n'.join(lines) + '\n')
print(json.dumps({'manifest': str(b / 'bundle-manifest.json'), 'requiredMaximumTranslationLogicalPixels': reg['requiredMaximumTranslationLogicalPixels'], 'sourceFeet': {p: reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses}, 'fullCanvasBounds': {p: reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}}, indent=2))
