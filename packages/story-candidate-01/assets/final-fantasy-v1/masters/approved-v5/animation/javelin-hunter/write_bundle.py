from pathlib import Path
from PIL import Image, ImageDraw
import json, hashlib

b = Path(__file__).parent
poses = ['idle', 'walk-a', 'attack', 'walk-b']
scale = 44 / 878
canvas = [1586, 993]
foot = [737, 976]
origin = [8, 4]
frame = [104, 64]
selected = {'idle': 'attempt-01', 'walk-a': 'attempt-01', 'attack': 'attempt-02', 'walk-b': 'attempt-02'}
identity = ('Dark-brown-skinned young clean-shaven man, short curled black hair, bold brows, slate blue-gray scarf, '
            'ochre large-diamond quilted coat, brown strap/gloves/boots, brass belt buckle and beige cuff wraps. '
            'Exactly three spare pointed javelins in back carrier plus one held javelin, one original wooden plank shield '
            'with gray riveted rim and pale brass boss/brace bands. Shield stays screen-left and held spear screen-right. '
            'Exaggerated chunky head/hands/boots and broad matte color planes.')
contract = ('Catalog-only C01-UNIT-JAVELIN-HUNTER; no current campaign/built-in unit binding. '
            'Existing built-in soldier_javelin is on soldier, which uses C01-UNIT-SWORDSMAN, not this topic. '
            'Preserve original four-slot idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1. '
            'Reconstruct opposite walking contacts, retain shield throughout, and depict a clear overhand throwing wind-up '
            'just before release, retaining one held javelin and three back spares. No independent projectile/FX or new consumer.')
padding = ('Native idle1585x992, walk-a1586x992, attack/walk-b1584x993. Export preparation may add transparent right/bottom '
           'padding to common1586x993; maximum2right/1bottom. No native pixel changes or per-pose fitting.')
registration = {
    'status': 'Measured complete-canvas proposal; coordinator final review/export pending',
    'sharedScale': scale, 'scaleFraction': '44/878', 'bodyReferenceSourcePixels': 878,
    'bodyTargetHeightLogicalPixels': 44,
    'bodyMeasurement': 'Idle hair crown y95 to mean of boot soles y973 (soles976/970).',
    'canonicalSourceFoot': foot, 'commonSourceCanvas': canvas,
    'paddingPolicy': padding, 'canvasPaddingToleranceRequired': 2,
    'maximumPadRight': 2, 'maximumPadBottom': 1,
    'proposedCommonFrameSize': frame, 'proposedOrigin': origin,
    'registrationMethod': 'One44/878 scale on every complete authored canvas. Translate from mean x of both bottom-four-row alpha>127 boot sole bands and lowest sole y. Individual boot ROIs are recorded in alpha-qa.json; no crop or silhouette fitting.',
    'poses': {}
}
manifest = {
    'asset': 'C01-UNIT-JAVELIN-HUNTER', 'actualConsumers': [],
    'consumerStatus': 'Catalog-only; no current binding. No consumer invented.',
    'status': 'Native quartet complete; coordinator final sequence review/export pending',
    'generationTool': 'built-in image_gen', 'transparentBackground': True,
    'masterPolicy': 'Selected native/raw/master bytes identical. No painting, resizing, cropping, recoloring or alpha cleanup. QA composites and whole-canvas fixed-scale previews are separate review artifacts only.',
    'sharedScale': scale, 'bodyReferenceSourcePixels': 878, 'bodyTargetHeightLogicalPixels': 44,
    'frameOrder': poses, 'commonSourceCanvas': canvas,
    'identityContract': identity, 'semanticContract': contract,
    'sourceEvidence': str(b/'source-evidence.json'), 'registrationEvidence': str(b/'registration-evidence.json'),
    'equipmentTopicDifference': 'Separate equipment icon has three total javelins and hide shield; original unit has three back spares plus held javelin and wooden plank shield. Preserve original unit identity.',
    'idleApproval': 'Coordinator2026-10-19 22:32UTC accepted native idle and44/878 human comparison beside spearman and Torren; required shield and three spares plus held spear throughout.',
    'selectedAttempts': selected, 'poses': {},
    'rejectedAttempts': {
        'attack/attempt-01': 'Rejected because shield and throwing arm switched screen sides relative to idle.',
        'walk-b/attempt-01': 'Rejected because it repeated walk-a forward-right-leg contact.'
    }
}
review_sheet = Image.new('RGBA', (104*4, 86), (239,239,229,255))
draw = ImageDraw.Draw(review_sheet)
for i, pose in enumerate(poses):
    d=b/pose
    q=json.loads((d/'alpha-qa.json').read_text())
    req=json.loads((d/'request.json').read_text())
    for directory in d.glob('attempt-*'):
        rq=json.loads((directory/'request.json').read_text())
        actual={k:rq[k] for k in ['prompt','referenced_image_paths','transparent_background']}
        (directory/'tool-request.json').write_text(json.dumps(actual,indent=2)+'\n')
    actual={k:req[k] for k in ['prompt','referenced_image_paths','transparent_background']}
    (d/'tool-request.json').write_text(json.dumps(actual,indent=2)+'\n')
    native=Path(q['nativeSource'])
    h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    assert h(native)==h(d/'raw.png')==h(d/'master.png')==q['sha256']
    assert h(d/'prompt.txt')==req['promptSha256']
    assert (d/'prompt.txt').read_text()==req['prompt']
    assert q['perimeterCountAlphaGreaterThan1']==0
    posefoot=q['suggestedSourceFoot']
    offset=[(foot[j]-posefoot[j])*scale for j in range(2)]
    full=[origin[0]+offset[0],origin[1]+offset[1],origin[0]+offset[0]+canvas[0]*scale,origin[1]+offset[1]+canvas[1]*scale]
    fit=full[0]>=0 and full[1]>=0 and full[2]<=frame[0] and full[3]<=frame[1]
    bounds=q['bboxAlphaGreaterThan127']
    visible=[origin[j%2]+offset[j%2]+bounds[j]*scale for j in range(4)]
    assert fit
    registration['poses'][pose]={
        'nativeSize':q['size'], 'padRight':canvas[0]-q['size'][0], 'padBottom':canvas[1]-q['size'][1],
        'sourceSupportAnchorEstimated':posefoot,'bothBootSoleBandMidpoint':q['sourceFootMidpoint'],
        'registrationOffsetLogical':[round(v,9) for v in offset],
        'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],
        'proposedLogicalVisibleBounds':[round(v,6) for v in visible],
        'proposedLogicalFullCanvasBounds':[round(v,6) for v in full],
        'wholeCanvasFitsProposedFrame':fit,
        'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':True,'interpretation':'Native alpha1 specks retained unchanged; no alpha>1 reaches perimeter.'}
    }
    review=q.get('semanticReview','Approved idle preserves original identity/gear; both boots planted with full shield, three back spare javelins and one held spear.')
    manifest['poses'][pose]={
        'master':str(d/'master.png'),'raw':str(d/'raw.png'),'exactPrompt':str(d/'prompt.txt'),
        'request':str(d/'request.json'),'exactToolRequest':str(d/'tool-request.json'),
        'exactToolCallPromptVerified':True,'exactPromptSha256':req['promptSha256'],
        'alphaQA':str(d/'alpha-qa.json'),'qaWhite':str(d/'qa-white.png'),'qaBlack':str(d/'qa-black.png'),
        'nativeSize':q['size'],'sourceFootAnchorSuggested':posefoot,'sha256':q['sha256'],
        'generationSource':str(native),'review':review
    }
    # Review-only fixed scale of entire canvas. No source edits or independent frame fitting.
    im=Image.open(d/'master.png').convert('RGBA')
    padded=Image.new('RGBA',canvas,(0,0,0,0));padded.alpha_composite(im,(0,0))
    scaled=padded.resize((round(canvas[0]*scale),round(canvas[1]*scale)),Image.Resampling.LANCZOS)
    review_sheet.alpha_composite(scaled,(round(i*104+origin[0]+offset[0]),round(17+origin[1]+offset[1])))
    draw.text((i*104+6,2),pose,fill=(20,20,20,255))
    draw.text((i*104+6,74),'44/878 fixed',fill=(20,20,20,255))
registration['requiredMaximumTranslationLogicalPixels']=max(abs(v) for p in registration['poses'].values() for v in p['registrationOffsetLogical'])
registration['recommendedRegistrationLimitLogicalPixels']=3
registration['translationBasis']='Largest measured adjustment is walk-a x+2.004555809logical from idlefoot737 to walk-afoot697. Recommend explicit limit3; all complete1586x993 padded canvases fit104x64 at origin[8,4].'
(b/'registration-evidence.json').write_text(json.dumps(registration,indent=2)+'\n')
(b/'bundle-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
review_sheet.convert('RGB').save(b/'qa-sequence-1x.png')
review_sheet.resize((1664,344),Image.Resampling.NEAREST).convert('RGB').save(b/'qa-sequence-4x.png')
qa={
    'status':'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence/export review pending',
    'topic':'C01-UNIT-JAVELIN-HUNTER','actualConsumers':[],'consumerStatus':manifest['consumerStatus'],
    'bodyScale':'44/878','selectedNativeCanvasSizes':{p:manifest['poses'][p]['nativeSize'] for p in poses},
    'commonSourceCanvas':canvas,'sourcePaddingRequired':padding,
    'nativeMastersUnmodified':True,'allRawMasterNativeHashesMatch':True,'allExactPromptsVerified':True,
    'allExactToolRequestsStored':True,'rejectedAttemptsRetained':manifest['rejectedAttempts'],
    'alphaPerimeterGate':'All selected native perimeters alpha<=1; no silhouette alpha>1 on edges; alpha1 specks retained.',
    'nativeAndWhiteBackgroundViewsInspected':poses,'blackAndWhiteCompositesSaved':poses,
    'identityPass':True,'shieldPresentEveryPose':True,'sameShieldArmEveryPose':True,
    'exactlyThreeBackSparesPlusOneHeldJavelinEveryPose':True,'allWeaponAndShieldTipsComplete':True,
    'walkContactsOppose':True,'walkReview':'Walk-a far/right heel leads and near/left toe trails. Walk-b near/left heel leads toward foreground with toe lifted and sole visible; far/right toe trails with heel lifted.',
    'attackReview':manifest['poses']['attack']['review'],'independentFXBakedIntoCharacter':False,
    'transparentLegAndGearGapsRetained':True,'registrationEvidence':'registration-evidence.json',
    'recommendedRegistrationLimitLogicalPixels':3,'proposedFullCanvasFit':True,
    'runtimeOrRepositoryWrites':False,'weaponRulesChanged':False,'newConsumerBindingInvented':False
}
(b/'qa-review.json').write_text(json.dumps(qa,indent=2)+'\n')
for name in ['source-evidence.json','idle-review.json']:
    p=b/name;record=json.loads(p.read_text());record['status']='Native quartet complete; coordinator final sequence/export review pending';record['quartetManifest']='bundle-manifest.json'
    if name=='source-evidence.json':record['scaleContract']='Coordinator accepted44/878; every complete native canvas uses this single body-derived scale.'
    if name=='idle-review.json':record['coordinatorApproved']=True;record['remainingPosesGenerated']=True
    p.write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
lines=['# Javelin hunter four-pose review','',contract,'',identity,'',padding,'',registration['translationBasis'],'']
for pose in poses:
    r=registration['poses'][pose]
    lines.extend(['## '+pose,manifest['poses'][pose]['review'],f"Source foot {r['sourceSupportAnchorEstimated']}; sole-band midpoint {r['bothBootSoleBandMidpoint']}; complete-canvas offset {r['registrationOffsetLogical']}; full logical bounds {r['proposedLogicalFullCanvasBounds']}.",''])
lines.extend(['## Rejected attempts','Attack attempt01 switched gear arms; walk-b attempt01 repeated right-foot-leading contact. Both raw images, exact prompts/requests, hashes and QA preserved.','', '## Provenance','Built-in imagegen with transparent_background=true. All native/raw/master and exact prompt hashes verified; source board/runtime/HD strips preserved. No source cleanup, per-pose fitting, repository edits, rule changes or invented binding.'])
(b/'pose-review.md').write_text('\n'.join(lines)+'\n')
files=sorted(p for p in b.rglob('*') if p.is_file() and p.name!='SHA256SUMS')
(b/'SHA256SUMS').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+str(p.relative_to(b))+'\n' for p in files))
print(json.dumps({'manifest':str(b/'bundle-manifest.json'),'sourceFeet':{p:registration['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'maxTranslation':registration['requiredMaximumTranslationLogicalPixels'],'frame':frame,'commonCanvas':canvas,'filesHashed':len(files)},indent=2))
