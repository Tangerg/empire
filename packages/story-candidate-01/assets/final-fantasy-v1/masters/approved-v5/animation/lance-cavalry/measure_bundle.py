from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib,math
b=Path(__file__).parent;poses=['idle','walk-a','attack','walk-b'];s=56/939;canonical=[702,977];origin=[14,6];frame=[128,80]
rois={
'idle':[[340,870,490,985],[550,870,695,975],[700,890,850,990],[900,880,1050,985]],
'walk-a':[[340,880,490,980],[530,800,655,905],[675,820,790,935],[900,880,1050,985]],
'attack':[[155,865,310,970],[460,850,625,950],[735,885,905,985],[960,850,1130,970]],
'walk-b':[[305,770,465,910],[500,875,655,975],[695,885,850,990],[855,770,985,915]]}
probe_centers={'idle':[(510,915),(691,878),(870,910),(1150,510)],'walk-a':[(505,909),(665,888),(847,915),(1150,510)],'attack':[(385,890),(685,895),(930,915),(1220,405)],'walk-b':[(475,877),(670,917),(844,918),(1150,510)]}
reviews={
'idle':'Approved mounted idle: all four connected horse legs planted; young original helmeted rider, shield, reins/lance hands, barding, horse and functional colors retained.',
'walk-a':'Outer hind and fore hooves support; two inner hooves are bent and visibly airborne. Same upper identity, horse torso, lance and canvas scale; minor tail sway.',
'attack':'Rider leans forward and visibly drives lance with right glove; left glove controls reins and left arm retains cream shield. Horse braces in wider stance. Same lance length within measured redraw variation; no projectile or external FX.',
'walk-b':'Both hind and fore supports reverse walk-a: outer hind and fore hooves lift; inner hind and fore hooves support. Four connected legs, four separate hooves and transparent negative spaces retained.'}
reg={'status':'Native quartet complete; coordinator final sequence review and formal export pending','sharedScale':s,'scaleFraction':'56/939','bodyReferenceSourcePixels':939,'bodyTargetHeightLogicalPixels':56,'canonicalSourceFoot':canonical,'commonSourceCanvas':[1560,1008],'proposedCommonFrameSize':frame,'proposedOrigin':origin,'canvasPaddingToleranceRequired':0,'paddingPolicy':'No padding used or required. All four native outputs are exactly1560x1008. Preserve entire native canvases including all alpha1 pixels.','registrationMethod':'Visually inspected four horse hoof ROIs; alpha>127 supports measured. Each sole is max y; hoof x is mean of bottom4rows. Source foot x averages all four hoof centers including raised hooves, y is lowest sole. One shared56/939 scale, one origin, whole-canvas translation only. Rider boot/stirrup excluded.','poses':{}}
review=Image.new('RGB',(1024,540),(220,220,220));draw=ImageDraw.Draw(review);qd=b/'qa-common-scale';qd.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
for i,pose in enumerate(poses):
 d=b/pose;im=Image.open(d/'master.png').convert('RGBA');a=np.array(im)[:,:,3];q=json.loads((d/'alpha-qa.json').read_text());assert im.size==(1560,1008);assert q['perimeterCountAlphaGreaterThan1']==0
 assert sha(d/'master.png')==sha(d/'raw.png')==sha(q['nativeSource'])==q['sha256']
 feet=[]
 for idx,(x0,y0,x1,y1) in enumerate(rois[pose]):
  yy,xx=np.where(a[y0:y1,x0:x1]>127);yy+=y0;xx+=x0;sole=int(yy.max());feet.append({'hoofIndex':idx,'roi':[x0,y0,x1,y1],'bounds':[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)],'soleY':sole,'bottomFourRowsMeanX':float(xx[yy>sole-4].mean())})
 foot=[round(np.mean([f['bottomFourRowsMeanX'] for f in feet])),max(f['soleY'] for f in feet)];delta=[canonical[j]-foot[j] for j in range(2)];logical=[v*s for v in delta];tx=origin[0]+logical[0];ty=origin[1]+logical[1];bounds=[tx,ty,tx+im.width*s,ty+im.height*s];assert min(bounds[:2])>=0 and bounds[2]<=128 and bounds[3]<=80
 probes=[]
 for idx,(cx,cy) in enumerate(probe_centers[pose]):
  vals=[]
  for y in range(cy-25,cy+26):
   for x in range(cx-25,cx+26):
    v=a[y-3:y+4,x-3:x+4]
    if v.shape==(7,7) and not v.any():vals.append(((x-cx)**2+(y-cy)**2,x,y))
  assert vals,(pose,idx);_,x,y=min(vals);probes.append({'name':['hind-gap','central-gap','fore-gap','lance-clearance'][idx],'rectangle':[x-3,y-3,x+4,y+4],'maxAlpha':0,'zeroPixels':49})
 endpoints=[]
 for j,(x0,y0,x1,y1) in enumerate(([[0,230,400,390],[1250,220,1560,400]] if pose=='attack' else [[0,220,400,520],[1250,220,1560,500]])):
  yy,xx=np.where(a[y0:y1,x0:x1]>127);yy+=y0;xx+=x0;extreme=int(xx.min() if j==0 else xx.max());ys=yy[xx==extreme];endpoints.append([extreme,float((ys.min()+ys.max())/2)])
 length=math.dist(*endpoints)
 rgba=im.transform(tuple(frame),Image.Transform.AFFINE,(1/s,0,-tx/s,0,1/s,-ty/s),resample=Image.Resampling.BICUBIC,fillcolor=(0,0,0,0));ra=np.array(rgba)[:,:,3];edge=np.r_[ra[0],ra[-1],ra[:,0],ra[:,-1]];assert edge.max()==0;rgba.save(qd/f'{pose}-rgba.png')
 for row,bgcol in enumerate(['white','black','#626b59']):
  bg=Image.new('RGBA',tuple(frame),bgcol);bg.alpha_composite(rgba);review.paste(bg.convert('RGB').resize((256,160),Image.Resampling.NEAREST),(i*256,row*180+20));draw.text((i*256+5,row*180+4),pose+' / '+bgcol,fill=(0,0,0))
 rec={'nativeSize':list(im.size),'sourceSupportAnchorEstimated':foot,'sourceFootMidpoint':[float(np.mean([f['bottomFourRowsMeanX'] for f in feet])),foot[1]],'hoofMeasurements':feet,'registrationOffsetSource':delta,'registrationOffsetLogical':logical,'fullCanvasBoundsLogical':bounds,'visibleBoundsAlphaGreaterThan127':q['bboxAlphaGreaterThan127'],'projectedLanceEndpoints':endpoints,'projectedLanceLengthSourcePixels':length,'nativeLanceTipRightMarginPixels':1559-endpoints[1][0],'negativeSpaceProbes':probes,'nativePerimeterMaxAlpha':q['perimeterMaxAlpha'],'nativePerimeterAlphaGreaterThan1Count':0,'diagnosticDerivedPerimeterMaxAlpha':int(edge.max()),'semanticReview':reviews[pose]};reg['poses'][pose]=rec
 if pose!='idle':
  prov=json.loads((d/'input-provenance.json').read_text());req=json.loads((d/'request.json').read_text());assert req['prompt']==(d/'prompt.txt').read_text().rstrip('\n')
  for p,h in prov['referencesAtCall'].items():assert sha(p)==h
  prov.update({'exactTool':'built-in image_gen','requestFile':'request.json','outputNative':q['nativeSource'],'outputSha256':q['sha256'],'rawMasterByteIdentical':True,'sourcePixelEditing':'None; copied native image bytes unchanged.'});(d/'provenance.json').write_text(json.dumps(prov,indent=2)+'\n')
for p,v in reg['poses'].items():v['projectedLanceLengthVersusIdleRatio']=v['projectedLanceLengthSourcePixels']/reg['poses']['idle']['projectedLanceLengthSourcePixels']
reg['maximumMeasuredRegistrationLogicalPixels']=max(abs(v) for rec in reg['poses'].values() for v in rec['registrationOffsetLogical']);reg['recommendedRegistrationLimitLogicalPixels']=4
(b/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n');review.save(qd/'review-2x.png');(qd/'transform-evidence.json').write_text(json.dumps({'purpose':'QA-only diagnostic; no formal exports or repository changes','commonScale':s,'scaleFraction':'56/939','frameSize':frame,'origin':origin,'canonicalSourceFoot':canonical,'fullCanvasesPreserved':True,'noPixelCleanup':True,'derivedPerimeterExactlyZero':True,'poses':{p:{k:v[k] for k in ['registrationOffsetLogical','fullCanvasBoundsLogical','diagnosticDerivedPerimeterMaxAlpha']} for p,v in reg['poses'].items()}},indent=2)+'\n')
print(json.dumps({p:{k:v[k] for k in ['sourceSupportAnchorEstimated','registrationOffsetSource','registrationOffsetLogical','projectedLanceLengthSourcePixels','projectedLanceLengthVersusIdleRatio','nativeLanceTipRightMarginPixels']} for p,v in reg['poses'].items()},indent=2))
