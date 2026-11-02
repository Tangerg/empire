"""Visible real alpha feet and a conservative NEW base-plane envelope, not original hidden footprint certification."""
from pathlib import Path
import json,runpy,math
from PIL import Image
import numpy as np
r=Path('/workspace/scratch/84c9fe948755');out=r/'c07-static-preview';w=runpy.run_path(str(out/'verify-granary-courtyard-walk.py'));level=w['b']['level'];g=r/'empire-c01/godot';layout=json.load(open(g/'data/scenes/c01-07-selected-assembly-ground.json'));catalog=json.load(open(g/'data/scenes/c01-07-selected-assembly-objects.json'))['objects'];i=next(i for i in layout['objects'] if i['id']=='central-landmark-core');a=catalog[i['assetId']];im=np.array(Image.open(g/a['resourcePath'][6:]));alpha=im[:,:,3];anchor=a['anchorPixels'];scale=i['scale'];contact=[v*16 for v in i['contact']];envelope=[735,453,803,489]
feet=[]
for x in range(im.shape[1]):
 ys=np.where(alpha[:,x]>127)[0]
 if len(ys):
  y=int(ys.max());p=[contact[0]+(x-anchor[0])*scale,contact[1]+(y-anchor[1])*scale];cell=[math.floor(v/16) for v in p];assert level['terrain'][cell[1]][cell[0]]=='#',(x,y,p,cell);assert envelope[0]<=p[0]<=envelope[2] and envelope[1]<=p[1]<=envelope[3];feet.append({'texture':[x,y],'source':p,'canonicalCell':cell})
# Entire conservative base envelope is blocked; it deliberately does not claim the full50group.
insideCells=[[x,y] for y in range(math.floor(envelope[1]/16),math.floor(envelope[3]/16)+1) for x in range(math.floor(envelope[0]/16),math.floor(envelope[2]/16)+1)]
assert all(level['terrain'][y][x]=='#' for x,y in insideCells)
legal=[(x,y) for y in range(25,36) for x in range(40,56) if level['terrain'][y][x]=='p'];edges=[(p,q) for p in legal for q in legal if abs(p[0]-q[0])+abs(p[1]-q[1])==1];minimum=float('inf');witness=None;count=0
for p,q in edges:
 dx=q[0]-p[0]
 for west in ([False,True] if dx==0 else [dx<0]):
  for f in w['frames']:
   for a0,b0 in zip(w['mapped'](p,f,west),w['mapped'](q,f,west)):
    count+=1;gapx=max(envelope[0]-max(a0[0],b0[0]),min(a0[0],b0[0])-envelope[2],0);gapy=max(envelope[1]-max(a0[1],b0[1]),min(a0[1],b0[1])-envelope[3],0);distance=math.hypot(gapx,gapy)
    if distance<minimum:minimum=distance;witness={'from':p,'to':q,'frame':f['index'],'west':west,'soleFrom':a0,'soleTo':b0}
assert minimum>0
result={'scope':'Core visible alpha>.5 front/side feet; conservative authored hidden-base bound, NOT exact original hidden foundation or full50cell acceptance. Static real Torren walk contour math only.','visibleFootSamples':feet,'allVisibleFeetOnBlocked':True,'conservativeNewBaseEnvelope':envelope,'envelopeBlockedCells':insideCells,'canonicalJointResponsibilityCells':50,'outerLegalCells':legal,'directedAdjacentEdges':len(edges),'testedSoleSegments':count,'minimumSourcePixelClearance':minimum,'closestWitness':witness,'stateActions':0}
(out/'central-landmark-footing-result.json').write_text(json.dumps(result,indent=2)+'\n');print('CENTRAL_CORE_FOOTING',len(feet),'blocked envelope cells',len(insideCells),'edges',len(edges),'soleSegments',count,'minimum',minimum)
