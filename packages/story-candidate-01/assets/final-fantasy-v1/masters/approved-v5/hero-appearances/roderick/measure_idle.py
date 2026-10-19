from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im)[:,:,3]
q=json.loads((d/'alpha-qa.json').read_text());rois={'near-hind':[400,870,550,990],'far-hind':[620,860,780,970],'near-fore':[780,880,940,995],'far-fore':[990,870,1150,990]};feet={}
for name,(x0,y0,x1,y1) in rois.items():
 yy,xx=np.where(a[y0:y1,x0:x1]>127);yy+=y0;xx+=x0;sole=int(yy.max());feet[name]={'roi':[x0,y0,x1,y1],'soleY':sole,'bottomFourRowsMeanX':float(xx[yy>sole-4].mean())}
yy,xx=np.where(a[0:230,590:815]>127);crown=int(yy.min());nearmean=(feet['near-hind']['soleY']+feet['near-fore']['soleY'])/2;h=nearmean-crown;s=56/h;foot=[round(np.mean([f['bottomFourRowsMeanX'] for f in feet.values()])),max(f['soleY'] for f in feet.values())]
probes=[]
for name,cx,cy in [('hind-leg-gap',582,900),('middle-leg-gap',772,890),('fore-leg-gap',962,900),('lance-to-horse-gap',1110,317)]:
 vals=[]
 for y in range(cy-20,cy+21):
  for x in range(cx-20,cx+21):
   v=a[y-3:y+4,x-3:x+4]
   if v.shape==(7,7) and not v.any():vals.append(((x-cx)**2+(y-cy)**2,x,y))
 assert vals;_,x,y=min(vals);probes.append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':49,'maxAlpha':0})
perimeter={}
for tag,row in [('top',a[0]),('bottom',a[-1]),('left',a[:,0]),('right',a[:,-1])]:
 perimeter[tag]=[{'coordinateAlongEdge':int(i),'alpha':int(row[i])} for i in np.where(row>1)[0]]
measure={'hairROI':[590,0,815,230],'hairCrownY':crown,'hoofMeasurements':feet,'meanNearSideHoofSoleY':nearmean,'combinedHeightSourcePixels':h,'combinedTargetLogicalPixels':56,'proposedSharedScale':s,'scaleFraction':f'56/{h:g}','proposedSourceFoot':foot,'sourceFootMeanXBeforeRounding':float(np.mean([f['bottomFourRowsMeanX'] for f in feet.values()])),'anchorMethod':'x average four hoof bottom-band centers, y lowest hoof; rider boots/stirrups excluded. Height is hair crown to mean near-side hind/fore hoof soles.','nativeCanvas':list(im.size),'visibleWidthSourcePixels':q['bboxAlphaGreaterThan127'][2]-q['bboxAlphaGreaterThan127'][0],'visibleWidthLogical':(q['bboxAlphaGreaterThan127'][2]-q['bboxAlphaGreaterThan127'][0])*s,'candidateFrame':[128,80],'candidateOrigin':[14,6],'nativeLogicalFullCanvasBounds':[14,6,14+im.width*s,6+im.height*s],'proposedExplicitCanvasPadding':{'commonCanvasCandidate':list(im.size),'rightColumns':0,'bottomRows':0,'status':'None needed for raw acceptance; native perimeter gate passes. Final animation alignment canvas awaits all pose sizes.'}}
q.update({'bodyMeasurement':measure,'suggestedSourceFoot':foot,'negativeSpaceProbes':probes,'perimeterPixelsAlphaGreaterThan1':perimeter,'strictNativePerimeterGate':'PASS: native edge has no alpha>1 pixel; no padding or alpha cleanup applied. Bottom edge alpha is exactly0.','lowestVisibleHoofY':foot[1],'fullyTransparentBottomRows':int(im.height-1-np.where(a.any(axis=1))[0].max()),'selectedAttempt':'attempt-03','revisionReview':'Coordinator-directed larger older head/beard and simplified broad matte armor achieved. Left hand still holds reins below shaft, right hand retains lance. Native redraw returns1560x1008 versus prior1584x993; horse silhouette/lance span shift modestly, not pixel-identical. No programmatic scale/alpha edits. Final idle scale is not locked until coordinator review.','nativeHeadEstimate':{'method':'Manually inspected approximate hair/beard outline, excluding scarf; not an alpha component boundary','attempt02Bounds':[615,14,803,211],'attempt03Bounds':[587,22,813,247],'approximateWidthRatio':226/188,'approximateHeightRatio':225/197},'semanticReview':'Older exposed gray hair/full beard and original face retained, head/beard enlarged approximately15–20percent; breastplate/pauldrons/gauntlets now broad matte blocks without mottled chip texture. Round shield, correct right-lance and left-reins hand assignment, sheathed sword, brown four-hoof horse with black mane/tail and pale blaze stay coherent. Full lance tips/tail/hooves and transparent leg openings present. No external FX.','status':'Idle attempt03 candidate; raw perimeter gate passes; awaiting coordinator native proportion review before remaining poses'})
assert q['perimeterCountAlphaGreaterThan1']==0
for p in [d/'alpha-qa.json',b/'idle-review.json']:p.write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
req=json.loads((d/'request.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();assert sha(d/'master.png')==sha(d/'raw.png')==sha(q['nativeSource'])==q['sha256'];assert sha(d/'prompt.txt')==req['promptSha256']
for p,hsh in req['referencedImageSha256'].items():assert sha(p)==hsh
source=json.loads((b/'source-evidence.json').read_text());source['idleNativeReview']={'selectedAttempt':'attempt-03','nativeSha256':q['sha256'],'nativeSize':list(im.size),'scaleProposal':f'56/{h:g}','sourceFoot':foot,'visibleWidthLogical':measure['visibleWidthLogical'],'perimeterCaveat':q['strictNativePerimeterGate'],'status':q['status']};(b/'source-evidence.json').write_text(json.dumps(source,ensure_ascii=False,indent=2)+'\n')
entries=[('Roderick pilot56',im,s,foot),('Approved wolf rider52',Image.open('/workspace/shared/empire-art-rebuild/approved-v5/animation/wolf-rider/idle/master.png').convert('RGBA'),52/905.5,[828,970]),('Approved swordsman44',Image.open('/workspace/shared/empire-art-rebuild/approved-v5/swordsman/master.png').convert('RGBA'),1/18,[800,887])]
review=Image.new('RGB',(768,360),(218,218,218));draw=ImageDraw.Draw(review);bounds={}
for row,bgcol in enumerate(['white','black']):
 for col,(label,img,scale,anchor) in enumerate(entries):
  tx=64-anchor[0]*scale;ty=68-anchor[1]*scale;full=[tx,ty,tx+img.width*scale,ty+img.height*scale];assert full[0]>=0 and full[1]>=0 and full[2]<=128 and full[3]<=80;bounds[label]={'scale':scale,'sourceFoot':anchor,'fullCanvasBounds':full}
  preview=img.transform((128,80),Image.Transform.AFFINE,(1/scale,0,-tx/scale,0,1/scale,-ty/scale),resample=Image.Resampling.BICUBIC,fillcolor=(0,0,0,0));bg=Image.new('RGBA',(128,80),bgcol);bg.alpha_composite(preview);review.paste(bg.convert('RGB').resize((256,160),Image.Resampling.NEAREST),(col*256,row*180+20));draw.text((col*256+5,row*180+5),label+' / '+bgcol,fill=(0,0,0))
review.save(d/'native-scale-comparison.png');(d/'native-scale-comparison.json').write_text(json.dumps({'method':'Read-only diagnostic: full-canvas uniform affine scale and whole-canvas translation to sharedground68. No pixel cleanup, no trim, no per-body fit. Two-times nearest-neighbor display; source masters unchanged.','entries':bounds},indent=2)+'\n')
print(json.dumps({'measurement':measure,'strictNativePerimeterGate':q['strictNativePerimeterGate'],'integrity':'PASS','comparison':str(d/'native-scale-comparison.png')},indent=2))
