from pathlib import Path
from PIL import Image
from scipy.ndimage import label,find_objects
import numpy as np,json,hashlib,shutil
r=Path(__file__).parent;q=r/'qa';metrics=json.loads((q/'qa-metrics.json').read_text());phases=metrics['phaseOrder'];rows=[];edges=[];full=Image.new('RGB',(5016,2508));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for i,p in enumerate(phases):
 im=Image.open(r/p/'master.png');ar=np.array(im);alpha=ar[:,:,3];rgb=ar[:,:,:3].astype(int);lab,n=label(alpha>=128);components=[]
 for idx,sl in enumerate(find_objects(lab),1):
  y,x=sl;area=int((lab[sl]==idx).sum())
  if area>=300:components.append({'bbox':[x.start,y.start,x.stop,y.stop],'area':area,'center':[(x.start+x.stop)/2,(y.start+y.stop)/2]})
 components.sort(key=lambda c:c['area'],reverse=True)
 bases={}
 for side,x0,x1 in [('left',0,450),('right',800,1254)]:
  mask=(alpha>=128).copy();mask[:750,:]=False;mask[:,:x0]=False;mask[:,x1:]=False
  yy,xx=np.where(mask);bases[side]={'bbox':[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)],'alphaCentroid':[float(xx.mean()),float(yy.mean())]}
 corridor=(alpha>=128).copy();corridor[:,:445]=False;corridor[:,810:]=False;corridor[:480,:]=False;corridor[780:,:]=False
 row={'phase':p,'sourceHash':sha(r/p/'master.png'),'alphaMassSourcePx':float(alpha.sum()/255),'visibleBoundsAlpha128':Image.fromarray((alpha>=128).astype('uint8')).getbbox(),'bases':bases,'centralEnergyAreaAlpha128':int(corridor.sum()),'componentsAlpha128':components};rows.append(row)
 deviations={}
 for bi,(name,color) in enumerate([('black',(0,0,0,255)),('white',(255,255,255,255))]):
  comp=Image.new('RGBA',im.size,color);comp.alpha_composite(im);pixels=np.array(comp.convert('RGB'));edge=np.concatenate([pixels[0],pixels[-1],pixels[:,0],pixels[:,-1]]);deviations[name]=int(np.max(np.abs(edge.astype(int)-np.array(color[:3]))));full.paste(comp.convert('RGB'),(i*1254,bi*1254))
 ed=np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]]);edges.append({'phase':p,'edgeAlphaMax':int(ed.max()),'edgeAlphaNonzero':int(np.count_nonzero(ed)),'maxBlackWhiteEdgeRgbDeviation':deviations,'sourcePixelsUnchanged':True})
 refs=json.loads((r/p/'reference-hashes.json').read_text());sd=r/p/'reference-snapshots';sd.mkdir(exist_ok=True);snaps=[]
 for j,ref in enumerate(refs):
  orig=Path(ref['path']);assert sha(orig)==ref['sha256'];dst=sd/f'input-{j+1}.png';shutil.copy2(orig,dst);snaps.append({'original':str(orig),'snapshot':str(dst.relative_to(r/p)),'sha256':sha(dst)})
 (r/p/'reference-snapshots.json').write_text(json.dumps(snaps,indent=2)+'\n')
proof={'measurement':'Unedited source alpha >=128. Base registration sampled below y750 in left x<450 and right x>=800 regions, excluding middle connection. The central energy corridor is x445..809,y480..779 and excludes the cyan beacon cores. Masks are diagnostic only, never applied to artwork.','frames':rows,'baseBottomY':{side:[row['bases'][side]['bbox'][3] for row in rows] for side in ['left','right']},'baseBottomMaxVariationLogicalPx':{side:(max(row['bases'][side]['bbox'][3] for row in rows)-min(row['bases'][side]['bbox'][3] for row in rows))*28/1254 for side in ['left','right']},'baseCentroidMaxAxisVariationLogicalPx':{side:[(max(row['bases'][side]['alphaCentroid'][axis] for row in rows)-min(row['bases'][side]['alphaCentroid'][axis] for row in rows))*28/1254 for axis in [0,1]] for side in ['left','right']},'centralEnergyAreas':[row['centralEnergyAreaAlpha128'] for row in rows],'interpretation':'The original straight energy link becomes a connected zigzag, then separated short pieces and squares, then sparse squares without any connecting line. Both cyan-core gray/gold beacons remain planted.','caveats':['Small source redraw variation is measured; beacon pixels are not identical across four independent imagegen sources.','The center particles are authored phase changes, not asserted to track every individual particle continuously.','Source/static phase review and exact timing do not establish observed live playback.'],'consumerBoundary':'Catalog-only. No actual FX23 consumer or specific lifetime found; future consumers own hold and removal.','browserObserved':False}
(q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n');(q/'source-edge-composite-proof.json').write_text(json.dumps(edges,indent=2)+'\n');full.save(q/'all-source-black-white.png')
old=Image.open(r/'reference/original-board.png');aa=np.array(old.getchannel('A'));ed=np.concatenate([aa[0],aa[-1],aa[:,0],aa[:,-1]]);(q/'original-backing-evidence.json').write_text(json.dumps({'source':str(r/'reference/original-board.png'),'sha256':sha(r/'reference/original-board.png'),'size':list(old.size),'mode':old.mode,'alphaRange':[int(aa.min()),int(aa.max())],'edgeAlphaMax':int(ed.max()),'interpretation':'RGBA mode does not make the gray/white vignette a genuine transparent background. Its pixels are kept only as original reference evidence. New artwork was regenerated, never alpha-cleaned.'},indent=2)+'\n')
print(json.dumps({k:v for k,v in proof.items()if k!='frames'},indent=2))
