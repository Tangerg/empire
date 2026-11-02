"""Static actual sole-contour sweeps against conservative platform projection; no engine actions."""
from pathlib import Path
import json,runpy
r=Path('/workspace/scratch/84c9fe948755');o=r/'c07-static-preview';w=runpy.run_path(str(o/'verify-granary-courtyard-walk.py'));l=w['b']['level'];source=json.load(open(o/'central-platform-source.json'));points=sorted(set(tuple(v) for m in source['members'] for v in m['polygon']))
def hull(points):
 lower=[];upper=[]
 for p in points:
  while len(lower)>1 and w['cross'](lower[-2],lower[-1],p)<=0:lower.pop()
  lower.append(p)
 for p in reversed(points):
  while len(upper)>1 and w['cross'](upper[-2],upper[-1],p)<=0:upper.pop()
  upper.append(p)
 return lower[:-1]+upper[:-1]
base=hull(points);legal=[(x,y) for y in range(25,36) for x in range(40,56) if l['terrain'][y][x]=='p'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];bad=[];count=0
for a,b in edges:
 dx=b[0]-a[0]
 for west in ([False,True] if dx==0 else [dx<0]):
  for f in w['frames']:
   for j,(p,q) in enumerate(zip(w['mapped'](a,f,west),w['mapped'](b,f,west))):
    count+=1
    if w['hits'](p,q,base):bad.append({'from':a,'to':b,'frame':f['index'],'west':west,'sole':f['sole'][j],'segment':[p,q]})
result={'scope':'Conservative full platform projected convex hull; hidden base new design, not exact original support. Fixed actual Torren two walk phase lower-edge contours only; no movement/AI.','convexHull':base,'directedEdges':len(edges),'soleSegments':count,'violations':bad,'passed':not bad};(o/'central-platform-footing-result.json').write_text(json.dumps(result,indent=2)+'\n');print('PLATFORM_FOOTING',len(edges),count,'violations',len(bad));assert not bad,bad[:3]
