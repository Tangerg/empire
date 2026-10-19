from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib,math,numpy as np
b=Path(__file__).parent; poses=['idle','walk-a','attack','walk-b']; scale=88/1449; sourcefoot=[920,943];origin=[8,4];frame=[112,72]
reg={'status':'Measured whole-canvas proposal; no runtime writes','consumerClassification':'Catalog-only, not c01.supply-wagon','sharedScale':scale,'scaleFraction':'88/1449','scaleReference':'Coordinator accepted88 logical visible width from idle alpha>127 width1449; crew37.8 logical high beside44-high infantry. One shared scale, no independent fitting.','canonicalSourceFoot':sourcefoot,'commonSourceCanvas':[1536,1024],'paddingPolicy':'All native canvases exactly1536x1024. No padding, crop or alpha changes required.','proposedCommonFrameSize':frame,'proposedOrigin':origin,'proposedRuntimeAnchor':[origin[0]+sourcefoot[0]*scale,origin[1]+sourcefoot[1]*scale],'registrationMethod':'Mean x of two visible wheel bottom-four-row alpha>127 support bands; y nearest large wheel contact. Human boots never own pivot. Whole-canvas translation at single shared scale.','originalPresentationFootprint':[2,2],'originalRuntimeFrameSize':[64,48],'originalRuntimeAnchor':[32,47],'rulesFootprintChanged':False,'poses':{}}
entries=[];samples={}
for pose in poses:
 d=b/pose;im=Image.open(d/'master.png').convert('RGBA');a=np.array(im.getchannel('A'));qa=json.loads((d/'alpha-qa.json').read_text());wheels={}
 for name,roi in {'nearLargeWheel':[470,810,825,975],'frontSmallWheel':[1080,810,1400,935]}.items():
  x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);cy=int(ys.max()+y0); yy,xx=np.where(a[cy-3:cy+1,x0:x1]>127);wheels[name]={'roi':roi,'contactY':cy,'bottomBandRows':4,'meanX':round(float((xx+x0).mean()),6),'spanX':[int(xx.min()+x0),int(xx.max()+x0)]}
 anchor=[round(sum(w['meanX'] for w in wheels.values())/2),wheels['nearLargeWheel']['contactY']];offset=[(sourcefoot[0]-anchor[0])*scale,(sourcefoot[1]-anchor[1])*scale];bb=qa['bboxAlphaGreaterThan127']; bounds=[origin[0]+offset[0],origin[1]+offset[1],origin[0]+offset[0]+1536*scale,origin[1]+offset[1]+1024*scale]
 rec={'nativeSize':im.size,'sourceSupportAnchorEstimated':anchor,'wheelContacts':wheels,'registrationOffsetLogical':offset,'nativeVisibleBoundsAlphaGreaterThan127':bb,'nativeAllNonzeroAlphaBounds':qa['bboxAlphaNonzero'],'proposedLogicalFullCanvasBounds':bounds,'wholeCanvasFitsProposedFrame':bounds[0]>=0 and bounds[1]>=0 and bounds[2]<=frame[0] and bounds[3]<=frame[1],'perimeterGate':{'maxNativePerimeterAlpha':qa['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':qa['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':qa['perimeterCountAlphaGreaterThan1']==0,'interpretation':'Native alpha1 perimeter specks retained unchanged'}}
 reg['poses'][pose]=rec;qa.update({'wheelContacts':wheels,'suggestedSourceFoot':anchor,'sharedScale':scale,'registrationOffsetLogical':offset,'visualQA':'Reviewed at common logical scale and on black/white diagnostic composites. Source identity, one crew, two visible wheels, no permanent exterior glow/background.','nativeCanvasUnchanged':True});(d/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
 hashes=json.loads((d/'request-hashes.json').read_text());assert hashes['promptSha256']==hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest();assert hashes['requestFileSha256']==hashlib.sha256((d/'request.json').read_bytes()).hexdigest();r=json.loads((d/'request.json').read_text());assert r['prompt']==(d/'prompt.txt').read_text()
 assert (d/'raw.png').read_bytes()==(d/'master.png').read_bytes();assert im.size==(1536,1024);assert rec['wholeCanvasFitsProposedFrame'];assert rec['perimeterGate']['passesNativeSilhouettePerimeterGate']
 entries.append({'pose':pose,'master':str(d/'master.png'),'raw':str(d/'raw.png'),'nativeSize':im.size,'sha256':qa['sha256'],'promptSha256':hashes['promptSha256'],'requestSha256':hashes['requestFileSha256'],'alphaQa':str(d/'alpha-qa.json'),'registration':rec})
 # Diagnostic only, full native canvas at common scale and translation.
 out=Image.new('RGBA',(frame[0]*4,frame[1]*4));resized=im.resize((round(im.width*scale*4),round(im.height*scale*4)),Image.Resampling.LANCZOS);out.alpha_composite(resized,(round((origin[0]+offset[0])*4),round((origin[1]+offset[1])*4)));samples[pose]=out
reg['requiredMaximumTranslationLogicalPixels']=max(abs(v) for p in reg['poses'].values() for v in p['registrationOffsetLogical']);reg['recommendedRegistrationLimitLogicalPixels']=4
(b/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
qd=b/'qa-common-scale';qd.mkdir(exist_ok=True)
for bg in ['white','black']:
 sheet=Image.new('RGBA',(frame[0]*4*4,(frame[1]*4+36)),bg);dr=ImageDraw.Draw(sheet)
 for i,p in enumerate(poses):sheet.alpha_composite(samples[p],(i*frame[0]*4,0));dr.text((i*frame[0]*4+10,frame[1]*4+6),p,fill=('black' if bg=='white' else 'white'))
 sheet.convert('RGB').save(qd/f'quartet-{bg}@4x.png')
# Native logical-size diagnostic, enlarged nearest for fair legibility inspection.
sheet=Image.new('RGBA',(frame[0]*4,frame[1]+20),'#dadbd7');dr=ImageDraw.Draw(sheet)
for i,p in enumerate(poses):sheet.alpha_composite(samples[p].resize(tuple(frame),Image.Resampling.LANCZOS),(i*frame[0],0));dr.text((i*frame[0]+8,frame[1]+3),p,fill='black')
sheet.convert('RGB').save(qd/'quartet-native@1x.png');sheet.resize((sheet.width*3,sheet.height*3),Image.Resampling.NEAREST).convert('RGB').save(qd/'quartet-native@3x.png')
frames=[]
for p in ['idle','walk-a','walk-b','walk-a','walk-b','idle','attack','idle']:
 canvas=Image.new('RGBA',samples[p].size,'#cfd8c1');canvas.alpha_composite(samples[p]);frames.append(canvas.convert('RGB'))
frames[0].save(qd/'pose-cycle-diagnostic.gif',save_all=True,append_images=frames[1:],duration=[700,260,260,260,260,450,380,700],loop=0)
manifest={'topicId':'C01-UNIT-CANNON-WAGON','consumerClassification':'CATALOG-ONLY: no active domain unit or binding. Does not replace rule-derived c01.supply-wagon.','status':'Native quartet complete; coordinator export/review pending','tool':'built-in image_gen__imagegen','transparent_background':True,'sourceProvenance':str(b/'source-evidence.json'),'sharedScale':scale,'registrationEvidence':str(b/'registration-evidence.json'),'poses':entries,'rejectedAttempts':['attempts/attack-01','attempts/walk-b-01'],'rawMasterPolicy':'Native byte-identical unedited copies. No alpha cleanup, recolor, crop or source resampling. Diagnostic review images are derived separately only.','repositoryEdits':False,'gitActions':False}
(b/'bundle-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({'scale':scale,'anchor':reg['proposedRuntimeAnchor'],'poseAnchors':{p:reg['poses'][p]['sourceSupportAnchorEstimated'] for p in poses},'offsets':{p:reg['poses'][p]['registrationOffsetLogical'] for p in poses},'maxOffset':reg['requiredMaximumTranslationLogicalPixels'],'allNativePairsVerified':True},indent=2))
