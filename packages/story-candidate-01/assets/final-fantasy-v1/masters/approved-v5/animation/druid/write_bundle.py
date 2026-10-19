from pathlib import Path
import json
import hashlib

b=Path(__file__).parent
poses=['idle','walk-a','attack','walk-b']
s=44/921.5
canonical,canvas,frame,origin=[770,966],[1586,992],[104,64],[8,4]
source=json.loads((b/'source-evidence.json').read_text())
identity=source['originalIdentity']+' Approved chunky large head/hands/boots and broad matte planes; original green border patterns simplified consistently into readable arch/lozenge/leaf motifs.'
contract=('C01-UNIT-DRUID binds only c01.druid, the Silverwood druid, with three verified c01-15 actors. '
          'Preserve original idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1 and second-row source identity. '
          'Sole weapon c01.oath-bolt is range2 magic, compatible with a staff-forward cast. '
          'Shared equipment C01-EQUIP-BATTLE-MAGE and FX C01-FX-06 are rule presentation topics and do not redefine the original character as a battle-mage. '
          'Original attack is near-idle; quartet reconstructs a clear right-hand staff cast with an assisting empty left palm and opposite walking contacts. '
          'Heal shares the existing single action slot; no fifth pose or gameplay/rule change. Keep independent magic FX outside character.')
symbols=('Original wooden spiral is an OPEN aperture; magenta in original source was background through a hole, not a crystal or magical core. '
         'All four native outputs preserve real alpha-zero pixels within the aperture. Original leaves, rope and approved green robe-border motif family retained. '
         'No new cross, rune, antlers, aura or asserted religious/faction meaning.')
padding='Idle/walk-a/walk-b native1586x992; attack native1585x992. Common1586x992 requires one transparent right column for attack only in export preparation. All native pixels and full source canvases remain unchanged.'
reg={'status':'Measured whole-canvas proposal; coordinator export/binding pending','sharedScale':s,'scaleFraction':'44/921.5','bodyReferenceSourcePixels':921.5,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Accepted idle hair crown y40 to exact two-sole mean y961.5. One body-derived scale applies to all full native canvases.','canonicalSourceFoot':canonical,'commonSourceCanvas':canvas,'paddingPolicy':padding,'canvasPaddingToleranceRequired':1,'maximumPadRight':1,'maximumPadBottom':0,'proposedCommonFrameSize':frame,'proposedOrigin':origin,'registrationMethod':'Whole-canvas translation at locked44/921.5. Anchor x is mean of both boot bottom-four-row alpha>127 support bands; y is lowest sole. Boot-only ROIs avoid staff and robe; exact midpoint evidence retained.','poses':{}}
m={'asset':'C01-UNIT-DRUID','actualConsumers':['c01.druid'],'consumerStatus':'Campaign Silverwood druid; controlled-druid, oath-druid and neutral silverwood-witness in c01-15 verified','status':'Native quartet complete; coordinator final sequence review/export/binding pending','generationTool':'built-in image_gen','masterPolicy':'Every selected master/raw PNG is byte-identical to its native output. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.','sharedScale':s,'bodyReferenceSourcePixels':921.5,'bodyTargetHeightLogicalPixels':44,'frameOrder':poses,'commonSourceCanvas':canvas,'sourceEvidence':str(b/'source-evidence.json'),'registrationEvidence':str(b/'registration-evidence.json'),'identityContract':identity,'symbolContract':symbols,'semanticContract':contract,'selectedAttempts':{p:'first successful native output' for p in poses},'idleApproval':'Coordinator2026-10-19 18:14UTC viewed and approved identity,44/921.5 scale,1586x992 canvas and foot[770,966].','poses':{}}
reviews={}
for p in poses:
    d=b/p;q=json.loads((d/'alpha-qa.json').read_text());req=json.loads((d/'request.json').read_text());f=q['suggestedSourceFoot']
    reviews[p]=identity if p=='idle' else q['semanticReview']
    off=[(canonical[i]-f[i])*s for i in range(2)]
    full=[origin[0]+off[0],origin[1]+off[1],origin[0]+off[0]+canvas[0]*s,origin[1]+off[1]+canvas[1]*s]
    bounds=q['bboxAlphaGreaterThan127'];visible=[origin[i%2]+off[i%2]+bounds[i]*s for i in range(4)]
    fit=full[0]>=0 and full[1]>=0 and full[2]<=frame[0] and full[3]<=frame[1]
    reg['poses'][p]={'nativeSize':q['size'],'padRight':canvas[0]-q['size'][0],'padBottom':canvas[1]-q['size'][1],'sourceSupportAnchorEstimated':f,'bothBootSoleBandMidpoint':q['sourceFootMidpoint'],'registrationOffsetLogical':[round(x,9) for x in off],'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],'proposedLogicalVisibleBounds':[round(x,6) for x in visible],'proposedLogicalFullCanvasBounds':[round(x,6) for x in full],'wholeCanvasFitsProposedFrame':fit,'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterCountAlphaGreaterThan1']==0,'interpretation':'Native alpha=1 specks retained; no silhouette alpha>1 reaches any edge.'}}
    m['poses'][p]={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'exactPrompt':str(d/'prompt.txt'),'request':str(d/'request.json'),'exactToolCallPromptVerified':True,'exactPromptSha256':req['promptSha256'],'alphaQA':str(d/'alpha-qa.json'),'qaBlack':str(d/'qa-black.png'),'qaWhite':str(d/'qa-white.png'),'nativeSize':q['size'],'sourceFootAnchorSuggested':f,'visibleBoundsAlphaGreaterThan127':bounds,'sha256':q['sha256'],'generationSource':q['nativeSource'],'review':reviews[p],'staffSpiralApertureAlphaProbe':q['staffSpiralApertureAlphaProbe']}
    assert fit and q['perimeterCountAlphaGreaterThan1']==0 and q['staffSpiralApertureAlphaProbe']['alphaZeroPixels']>0
    assert hashlib.sha256((d/'master.png').read_bytes()).hexdigest()==q['sha256']==hashlib.sha256((d/'raw.png').read_bytes()).hexdigest()==hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
    assert hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest()==req['promptSha256']
    assert max(canvas[i]-q['size'][i] for i in range(2))<=1
reg['requiredMaximumTranslationLogicalPixels']=max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels']=4
reg['translationBasis']='Measured attack foot[694,958] differs from idle[770,966] by[76,8]source pixels. At44/921.5, whole-canvas offset is x+3.628865979,y+0.381985893logical. Existing limit4 suffices; every complete canvas fits104x64 at origin[8,4], without independent scale or silhouette fitting.'
(b/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n');(b/'bundle-manifest.json').write_text(json.dumps(m,indent=2)+'\n')
qa={'status':'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export/binding pending','topic':m['asset'],'actualConsumers':m['actualConsumers'],'bodyScale':'44/921.5','selectedNativeCanvasSizes':{p:m['poses'][p]['nativeSize'] for p in poses},'commonSourceCanvas':canvas,'sourcePaddingRequired':padding,'nativeMastersUnmodified':True,'allRawMasterNativeHashesMatch':True,'allExactPromptsVerified':True,'alphaPerimeterGate':'All native perimeter alpha<=1; zero silhouette alpha>1 on edges; native alpha=1 specks retained.','nativeAndWhiteBackgroundViewsInspected':poses,'blackAndWhiteCompositesSaved':poses,'identityPass':True,'rightStaffHandAndLeftFreeHandPreserved':True,'staffApertureRealTransparencyVerifiedEveryPose':True,'basketAndBottleRetainedEveryPose':True,'twoArmsAndTwoHandsOnly':True,'equipmentCompleteEveryPose':True,'walkContactsOppose':True,'attackAnatomyReview':reviews['attack'],'semanticContract':contract,'symbolContract':symbols,'healUsesExistingSingleActionSlot':True,'independentFXBakedIntoCharacter':False,'registrationEvidence':'registration-evidence.json','recommendedRegistrationLimitLogicalPixels':4,'proposedFullCanvasFit':all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),'runtimeOrRepositoryWrites':False,'weaponRulesChanged':False,'gameplayFixClaimed':False}
(b/'qa-review.json').write_text(json.dumps(qa,indent=2)+'\n')
for name in ['source-evidence.json','idle-review.json']:
    p=b/name;x=json.loads(p.read_text());x['status']='Native quartet complete; coordinator final sequence review/export/binding pending';x['quartetManifest']='bundle-manifest.json'
    if name=='idle-review.json':x['remainingPosesGenerated']=True
    p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
for name,r in source['references'].items():assert hashlib.sha256((b/'references'/name).read_bytes()).hexdigest()==r['sha256']
lines=['# Druid four-pose review','',contract,'','## Identity and symbols',identity,symbols,'','## Geometry',padding,reg['translationBasis'],'Shared44/921.5; canonical source foot[770,966].','']
for p in poses:
    r=reg['poses'][p];lines+=['### '+p,reviews[p],f"Source foot {r['sourceSupportAnchorEstimated']}; exact sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas offset {r['registrationOffsetLogical']}; source visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.",'']
lines+=['## Provenance and QA','All four are first successful native built-in image_gen outputs. Accepted idle was reviewed before other poses. Every raw/master/native and exact prompt hash matches. Complete original board, second-row/idle/staff/robe crops and original HD/runtime strips are preserved. All native and white-background views inspected; black/white QA composites saved. Staff aperture alpha-zero probes recorded per pose. Natural lower-shaft occlusion behind robe/boots is retained, with no canvas clipping. No repository/runtime writes.']
(b/'pose-review.md').write_text('\n'.join(lines)+'\n')
print(json.dumps({'manifest':str(b/'bundle-manifest.json'),'sourceFeet':{p:reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'maximumTranslationLogical':reg['requiredMaximumTranslationLogicalPixels'],'fullCanvasBounds':{p:reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}},indent=2))
