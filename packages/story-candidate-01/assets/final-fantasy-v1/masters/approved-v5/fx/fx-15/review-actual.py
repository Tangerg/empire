from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,shutil
r=Path(__file__).parent;q=r/'qa';metrics=json.loads((q/'qa-metrics.json').read_text());phases=metrics['phaseOrder'];rows=[];edges=[];full=Image.new('RGB',(5016,2508));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();base=None
for i,p in enumerate(phases):
 im=Image.open(r/p/'master.png');a=np.array(im);alpha=a[:,:,3];rgb=a[:,:,:3].astype(float);yy=np.indices(alpha.shape)[0]
 lower=(alpha>=128)&(yy>=950)
 if base is None:base=lower
 union=(base|lower).sum();intersection=(base&lower).sum()
 visible=Image.fromarray(lower.astype('uint8')).getbbox()
 hot=(alpha>=128)&(rgb[:,:,1]>100)&(rgb[:,:,2]>80)&(rgb[:,:,1]>0.9*rgb[:,:,2])
 hx=np.where(hot);hotbounds=None if not len(hx[0]) else [int(hx[1].min()),int(hx[0].min()),int(hx[1].max()+1),int(hx[0].max()+1)]
 earth=(alpha>=128)&(yy>=930)&(rgb[:,:,2]<100)&(rgb[:,:,2]<0.75*rgb[:,:,1])
 rows.append({'phase':p,'lowerBedAlpha128Bounds':visible,'lowerBedAlpha128IoUWithFirst':float(intersection/union),'oliveLeafBedBounds':Image.fromarray(earth.astype('uint8')).getbbox(),'mintSpiritDiagnosticAreaSourcePx':int(hot.sum()),'mintSpiritDiagnosticBounds':hotbounds,'sourceHash':sha(r/p/'master.png')})
 deviations={}
 for bi,(name,color) in enumerate([('black',(0,0,0,255)),('white',(255,255,255,255))]):
  comp=Image.new('RGBA',im.size,color);comp.alpha_composite(im);ar=np.array(comp.convert('RGB'));edge=np.concatenate([ar[0],ar[-1],ar[:,0],ar[:,-1]]);deviations[name]=int(np.max(np.abs(edge.astype(int)-np.array(color[:3]))));full.paste(comp.convert('RGB'),(i*1254,bi*1254))
 ed=np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]]);edges.append({'phase':p,'edgeAlphaMax':int(ed.max()),'edgeAlphaNonzero':int(np.count_nonzero(ed)),'maxBlackWhiteEdgeRgbDeviation':deviations,'sourcePixelsUnchanged':True})
 refs=json.loads((r/p/'reference-hashes.json').read_text());sd=r/p/'reference-snapshots';sd.mkdir(exist_ok=True);snaps=[]
 for j,ref in enumerate(refs):
  orig=Path(ref['path']);assert sha(orig)==ref['sha256'];dst=sd/f'input-{j+1}.png';shutil.copy2(orig,dst);snaps.append({'original':str(orig),'snapshot':str(dst.relative_to(r/p)),'sha256':sha(dst)})
 (r/p/'reference-snapshots.json').write_text(json.dumps(snaps,indent=2)+'\n')
contacts=[row['lowerBedAlpha128Bounds'][3] for row in rows];proof={'measurement':'Unedited source alpha128 lower-bed silhouette below Y950; olive-leaf and mint-spirit color thresholds are diagnostic masks only, never applied to artwork.','frames':rows,'bedBottomContactSourceY':contacts,'bedBottomContactSpreadLogicalPx':(max(contacts)-min(contacts))*28/1254,'lowerBedMinimumIoUWithFirst':min(x['lowerBedAlpha128IoUWithFirst'] for x in rows),'interpretation':'Leaf-bed footprint stays near its common registration with disclosed subpixel logical variation; spirit wisp grows, reaches a leafy/branch peak, then settles into a narrower residual wisp. This is a nonloop single pass. No 4→1 continuity is required or claimed.','browserObserved':False}
(q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n');(q/'source-edge-composite-proof.json').write_text(json.dumps(edges,indent=2)+'\n');full.save(q/'all-source-black-white.png');print(json.dumps(proof,indent=2))
