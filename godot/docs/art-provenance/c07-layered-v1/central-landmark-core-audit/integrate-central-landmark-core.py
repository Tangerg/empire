from pathlib import Path
from PIL import Image
import json,numpy as np,hashlib
r=Path('/workspace/scratch/84c9fe948755');g=r/'empire-c01/godot';out=r/'c07-static-preview';path=g/'assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png';im=Image.open(path);alpha=np.array(im)[:,:,3];w,h=im.size
strips=[]
for left in range(0,w,8):
 right=min(w,left+8);ys,xs=np.where(alpha[:,left:right]>127);x=(left+right-1)/2
 if len(ys): y=int(ys.max());basis='actual alpha>.5 foot band maximum'
 else: y=1205+(x-185)*176/318 if x<503 else 1381-(x-503)*171/349;basis='outside opaque body extrapolation only'
 strips.append({'rightPixels':right,'groundFrontPixels':[x,float(y)]})
key='central-landmark-core-v1';layout=json.load(open(g/'data/scenes/c01-07-selected-assembly-ground.json'));catalog=json.load(open(g/'data/scenes/c01-07-selected-assembly-objects.json'));assert key not in catalog['objects']
catalog['objects'][key]={'resourcePath':'res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png','pixelSize':[w,h],'anchorPixels':[503,1381],'groundFrontPixels':[503,1381],'contactShadowRadiiPixels':[35,6],'depthStrips':strips}
layout['objects'].append({'id':'central-landmark-core','assetId':key,'contact':[767/16,487/16],'scale':.0965,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
for name,d in [('c01-07-selected-assembly-ground.json',layout),('c01-07-selected-assembly-objects.json',catalog)]: (g/'data/scenes'/name).write_text(json.dumps(d,indent=2)+'\n')
report={'scope':'Core only: white finial/neck/stone cap/shaft/two lower steps; no canopy pole/outer platform/planters/benches','assetSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'canvas':[w,h],'scale':.0965,'anchor':[503,1381],'worldAnchor':[767,487],'sourceVisibleWitnesses':[[737,469],[767,486],[800,470]],'notExactOriginalFootClaims':[[735,470],[802,472]],'stripCount':len(strips),'stripRule':'Eight-image-pixel band max actual alpha>.5 lower edge, not ideal line clipping','runtimeOwnership':'Selected asset depthStrips own runtime contacts; this generator is offline reproducible provenance','alpha':{'max':int(alpha.max()),'nonzero':int((alpha>0).sum()),'over127':int((alpha>127).sum()),'below10ResidualOutsideBody':'present; not opaque/pointer material, no engine threshold change; raw alpha preserved'},'originalCapOverhangMaterialSamples':[]}
for p in [(782,393),(783,394)]:
 q=[503+(p[0]-767)/.0965,1381+(p[1]-487)/.0965];report['originalCapOverhangMaterialSamples'].append({'source':p,'textureFloat':q,'nearestRgba':im.getpixel(tuple(round(v) for v in q))})
(out/'central-landmark-core-registration.json').write_text(json.dumps(report,indent=2)+'\n');print('CENTRAL_CORE_SELECTED_CANDIDATE',len(strips))
