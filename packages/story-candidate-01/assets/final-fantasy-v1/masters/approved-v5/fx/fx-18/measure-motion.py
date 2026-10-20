from pathlib import Path
from PIL import Image
from scipy.ndimage import label,find_objects
import numpy as np,json,hashlib
R=Path(__file__).parent;Q=R/'qa';P=['scattered-bands','aligning-ring','closed-restraints','settled-restraints'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def runs(v):
 z=np.flatnonzero(np.diff(np.r_[False,v,False]));return z.reshape(-1,2).tolist()
def analyze(path):
 im=Image.open(path);a=np.array(im)[:,:,3];m=a>=128;ll,n=label(m);cs=[]
 for k,s in enumerate(find_objects(ll)):
  z=ll[s]==k+1
  if z.sum()>1000:
   yy,xx=np.where(z);cs.append({'area':int(z.sum()),'bounds':[s[1].start,s[0].start,s[1].stop,s[0].stop],'centroid':[float(xx.mean()+s[1].start),float(yy.mean()+s[0].start)]})
 main=[c for c in cs if c['area']>7000];b=np.array([c['bounds'] for c in main]);bounds=[int(b[:,0].min()),int(b[:,1].min()),int(b[:,2].max()),int(b[:,3].max())]
 small={}
 for c in cs:
  if c['area']<7000:
   x,y=c['centroid'];key=('upper-left' if y<430 else 'middle-left' if y<650 else 'lower-left') if x<627 else ('upper-right' if y<650 else 'lower-right');small[key]=c
 rs=runs(m[640,:]);centers=[(s+e)/2 for s,e in rs];mid=float(np.mean(centers));ys=[]
 for x in centers:
  rr=runs(m[500:780,round(x)]);s,e=max(rr,key=lambda r:r[1]-r[0]);ys.append(500+(s+e)/2)
 return {'sourceSHA256':sha(path),'alphaMass':float(a.sum()/255),'componentsAlpha128Over1000':cs,'mainAssemblyUnionBoundsAlpha128':bounds,'mainAssemblyWidth':bounds[2]-bounds[0],'mainAssemblyHeight':bounds[3]-bounds[1],'sideClaspScanlineY640IntervalsAlpha128':rs,'sideClaspScanlineY640Midpoints':centers,'sideClaspMidlineX':mid,'sideClaspColumnIntervalMidpointsY':ys,'sideClaspMidlineY':float(np.mean(ys)),'centerColumnX627Alpha128Intervals':runs(m[:,627]),'smallDiamondCohort':small}
a={p:analyze(R/p/'master.png')for p in P};old=analyze(R/'rejected/settled-restraints-attempt01-overcontracted/master.png');m=np.array([a[p]['alphaMass'] for p in P]);widths=[a[p]['mainAssemblyWidth'] for p in P];heights=[a[p]['mainAssemblyHeight'] for p in P]
proof={'topicId':'C01-FX-18','method':'Alpha128 connected components; components over 7000 pixels form the main-assembly union; smaller components over 1000 identify the five detached diamonds. Horizontal scanline at y640 identifies side-clasp extents; their midpoint columns are sampled only within y500..780. These are diagnostic landmarks, not image alignment or generated-pixel edits. The side clasps change shape and the scanline includes adjacent band junctions, so these measurements bound visual registration rather than proving pixel-identical rigid motion.','phaseOrder':P,'frames':a,'mainAssemblyWidthsSourcePx':widths,'mainAssemblyHeightsSourcePx':heights,'adjacentAlphaMassChangesPercent':(np.diff(m)/m[:-1]*100).tolist(),'mainAssemblyTerminalWidthContractionPercent':(1-widths[-1]/widths[-2])*100,'mainAssemblyTerminalHeightContractionPercent':(1-heights[-1]/heights[-2])*100,'sharedCenterMidlineXRange':[min(a[p]['sideClaspMidlineX'] for p in P),max(a[p]['sideClaspMidlineX'] for p in P)],'sharedCenterMidlineYRange':[min(a[p]['sideClaspMidlineY'] for p in P),max(a[p]['sideClaspMidlineY'] for p in P)],'rejectedTerminal':old,'rejectedTerminalWidthContractionPercent':(1-old['mainAssemblyWidth']/widths[-2])*100,'rejectedTerminalAlphaMassChangePercent':(old['alphaMass']/m[-2]-1)*100,'noFixedEmitterOrGroundObject':True,'noPerFrameFitOrShift':True,'nativeScale':28/1254,'loop':False,'livePlaybackObserved':False}
(Q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
color=Image.new('RGB',(1600,1600))
for i,p in enumerate(P):color.paste(Image.open(Q/f'{p}-green-ground-source-full.png').resize((800,800),Image.Resampling.LANCZOS),(i%2*800,i//2*800))
color.save(Q/'all-source-green-ground-overview.png')
print(json.dumps({k:proof[k] for k in ['mainAssemblyWidthsSourcePx','mainAssemblyHeightsSourcePx','adjacentAlphaMassChangesPercent','mainAssemblyTerminalWidthContractionPercent','mainAssemblyTerminalHeightContractionPercent','sharedCenterMidlineXRange','sharedCenterMidlineYRange','rejectedTerminalWidthContractionPercent']},indent=2))
