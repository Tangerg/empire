from pathlib import Path
from PIL import Image
from scipy.ndimage import label,find_objects
import numpy as np,json,hashlib,shutil,math
r=Path(__file__).parent;q=r/'qa';metrics=json.loads((q/'qa-metrics.json').read_text());phases=metrics['phaseOrder'];rows=[];edges=[];full=Image.new('RGB',(5016,2508));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for i,p in enumerate(phases):
 im=Image.open(r/p/'master.png');a=np.array(im);alpha=a[:,:,3];lab,n=label(alpha>=128);components=[]
 for idx,sl in enumerate(find_objects(lab),1):
  y,x=sl;area=int((lab[sl]==idx).sum())
  if area>=300:components.append({'bbox':[x.start,y.start,x.stop,y.stop],'area':area,'center':[(x.start+x.stop)/2,(y.start+y.stop)/2]})
 cross=[c for c in components if c['bbox'][0]>450 and c['bbox'][2]<800 and c['bbox'][1]>500 and c['bbox'][3]<900 and c['area']>20000]
 assert len(cross)==(0 if i==3 else 1)
 row={'phase':p,'sourceHash':sha(r/p/'master.png'),'componentsAlpha128':components,'plusComponent':cross[0] if cross else None,'alphaMassSourcePx':float(alpha.sum()/255),'centralRegionAlpha128Count':int((alpha[600:815,513:742]>=128).sum())};rows.append(row)
 deviations={}
 for bi,(name,color) in enumerate([('black',(0,0,0,255)),('white',(255,255,255,255))]):
  comp=Image.new('RGBA',im.size,color);comp.alpha_composite(im);ar=np.array(comp.convert('RGB'));edge=np.concatenate([ar[0],ar[-1],ar[:,0],ar[:,-1]]);deviations[name]=int(np.max(np.abs(edge.astype(int)-np.array(color[:3]))));full.paste(comp.convert('RGB'),(i*1254,bi*1254))
 ed=np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]]);edges.append({'phase':p,'edgeAlphaMax':int(ed.max()),'edgeAlphaNonzero':int(np.count_nonzero(ed)),'maxBlackWhiteEdgeRgbDeviation':deviations,'sourcePixelsUnchanged':True})
 refs=json.loads((r/p/'reference-hashes.json').read_text());sd=r/p/'reference-snapshots';sd.mkdir(exist_ok=True);snaps=[]
 for j,ref in enumerate(refs):
  orig=Path(ref['path']);assert sha(orig)==ref['sha256'];dst=sd/f'input-{j+1}.png';shutil.copy2(orig,dst);snaps.append({'original':str(orig),'snapshot':str(dst.relative_to(r/p)),'sha256':sha(dst)})
 (r/p/'reference-snapshots.json').write_text(json.dumps(snaps,indent=2)+'\n')
centers=[row['plusComponent']['center'] for row in rows[:3]];maxdist=max(math.dist(a,b) for a in centers for b in centers);proof={'measurement':'Connected opaque components at alpha >=128, selected central medical-plus component in source coordinates. These masks only measure artwork and are never applied to pixels.','frames':rows,'plusCentersSource':centers,'plusCentersLogical':[[2+x*28/1254,2+y*28/1254] for x,y in centers],'plusCenterMaximumPairDistanceSourcePx':maxdist,'plusCenterMaximumPairDistanceLogicalPx':maxdist*28/1254,'plusCenterVerticalSpreadLogicalPx':(max(c[1] for c in centers)-min(c[1] for c in centers))*28/1254,'terminalCentralRegionAlpha128Count':rows[-1]['centralRegionAlpha128Count'],'interpretation':'The medical plus retains its apparent scale and central placement with measured subpixel variation. The sparse onset orbit develops an open spiral, becomes fullest at peak, then the plus and bandages disappear while broken arcs and five leaves remain. This is a nonloop heal effect, with the final sample displayed for 250 ms by the real consumer.','caveats':['Leaf correspondence is not tracked as an exact continuous path; static evidence establishes purposeful changing poses and orbit geometry, not every leaf trajectory.','The developing spiral envelope is already nearly peak width, but open arcs and lower alpha mass distinguish it from the full peak.','Source center variation is reported rather than corrected by shifting pixels.','Decoded frame/timing verification is not observed browser playback.'],'browserObserved':False}
(q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n');(q/'source-edge-composite-proof.json').write_text(json.dumps(edges,indent=2)+'\n');full.save(q/'all-source-black-white.png');print(json.dumps({k:v for k,v in proof.items() if k!='frames'},indent=2))
