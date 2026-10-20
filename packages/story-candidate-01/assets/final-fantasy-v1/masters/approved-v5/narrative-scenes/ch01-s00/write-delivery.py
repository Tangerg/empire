from pathlib import Path
from PIL import Image,ImageChops
import json,hashlib
O=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-scenes/ch01-s00')
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
a=json.loads((O/'audited-source.json').read_text());q=json.loads((O/'source-qa.json').read_text());e=json.loads((R/'docs/art-rebuild-20261019/approved-v5/opening-scene-classification/evidence.json').read_text())
source=R/e['originalSource']['path'];im=Image.open(source)
full=im.crop((627,627,1254,1254));inset=im.crop((632,632,1249,1249))
readers=['packages/story-candidate-01/src/story.ts','packages/story-candidate-01/src/presentation/candidate-01-story.ts','packages/story-candidate-01/src/presentation/candidate-01-bindings.ts','packages/story-candidate-01/src/campaign.ts']
proof={'topicId':'C01-CH01-S00','classification':'128x128 scene; legacy lone-bracer source retained as evidence only','originalBoardPath':str(source),'originalBoardSha256':sha(source),'originalBoardMatchesAudit':sha(source)==e['originalSource']['sha256'],'copiedBoardByteIdentical':sha(source)==sha(O/'original-full-board.png'),'fullCellBox':[627,627,1254,1254],'fullCellPixelSha256':hashlib.sha256(full.tobytes()).hexdigest(),'fullCellMatchesAudit':hashlib.sha256(full.tobytes()).hexdigest()==e['originalSource']['fullCellPixelSha256'],'fullCellCopyPixelIdentical':full.tobytes()==Image.open(O/'original-full-cell-627.png').tobytes(),'legacyInsetBox':[632,632,1249,1249],'legacyInsetPixelSha256':hashlib.sha256(inset.tobytes()).hexdigest(),'legacyInsetMatchesAudit':hashlib.sha256(inset.tobytes()).hexdigest()==e['originalSource']['legacyInsetPixelSha256'],'legacyRuntimeSha256':sha(O/'legacy-runtime-128.png'),'legacyRuntimeMatchesAudit':sha(O/'legacy-runtime-128.png')==e['runtime']['sha256'],'fullBoardAndExactCellVisuallyInspected':True,'namedReferencesVisuallyInspected':True,'sourceInterpretation':'Original bracer cell proves original subject mapping, not intended scene identity. Authored S00 and distinct PROP-LEFT-GLOVE IDs determine the two-person scene.','currentConsumerCheck':{'classificationAuditCount':35,'auditedConsumers':[],'prologueUses':'C01-CH01-S01','files':[{'path':p,'sha256':sha(R/p),'openingTopicOccurrences':(R/p).read_text().count('C01-CH01-S00'),'sameHashAsClassification':sha(R/p)==e['consumerSourceHashes'][p]} for p in readers]},'noNewSelector':True,'repositoryWrites':False}
dump(O/'source-proof.json',proof)
semantic={'topicId':'C01-CH01-S00','selectedAttempt':2,'sourceStatus':'worker visual review passed for exact square source; coordinator acceptance and registration pending','identity':{'Leia':'Approved eighteen-year-old apprentice, chestnut braid over anatomical right side, youthful face and blue-gray quilted light armor; guard on anatomical LEFT arm; no later injury or queen symbols','Roderick':'Approved older swept-gray-haired full-bearded mentor in practical steel and blue-gray armor; numeric age not asserted; old patched anatomical LEFT leather glove forms tie; right glove supports forearm'},'moment':'Pre-dawn wet border camp before first independent command. Calm equipment instruction and care expressed through a worn wristguard, not a medical treatment or separate prop.','handednessReview':'Leia left arm emerges from screen-right shoulder on her left-positioned body and continues to guarded wrist and exposed hand. Other bare hand is at her right-side belt. Roderick patched left hand connects to screen-right forearm on his right-positioned body; supporting right hand connects from his inner side.','visualReview':{'native1254':'pass: both approved named faces, full heads, fully connected arms and clear guard/tie','canonical128':'pass: recognizable two-person care gesture, large contrasting wristguard and tan strap; fine glove patch and knot mechanism naturally subordinate','fullPlate627':'pass: complete square story composition','broadMatteStyle':'pass: broad steel/cloth areas, chunky hands/heads, dark contours and simplified camp; no grain or uniformly repeated microtexture','storyDesktop960x534':'diagnostic limitation: centered landscape cover clips top of both heads, most of Roderick upper face and lower part of Leia hand; not approved as an actual consumer','storyMobile390x778':'diagnostic limitation: center portrait keeps knot and hand action but clips outer portions of both heads, much of braid and right shoulder; not a complete dual-identity frame','stagingInset280x245':'diagnostic: both faces and wrist action remain; Roderick hair top is near/cut at top edge','stagingMobile366x220':'diagnostic limitation: center landscape crop loses top of heads and much of Roderick eyes; not approved as an actual consumer'},'currentShippedConsumers':[],'sourceCropBox':None,'recommendedExport':'Existing opaque-scene-v1; full square canonical128x128 and source-native1254x1254 at density9.796875; do not stretch to landscape or add a selector','knownLimits':['Centered non-square cover viewports are unsuitable for complete two-face identity; use square/full-image framing if a future consumer is requested','Exact release mechanics and patch stitching are visible at native scale, not asserted readable at128','No actual game browser consumer acceptance can be claimed because S00 has no shipped selector'],'rejectedAttempt':'attempt-01: wrong anatomical bracer/costume sides retained with exact generation request and native source. Attempt02 corrects via imagegen, no manual raster changes.'}
dump(O/'semantic-review.json',semantic)
(O/'source-review.md').write_text('''# Opening wristguard scene source review

C01-CH01-S00 is an opaque 128×128 narrative scene. It shows Roderick fastening eighteen-year-old Leia’s left wristguard before her first independent command. It is distinct from C01-PROP-LEFT-GLOVE. The old source cell contains only a loose bracer and was rejected as the scene’s meaning, not repurposed into the new scene.

## Identity and chronology

- Latest novel, deep-novel-v2/01-border-fire-novel.md opening: pre-dawn camp after night rain; Roderick loosens her overtight strap and ties a knot that can be released one-handed. His old left glove has a repaired thumb base
- Character age: candidate-01/01-border-fire.md gives Leia age18; deep-customized/09-static-assets.md identifies her as the stage01 apprentice. Roderick’s numerical age is not specified and was not invented
- Approved named stage01 masters control the brown braid, youthful face, gray-blue light armor and scarf clasp; gray swept hair, full beard, elder face and practical steel armor
- Scene precedes Leia’s later cut wristguard at Redstone, gray-flag rebellion, shoulder scars and Roderick’s later hand impairment. No blood, bandage or injury appears
- Full original board04 and exact bottom-right full/inset cell were inspected; hashes are verified in source-proof.json. Original wrong subject remains historical evidence

## Selected output

- Built-in image_gen, attempt02; RGB1254×1254 and fully opaque
- master.png, raw.png, native.png preserve the selected generated output byte-for-byte. No raster editing, crop, cleanup, padding, extension or re-encoding
- Attempt01 had the bracer on Leia’s anatomical right arm and mirrored costume sides. It is preserved unedited with its full prompt, request, hashes and rejection reason
- Attempt02 used imagegen to reverse the whole scene and clarify Roderick’s fully covered patched left glove. Leia’s guarded forearm is now unambiguously anatomical left; her braid and sword lie on her anatomical right side
- Source SHA-256: '''+q['sha256']+'''
- Full square sourceCropBox=null; intended existing opaque-scene-v1 export is128×128 canonical plus1254×1254 source-native at density9.796875. Parent owns registration

## Visual QA

The native square,627px full-plate proof and exact128×128 canonical proof retain both recognizable named faces, intact arms, worn wristguard and Roderick’s tan-cord fastening gesture. The square composition is deliberately intimate rather than a wide panorama. Broad armor/cloth planes, large hands and heads, dark contours and quiet slate camp forms match the approved chunky matte language. No grain, all-over microtexture, invented insignia or readable text appears.

Cover views are stress tests only.960×534 and366×220 centered landscape crops cut head tops and much of Roderick’s upper face;390×778 portrait cover cuts outer parts of both heads and braid.280×245 is comparatively usable but brings Roderick’s hair against the top edge. These are not complete dual-identity consumer acceptances. Do not change the square source, stretch it or invent a selector to make a wide scene claim. A future consumer should show the complete square image.

## Scope and limits

S00 has no current shipped story/staging selector. The classification audit examined35 story entries; the prologue remains S01. Relevant consumer source files are rehashed in source-proof.json. No live-game acceptance is claimed. No repository code, manifests, exporter, UI selector or Git state was modified by this worker. Parent owns integration and formal checks.
''')
checks={'native1254Square':q['nativeSize']==[1254,1254],'fullyOpaque':q['fullyOpaque'],'nativeRawMasterByteIdentical':q['nativeRawMasterByteIdentical'],'referenceHashesMatch':q['referenceHashesMatch'],'promptMatchesRequest':(O/'prompt.txt').read_text()==json.loads((O/'request.json').read_text())['prompt'],'canonicalExactWholeCanvasResize':q['canonicalQAExactlyMatchesWholeNativeLanczos'],'originalBoardAndCellsVerified':all(proof[k] for k in ['originalBoardMatchesAudit','copiedBoardByteIdentical','fullCellMatchesAudit','fullCellCopyPixelIdentical','legacyInsetMatchesAudit','legacyRuntimeMatchesAudit']),'rejectedAttemptRetained':(O/'attempt-01/raw.png').exists(),'storyAndIdentityAuditComplete':True,'canonical128AndFullPlateVisualPass':True,'coverLimitationsRecorded':True,'sourceCropNull':q['sourceCropBox'] is None,'currentConsumerAbsenceVerified':all(x['openingTopicOccurrences']==0 and x['sameHashAsClassification'] for x in proof['currentConsumerCheck']['files'])}
verification={'topicId':'C01-CH01-S00','sourceWorkerAccepted':True,'coordinatorAcceptance':'pending','checks':checks,'allSourceChecksPassed':all(checks.values()),'nonSquareConsumerAccepted':False,'nativeSize':[1254,1254],'sourceCropBox':None,'logicalSize':[128,128],'nativePixelDensity':1254/128,'sourceSha256':q['sha256'],'formalBinding':'not performed by this worker','liveGame':'not checked; no current consumer','rejectedAttempts':1}
dump(O/'final-verification.json',verification)
files=[]
for p in sorted(O.rglob('*')):
 if p.is_file() and p.name!='bundle-manifest.json':
  v={'path':str(p.relative_to(O)),'bytes':p.stat().st_size,'sha256':sha(p)}
  if p.suffix=='.png':
   pi=Image.open(p);v.update(size=list(pi.size),mode=pi.mode)
  files.append(v)
dump(O/'bundle-manifest.json',{'schemaVersion':1,'topicId':'C01-CH01-S00','label':'罗德里克系护腕','kind':'scene','selectedAttempt':2,'approvedSource':'master.png','sourceStatus':'worker visual pass; coordinator pending','sourceSha256':q['sha256'],'nativeCopies':['raw.png','native.png'],'nativeSize':[1254,1254],'sourceCropBox':None,'logicalSize':[128,128],'density':1254/128,'deliberatelyOpaque':True,'currentConsumers':[],'noFormalBindingClaim':True,'files':files})
print(json.dumps(verification,indent=2))
