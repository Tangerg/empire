from pathlib import Path
from PIL import Image, ImageDraw
import hashlib, json, numpy as np
from scipy.ndimage import distance_transform_edt
import subprocess

B = Path(__file__).parent
POSES = [p for p in ['idle', 'walk-a', 'carry-or-work', 'walk-b'] if (B/p/'master.png').exists()]
SCALE = 44 / 923
CANONICAL = [807.75, 963]
ORIGIN = [8, 4]
FRAME = [104, 64]
SOLE_RANGES = {'idle': [(560,790),(790,1050)], 'walk-a': [(450,780),(800,1150)], 'carry-or-work': [(500,790),(790,1100)], 'walk-b': [(450,780),(800,1150)]}
COLORS = {'black': (0,0,0), 'white': (255,255,255), 'scene': (109,126,93)}
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def bounds(a,t):
    y,x=np.where(a>t)
    return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)] if len(x) else None
images = {p:Image.open(B/p/'master.png') for p in POSES}
common = [max(im.width for im in images.values()),max(im.height for im in images.values())]
rows={}
for pose,im in images.items():
    d=B/pose; a=np.array(im)[:,:,3]
    perimeter=np.concatenate([a[0,:],a[-1,:],a[:,0],a[:,-1]])
    feet=[]
    for x0,x1 in SOLE_RANGES[pose]:
        yy,xx=np.where(a[800:,x0:x1]>127); bottom=int(yy.max()+800)
        yy,xx=np.where(a[bottom-3:bottom+1,x0:x1]>127)
        feet.append({'xRange':[x0,x1],'bottom':bottom,'bottom4RowsBounds':[int(xx.min()+x0),bottom-3,int(xx.max()+x0+1),bottom+1],'soleCenterX':float((xx.min()+xx.max())/2+x0)})
    foot=[sum(f['soleCenterX'] for f in feet)/2,max(f['bottom'] for f in feet)]
    delta=[(CANONICAL[i]-foot[i])*SCALE for i in range(2)]
    distance=distance_transform_edt(a<128)
    qa={'nativeSize':list(im.size),'mode':im.mode,'rawSha256':sha(d/'raw.png'),'masterSha256':sha(d/'master.png'),'nativeSha256':sha(d/'native.png'),'rawMasterIdentical':sha(d/'raw.png')==sha(d/'master.png')==sha(d/'native.png'),'decodedPixelsSha256':hashlib.sha256(im.tobytes()).hexdigest(),'bboxAlphaNonzero':bounds(a,0),'bboxAlphaGreaterThan1':bounds(a,1),'bboxAlphaGreaterThan127':bounds(a,127),'perimeterMaxAlpha':int(perimeter.max()),'perimeterCountAlphaGreaterThan1':int((perimeter>1).sum()),'alphaCountsAboveThreshold':{str(t):int((a>t).sum()) for t in [0,1,16,64,127,240,250,254]},'maxAlphaMoreThan3pxFromOpaqueBody':int(a[distance>3].max()),'measuredBootSoleBands':feet,'sourceFoot':foot,'sourcePolicy':'Native bytes unchanged. No cleanup, crop, alpha editing, recoloring, source padding or per-pose scaling. Separate whole-canvas technical exports and QA composites only.'}
    (d/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
    request=json.loads((d/'request.json').read_text())
    provenance={'tool':'built-in image_gen.imagegen','promptFileSha256':sha(d/'prompt.txt'),'requestFileSha256':sha(d/'request.json'),'references':[{'path':p,'sha256':sha(Path(p))} for p in request['referenced_image_paths']],'rawSha256':qa['rawSha256'],'masterSha256':qa['masterSha256'],'nativeSha256':qa['nativeSha256'],'allNativeRawMasterIdentical':qa['rawMasterIdentical']}
    (d/'request-provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
    visible=qa['bboxAlphaGreaterThan127']; full=[ORIGIN[0]+delta[0],ORIGIN[1]+delta[1],ORIGIN[0]+delta[0]+common[0]*SCALE,ORIGIN[1]+delta[1]+common[1]*SCALE]
    rows[pose]={'nativeSize':list(im.size),'padRight':common[0]-im.width,'padBottom':common[1]-im.height,'measuredBootSoleBands':feet,'sourceSupportAnchor':foot,'registrationOffsetLogical':delta,'nativeVisibleBoundsAlphaGreaterThan127':visible,'nativeAllNonzeroAlphaBounds':qa['bboxAlphaNonzero'],'proposedLogicalVisibleBounds':[ORIGIN[j%2]+delta[j%2]+visible[j]*SCALE for j in range(4)],'proposedLogicalFullCanvasBounds':full,'wholeCanvasFitsProposedFrame':full[0]>=0 and full[1]>=0 and full[2]<=FRAME[0] and full[3]<=FRAME[1],'perimeterGate':{'maxNativePerimeterAlpha':qa['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':qa['perimeterCountAlphaGreaterThan1'],'passesNativeSilhouettePerimeterGate':qa['perimeterMaxAlpha']<=1}}
    for name,color in COLORS.items():
        canvas=Image.new('RGBA',im.size,color+(255,));canvas.alpha_composite(im);canvas.convert('RGB').save(d/f'qa-{name}.png')
    annotated=Image.open(d/'qa-white.png');draw=ImageDraw.Draw(annotated)
    for f in feet:
        box=f['bottom4RowsBounds'];draw.rectangle((box[0],box[1]-3,box[2],box[3]+3),outline=(255,0,128),width=3);draw.text((box[0],box[1]-22),f"bottom={f['bottom']} centerX={f['soleCenterX']}",fill=(255,0,128))
    annotated.save(d/'qa-foot-measurements.png')
registration={'status':'Measured native source proposal; coordinator scale/registration gate required before formal integration. No repository writes.','sharedScale':SCALE,'bodyReferenceSourcePixels':923,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Idle helmet crown alpha>127 y40 to supporting sole y963 =923 pixels. One fixed44/923 scale for every whole source canvas.','canonicalSourceFoot':CANONICAL,'commonSourceCanvas':common,'paddingPolicy':'Technical transparent right/bottom padding at export only; native bytes unchanged.','canvasPaddingToleranceRequired':max(max(r['padRight'],r['padBottom']) for r in rows.values()),'proposedCommonFrameSize':FRAME,'proposedOrigin':ORIGIN,'proposedAnchor':[ORIGIN[i]+CANONICAL[i]*SCALE for i in range(2)],'registrationMethod':'Independent alpha>127 bottom4-row sole bands; x average of two sole centers, y lowest sole. Whole-canvas translation only; no per-pose fitting.','poses':rows,'requiredMaximumTranslationLogicalPixels':max(abs(x) for r in rows.values() for x in r['registrationOffsetLogical'])}
(B/'registration-evidence.json').write_text(json.dumps(registration,indent=2)+'\n')
if len(POSES)==1:(B/'idle-registration-proposal.json').write_text(json.dumps(registration,indent=2)+'\n')
export=B/'qa-export';export.mkdir(exist_ok=True)
for pose in POSES:
    delta=rows[pose]['registrationOffsetLogical'];im=images[pose]
    svg=f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="104" height="64" viewBox="0 0 104 64"><image x="{ORIGIN[0]+delta[0]}" y="{ORIGIN[1]+delta[1]}" width="{im.width*SCALE}" height="{im.height*SCALE}" xlink:href="{(B/pose/"master.png").as_uri()}"/></svg>'
    (export/f'{pose}@1x.svg').write_text(svg)
    for density in [1,2]:subprocess.run(['inkscape',str(export/f'{pose}@1x.svg'),'--export-type=png',f'--export-filename={export/f"{pose}@{density}x.png"}',f'--export-width={104*density}',f'--export-height={64*density}'],check=True,capture_output=True)
panel=Image.new('RGB',(104*len(POSES),64*3))
for row,(name,color) in enumerate(COLORS.items()):
    montage=Image.new('RGB',(104*len(POSES),64),color)
    for col,p in enumerate(POSES):
        im=Image.open(export/f'{p}@1x.png');montage.paste(im,(col*104,0),im)
    montage.save(B/f'qa-native-montage-{name}.png');panel.paste(montage,(0,row*64))
panel.save(B/'qa-native44-three-backgrounds.png');panel.resize((panel.width*3,panel.height*3),Image.Resampling.NEAREST).save(B/'qa-native44-three-backgrounds-3x.png')
if len(POSES)==1:
    panel.save(B/'qa-idle-native44-three-backgrounds.png');panel.resize((panel.width*3,panel.height*3),Image.Resampling.NEAREST).save(B/'qa-idle-native44-three-backgrounds-3x.png')
if len(POSES)==4:
    for density in [1,2]:
        sheet=Image.new('RGBA',(104*4*density,64*density))
        for col,p in enumerate(POSES):sheet.alpha_composite(Image.open(export/f'{p}@{density}x.png'),(104*col*density,0))
        sheet.save(export/f'sheet@{density}x.png')
    sequence=['idle','walk-a','idle','walk-b'];frames=[]
    for p in sequence:
        canvas=Image.new('RGBA',FRAME,COLORS['scene']+(255,));canvas.alpha_composite(Image.open(export/f'{p}@1x.png'));frames.append(canvas.convert('RGB'))
    frames[0].save(B/'qa-walk-6.25fps.gif',save_all=True,append_images=frames[1:],duration=160,loop=0,disposal=2)
    (export/'sequence.json').write_text(json.dumps({'poses':POSES,'walkSequence':sequence,'durationMs':160,'diagnosticOnly':True},indent=2)+'\n')
print(json.dumps(registration,indent=2))
