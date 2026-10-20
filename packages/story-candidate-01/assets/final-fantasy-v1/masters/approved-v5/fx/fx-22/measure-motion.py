from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,math
R=Path(__file__).parent;Q=R/'qa';P=['loose-hardware','seating-impact','repair-swirl','finished-fit'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();A={p:np.array(Image.open(R/p/'master.png')).astype(int)for p in P};ref=A['finished-fit']
rois={'nut-front':[480,875,750,1085],'nut-left-facet':[365,845,500,1010],'stone-left':[120,945,275,1070],'stone-lower-left':[295,1040,405,1140],'stone-lower-right':[830,1040,965,1140],'stone-right':[990,945,1130,1060]}
stone_windows={'left':[100,930,285,1110],'lower-left':[285,1020,420,1190],'lower-right':[815,1020,975,1190],'right':[980,930,1145,1100]}
landmarks={'loose-hardware':{'headCenter':[972,350],'shaftTip':[780,720]},'seating-impact':{'headCenter':[918,510],'shaftTip':[660,810]},'repair-swirl':{'headCenter':[627,300],'shaftTip':[627,780]},'finished-fit':{'headCenter':[627,300],'shaftTip':[627,780]}}
def analyze(p,a):
 reg={}
 for name,(x0,y0,x1,y1) in rois.items():
  m=(ref[y0:y1:3,x0:x1:3,3]>=128);target=ref[y0:y1:3,x0:x1:3,:3]
  def err(dx,dy):return float(np.abs(a[y0+dy:y1+dy:3,x0+dx:x1+dx:3,:3]-target)[m].mean())
  best=min((err(dx,dy),dx,dy)for dy in range(-36,37,3)for dx in range(-12,13,3));_,cx,cy=best
  best=min((err(dx,dy),dx,dy)for dy in range(cy-2,cy+3)for dx in range(cx-2,cx+3));reg[name]={'roi':rois[name],'bestOffsetXY':[best[1],best[2]],'meanAbsoluteRGBError':best[0]}
 stones={}
 for n,(x0,y0,x1,y1) in stone_windows.items():
  m=a[y0:y1,x0:x1,3]>=128;yy,xx=np.where(m);stones[n]={'pixels':int(m.sum()),'bounds':[int(xx.min()+x0),int(yy.min()+y0),int(xx.max()+x0+1),int(yy.max()+y0+1)]if len(xx)else None}
 m=a[900:1160,480:760,3]>=128;yy,xx=np.where(m);baseline=int(yy.max()+900)
 l=landmarks[p];hx,hy=l['headCenter'];tx,ty=l['shaftTip'];angle=math.degrees(math.atan2(hx-tx,ty-hy))
 return {'sourceSHA256':sha(R/p/'master.png'),'sourceAlphaMass':float(a[:,:,3].sum()/255),'fixedMaterialROIs':reg,'groundStoneWindows':stones,'centralNutBaselineYAlpha128':baseline,'approximateVisualBoltLandmarks':{**l,'leanFromVerticalDegrees':angle,'uncertaintySourcePixels':20,'note':'Hand-read semantic landmarks, not optical flow. Impact obscures the exact shaft endpoint in seating-impact.'}}
frames={p:analyze(p,a)for p,a in A.items()};m=np.array([frames[p]['sourceAlphaMass']for p in P]);bases=[frames[p]['centralNutBaselineYAlpha128']for p in P]
old=np.array(Image.open(R/'rejected/repair-swirl-attempt01-missing-ground-stones/master.png'));oldstones={n:int((old[y0:y1,x0:x1,3]>=128).sum())for n,(x0,y0,x1,y1)in stone_windows.items()}
proof={'topicId':'C01-FX-22','phaseOrder':P,'method':'Fixed nut and stone ROIs compared with finished-fit using RGB mean absolute error on its alpha128 mask, subsampled every3pixels; coarse offset search then one-pixel refinement. Diagnostic offsets are never applied to images. Ground-stone windows and the central nut baseline are separate alpha128 checks. Approximate bolt landmarks distinguish intended articulation from fixed-material drift.','frames':frames,'nutBaselineYSourcePixels':bases,'nutBaselineSpreadSourcePixels':max(bases)-min(bases),'nutBaselineSpreadLogicalPixels':(max(bases)-min(bases))*28/1254,'maxFixedMaterialBestOffsetSourcePixels':max(abs(v)for f in frames.values()for roi in f['fixedMaterialROIs'].values()for v in roi['bestOffsetXY']),'adjacentSourceAlphaMassChangesPercent':(np.diff(m)/m[:-1]*100).tolist(),'missingStoneCorrection':{'rejectedSourceSHA256':sha(R/'rejected/repair-swirl-attempt01-missing-ground-stones/master.png'),'selectedSourceSHA256':sha(R/'repair-swirl/master.png'),'rejectedGroundStoneWindowAlpha128Counts':oldstones,'selectedGroundStoneWindowAlpha128Counts':{k:v['pixels']for k,v in frames['repair-swirl']['groundStoneWindows'].items()},'method':'One bounded built-in imagegen repair, no code compositing or source alpha cleanup.'},'noPerFrameFitOrShift':True,'loop':False,'livePlaybackObserved':False};proof['method']=proof['method'].replace('every3pixels','every 3 pixels');(Q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
color=Image.new('RGB',(1600,1600))
for i,p in enumerate(P):color.paste(Image.open(Q/f'{p}-green-ground-source-full.png').resize((800,800),Image.Resampling.LANCZOS),(i%2*800,i//2*800))
color.save(Q/'all-source-green-ground-overview.png')
print(json.dumps({k:proof[k]for k in ['nutBaselineYSourcePixels','nutBaselineSpreadSourcePixels','nutBaselineSpreadLogicalPixels','maxFixedMaterialBestOffsetSourcePixels','adjacentSourceAlphaMassChangesPercent','missingStoneCorrection']},indent=2));print(json.dumps({p:{k:v['bestOffsetXY']for k,v in f['fixedMaterialROIs'].items()}for p,f in frames.items()},indent=2))
