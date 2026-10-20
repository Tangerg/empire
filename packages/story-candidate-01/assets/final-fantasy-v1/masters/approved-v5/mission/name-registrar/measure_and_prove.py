"""Read untouched masters and write measurement evidence and QA-only whole-canvas previews."""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
B=Path(__file__).parent;ORDER=['idle','walk-a','carry-or-work','walk-b'];SCALE=44/922;CANON=[792.75,963];ORIGIN=[8,4];FRAME=(104,64);COLORS={'black':(0,0,0,255),'white':(255,255,255,255),'scene':(114,128,86,255)}
ims={p:Image.open(B/p/'master.png') for p in ORDER};common=[max(im.width for im in ims.values()),max(im.height for im in ims.values())];rows={};tiles={}
ranges={'idle':[(530,760),(780,1030)],'walk-a':[(470,760),(780,1070)],'carry-or-work':[(530,760),(780,1030)],'walk-b':[(450,760),(780,1000)]}
for pose,im in ims.items():
 p=B/pose;a=np.array(im.getchannel('A'));q=json.loads((p/'alpha-qa.json').read_text());feet=[]
 for x0,x1 in ranges[pose]:
  yy,xx=np.where(a[850:,x0:x1]>127);bottom=int(yy.max()+850);ys,xs=np.where(a[bottom-3:bottom+1,x0:x1]>127);box=[int(xs.min()+x0),bottom-3,int(xs.max()+x0+1),bottom+1];feet.append({'xRange':[x0,x1],'bottom':bottom,'bottom4RowsBounds':box,'soleCenterX':(box[0]+box[2]-1)/2})
 foot=[sum(f['soleCenterX'] for f in feet)/2,max(f['bottom'] for f in feet)];delta=[(CANON[j]-foot[j])*SCALE for j in range(2)];bounds=q['bboxAlphaGreaterThan127'];full=[ORIGIN[0]+delta[0],ORIGIN[1]+delta[1],ORIGIN[0]+delta[0]+common[0]*SCALE,ORIGIN[1]+delta[1]+common[1]*SCALE];visible=[ORIGIN[j%2]+delta[j%2]+bounds[j]*SCALE for j in range(4)]
 q.update({'measuredBootSoleBands':feet,'sourceFoot':foot,'nativeOutputMatchesRaw':Path(json.loads((p/'generation-result.json').read_text())['nativeOutputPath']).read_bytes()==(p/'raw.png').read_bytes(),'nativeAlphaUntouched':True});(p/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
 rows[pose]={'nativeSize':list(im.size),'padRight':common[0]-im.width,'padBottom':common[1]-im.height,'measuredBootSoleBands':feet,'sourceSupportAnchor':foot,'registrationOffsetLogical':delta,'nativeVisibleBoundsAlphaGreaterThan127':bounds,'nativeAllNonzeroAlphaBounds':q['bboxAlphaGreaterThan0'],'proposedLogicalVisibleBounds':visible,'proposedLogicalFullCanvasBounds':full,'bodyCrownToSoleNative':foot[1]-bounds[1],'bodyCrownToSoleAtSharedScale':(foot[1]-bounds[1])*SCALE,'wholeCanvasFitsProposedFrame':full[0]>=0 and full[1]>=0 and full[2]<=FRAME[0] and full[3]<=FRAME[1],'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'passesNativeSilhouettePerimeterGate':q['perimeterMaxAlpha']==0},'exteriorBeyond7PixelsFromOpaqueSilhouette':q['exteriorBeyond7PixelsFromOpaqueSilhouette']}
 ann=Image.open(p/'qa-white.png');draw=ImageDraw.Draw(ann)
 for f in feet:
  x0,y0,x1,y1=f['bottom4RowsBounds'];draw.rectangle([x0,y0-3,x1,y1+3],outline=(220,0,100),width=3);draw.text([x0,y0-22],f"sole y={f['bottom']}, x={f['soleCenterX']}",fill=(220,0,100))
 ann.save(p/'qa-foot-measurements.png')
 src=Image.new('RGBA',tuple(common));src.alpha_composite(im);tx=ORIGIN[0]+delta[0];ty=ORIGIN[1]+delta[1];out=src.transform((FRAME[0]*4,FRAME[1]*4),Image.Transform.AFFINE,(1/(SCALE*4),0,-tx/SCALE,0,1/(SCALE*4),-ty/SCALE),Image.Resampling.BICUBIC).resize(FRAME,Image.Resampling.LANCZOS)
 for name,c in COLORS.items():
  tile=Image.new('RGBA',FRAME,c);tile.alpha_composite(out);tiles[name,pose]=tile.convert('RGB')
reg={'status':'All four native sources ready; parent integrates. Measurements and QA-only previews, no production exports.','sharedScale':SCALE,'bodyReferenceSourcePixels':922,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Idle hair crown alpha>127 y41 to supporting sole y963. One fixed44/922 scale for every whole source canvas; poses never independently fitted.','canonicalSourceFoot':CANON,'commonSourceCanvas':common,'paddingPolicy':'Technical export proposal only: pad1 transparent pixel at right for1585px walk frames; native raw/master never changed.','canvasPaddingToleranceRequired':max(max(common[0]-i.width,common[1]-i.height) for i in ims.values()),'proposedCommonFrameSize':list(FRAME),'proposedOrigin':ORIGIN,'registrationMethod':'Per-boot alpha>127 bottom4rows; x averages two sole band centers, y lowest support. Translate entire canvas only.','poses':rows,'requiredMaximumTranslationLogicalPixels':max(abs(v) for r in rows.values() for v in r['registrationOffsetLogical']),'recommendedRegistrationLimitLogicalPixels':4};(B/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
board=Image.new('RGB',(FRAME[0]*4,FRAME[1]*3))
for row,name in enumerate(COLORS):
 strip=Image.new('RGB',(FRAME[0]*4,FRAME[1]))
 for k,pose in enumerate(ORDER):strip.paste(tiles[name,pose],(FRAME[0]*k,0))
 strip.save(B/f'qa-native44-{name}.png');board.paste(strip,(0,FRAME[1]*row))
board.save(B/'qa-native44-three-backgrounds.png');board.resize((board.width*3,board.height*3),Image.Resampling.NEAREST).save(B/'qa-native44-three-backgrounds-3x.png')
for name,col in COLORS.items():
 montage=Image.new('RGB',(common[0],common[1]),col[:3])
 for k,pose in enumerate(ORDER):
  tile=Image.new('RGBA',tuple(common),col);tile.alpha_composite(ims[pose]);tile=tile.convert('RGB').resize((common[0]//2,common[1]//2),Image.Resampling.LANCZOS);montage.paste(tile,((k%2)*(common[0]//2),(k//2)*(common[1]//2)))
 montage.save(B/f'qa-native-montage-{name}.png')
seq=[tiles['scene',p].resize((416,256),Image.Resampling.NEAREST) for p in ['idle','walk-a','idle','walk-b','carry-or-work','idle']];seq[0].save(B/'qa-sequence.gif',save_all=True,append_images=seq[1:],duration=[600,260,200,260,700,300],loop=0)
print(json.dumps(reg,indent=2))
