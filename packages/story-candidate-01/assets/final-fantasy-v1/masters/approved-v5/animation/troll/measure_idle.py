from pathlib import Path
from PIL import Image,ImageDraw
import json,numpy as np
b=Path(__file__).parent;q=json.loads((b/'idle/alpha-qa.json').read_text());im=Image.open(b/'idle/master.png');a=np.array(im.getchannel('A'));feet=[]
for name,roi in [('screen-left',[495,843,724,978]),('screen-right',[1010,836,1120,956])]:
 x0,y0,x1,y1=roi;ys,xs=np.where(a[y0:y1,x0:x1]>127);sole=int(ys.max()+y0);ys,xs=np.where(a[max(y0,sole-3):sole+1,x0:x1]>127);feet.append({'foot':name,'roi':roi,'soleY':sole,'bottomFourRowsMeanX':float(xs.mean()+x0)})
mx=sum(f['bottomFourRowsMeanX'] for f in feet)/2;my=sum(f['soleY'] for f in feet)/2;foot=[round(mx),max(f['soleY'] for f in feet)];body=my-q['bboxAlphaGreaterThan127'][1];s=58/body
probes=[]
for name,x,y in [('finger-gap',1084,662),('leg-gap',745,881),('outside-left',250,500)]:
 r=a[y-2:y+3,x-2:x+3];probes.append({'name':name,'rectangle':[x-2,y-2,x+3,y+3],'zeroPixels':int((r==0).sum()),'maxAlpha':int(r.max())})
q.update({'feetMeasurement':feet,'sourceFootMidpoint':[mx,my],'suggestedSourceFoot':foot,'sharedBodyScale':s,'sharedScaleFraction':f'58/{body}','bodyReferenceSourcePixels':body,'bodyTargetHeightLogicalPixels':58,'negativeSpaceProbes':probes})
(b/'idle/alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
review={'status':'Await coordinator idle and scale approval','nativeSize':im.size,'sharedScale':s,'scaleFraction':f'58/{body}','bodyReferenceSourcePixels':body,'bodyTargetHeightLogicalPixels':58,'bodyMeasurement':f'Crown y{q["bboxAlphaGreaterThan127"][1]} to both-bare-foot mean sole y{my}; hammer excluded. Sole ROIs and mean x are in alpha QA.','canonicalSourceFoot':foot,'commonSourceCanvas':[1586,992],'proposedCommonFrameSize':[128,80],'proposedOrigin':[12,4],'proposedRuntimePivot':[12+foot[0]*s,4+foot[1]*s],'artFootprint':[2,2],'noRuleChange':True,'humanReferenceBodyLogicalPixels':44,'stoneGolemReferenceBodyLogicalPixels':62,'proposedLogicalVisibleWidth':(q['bboxAlphaGreaterThan127'][2]-q['bboxAlphaGreaterThan127'][0])*s,'alphaPerimeterMax':int(max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())),'negativeSpaceProbes':probes,'nativeMastersUnmodified':True}
(b/'idle-review.json').write_text(json.dumps(review,indent=2)+'\n')
for n,scale in [(1,1),(2,2),(6,6)]:
 board=Image.new('RGBA',(430*scale,130*scale),'#e6e2d8');d=ImageDraw.Draw(board);baseline=109*scale
 refs=[('human 44',b.parent/'rune-shield/idle/master.png',44/904.5,[780,973],72),('troll 58',b/'idle/master.png',s,foot,206),('golem 62',b.parent/'stone-golem/idle/master.png',62/754.5,[764,974],348)]
 for label,path,k,anchor,x in refs:
  ref=Image.open(path).convert('RGBA');rs=ref.resize((round(ref.width*k*scale),round(ref.height*k*scale)),Image.Resampling.LANCZOS);ox=round(x*scale-anchor[0]*k*scale);oy=round(baseline-anchor[1]*k*scale);board.alpha_composite(rs,(ox,oy));d.text((x*scale-27*scale,114*scale),label,fill='black',font_size=8*scale)
 d.line((0,baseline,430*scale,baseline),fill='#9a988c',width=scale)
 board.convert('RGB').save(b/f'idle/size-comparison-{n}x.png')
# QA preview of complete native canvas at one shared scale, not master output
for factor in [1,2,4]:
 can=Image.new('RGBA',(128*factor,80*factor));r=im.resize((round(im.width*s*factor),round(im.height*s*factor)),Image.Resampling.LANCZOS);can.alpha_composite(r,(12*factor,4*factor));can.save(b/f'idle/review-transparent-{factor}x.png')
print(json.dumps(review,indent=2))
