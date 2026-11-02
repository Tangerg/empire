"""Read-only alpha measurements: explicit stone-base ROI, never whole bush alpha as feet."""
from pathlib import Path
from PIL import Image
import numpy as np,json,runpy,math,os
r=Path('/workspace/scratch/84c9fe948755');o=r/'c07-static-preview';design=json.load(open(o/os.environ.get('C07_WOOD_DESIGN','central-front-wood-planter-design.json')));im=np.array(Image.open(r/design['assetPath']));scale=design['scale'];anchor=design['anchor'];world=design['worldAnchor']
def source(p):return [world[k]+(p[k]-anchor[k])*scale for k in [0,1]]
feet=[]
# Manually visible solid lower stone plinth, not wood body or hanging foliage.
for x in range(design['stoneFootRoi'][0],design['stoneFootRoi'][2]):
 ys=np.where(im[design['stoneFootRoi'][1]:design['stoneFootRoi'][3],x,3]>127)[0]
 if len(ys):feet.append({'texture':[x,int(ys.max()+design['stoneFootRoi'][1])],'source':source([x,int(ys.max()+design['stoneFootRoi'][1])])})
assert len(feet)>550
points=sorted(set(tuple(v['source']) for v in feet)|{tuple(design['hiddenBack'])})
w=runpy.run_path(str(o/'verify-granary-courtyard-walk.py'));l=w['b']['level']
def hull(points):
 lo=[];hi=[]
 for p in points:
  while len(lo)>1 and w['cross'](lo[-2],lo[-1],p)<=0:lo.pop()
  lo.append(p)
 for p in reversed(points):
  while len(hi)>1 and w['cross'](hi[-2],hi[-1],p)<=0:hi.pop()
  hi.append(p)
 return lo[:-1]+hi[:-1]
base=hull(points);legal=[(x,y) for y in range(25,36) for x in range(40,56) if l['terrain'][y][x]=='p'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];bad=[];count=0
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in w['frames']:
   for j,(p,q) in enumerate(zip(w['mapped'](a,f,west),w['mapped'](b,f,west))):
    count+=1
    if w['hits'](p,q,base):bad.append({'from':a,'to':b,'frame':f['index'],'west':west,'sole':f['sole'][j],'segment':[p,q]})
feetOnBlocked=all(l['terrain'][math.floor(f['source'][1]/16)][math.floor(f['source'][0]/16)]=='#' for f in feet)
a=im[:,:,3];alpha={}
for threshold in [0,9,127,244]:
 yy,xx=np.where(a>threshold);alpha[str(threshold)]={'count':len(xx),'bounds':[int(xx.min()),int(yy.min()),int(xx.max()),int(yy.max())]}
result={'scope':design['scope']+' Actual visible stone-foot ROI, not foliage bottom; hidden rear is authored.','canvas':[im.shape[1],im.shape[0]],'scale':scale,'anchor':anchor,'worldAnchor':world,'stoneRoi':design['stoneFootRoi'],'actualVisibleStoneFeet':feet,'allFeetOnBlocked':feetOnBlocked,'newConservativeBaseHull':base,'frames':[f['index'] for f in w['frames']],'directedEdges':len(edges),'soleSegments':count,'violations':bad,'alpha':alpha};(o/os.environ.get('C07_WOOD_FOOT_OUTPUT','central-front-wood-planter-actual-footing.json')).write_text(json.dumps(result,indent=2)+'\n');print('FRONT_WOOD_PLANTER_ACTUAL_FOOTING',len(feet),feetOnBlocked,count,len(bad));assert feetOnBlocked,'Front wood planter stone plinth foot must remain on blocked cells';assert not bad,('Front wood planter stone plinth foot must clear actual walk soles',bad[:3])
