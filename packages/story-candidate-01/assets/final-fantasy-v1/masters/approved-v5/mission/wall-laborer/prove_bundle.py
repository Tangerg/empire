from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib,numpy as np
from scipy.ndimage import distance_transform_edt
B=Path(__file__).parent; POSES=[p for p in ['idle','walk-a','carry-or-work','walk-b'] if (B/p/'master.png').exists()];S=44/941; CANONICAL=[836.5,966];ORIGIN=[8,4];FRAME=[104,64]; COLORS={'black':(0,0,0),'white':(255,255,255),'scene':(114,128,86)}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def bbox(a,t):
 y,x=np.where(a>t);return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)] if len(x) else None
ims={p:Image.open(B/p/'master.png') for p in POSES};common=[max(i.width for i in ims.values()),max(i.height for i in ims.values())];rows={}
for pose,im in ims.items():
 d=B/pose;a=np.array(im)[:,:,3];edge=np.concatenate([a[0,:],a[-1,:],a[:,0],a[:,-1]]);distance=distance_transform_edt(a<128);feet=[]
 for x0,x1 in {'idle':[(600,812),(815,1060)],'walk-a':[(580,820),(850,1160)],'carry-or-work':[(540,790),(850,1140)],'walk-b':[(560,790),(795,1040)]}[pose]:
  yy,xx=np.where(a[800:,x0:x1]>127);bot=int(yy.max()+800);yy,xx=np.where(a[bot-3:bot+1,x0:x1]>127);feet.append({'xRange':[x0,x1],'bottom':bot,'bottom4RowsBounds':[int(xx.min()+x0),bot-3,int(xx.max()+x0+1),bot+1],'soleCenterX':float((xx.min()+xx.max())/2+x0)})
 foot=[sum(f['soleCenterX'] for f in feet)/2,max(f['bottom'] for f in feet)];delta=[(CANONICAL[j]-foot[j])*S for j in range(2)];q={'mode':im.mode,'nativeSize':list(im.size),'rawSha256':sha(d/'raw.png'),'masterSha256':sha(d/'master.png'),'rawMasterIdentical':sha(d/'raw.png')==sha(d/'master.png'),'bboxAlphaNonzero':bbox(a,0),'bboxAlphaGreaterThan1':bbox(a,1),'bboxAlphaGreaterThan127':bbox(a,127),'perimeterMaxAlpha':int(edge.max()),'perimeterCountAlphaGreaterThan1':int((edge>1).sum()),'alphaCounts':{str(t):int((a>t).sum()) for t in [0,1,16,64,127,240,250,254]},'pixelsAlphaGreaterThan1MoreThan3pxFromOpaqueBody':int(((a>1)&(distance>3)).sum()),'maxAlphaMoreThan3pxFromOpaqueBody':int(a[distance>3].max()),'measuredBootSoleBands':feet,'sourceFoot':foot,'unchangedNativePolicy':'No source pixel cleanup, alpha modification, crop, rescale, or painting. QA transformations only.'};(d/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
 request=json.loads((d/'request.json').read_text()); prov={'tool':'built-in image_gen.imagegen','promptFileSha256':sha(d/'prompt.txt'),'requestFileSha256':sha(d/'request.json'),'references':[{'path':p,'sha256':sha(Path(p))} for p in request['referenced_image_paths']],'rawSha256':q['rawSha256'],'masterSha256':q['masterSha256']};(d/'request-provenance.json').write_text(json.dumps(prov,indent=2)+'\n')
 bounds=q['bboxAlphaGreaterThan127'];full=[ORIGIN[0]+delta[0],ORIGIN[1]+delta[1],ORIGIN[0]+delta[0]+common[0]*S,ORIGIN[1]+delta[1]+common[1]*S]
 rows[pose]={'nativeSize':list(im.size),'padRight':common[0]-im.width,'padBottom':common[1]-im.height,'measuredBootSoleBands':feet,'sourceSupportAnchor':foot,'registrationOffsetLogical':delta,'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],'proposedLogicalVisibleBounds':[ORIGIN[j%2]+delta[j%2]+bounds[j]*S for j in range(4)],'proposedLogicalFullCanvasBounds':full,'wholeCanvasFitsProposedFrame':full[0]>=0 and full[1]>=0 and full[2]<=FRAME[0] and full[3]<=FRAME[1],'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterMaxAlpha']<=1}}
 for name,col in COLORS.items():
  canvas=Image.new('RGBA',im.size,col+(255,));canvas.alpha_composite(im);canvas.convert('RGB').save(d/f'qa-{name}.png')
 annotated=Image.open(d/'qa-white.png');dr=ImageDraw.Draw(annotated)
 for f in feet:
  box=f['bottom4RowsBounds'];dr.rectangle((box[0],box[1]-3,box[2],box[3]+3),outline=(255,0,128),width=3);dr.text((box[0],box[1]-22),f"bottom={f['bottom']} centerX={f['soleCenterX']}",fill=(255,0,128))
 annotated.save(d/'qa-foot-measurements.png')
reg={'status':'Measured source proposal only; no runtime writes','sharedScale':S,'bodyReferenceSourcePixels':941,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Idle headscarf crown alpha>127 y25 to lowest supporting boot sole y966 =941 pixels. One fixed 44/941 scale for every pose.','canonicalSourceFoot':CANONICAL,'commonSourceCanvas':common,'paddingPolicy':'Technical transparent right/bottom padding in export only; preserve native raw/master unchanged.','canvasPaddingToleranceRequired':max(max(r['padRight'],r['padBottom']) for r in rows.values()),'proposedCommonFrameSize':FRAME,'proposedOrigin':ORIGIN,'registrationMethod':'Independent alpha>127 bottom4-row sole bands for each boot; x is average sole-band center, y lowest supporting sole. Whole-canvas translation only; no per-pose rescale or fitting.','poses':rows,'requiredMaximumTranslationLogicalPixels':max(abs(x) for r in rows.values() for x in r['registrationOffsetLogical']),'recommendedRegistrationLimitLogicalPixels':4,'registrationLimitApproval':'Coordinator approved fixed44/941 scale, canonical foot[836.5,966],104x64 frame origin[8,4] after idle gate. Measured final per-pose translations are below1logical pixel; final quartet review pending.'}
(B/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
def tile(im,scale,foot,color,anchor=(52,51),frame=(104,64)):
 tx=anchor[0]-foot[0]*scale;ty=anchor[1]-foot[1]*scale
 transformed=im.transform((frame[0]*4,frame[1]*4),Image.Transform.AFFINE,(1/(scale*4),0,-tx/scale,0,1/(scale*4),-ty/scale),Image.Resampling.BICUBIC).resize(frame,Image.Resampling.LANCZOS)
 bg=Image.new('RGBA',frame,color+(255,));bg.alpha_composite(transformed);return bg.convert('RGB')
board=Image.new('RGB',(104*len(POSES),64*3)); tiles={}
for r,(name,col) in enumerate(COLORS.items()):
 for i,p in enumerate(POSES):tiles[name,p]=tile(ims[p],S,rows[p]['sourceSupportAnchor'],col);board.paste(tiles[name,p],(104*i,64*r))
board.save(B/'qa-native44-three-backgrounds.png');board.resize((board.width*3,board.height*3),Image.Resampling.NEAREST).save(B/'qa-native44-three-backgrounds-3x.png')
family=['border-farmer','baker','forge-artisan','wall-laborer'];comparison=Image.new('RGB',(104*4,64*3));
for i,p in enumerate(family):
 base=B.parent/p; rr=json.loads((base/'registration-evidence.json').read_text());row=rr['poses']['idle'];foot=row.get('sourceSupportAnchor',row.get('sourceSupportAnchorEstimated'));im=Image.open(base/'idle/master.png')
 for j,col in enumerate(COLORS.values()):comparison.paste(tile(im,rr['sharedScale'],foot,col),(104*i,64*j))
comparison.save(B/'qa-family-native44.png');comparison.resize((comparison.width*3,comparison.height*3),Image.Resampling.NEAREST).save(B/'qa-family-native44-3x.png')
if len(POSES)==4:
 frames=[tiles['scene',p].resize((416,256),Image.Resampling.NEAREST) for p in ['walk-a','walk-b']];frames[0].save(B/'qa-walk-6.25fps.gif',save_all=True,append_images=frames[1:],duration=160,loop=0)
 for name,col in COLORS.items():
  montage=Image.new('RGB',(round(common[0]*.4)*2,round(common[1]*.4)*2),col)
  for k,p in enumerate(POSES):
   src=Image.new('RGBA',tuple(common),col+(255,));src.alpha_composite(ims[p]);t=src.convert('RGB').resize((round(common[0]*.4),round(common[1]*.4)),Image.Resampling.LANCZOS);montage.paste(t,((k%2)*t.width,(k//2)*t.height))
  montage.save(B/f'qa-native-montage-{name}.png')
print(json.dumps(reg,indent=2))
