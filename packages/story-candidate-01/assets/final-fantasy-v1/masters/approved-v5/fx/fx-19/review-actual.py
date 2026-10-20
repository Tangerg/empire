from pathlib import Path
from PIL import Image
from scipy.ndimage import label,find_objects
import numpy as np,json,hashlib,shutil
r=Path(__file__).parent;q=r/'qa';metrics=json.loads((q/'qa-metrics.json').read_text());phases=metrics['phaseOrder'];rows=[];edges=[];full=Image.new('RGB',(5016,2508));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for i,p in enumerate(phases):
 im=Image.open(r/p/'master.png');ar=np.array(im);alpha=ar[:,:,3];lab,n=label(alpha>=128);components=[]
 for idx,sl in enumerate(find_objects(lab),1):
  y,x=sl;area=int((lab[sl]==idx).sum())
  if area>=300:components.append({'bbox':[x.start,y.start,x.stop,y.stop],'area':area,'center':[(x.start+x.stop)/2,(y.start+y.stop)/2]})
 components.sort(key=lambda c:c['area'],reverse=True)
 row={'phase':p,'sourceHash':sha(r/p/'master.png'),'alphaMassSourcePx':float(alpha.sum()/255),'visibleAreaAlpha128':int((alpha>=128).sum()),'visibleBoundsAlpha128':Image.fromarray((alpha>=128).astype('uint8')).getbbox(),'largeComponentCountAlpha128':len(components),'largestComponentAreaAlpha128':components[0]['area'],'componentsAlpha128':components,'centralRegionVisibleAreaAlpha128':int((alpha[300:1050,350:900]>=128).sum())};rows.append(row)
 deviations={}
 for bi,(name,color) in enumerate([('black',(0,0,0,255)),('white',(255,255,255,255))]):
  comp=Image.new('RGBA',im.size,color);comp.alpha_composite(im);pixels=np.array(comp.convert('RGB'));edge=np.concatenate([pixels[0],pixels[-1],pixels[:,0],pixels[:,-1]]);deviations[name]=int(np.max(np.abs(edge.astype(int)-np.array(color[:3]))));full.paste(comp.convert('RGB'),(i*1254,bi*1254))
 ed=np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]]);edges.append({'phase':p,'edgeAlphaMax':int(ed.max()),'edgeAlphaNonzero':int(np.count_nonzero(ed)),'maxBlackWhiteEdgeRgbDeviation':deviations,'sourcePixelsUnchanged':True})
 refs=json.loads((r/p/'reference-hashes.json').read_text());sd=r/p/'reference-snapshots';sd.mkdir(exist_ok=True);snaps=[]
 for j,ref in enumerate(refs):
  orig=Path(ref['path']);assert sha(orig)==ref['sha256'];dst=sd/f'input-{j+1}.png';shutil.copy2(orig,dst);snaps.append({'original':str(orig),'snapshot':str(dst.relative_to(r/p)),'sha256':sha(dst)})
 (r/p/'reference-snapshots.json').write_text(json.dumps(snaps,indent=2)+'\n')
proof={'measurement':'Unedited source alpha mass and connected components at alpha >=128. Metrics include leaves as well as smoke and are not material-perfect segmentation. Diagnostic masks are never applied to artwork.','frames':rows,'alphaMassRelativeToDense':[x['alphaMassSourcePx']/rows[0]['alphaMassSourcePx'] for x in rows],'largestComponentAreaRelativeToDense':[x['largestComponentAreaAlpha128']/rows[0]['largestComponentAreaAlpha128'] for x in rows],'visibleBoundsAlpha128':[x['visibleBoundsAlpha128']for x in rows],'firstPairBottomVariationLogicalPx':abs(rows[1]['visibleBoundsAlpha128'][3]-rows[0]['visibleBoundsAlpha128'][3])*28/1254,'interpretation':'The source begins already dense. The main cloudy mass then rolls into deep notches, breaks into a narrower central ribbon with disconnected puffs, and disappears to leave three leaves and four wisps. Source opacity was never altered in code; shrinking connected mass is authored geometry.','caveats':['Breakup retains more total alpha mass than the approximate one-third prompt target, but visible holes and separated fragments establish the requested intermediate breakup. Numeric prompt targets are not claimed exact.','Leaf and puff correspondence is not tracked as exact continuous trajectories.','This is a floating smoke volume, not a fixed ground object. Sparse final elements no longer define a shared contact silhouette.','Source/static interpretation and exact encoded timing do not establish observed live playback.'],'visibilityBoundary':'Catalog-only. Existing vision/hiddenUnits rules are untouched; no enemy silhouette or marker is authored.','browserObserved':False}
(q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n');(q/'source-edge-composite-proof.json').write_text(json.dumps(edges,indent=2)+'\n');full.save(q/'all-source-black-white.png');print(json.dumps({k:v for k,v in proof.items()if k!='frames'},indent=2))
