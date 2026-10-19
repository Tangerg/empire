from PIL import Image,ImageDraw
from pathlib import Path
import numpy as np,json
p=Path('/workspace/shared/empire-art-rebuild/approved-v5/animation/stone-golem');im=Image.open(p/'idle/master.png').convert('RGBA');a=np.array(im)[:,:,3]
feet={}
for name,roi in {'screenLeft':[420,850,700,991],'screenRight':[800,850,1115,991]}.items():
 x0,y0,x1,y1=roi; yy,xx=np.where(a[y0:y1,x0:x1]>127);sole=int(yy.max()+y0);by,bx=np.where(a[max(sole-3,y0):sole+1,x0:x1]>127);feet[name]={'roi':roi,'soleY':sole,'bottomBandMeanX':float(bx.mean()+x0),'threshold':127,'bottomBandRows':4}
x0,y0,x1,y1=[710,170,940,470];yy,xx=np.where(a[y0:y1,x0:x1]>127);head=int(yy.min()+y0)
meanSole=sum(f['soleY'] for f in feet.values())/2;anchor=[round(sum(f['bottomBandMeanX'] for f in feet.values())/2),max(f['soleY'] for f in feet.values())];body=meanSole-head;scale=62/body
q=json.loads((p/'idle/alpha-qa.json').read_text());q.update({'feet':feet,'headTopY':head,'meanSoleY':meanSole,'bodyHeightSource':body,'sourceSupportAnchorEstimated':anchor});(p/'idle/alpha-qa.json').write_text(json.dumps(q,indent=2))
reg={'status':'Idle reviewed by worker; coordinator acceptance pending','sharedScale':scale,'scaleFraction':f'62/{body}','bodyReferenceSourcePixels':body,'bodyTargetHeightLogicalPixels':62,'humanReferenceBodyLogicalPixels':44,'canonicalSourceFoot':anchor,'commonSourceCanvas':im.size,'proposedCommonFrameSize':[144,104],'proposedOrigin':[6,6],'originalPresentationFootprint':[2,2],'originalRuntimeFrameSize':[64,64],'originalRuntimeAnchor':[32,63],'gameplaySpecialExpandedOccupancy':False,'footprintChanged':False,'registrationMethod':'One idle head-to-mean-sole derived scalar. Each complete authored canvas translated only according to measured mean two-sole x and lowest-sole y. No independent scale or bbox fit.','idleFullCanvasLogicalSize':[im.width*scale,im.height*scale],'idleVisibleBoundsLogical':[6+v*scale for v in q['bboxAlphaGreaterThan127']],'idleHeadToSoleHeight':62,'idleRaisedFistTotalHeight':(meanSole-q['bboxAlphaGreaterThan127'][1])*scale,'poses':{'idle':{'sourceSupportAnchorEstimated':anchor,'registrationOffsetLogical':[0,0]}}}
(p/'registration-evidence.json').write_text(json.dumps(reg,indent=2))
# Inspection derivative only: complete source-canvas scaling, no alpha modification.
normal=Path('/workspace/shared/empire-art-rebuild/approved-v5/animation/rune-shield')
nreg=json.loads((normal/'registration-evidence.json').read_text());ni=Image.open(normal/'idle/master.png').convert('RGBA');ns=nreg['sharedScale'];nfoot=nreg['canonicalSourceFoot']; nsize=tuple(round(v*ns) for v in ni.size);gsize=tuple(round(v*scale) for v in im.size)
for background,color in [('white','white'),('black','black'),('neutral','#73776a')]:
 board=Image.new('RGBA',(300,126),color);d=ImageDraw.Draw(board);d.text((5,2),'STONE GOLEM: head-to-sole 62',fill='#aa7722' if background=='black' else '#33291d');d.text((170,2),'RUNE SHIELD: 44',fill='#aa7722' if background=='black' else '#33291d')
 ground=110; gx=6;gy=round(ground-anchor[1]*scale);nx=165;ny=round(ground-nfoot[1]*ns)
 board.alpha_composite(im.resize(gsize,Image.Resampling.LANCZOS),(gx,gy));board.alpha_composite(ni.resize(nsize,Image.Resampling.LANCZOS),(nx,ny));d.line((0,111,299,111),fill='#999999');board.convert('RGB').save(p/f'idle-review-native-{background}.png');board.resize((1200,504),Image.Resampling.NEAREST).convert('RGB').save(p/f'idle-review-4x-{background}.png')
review={'workerVisualReview':'Pass identity, matte broad stone planes, retained original glyph families, big fist/feet, no ground shadow, no external glow. Raw black/white and logical-size side-by-side reviewed. Await coordinator review.','scaleProposal':reg,'sourceGlyphCaveat':'Source tiny engraved forms interpreted in broader line language; no extra symbol family or detached ornament.','runtimeReview':'Not performed; no repo/runtime writes.'}
(p/'idle-review.json').write_text(json.dumps(review,indent=2));print(json.dumps(reg,indent=2))
