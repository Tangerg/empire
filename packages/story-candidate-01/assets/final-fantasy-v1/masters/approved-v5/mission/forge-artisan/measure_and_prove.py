from pathlib import Path
from PIL import Image,ImageDraw
import json,numpy as np
B=Path(__file__).parent;POSES=['idle','walk-a','carry-or-work','walk-b'];SCALE=44/930;CANONICAL=[805.25,968];ORIGIN=[8,4];FRAME=[104,64];COLORS={'black':(16,16,16),'white':(255,255,255),'scene':(114,128,86)}
ims={p:Image.open(B/p/'master.png') for p in POSES};common=[max(i.width for i in ims.values()),max(i.height for i in ims.values())];rows={}
for p,im in ims.items():
 d=B/p;q=json.loads((d/'alpha-qa.json').read_text());a=np.array(im)[:,:,3];feet=[]
 for x0,x1 in [(400,770),(770,1200)]:
  yy,xx=np.where(a[850:,x0:x1]>127);bot=int(yy.max()+850);yy,xx=np.where(a[bot-3:bot+1,x0:x1]>127);feet.append({'xRange':[x0,x1],'bottom':bot,'bottom4RowsBounds':[int(xx.min()+x0),bot-3,int(xx.max()+x0+1),bot+1],'soleCenterX':float((xx.min()+xx.max())/2+x0)})
 foot=[sum(f['soleCenterX'] for f in feet)/2,max(f['bottom'] for f in feet)];delta=[(CANONICAL[j]-foot[j])*SCALE for j in range(2)];bounds=q['bboxAlphaGreaterThan127'];vbox=[ORIGIN[j%2]+delta[j%2]+bounds[j]*SCALE for j in range(4)];full=[ORIGIN[0]+delta[0],ORIGIN[1]+delta[1],ORIGIN[0]+delta[0]+common[0]*SCALE,ORIGIN[1]+delta[1]+common[1]*SCALE]
 q.update({'measuredBootSoleBands':feet,'sourceFoot':foot,'bootMeasurement':'Separate boots, alpha>127 bottom4rows; x averages underside band centers, y uses lowest weight-bearing support sole, all transforms translate the entire canvas only.'});(d/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
 rows[p]={'nativeSize':list(im.size),'padRight':common[0]-im.width,'padBottom':common[1]-im.height,'measuredBootSoleBands':feet,'sourceSupportAnchor':foot,'registrationOffsetLogical':delta,'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaNonzero'],'proposedLogicalVisibleBounds':vbox,'proposedLogicalFullCanvasBounds':full,'wholeCanvasFitsProposedFrame':full[0]>=0 and full[1]>=0 and full[2]<=FRAME[0] and full[3]<=FRAME[1],'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':q['perimeterMaxAlpha']<=1}}
 annotated=Image.open(d/'qa-white.png').copy();dr=ImageDraw.Draw(annotated)
 for f in feet:
  box=f['bottom4RowsBounds'];dr.rectangle((box[0],box[1]-3,box[2],box[3]+3),outline=(255,0,128),width=3);dr.text((box[0],box[1]-22),f"bottom={f['bottom']} centerX={f['soleCenterX']}",fill=(255,0,128))
 annotated.save(d/'qa-foot-measurements.png')
reg={'status':'Native quartet complete; measured whole-canvas export proposal only. No runtime edits.','sharedScale':SCALE,'bodyReferenceSourcePixels':930,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Accepted idle bun crown alpha>127 y38 to lowest supporting boot sole y968. One fixed44/930 across every whole source canvas.','canonicalSourceFoot':CANONICAL,'commonSourceCanvas':common,'paddingPolicy':'Export only: pad transparent right/bottom to1586x993. Native raw/master images are never altered.','canvasPaddingToleranceRequired':2,'proposedCommonFrameSize':FRAME,'proposedOrigin':ORIGIN,'registrationMethod':'Separate boot bottom4-row alpha>127 bands. Anchor x averages underside band centers; anchor y is lowest weight-bearing supporting sole. Translate whole canvas only; no per-pose rescale/crop/fitting.','poses':rows,'requiredMaximumTranslationLogicalPixels':max(abs(x) for r in rows.values() for x in r['registrationOffsetLogical']),'recommendedRegistrationLimitLogicalPixels':4}
(B/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
# These are review exports only, using exactly one full-canvas transform per pose.
tiles={}
for name,col in COLORS.items():
 strip=Image.new('RGB',(FRAME[0]*4,FRAME[1]),col)
 for k,p in enumerate(POSES):
  im=ims[p];r=rows[p];src=Image.new('RGBA',tuple(common),(0,0,0,0));src.alpha_composite(im,(0,0));dx,dy=r['registrationOffsetLogical'];tx=ORIGIN[0]+dx;ty=ORIGIN[1]+dy
  registered=src.transform((FRAME[0]*4,FRAME[1]*4),Image.Transform.AFFINE,(1/(SCALE*4),0,-tx/SCALE,0,1/(SCALE*4),-ty/SCALE),Image.Resampling.BICUBIC).resize(tuple(FRAME),Image.Resampling.LANCZOS)
  tile=Image.new('RGBA',tuple(FRAME),col+(255,));tile.alpha_composite(registered);tiles[name,p]=tile.convert('RGB');strip.paste(tiles[name,p],(k*FRAME[0],0))
 strip.save(B/f'qa-native44-{name}.png')
board=Image.new('RGB',(FRAME[0]*4,FRAME[1]*3),(255,255,255))
for row,name in enumerate(COLORS):
 for k,p in enumerate(POSES):board.paste(tiles[name,p],(k*FRAME[0],row*FRAME[1]))
board.save(B/'qa-native44-three-backgrounds.png');board.resize((FRAME[0]*4*3,FRAME[1]*3*3),Image.Resampling.NEAREST).save(B/'qa-native44-three-backgrounds-3x.png')
frames=[tiles['scene',p].resize((FRAME[0]*4,FRAME[1]*4),Image.Resampling.NEAREST) for p in ['idle','walk-a','idle','walk-b','carry-or-work','idle']];frames[0].save(B/'qa-sequence.gif',save_all=True,append_images=frames[1:],duration=[600,260,200,260,700,300],loop=0)
# Medium-size native composites retain the full native canvas at a uniform40% scale.
for name,col in COLORS.items():
 board=Image.new('RGB',(round(common[0]*.4)*2,round(common[1]*.4)*2),col)
 for k,p in enumerate(POSES):
  src=Image.new('RGBA',tuple(common),col+(255,));src.alpha_composite(ims[p]);tile=src.convert('RGB').resize((round(common[0]*.4),round(common[1]*.4)),Image.Resampling.LANCZOS);board.paste(tile,((k%2)*tile.width,(k//2)*tile.height))
 board.save(B/f'qa-native-montage-{name}.png')
print(json.dumps(reg,indent=2))
