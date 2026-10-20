from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import numpy as np,json,hashlib
B=Path(__file__).parent;ORDER=['idle','walk-a','carry-or-work','walk-b'];POSES=[p for p in ORDER if (B/p/'master.png').exists()];SCALE=44/887;CANONICAL=[819.5,933];ORIGIN=[8,4];FRAME=[104,64];COLORS={'black':(16,16,16),'white':(255,255,255),'scene':(114,128,86)}
CONFIG={'idle':{'boots':[(590,800,790,940),(790,800,1030,940)],'exclusions':[(640,900,705,993)]},'walk-a':{'boots':[(570,770,790,920),(850,730,1150,920)],'exclusions':[(600,880,666,993)]},'carry-or-work':{'boots':[(490,780,710,950),(810,780,1060,950)],'exclusions':[(610,890,666,993)]},'walk-b':{'boots':[(620,720,795,910),(810,780,1040,950)],'exclusions':[]}}
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();ims={p:Image.open(B/p/'master.png') for p in POSES};common=[max(i.width for i in ims.values()),max(i.height for i in ims.values())];rows={}
for p,im in ims.items():
 d=B/p;a=np.array(im.getchannel('A'));mask=a>127;perim=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]]);feet=[];cfg=CONFIG[p]
 bbox=lambda t:list(Image.fromarray((a>t).astype('uint8')*255).getbbox())
 for x0,y0,x1,y1 in cfg.get('exclusions',[]):mask[y0:y1,x0:x1]=False
 for x0,y0,x1,y1 in cfg['boots']:
  yy,xx=np.where(mask[y0:y1,x0:x1]);bot=int(yy.max()+y0);yy,xx=np.where(mask[bot-3:bot+1,x0:x1]);feet.append({'measurementROI':[x0,y0,x1,y1],'bottom':bot,'bottom4RowsBounds':[int(xx.min()+x0),bot-3,int(xx.max()+x0+1),bot+1],'soleCenterX':float((xx.min()+xx.max())/2+x0)})
 foot=[sum(f['soleCenterX'] for f in feet)/2,max(f['bottom'] for f in feet)];delta=[(CANONICAL[j]-foot[j])*SCALE for j in range(2)];bounds=bbox(127);vbox=[ORIGIN[j%2]+delta[j%2]+bounds[j]*SCALE for j in range(4)];full=[ORIGIN[0]+delta[0],ORIGIN[1]+delta[1],ORIGIN[0]+delta[0]+common[0]*SCALE,ORIGIN[1]+delta[1]+common[1]*SCALE]
 q={'nativeSize':list(im.size),'nativeMode':im.mode,'bboxAlphaNonzero':bbox(0),'bboxAlphaGreaterThan1':bbox(1),'bboxAlphaGreaterThan127':bounds,'alphaZeroFraction':float((a==0).mean()),'alphaRange':[int(a.min()),int(a.max())],'perimeterMaxAlpha':int(perim.max()),'perimeterCountAlphaGreaterThan1':int((perim>1).sum()),'measuredBootSoleBands':feet,'measurementOnlyCaneExclusion':cfg.get('exclusions',[]),'sourceFoot':foot,'sha256':sha(d/'master.png'),'rawMatchesMaster':sha(d/'raw.png')==sha(d/'master.png'),'nativeAlphaUntouched':True,'bootMeasurement':'Separate visible boot sole alpha>127 bottom4-row bands. Cane rectangle excluded ONLY in measurement mask, never from source art. Mean underside centers supplies x; lowest sole supplies y. Whole canvas translation only.'};outside=np.array(Image.fromarray((a>127).astype('uint8')*255).filter(ImageFilter.MaxFilter(15)))==0;q['exteriorBeyond7PixelsFromOpaqueSilhouette']={'pixelCountAlphaGreaterThan1':int(((a>1)&outside).sum()),'maxAlpha':int(a[outside].max())};(d/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
 rows[p]={'nativeSize':list(im.size),'padRight':common[0]-im.width,'padBottom':common[1]-im.height,'measuredBootSoleBands':feet,'measurementOnlyCaneExclusion':cfg.get('exclusions',[]),'sourceSupportAnchor':foot,'registrationOffsetLogical':delta,'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],'proposedLogicalVisibleBounds':vbox,'proposedLogicalFullCanvasBounds':full,'wholeCanvasFitsProposedFrame':full[0]>=0 and full[1]>=0 and full[2]<=FRAME[0] and full[3]<=FRAME[1],'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterMaxAlpha']<=1}}
 for name,col in COLORS.items():
  bg=Image.new('RGBA',im.size,col+(255,));bg.alpha_composite(im);bg.convert('RGB').save(d/f'qa-{name}.png')
 annotated=Image.open(d/'qa-white.png').copy();dr=ImageDraw.Draw(annotated)
 for f in feet:
  box=f['bottom4RowsBounds'];dr.rectangle((box[0],box[1]-3,box[2],box[3]+3),outline=(255,0,128),width=3);dr.text((box[0],box[1]-22),f"bottom={f['bottom']} centerX={f['soleCenterX']}",fill=(255,0,128))
 for box in cfg.get('exclusions',[]):dr.rectangle(box,outline=(0,150,240),width=2)
 annotated.save(d/'qa-foot-measurements.png')
reg={'status':'Measured source proposal only; no runtime edits.','sharedScale':SCALE,'bodyReferenceSourcePixels':887,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Idle hair crown alpha>127 y46 to lowest supporting boot sole y933. One fixed44/887 across every whole source canvas. Cane tip extends 9 source pixels (0.45 logical pixels) below lowest boot, retained.','canonicalSourceFoot':CANONICAL,'commonSourceCanvas':common,'paddingPolicy':'Export only: transparent pad right/bottom to common canvas by at most 2 source pixels. Native raw/master untouched.','canvasPaddingToleranceRequired':max(max(common[0]-i.width,common[1]-i.height) for i in ims.values()),'proposedCommonFrameSize':FRAME,'proposedOrigin':ORIGIN,'proposedAnchor':[ORIGIN[0]+CANONICAL[0]*SCALE,ORIGIN[1]+CANONICAL[1]*SCALE],'registrationMethod':'Separate visible boot bottom4-row alpha>127 bands, excluding cane in measurement only. Average band centers x, lowest supporting boot sole y. Translate whole canvas only; no per-pose rescale/crop/fitting.','poses':rows,'requiredMaximumTranslationLogicalPixels':max(abs(x) for r in rows.values() for x in r['registrationOffsetLogical']),'recommendedRegistrationLimitLogicalPixels':4};(B/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
def render(im,scale,tx,ty):return im.transform((FRAME[0]*4,FRAME[1]*4),Image.Transform.AFFINE,(1/(scale*4),0,-tx/scale,0,1/(scale*4),-ty/scale),Image.Resampling.BICUBIC).resize(tuple(FRAME),Image.Resampling.LANCZOS)
tiles={}
for name,col in COLORS.items():
 strip=Image.new('RGB',(FRAME[0]*len(POSES),FRAME[1]),col)
 for k,p in enumerate(POSES):
  src=Image.new('RGBA',tuple(common));src.alpha_composite(ims[p]);dx,dy=rows[p]['registrationOffsetLogical'];tile=Image.new('RGBA',tuple(FRAME),col+(255,));tile.alpha_composite(render(src,SCALE,ORIGIN[0]+dx,ORIGIN[1]+dy));tiles[name,p]=tile.convert('RGB');strip.paste(tiles[name,p],(k*FRAME[0],0))
 strip.save(B/f'qa-native44-{name}.png')
board=Image.new('RGB',(FRAME[0]*len(POSES),FRAME[1]*3))
for row,name in enumerate(COLORS):
 for k,p in enumerate(POSES):board.paste(tiles[name,p],(k*FRAME[0],row*FRAME[1]))
board.save(B/'qa-native44-three-backgrounds.png');board.resize((board.width*3,board.height*3),Image.Resampling.NEAREST).save(B/'qa-native44-three-backgrounds-3x.png')
if len(POSES)==4:
 for file,seq,durations in [('qa-sequence.gif',['idle','walk-a','idle','walk-b','carry-or-work','idle'],[600,300,200,300,700,300]),('qa-walking-loop.gif',['walk-a','idle','walk-b','idle'],[300,100,300,100])]:
  frames=[tiles['scene',p].resize((FRAME[0]*4,FRAME[1]*4),Image.Resampling.NEAREST) for p in seq];frames[0].save(B/file,save_all=True,append_images=frames[1:],duration=durations,loop=0)
 for name,col in COLORS.items():
  board=Image.new('RGB',(round(common[0]*.5)*2,round(common[1]*.5)*2),col)
  for k,p in enumerate(POSES):
   src=Image.new('RGBA',tuple(common),col+(255,));src.alpha_composite(ims[p]);tile=src.convert('RGB').resize((round(common[0]*.5),round(common[1]*.5)),Image.Resampling.LANCZOS);board.paste(tile,((k%2)*tile.width,(k//2)*tile.height))
  board.save(B/f'qa-native-montage-{name}.png')
print(json.dumps(reg,indent=2))
