from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im.getchannel('A'));soles=[]
for name,roi in [('image-left-boot',[450,850,650,982]),('image-right-boot',[800,850,1060,982])]:
 x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);bottom=int(ys.max()+y0);center=float((xs[ys>=ys.max()-3]+x0).mean());soles.append({'name':name,'roi':roi,'soleCenterXBottomFourRows':center,'soleY':bottom})
hoodTop=47;height=sum(s['soleY'] for s in soles)/2-hoodTop;scale=44/height;foot=[round(sum(s['soleCenterXBottomFourRows'] for s in soles)/2),max(s['soleY'] for s in soles)];origin=[8,4];frame=[104,64]
probes=[]
for name,rect in [('lantern-handle-open-center',[1139,520,1146,527]),('between-boots',[720,930,727,937]),('between-lantern-and-body',[1015,660,1022,667])]:
 x0,y0,x1,y1=rect;q=a[y0:y1,x0:x1];probes.append({'name':name,'rectangle':rect,'maxAlpha':int(q.max()),'zeroPixels':int((q==0).sum())})
qa=json.load(open(d/'alpha-qa.json'));qa.update({'status':'Native inspected; awaiting coordinator identity and shared-scale idle review','bodyTopY':hoodTop,'soleMeasurements':soles,'bodyHeight':height,'proposedLogicalBodyHeight':44,'proposedScale':scale,'proposedSourceFoot':foot,'proposedFrame':frame,'proposedOrigin':origin,'negativeSpaceProbes':probes,'semanticReview':'Middle-aged female face and cream hood, dark hair, charcoal robe/cream front, original hem geometry, chest/belt name tags, right-arm ledger/image-left and left-hand hanging lantern/image-right intact. Two large gloved hands, two boots. No staff or shovel. Lantern light remains bounded to glass body; no external FX. Broad proportions and clean matte cloth masses match approved family; mature cheek lines remain readable.','nativeCanvasDiscrepancy':'1585×992 vs requested1586×992; retain unchanged raw. If future frames differ, explicit transparent shared-canvas alignment only after raw gate.','rawEqualsMaster':(d/'raw.png').read_bytes()==(d/'master.png').read_bytes(),'visibleBoundsAlphaGreaterThan127':[417,47,1225,968],'bodyMeasurementDefinition':'Hood crown y47 to mean sole y966 =919; common foot horizontal mean of two sole center samples rounded758, lowest sole967.'})
(d/'alpha-qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n');(b/'idle-review.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n')
def trans(img,s,tx,ty):return img.transform(tuple(frame),Image.Transform.AFFINE,(1/s,0,-tx/s,0,1/s,-ty/s),resample=Image.Resampling.BICUBIC)
# Fixed proposed scale, entire source canvas. This is diagnostic only, not a fit or source edit.
out=trans(im,scale,*origin);out.save(d/'qa-native-frame-rgba.png');pan=Image.new('RGB',(frame[0]*2,frame[1]*2*3+24*3),'#999999');draw=ImageDraw.Draw(pan)
sw=Image.open('/workspace/shared/empire-art-rebuild/approved-v5/swordsman/master.png').convert('RGBA');swordScale=1/18;target=[52,54];cmp=Image.new('RGB',(104*2*2,(64*2+24)*3),'#999999');cd=ImageDraw.Draw(cmp)
for row,color in enumerate(['white','#151c25','#7c8c76']):
 bg=Image.new('RGBA',tuple(frame),color);bg.alpha_composite(out);p=bg.convert('RGB').resize((208,128),Image.Resampling.NEAREST);pan.paste(p,(0,row*152+24));draw.text((5,row*152+5),f'Mirelle 44px | {color}',fill='black')
 for col,(label,img,s,ft) in enumerate([('Mirelle44',im,scale,foot),('Approved swordsman44',sw,swordScale,[800,887])]):
  q=trans(img,s,target[0]-ft[0]*s,target[1]-ft[1]*s);qbg=Image.new('RGBA',tuple(frame),color);qbg.alpha_composite(q);cmp.paste(qbg.convert('RGB').resize((208,128),Image.Resampling.NEAREST),(col*208,row*152+24));cd.text((col*208+5,row*152+5),label,fill='black')
pan.save(d/'qa-native-2x-three-backgrounds.png');cmp.save(d/'native-scale-comparison.png')
e=json.load(open(b/'source-evidence.json'));e['status']='First native idle inspected and measured; awaiting coordinator review before pose expansion';e['idleCandidate']={'raw':str(d/'raw.png'),'master':str(d/'master.png'),'sha256':qa['sha256'],'nativeSize':list(im.size),'proposedScaleRatio':'44/919','sourceFoot':foot,'review':str(b/'idle-review.json'),'comparison':str(d/'native-scale-comparison.png')};(b/'source-evidence.json').write_text(json.dumps(e,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'scaleRatio':'44/919','scale':scale,'sourceFoot':foot,'soles':soles,'strictNativePerimeter':qa['perimeterCountAlphaGreaterThan1'],'probes':probes},indent=2))
