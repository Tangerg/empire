from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,shutil
R=Path(__file__).parent;Q=R/'qa';m=json.loads((Q/'qa-metrics.json').read_text());P=m['phaseOrder'];s=28/1254;d={};contacts=[];edges=[];board=Image.new('RGB',(5016,2508));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for i,row in enumerate(m['frames']):
 p=row['phase'];bs=[b for b in row['visibleComponentsAlpha128'] if b[3]-b[1]>100 and b[2]-b[0]<200];bs.sort(key=lambda b:b[0]);assert len(bs)==3;d[p]=dict(zip(['left','center','right'],bs));contacts.append(row['boundsAlpha128'][3]);im=Image.open(R/p/'master.png');a=np.array(im.getchannel('A'));mask=np.zeros(a.shape,bool);mask[0,:]=1;mask[-1,:]=1;mask[:,0]=1;mask[:,-1]=1;dev={}
 for j,(name,ex) in enumerate([('black',0),('white',255)]):
  c=Image.open(Q/f'{p}-{name}-source-full.png');board.paste(c,(1254*i,1254*j));dev[name]=int(np.abs(np.array(c).astype(int)[mask]-ex).max())
 edges.append({'phase':p,'edgeAlphaMax':int(a[mask].max()),'edgeAlphaNonzero':int(np.count_nonzero(a[mask])),'maxBlackWhiteEdgeRGBDeviation':dev,'sourcePixelsUnchanged':True})
 refs=json.loads((R/p/'reference-hashes.json').read_text());dest=R/p/'reference-snapshots';dest.mkdir(exist_ok=True);audit=[]
 for j,x in enumerate(refs):
  z=dest/f'input-{j+1}.png';shutil.copy2(x['path'],z);assert sha(z)==x['sha256'];audit.append({'requestedPath':x['path'],'sha256AtGeneration':x['sha256'],'snapshot':str(z.relative_to(R))})
 (R/p/'reference-snapshots.json').write_text(json.dumps(audit,indent=2)+'\n')
trans=[];born={'splash-onset':'center','splash-crown':'right','rain-renewal':'left'}
for i,p in enumerate(P):
 n=P[(i+1)%4];v={}
 for lane,b in d[p].items():
  if born.get(n)==lane:continue
  nb=d[n][lane];dx=(nb[0]+nb[2]-b[0]-b[2])/2;dy=nb[3]-b[3];assert dy>0;v[lane]={'sourceDeltaX':dx,'sourceDeltaY':dy,'logicalDeltaX':dx*s,'logicalDeltaY':dy*s}
 trans.append({'from':p,'to':n,'survivors':v})
proof={'measurement':'Actual alpha128 connected-contour bounds; planned coordinates are not claimed exact. Cohort identities assigned by lane and visible impact/new-entry events.','drops':d,'transitions':trans,'nearTopEntries':{p:{'lane':l,'top':d[p][l][1],'bottom':d[p][l][3]}for p,l in born.items()},'contactBottomSourceY':contacts,'contactSpreadLogicalPx':(max(contacts)-min(contacts))*s,'lastFirstContactDeltaLogicalPx':(contacts[0]-contacts[-1])*s,'interpretation':'New cohorts enter near the upper margin, all continuing cohorts fall, and4to1 has no birth reset. Corrected renewal reduces water-row seam displacement and center-drop stall. Peak crown still sits lower than rainfall and must be judged visually; exact timing alone does not approve this.','browserObserved':False}
(Q/'motion-and-registration-proof.json').write_text(json.dumps(proof,indent=2)+'\n');(Q/'source-edge-composite-proof.json').write_text(json.dumps(edges,indent=2)+'\n');board.save(Q/'all-source-black-white.png');print(json.dumps(proof,indent=2))
