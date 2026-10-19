from pathlib import Path
import json
import hashlib

b=Path(__file__).parent
poses=['idle','walk-a','attack','walk-b']
s=44/909.5
canonical,canvas,frame,origin=[782,962],[1586,993],[104,64],[8,4]
source=json.loads((b/'source-evidence.json').read_text())
identity=source['originalIdentity']+' Approved chunky head/hands/boots, broad matte cel planes and clean complete alpha silhouette.'
contract=('C01-UNIT-BERSERKER is a catalog-only canonical combat-unit with no current campaign/built-in rule consumer or weapon binding. '
          'Preserve idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1. '
          'Original attack drops the near screen-left axe and both original walks advance the same front foot. '
          'Coordinator explicitly approved repairing continuity using verified idle identity: two separate short single-bladed axes, one in each original hand, in all four poses. '
          'New attack is an asymmetric forward chop with the other axe raised in guard; this is a deliberate continuity repair, not exact replication of the defective source frame. '
          'New walks have opposite contacts. No additional rule, consumer binding, fifth pose or independent rage/impact FX.')
padding='Idle native1586x992; walk-a/walk-b native1584x993; attack native1585x992. Common1586x993 requires two transparent right columns for walks, one right column for attack, and one bottom row for idle/attack during export preparation only. Native pixels/canvases remain unchanged.'
reg={'status':'Measured whole-canvas proposal; coordinator final export/review pending','sharedScale':s,'scaleFraction':'44/909.5','bodyReferenceSourcePixels':909.5,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Accepted idle crown y48 to exact two-sole mean y957.5. One body-derived scale applies to every complete native canvas.','canonicalSourceFoot':canonical,'commonSourceCanvas':canvas,'paddingPolicy':padding,'canvasPaddingToleranceRequired':2,'maximumPadRight':2,'maximumPadBottom':1,'proposedCommonFrameSize':frame,'proposedOrigin':origin,'registrationMethod':'Whole-canvas translation at locked44/909.5. Anchor x is mean of both boot bottom-four-row alpha>127 support bands; y is lowest sole. Manual boot-only ROIs exclude the near axe blade; exact midpoint and ROIs preserved.','poses':{}}
m={'asset':'C01-UNIT-BERSERKER','actualConsumers':[],'consumerStatus':'Catalog-only; no current campaign or built-in rule consumer/binding','status':'Native quartet complete; coordinator final sequence review/export pending','generationTool':'built-in image_gen','masterPolicy':'Every selected master/raw PNG is byte-identical to its native output. No painting, recoloring, resizing, cropping or alpha cleanup. Black/white composites are QA only.','sharedScale':s,'bodyReferenceSourcePixels':909.5,'bodyTargetHeightLogicalPixels':44,'frameOrder':poses,'commonSourceCanvas':canvas,'sourceEvidence':str(b/'source-evidence.json'),'registrationEvidence':str(b/'registration-evidence.json'),'identityContract':identity,'semanticContract':contract,'sourceContinuityRepair':source['sourceContinuityRepairApproval'],'selectedAttempts':{p:'first successful native output' for p in poses},'idleApproval':'Coordinator2026-10-19 18:02UTC viewed and accepted idle,44/909.5 scale,1586x992 canvas and foot[782,962].','poses':{}}
reviews={}
for p in poses:
    d=b/p;q=json.loads((d/'alpha-qa.json').read_text());req=json.loads((d/'request.json').read_text());f=q['suggestedSourceFoot']
    reviews[p]=identity if p=='idle' else q['semanticReview']
    off=[(canonical[i]-f[i])*s for i in range(2)]
    full=[origin[0]+off[0],origin[1]+off[1],origin[0]+off[0]+canvas[0]*s,origin[1]+off[1]+canvas[1]*s]
    bounds=q['bboxAlphaGreaterThan127'];visible=[origin[i%2]+off[i%2]+bounds[i]*s for i in range(4)]
    fit=full[0]>=0 and full[1]>=0 and full[2]<=frame[0] and full[3]<=frame[1]
    reg['poses'][p]={'nativeSize':q['size'],'padRight':canvas[0]-q['size'][0],'padBottom':canvas[1]-q['size'][1],'sourceSupportAnchorEstimated':f,'bothBootSoleBandMidpoint':q['sourceFootMidpoint'],'registrationOffsetLogical':[round(x,9) for x in off],'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],'proposedLogicalVisibleBounds':[round(x,6) for x in visible],'proposedLogicalFullCanvasBounds':[round(x,6) for x in full],'wholeCanvasFitsProposedFrame':fit,'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterCountAlphaGreaterThan1']==0,'interpretation':'Native alpha=1 specks retained; no silhouette alpha>1 reaches any edge.'}}
    m['poses'][p]={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'exactPrompt':str(d/'prompt.txt'),'request':str(d/'request.json'),'exactToolCallPromptVerified':True,'exactPromptSha256':req['promptSha256'],'alphaQA':str(d/'alpha-qa.json'),'qaBlack':str(d/'qa-black.png'),'qaWhite':str(d/'qa-white.png'),'nativeSize':q['size'],'sourceFootAnchorSuggested':f,'visibleBoundsAlphaGreaterThan127':bounds,'sha256':q['sha256'],'generationSource':q['nativeSource'],'review':reviews[p]}
    assert fit and q['perimeterCountAlphaGreaterThan1']==0
    assert hashlib.sha256((d/'master.png').read_bytes()).hexdigest()==q['sha256']==hashlib.sha256((d/'raw.png').read_bytes()).hexdigest()==hashlib.sha256(Path(q['nativeSource']).read_bytes()).hexdigest()
    assert hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest()==req['promptSha256']
    assert max(canvas[i]-q['size'][i] for i in range(2))<=2
reg['requiredMaximumTranslationLogicalPixels']=max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical'])
reg['recommendedRegistrationLimitLogicalPixels']=4
reg['translationBasis']='Measured attack foot[714,952] differs from idle[782,962] by[68,10]source pixels. At44/909.5, whole-canvas offset is x+3.289719626,y+0.483782298logical. Existing limit4 suffices; all complete canvases fit104x64 at origin[8,4], with no independent scale or silhouette fitting.'
(b/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n');(b/'bundle-manifest.json').write_text(json.dumps(m,indent=2)+'\n')
qa={'status':'Native quartet semantic/alpha/geometry QA passes; coordinator final sequence review/export pending','topic':m['asset'],'actualConsumers':[],'consumerStatus':m['consumerStatus'],'bodyScale':'44/909.5','selectedNativeCanvasSizes':{p:m['poses'][p]['nativeSize'] for p in poses},'commonSourceCanvas':canvas,'sourcePaddingRequired':padding,'nativeMastersUnmodified':True,'allRawMasterNativeHashesMatch':True,'allExactPromptsVerified':True,'alphaPerimeterGate':'All native perimeter alpha<=1; zero silhouette alpha>1 on edges; native alpha=1 specks retained.','nativeAndWhiteBackgroundViewsInspected':poses,'blackAndWhiteCompositesSaved':poses,'identityPass':True,'exactlyTwoSeparateAxesEveryPose':True,'oneOriginalAxePerOriginalHand':True,'twoArmsAndTwoHandsOnly':True,'equipmentCompleteEveryPose':True,'walkContactsOppose':True,'attackAnatomyReview':reviews['attack'],'semanticContract':contract,'sourceContinuityRepair':m['sourceContinuityRepair'],'independentFXBakedIntoCharacter':False,'registrationEvidence':'registration-evidence.json','recommendedRegistrationLimitLogicalPixels':4,'proposedFullCanvasFit':all(x['wholeCanvasFitsProposedFrame'] for x in reg['poses'].values()),'runtimeOrRepositoryWrites':False,'weaponRulesChanged':False,'newConsumerBindingInvented':False,'gameplayFixClaimed':False}
(b/'qa-review.json').write_text(json.dumps(qa,indent=2)+'\n')
for name in ['source-evidence.json','idle-review.json']:
    p=b/name;x=json.loads(p.read_text());x['status']='Native quartet complete; coordinator final sequence review/export pending';x['quartetManifest']='bundle-manifest.json'
    if name=='idle-review.json':x['remainingPosesGenerated']=True
    p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
for name,r in source['references'].items():assert hashlib.sha256((b/'references'/name).read_bytes()).hexdigest()==r['sha256']
lines=['# Berserker four-pose review','',contract,'','## Identity',identity,'','## Geometry',padding,reg['translationBasis'],'Shared44/909.5; canonical source foot[782,962].','']
for p in poses:
    r=reg['poses'][p];lines+=['### '+p,reviews[p],f"Source foot {r['sourceSupportAnchorEstimated']}; exact sole midpoint {r['bothBootSoleBandMidpoint']}; whole-canvas offset {r['registrationOffsetLogical']}; source visible bounds {r['nativeVisibleBoundsAlphaGreaterThan127']}; full logical canvas {r['proposedLogicalFullCanvasBounds']}.",'']
lines+=['## Provenance and QA','All four are first successful native built-in image_gen outputs. Accepted idle was reviewed before the other poses. Every raw/master/native and exact prompt hash matches. Complete original board, first-row/idle/attack crops and original HD/runtime strips are preserved. All native and white-background views inspected; black/white composites saved. No repository/runtime writes or new consumer binding. The original attack missing axe is documented as an explicitly approved continuity repair, not exact source replication.']
(b/'pose-review.md').write_text('\n'.join(lines)+'\n')
print(json.dumps({'manifest':str(b/'bundle-manifest.json'),'sourceFeet':{p:reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'maximumTranslationLogical':reg['requiredMaximumTranslationLogicalPixels'],'fullCanvasBounds':{p:reg['poses'][p]['proposedLogicalFullCanvasBounds'] for p in poses}},indent=2))
