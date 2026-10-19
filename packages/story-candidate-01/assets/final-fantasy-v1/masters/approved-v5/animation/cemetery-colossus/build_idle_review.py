from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,numpy as np
b=Path(__file__).parent;q=json.loads((b/'idle/alpha-qa.json').read_text());im=Image.open(b/'idle/master.png').convert('RGBA');a=np.array(im.getchannel('A'));probes=[]
for name,x,y in [('left-arm-gap',551,670),('right-arm-gap',1060,650),('leg-gap',817,862)]:
 r=a[y-3:y+4,x-3:x+4];probes.append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'zeroPixels':int((r==0).sum()),'maxAlpha':int(r.max())})
q['negativeSpaceProbes']=probes;(b/'idle/alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
s=60/878;origin=[8,4];frame=[128,80];foot=q['suggestedSourceFoot'];piv=[origin[i]+foot[i]*s for i in range(2)]
# QA projections only. Raw and master stay unchanged. Every projection uses the complete native canvas.
def project(im,scale,canv,xy):
 return im.transform(canv,Image.Transform.AFFINE,(1/scale,0,-xy[0]/scale,0,1/scale,-xy[1]/scale),resample=Image.Resampling.BICUBIC)
preview=project(im,s,tuple(frame),origin);preview.save(b/'idle/review-transparent-1x.png')
project(im,s*2,(256,160),[16,8]).save(b/'idle/review-transparent-2x.png')
human=Image.open('/workspace/shared/empire-art-rebuild/approved-v5/animation/skeleton-guard/idle/master.png').convert('RGBA');hs=44/860;hp=[843,979]
for factor in [1,2,6]:
 sz=(300*factor,100*factor);bg=Image.new('RGBA',sz,(230,232,225,255));d=ImageDraw.Draw(bg);ground=83*factor
 for src,scale,fp,cx in [(human,hs,hp,65),(im,s,foot,190)]:
  sc=scale*factor;xy=[cx*factor-fp[0]*sc,ground-fp[1]*sc];bg.alpha_composite(project(src,sc,sz,xy))
 d=ImageDraw.Draw(bg);font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',max(8,9*factor))
 d.line((10*factor,ground,290*factor,ground),fill=(119,125,112,255),width=max(1,factor//3))
 d.text((28*factor,87*factor),'Human body 44',font=font,fill=(30,35,28,255));d.text((151*factor,87*factor),'Colossus crown 60',font=font,fill=(30,35,28,255))
 bg.convert('RGB').save(b/f'idle/human-scale-{factor}x.png')
r={'status':'Idle native and alpha self-review passed; coordinator native/human-scale acceptance pending before next poses','nativeMaster':'idle/master.png','nativeRaw':'idle/raw.png','previewTransparent':'idle/review-transparent-2x.png','humanScaleComparisons':['idle/human-scale-1x.png','idle/human-scale-2x.png','idle/human-scale-6x.png'],'nativeSize':q['size'],'sharedScale':s,'scaleFraction':'60/878','bodyTargetHeightLogicalPixels':60,'bodyReferenceSourcePixels':878,'bodyMeasurement':'Tallest grave tablet y57 to two-foot mean sole y935.0. Source sole bands: left946,right924. Exact mean x820.7551; canonical foot [821,946].','logicalVisibleWidth':934*s,'canonicalSourceFoot':foot,'commonSourceCanvas':[1586,992],'proposedCommonFrameSize':frame,'proposedOrigin':origin,'proposedRuntimePivot':piv,'fullCanvasBounds':[8,4,8+1586*s,4+992*s],'completeCanvasFits':True,'perimeterMaxAlpha':q['perimeterMaxAlpha'],'negativeSpaceProbes':probes,'nativeMastersUnmodified':True,'artFootprint':[2,2],'gameplayOccupancyChanged':False,'semanticReview':q['semanticReview'],'remainingPosesGenerated':False}
(b/'idle-review.json').write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r,indent=2))
