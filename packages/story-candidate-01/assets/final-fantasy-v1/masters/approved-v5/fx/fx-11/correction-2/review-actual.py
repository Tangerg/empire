from pathlib import Path
from PIL import Image
from scipy.ndimage import label,find_objects,gaussian_filter1d
from scipy.signal import find_peaks
import numpy as np,json,hashlib,shutil
r=Path(__file__).parent;q=r/'qa';metrics=json.loads((q/'qa-metrics.json').read_text());phases=metrics['phaseOrder'];out=[];edges=[];full=Image.new('RGB',(5016,2508));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for i,p in enumerate(phases):
 im=Image.open(r/p/'master.png');a=np.array(im.getchannel('A'));mask=a>=128;labs,n=label(mask);components=[];wind=[]
 for k,sl in enumerate(find_objects(labs),1):
  y,x=sl;box=[x.start,y.start,x.stop,y.stop];area=int((labs[sl]==k).sum())
  if area>200:components.append({'bounds':box,'alpha128Area':area})
  if area>8000 and y.start<650:
   xs=[];tops=[]
   for xx in range(x.start,x.stop):
    yy=np.flatnonzero(labs[:,xx]==k)
    if len(yy):xs.append(xx);tops.append(int(yy[0]))
   smooth=gaussian_filter1d(np.array(tops,dtype=float),12);peaks,_=find_peaks(-smooth,prominence=12,distance=100)
   wind.append({'bounds':box,'interiorTopCrests':[{'x':xs[j],'y':tops[j]} for j in peaks]})
 tufts=[c for c in components if c['bounds'][1]>780 and c['bounds'][2]-c['bounds'][0]>140 and c['bounds'][3]-c['bounds'][1]>100];tufts.sort(key=lambda c:c['bounds'][0]);assert len(tufts)==2
 out.append({'phase':p,'tufts':tufts,'windComponents':wind,'otherComponents':[c for c in components if c not in tufts]})
 ev=[]
 for bgname,color in [('black',(0,0,0,255)),('white',(255,255,255,255))]:
  comp=Image.new('RGBA',im.size,color);comp.alpha_composite(im);arr=np.array(comp.convert('RGB'));edge=np.concatenate([arr[0],arr[-1],arr[:,0],arr[:,-1]]);ev.append(int(np.max(np.abs(edge.astype(int)-np.array(color[:3])))));full.paste(comp.convert('RGB'),(i*1254,0 if bgname=='black' else 1254))
 edgea=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]]);edges.append({'phase':p,'edgeAlphaMax':int(edgea.max()),'edgeAlphaNonzero':int(np.count_nonzero(edgea)),'maxBlackWhiteEdgeRgbDeviation':dict(zip(['black','white'],ev)),'sourcePixelsUnchanged':True})
 refs=json.loads((r/p/'reference-hashes.json').read_text());sd=r/p/'reference-snapshots';sd.mkdir(exist_ok=True);snaps=[]
 for j,ref in enumerate(refs):
  orig=Path(ref['path']);assert sha(orig)==ref['sha256'];dst=sd/f'input-{j+1}.png';shutil.copy2(orig,dst);snaps.append({'original':str(orig),'snapshot':str(dst.relative_to(r/p)),'sha256':sha(dst)})
 (r/p/'reference-snapshots.json').write_text(json.dumps(snaps,indent=2)+'\n')
contacts={side:[v['tufts'][ix]['bounds'][3] for v in out] for ix,side in enumerate(['left','right'])};spreads={s:(max(v)-min(v))*28/1254 for s,v in contacts.items()};seam={s:(v[0]-v[-1])*28/1254 for s,v in contacts.items()};masses=[f['alphaMass'] for f in metrics['frames']]
proof={'measurement':'Actual alpha128 connected contours and smoothed top-boundary local minima. Crests are descriptive shape landmarks, not proof of rigid material-point identity.','frames':out,'tuftContactBottomsSourceY':contacts,'tuftContactSpreadLogicalPx':spreads,'lastFirstTuftContactDeltaLogicalPx':seam,'sourceAlphaMasses':masses,'maximumMassSpreadRelativeToMinimum':max(masses)/min(masses)-1,'motionReview':'qa/COHORT-REVIEW.md','browserObserved':False}
(q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n');(q/'source-edge-composite-proof.json').write_text(json.dumps(edges,indent=2)+'\n');full.save(q/'all-source-black-white.png');print(json.dumps({'contacts':contacts,'spreadLogicalPx':spreads,'lastFirstLogicalPx':seam,'wind':[v['windComponents'] for v in out]},indent=2))
