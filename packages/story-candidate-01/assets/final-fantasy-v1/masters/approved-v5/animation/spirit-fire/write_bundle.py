from pathlib import Path
import json, hashlib, math
from PIL import Image,ImageDraw
import numpy as np
from scipy import ndimage
P=Path(__file__).parent
ORDER=['idle','walk-a','attack','walk-b']
SCALE=60/940
CANVAS=(1586,993)
FRAME=(116,76)
ORIGIN=(6,5)
GEN={'idle':'exec-5ea6729c-e914-4adc-b8ba-d8fe739765fd.png','walk-a':'exec-fb068e60-048e-42e2-921e-c1ecb4d52598.png','attack':'exec-ae518589-61df-4b98-91a1-b4d4a8e0fb69.png','walk-b':'exec-af4e5490-14d2-4574-aed7-2a101b8298bd.png'}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def measure(im):
 ar=np.array(im).astype(int);r,g,b,a=[ar[:,:,i] for i in range(4)]
 # READ-ONLY stone-color diagnostic; no operations affect master or exported art.
 m=(a>200)&(r>55)&(g>50)&(b>40)&(r>=g)&(g>=b)&(r-g<23)&(g-b<26)
 m=ndimage.binary_closing(m,np.ones((13,13)))
 labs,n=ndimage.label(m,np.ones((3,3)));areas=np.bincount(labs.ravel());areas[0]=0;ys,xs=np.where(labs==areas.argmax());band=ys>=ys.max()-8
 return {'filledStoneBoundsInclusive':[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())],'filledStoneHeight':int(ys.max()-ys.min()+1),'chinBandCenterX':float(xs[band].mean()),'chinBandBottomY':int(ys.max()),'chinBandPixelCount':int(band.sum()),'maskPixels':int(len(xs))}
ims={k:Image.open(P/k/'master.png') for k in ORDER};measurements={k:measure(v) for k,v in ims.items()}
calx=870-measurements['idle']['chinBandCenterX'];caly=958-measurements['idle']['chinBandBottomY']
reg={'status':'Coordinator accepted native source quartet; formal export pending integrator','sharedScale':SCALE,'scaleFraction':'60/940','overallIdleHeightLogicalPixels':60,'bodyReferenceSourcePixels':940,'bodyTargetHeightLogicalPixels':60,'bodyMeasureQualification':'Here body refers to complete original floating spirit including attached flame mantle, not humanoid anatomy. Stone face itself has inspected outer height513sourcepx,32.7447logical.','canonicalSourceFoot':[870,958],'virtualAnchorNotPhysicalFootContact':True,'commonSourceCanvas':list(CANVAS),'proposedCommonFrameSize':list(FRAME),'proposedOrigin':list(ORIGIN),'canvasPaddingToleranceRequired':2,'maximumPadRight':2,'maximumPadBottom':1,'paddingPolicy':'Native idle/walk-a1585x992 pad1right+1bottom; attack1584x993 pad2right; walk-b1586x992 pad1bottom. Transparent padding is export/QA-only; raw/master remain unchanged.','registrationMethod':'A virtual hover presentation anchor, not contact. Read-only gray-stone segmentation selects largest13x13-closed8-connected diagnostic component. CenterX of its lowest9-row stone band and its lowestY supply rigid chin landmarks. Fixed idle calibration adds26.3272727273x and165y to give accepted idle anchor870,958. Apply same calibration to all poses, round-half-up, translate complete authored canvas at one locked60/940 scale. Wisp-tip swing does not pull the face around.','measurementMask':'Within full read-only native RGBA: alpha>200,R>55,G>50,B>40,R>=G>=B,R-G<23,G-B<26.13x13closing and8-connected largest component used ONLY for measurement, never to generate or alter alpha/art.','virtualHoverExplanation':'At idle, anchor x870 is the inspected stone-chin-axis projection and y958 is the lowest attached cyan-wisp pixel. Since this floating being has no ground contact, equivalent later anchors follow rigid chin with the same idle-derived hover offset; changing wisp tails are motion, not feet.','idleCalibration':[calx,caly],'originalFootprint':[2,2],'originalRuntimeFrame':[64,64],'originalRuntimeAnchor':[32,63],'footprintChanged':False,'poses':{}}
poses={};frames=[];hdframes=[];qa_all={}
for name,im in ims.items():
 d=P/name;ar=np.array(im);a=ar[:,:,3];m=measurements[name]
 anchor=[math.floor(m['chinBandCenterX']+calx+.5),m['chinBandBottomY']+caly]
 off=[(870-anchor[0])*SCALE,(958-anchor[1])*SCALE]
 b={str(t):Image.fromarray(np.uint8(a>t)*255).getbbox() for t in [0,1,16,64,127,254]}
 perimeter=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 qa={'mode':im.mode,'size':list(im.size),'sha256':sha(d/'master.png'),'rawSha256':sha(d/'raw.png'),'rawMasterByteIdentical':sha(d/'master.png')==sha(d/'raw.png'),'alphaBounds':b,'alphaZeroPixels':int((a==0).sum()),'alphaOpaquePixels':int((a==255).sum()),'alphaIntermediatePixels':int(((a>0)&(a<255)).sum()),'perimeterMaxAlpha':int(perimeter.max()),'perimeterCountAbove1':int((perimeter>1).sum()),'sourceAlphaPreserved':True,'editingPolicy':'No alpha cleanup, recolor, crop, resize or painting on raw/master. Black/white composites and common-scale previews are QA diagnostics only.','generationSource':'/workspace/scratch/84c9fe948755/generated_images/'+GEN[name]}
 (d/'alpha-qa.json').write_text(json.dumps(qa,indent=2));qa_all[name]=qa
 for bgname,c in [('black',(18,20,23,255)),('white',(255,255,255,255))]:
  bg=Image.new('RGBA',im.size,c);bg.alpha_composite(im);bg.convert('RGB').save(d/f'qa-{bgname}.png')
 fullbounds=[ORIGIN[0]+off[0],ORIGIN[1]+off[1],ORIGIN[0]+off[0]+CANVAS[0]*SCALE,ORIGIN[1]+off[1]+CANVAS[1]*SCALE]
 visible=[ORIGIN[0]+off[0]+b['127'][0]*SCALE,ORIGIN[1]+off[1]+b['127'][1]*SCALE,ORIGIN[0]+off[0]+b['127'][2]*SCALE,ORIGIN[1]+off[1]+b['127'][3]*SCALE]
 r={'nativeSize':list(im.size),'padRight':CANVAS[0]-im.width,'padBottom':CANVAS[1]-im.height,'sourceSupportAnchorEstimated':anchor,'chinLandmarkDiagnostic':m,'stoneHeightDeviationPercentVsIdle':(m['filledStoneHeight']/measurements['idle']['filledStoneHeight']-1)*100,'registrationOffsetSource':[870-anchor[0],958-anchor[1]],'registrationOffsetLogical':off,'nativeVisibleBoundsAlphaGreaterThan127':b['127'],'nativeAllNonzeroAlphaBounds':b['0'],'proposedLogicalVisibleBounds':visible,'proposedLogicalFullCanvasBounds':fullbounds,'wholeCanvasFitsProposedFrame':fullbounds[0]>=0 and fullbounds[1]>=0 and fullbounds[2]<=FRAME[0] and fullbounds[3]<=FRAME[1],'perimeterMaxAlpha':qa['perimeterMaxAlpha']}
 reg['poses'][name]=r
 req=json.loads((d/'request.json').read_text());assert req['prompt']==(d/'prompt.txt').read_text()
 poses[name]={'master':str(d/'master.png'),'raw':str(d/'raw.png'),'exactPrompt':str(d/'prompt.txt'),'request':str(d/'request.json'),'exactToolCallPromptVerified':True,'exactPromptSha256':sha(d/'prompt.txt'),'verifiedGenerationReferences':{s:{'sha256':sha(Path(s)),'verifiedAt':s} for s in req['referenced_image_paths']},'alphaQA':str(d/'alpha-qa.json'),'qaBlack':str(d/'qa-black.png'),'qaWhite':str(d/'qa-white.png'),'nativeSize':list(im.size),'sourceFootAnchorSuggested':anchor,'visibleBoundsAlphaGreaterThan127':b['127'],'sha256':sha(d/'master.png'),'generationSource':qa['generationSource']}
 # Full-canvas, single locked scalar; padding and translation only. These are inspection proofs, not final runtime export.
 padded=Image.new('RGBA',CANVAS);padded.alpha_composite(im)
 for density,collection in [(1,frames),(2,hdframes)]:
  f=Image.new('RGBA',(FRAME[0]*density,FRAME[1]*density));small=padded.resize((round(CANVAS[0]*SCALE*density),round(CANVAS[1]*SCALE*density)),Image.Resampling.LANCZOS)
  xy=(round((ORIGIN[0]+off[0])*density),round((ORIGIN[1]+off[1])*density));f.alpha_composite(small,xy);collection.append(f);f.save(d/f'native-{density}x-preview.png')
reg['requiredMaximumTranslationLogicalPixels']=max(abs(v) for r in reg['poses'].values() for v in r['registrationOffsetLogical']);reg['recommendedRegistrationLimitLogicalPixels']=6
(P/'registration-evidence.json').write_text(json.dumps(reg,indent=2))
reviews={'idle':'Stable upright stone-face hover; gold crown rises and cyan lower tip curls centrally.','walk-a':'Leftward trailing gold/blue mantle and broad lower-left cyan sweep, rigid face gently tilted. Attempt02 restores source face size; filled-stone span486 vs idle502(-3.19%).','attack':'Forward compact gold flare attached to mantle at right, narrow open dark mouth, no weapon/body. Attempt02 restores rigid mask scale; filled-stone span489 vs502(-2.59%).','walk-b':'Opposite rightward crown/lower-cyan sweep; preserved stone identity and closed mouth, no mirroring. Filled-stone span493 vs502(-1.79%).'}
for k in ORDER:poses[k]['review']=reviews[k]
bundle={'asset':'C01-UNIT-SPIRIT-FIRE','actualConsumers':[],'consumerQualification':'Catalog topic and narrative design roster exist; no actual static campaign actor binding found. Do not claim gameplay scene verification.','status':'Coordinator accepted native source quartet; formal export pending integrator','generationTool':'built-in image_gen','frameOrder':ORDER,'sharedScale':SCALE,'bodyReferenceSourcePixels':940,'bodyTargetHeightLogicalPixels':60,'canonicalSourceFoot':[870,958],'virtualAnchorNotPhysicalFootContact':True,'commonSourceCanvas':list(CANVAS),'masterPolicy':'Every selected raw/master is a byte-identical native image_gen output; superseded originals preserved in attempt-01. No alpha cleanup, recolor, crop, fit or source image edits. Diagnostic composites are separate.','sourceEvidence':str(P/'source-evidence.json'),'registrationEvidence':str(P/'registration-evidence.json'),'identityContract':'Floating cracked warm-gray stone face-mask with hollow eyes/long nose/closed mouth except attack; distinct gold/cream tongues and blue/cyan wisps plus small stone chips; no humanoid body/feet/weapon.','selectedAttempts':{'idle':'attempt01','walk-a':'attempt02 face-scale correction','attack':'attempt02 face-scale correction','walk-b':'attempt01'},'supersededAttempts':[{'path':'walk-a/attempt-01','reason':'Filled-stone face span461 vs502idle(-8.17%); imagegen-corrected to486(-3.19%).'},{'path':'attack/attempt-01','reason':'Filled-stone face span472 vs502idle(-5.98%); imagegen-corrected to489(-2.59%).'}],'poses':poses,'quartetManifest':'bundle-manifest.json'}
(P/'bundle-manifest.json').write_text(json.dumps(bundle,indent=2));(P/'qa-review.json').write_text(json.dumps({'status':bundle['status'],'identityAndNativeScale':'All poses personally inspected on native transparent and white backgrounds; black variants and registered logical sequence saved for coordinator.','alpha':qa_all,'poseReviews':reviews,'allWholeCanvasesFit':all(r['wholeCanvasFitsProposedFrame'] for r in reg['poses'].values()),'faceHeightContinuity':'Read-only filled-stone height502,486,489,493sourcepx. Largest residual3.19%=1.02logical pixel; same shared scalar applied to every pose. No per-frame resizing.','scope':'No repo or Git writes. Formal exporter integration and actor/scene claims remain coordinator-owned.'},indent=2))
for density,fs in [(1,frames),(2,hdframes)]:
 strip=Image.new('RGBA',(FRAME[0]*4*density,FRAME[1]*density))
 for i,f in enumerate(fs):strip.alpha_composite(f,(i*FRAME[0]*density,0))
 strip.save(P/f'quartet-native-{density}x.png')
board=Image.new('RGB',(700,330),(56,61,58));draw=ImageDraw.Draw(board);draw.text((12,8),'SPIRIT FIRE | one common scale 60/940 | original actions',(245,245,240))
for row,(bgname,c) in enumerate([('white',(255,255,255)),('black',(18,20,23)),('grass',(76,100,60))]):
 y=32+row*96;draw.text((12,y+30),bgname,(245,245,240))
 for i,f in enumerate(frames):
  x=80+i*150;bg=Image.new('RGBA',FRAME,c+(255,));bg.alpha_composite(f);board.paste(bg.convert('RGB'),(x,y));draw.text((x,y+78),ORDER[i],(245,245,240))
board.save(P/'quartet-native-review.png')
anim=[]
for i in [0,1,0,3,0,2,0,3]:
 f=frames[i];bg=Image.new('RGBA',(180,110),(76,100,60,255));bg.alpha_composite(f,(32,20));draw=ImageDraw.Draw(bg);draw.text((4,2),ORDER[i],(255,255,255,255));anim.append(bg.convert('RGB'))
anim[0].save(P/'quartet-native-animation.gif',save_all=True,append_images=anim[1:],duration=[400,300,160,300,400,500,200,300],loop=0)
(P/'pose-review.md').write_text('# Spirit Fire pose review\n\n'+'\n\n'.join(f'## {k}\n{reviews[k]}' for k in ORDER)+'\n\nAll exact prompts and referenced native outputs are recorded in the manifest. Floating virtual support is derived from the same rigid chin landmark calibration; neither flames nor stones are feet. Native alpha is preserved, including alpha1 traces. All complete padded canvases fit the approved116×76 frame at the locked scalar. No external silhouette is clipped or individually fitted.\n')
print(json.dumps({'landmarks':measurements,'anchors':{k:r['sourceSupportAnchorEstimated'] for k,r in reg['poses'].items()},'fullCanvasFits':{k:r['wholeCanvasFitsProposedFrame'] for k,r in reg['poses'].items()},'requiredRegistration':reg['requiredMaximumTranslationLogicalPixels']},indent=2))

# Approval is preserved if diagnostic previews are reproduced.
APPROVAL='Coordinator2026-10-19 23:34UTC reviewed final registered quartet on white/black/grass. Original stone-mask identity and distinct directional wisps/attack accepted; no broad halo. Shared60/940, chin-calibrated virtual anchors, maximum5.489logical translation with explicit limit6 and2px native padding accepted. About1logical-pixel rigid-face variation accepted as documented redraw difference, never per-pose scaling. Formal export is coordinator-owned; preserve catalog-only status.'
for fn in ["bundle-manifest.json","registration-evidence.json","qa-review.json"]:
 d=json.loads((P/fn).read_text());d["coordinatorSequenceApproval"]=APPROVAL;(P/fn).write_text(json.dumps(d,indent=2))
