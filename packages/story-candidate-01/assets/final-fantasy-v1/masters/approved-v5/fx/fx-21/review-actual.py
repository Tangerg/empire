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
 dust=(alpha>=128)&(rgb[:,:,0]>rgb[:,:,1]+15)&(rgb[:,:,1]>=rgb[:,:,2])&(rgb[:,:,2]>60)&(rgb[:,:,1]-rgb[:,:,2]<70)
 row={'phase':p,'sourceHash':sha(r/p/'master.png'),'alphaMassSourcePx':float(alpha.sum()/255),'visibleAreaAlpha128':int((alpha>=128).sum()),'visibleBoundsAlpha128':Image.fromarray((alpha>=128).astype('uint8')).getbbox(),'largeComponentCountAlpha128':len(components),'largestComponentAreaAlpha128':components[0]['area'],'componentsAlpha128':components,'warmDustDiagnosticArea':int(dust.sum())if i>=2 else None,'warmDustDiagnosticBounds':Image.fromarray(dust.astype('uint8')).getbbox()if i>=2 else None};rows.append(row)
 deviations={}
 for bi,(name,color) in enumerate([('black',(0,0,0,255)),('white',(255,255,255,255))]):
  comp=Image.new('RGBA',im.size,color);comp.alpha_composite(im);pixels=np.array(comp.convert('RGB'));edge=np.concatenate([pixels[0],pixels[-1],pixels[:,0],pixels[:,-1]]);deviations[name]=int(np.max(np.abs(edge.astype(int)-np.array(color[:3]))));full.paste(comp.convert('RGB'),(i*1254,bi*1254))
 ed=np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]]);edges.append({'phase':p,'edgeAlphaMax':int(ed.max()),'edgeAlphaNonzero':int(np.count_nonzero(ed)),'maxBlackWhiteEdgeRgbDeviation':deviations,'sourcePixelsUnchanged':True})
 refs=json.loads((r/p/'reference-hashes.json').read_text());sd=r/p/'reference-snapshots';sd.mkdir(exist_ok=True);snaps=[]
 for j,ref in enumerate(refs):
  orig=Path(ref['path']);assert sha(orig)==ref['sha256'];dst=sd/f'input-{j+1}.png';shutil.copy2(orig,dst);snaps.append({'original':str(orig),'snapshot':str(dst.relative_to(r/p)),'sha256':sha(dst)})
 (r/p/'reference-snapshots.json').write_text(json.dumps(snaps,indent=2)+'\n')
proof={'measurement':'Unedited source alpha mass, visible bounds and connected components at alpha >=128. The warm-color mask is a diagnostic for dust only in phases3/4 and can include warm fragment edges. No mask is applied to artwork.','frames':rows,'sourceAlphaMass':[x['alphaMassSourcePx']for x in rows],'visibleBoundsAlpha128':[x['visibleBoundsAlpha128']for x in rows],'peakAndTerminalOverallBottomSourceY':[rows[i]['visibleBoundsAlpha128'][3]for i in [2,3]],'peakTerminalOverallBottomDifferenceLogicalPx':abs(rows[3]['visibleBoundsAlpha128'][3]-rows[2]['visibleBoundsAlpha128'][3])*28/1254,'peakAndTerminalWarmDustDiagnosticBounds':[rows[i]['warmDustDiagnosticBounds']for i in [2,3]],'interpretation':'The source starts with fragments only, scatters more rubble, introduces localized warm dust at the impact peak, then removes most large debris while rounded dust puffs remain. Same full canvas and transform throughout.','caveats':['The original source changes fragment distribution and sizes; not every stone/splinter is claimed to be a continuously tracked ballistic particle.','Overall lower bounds include rocks and chips, so their close match is not proof of exact dust contact geometry.','Warm dust color thresholds can include fragment edges and are diagnostic, not material-perfect segmentation.','Source/static review and exact APNG timing do not establish observed live playback.'],'consumerBoundary':'Catalog-only. Structure events currently use weapon-selected FX01–08 via animateHit; no actual FX21 lifetime is assigned.','browserObserved':False}
(q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n');(q/'source-edge-composite-proof.json').write_text(json.dumps(edges,indent=2)+'\n');full.save(q/'all-source-black-white.png')
old=Image.open(r/'reference/original-board.png');aa=np.array(old.getchannel('A'));ed=np.concatenate([aa[0],aa[-1],aa[:,0],aa[:,-1]]);(q/'original-backing-evidence.json').write_text(json.dumps({'source':str(r/'reference/original-board.png'),'sha256':sha(r/'reference/original-board.png'),'size':list(old.size),'mode':old.mode,'alphaRange':[int(aa.min()),int(aa.max())],'edgeAlphaMax':int(ed.max()),'interpretation':'RGBA mode does not make the gray/white vignette a genuine transparent background. Its pixels are kept only as original reference evidence. New artwork was regenerated, never alpha-cleaned.'},indent=2)+'\n')
print(json.dumps({k:v for k,v in proof.items()if k!='frames'},indent=2))
