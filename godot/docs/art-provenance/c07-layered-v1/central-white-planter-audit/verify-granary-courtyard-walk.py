"""Static mathematical replay of sprite support transforms, never engine commands.
Manual actual front-support boot ROIs, not whole-alpha bounds or airborne boot.
"""
from pathlib import Path
import json,runpy
from PIL import Image
r=Path('/workspace/scratch/84c9fe948755');out=r/'c07-static-preview'
b=runpy.run_path(str(out/'verify-granary-courtyard-boot.py'))
a=b['asset'];im=b['im'];s=a['presentationScale'];inside=b['inside'];bases=[b['main'],b['new']]
frames=[]
for index,roi in [(1,[280,515,380,575]),(3,[30,515,125,575])]:
 reg=a['frameRegions'][index];sole=[]
 for x in range(roi[0],roi[2]+1):
  ys=[y for y in range(roi[1],roi[3]+1) if im.getpixel((reg[0]+x,reg[1]+y))[3]>=200]
  if ys:sole.append([x,max(ys)])
 frames.append({'index':index,'roi':roi,'anchor':a['frameAnchors'][index],'sole':sole})
assert [len(f['sole']) for f in frames]==[89,86]
def mapped(cell,f,west):return [[16*cell[0]+8+(x-f['anchor'][0])*s*(-1 if west else 1),16*cell[1]+8+(y-f['anchor'][1])*s] for x,y in f['sole']]
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def on(a,b,p):return cross(a,b,p)==0 and min(a[0],b[0])<=p[0]<=max(a[0],b[0]) and min(a[1],b[1])<=p[1]<=max(a[1],b[1])
def intersects(a,b,c,d):
 x,y,z,w=cross(a,b,c),cross(a,b,d),cross(c,d,a),cross(c,d,b)
 return (x*y<0 and z*w<0) or any((on(a,b,c),on(a,b,d),on(c,d,a),on(c,d,b)))
def hits(a,z,poly):return inside(a,poly) or inside(z,poly) or any(intersects(a,z,p,poly[(i+1)%len(poly)]) for i,p in enumerate(poly))
legal=b['legal'];segments=[(a,z) for a in legal for z in legal if abs(a[0]-z[0])+abs(a[1]-z[1])==1]
violations=[];tested=0
for start,end in segments:
 dx=end[0]-start[0]
 for west in ([False,True] if dx==0 else [dx<0]):
  for f in frames:
   for foot,(p,q) in enumerate(zip(mapped(start,f,west),mapped(end,f,west))):
    tested+=1
    for part,poly in zip(['main','pier'],bases):
     if hits(p,q,poly):violations.append({'start':start,'end':end,'west':west,'frame':f['index'],'sole':f['sole'][foot],'part':part,'from':p,'to':q})
oldbases=[[(326,231),(333,227),(378,249),(371,253)],[(369,251),(379,248),(387,252),(377,257)]]
oldfixed=[]
for f,pt,west,t in [(frames[0],[338,564],True,.25),(frames[1],[40,559],False,.125)]:
 assert pt in f['sole'];p=mapped((24,15+t),{'anchor':f['anchor'],'sole':[pt]},west)[0];oldfixed.append({'frame':f['index'],'pixel':pt,'point':p,'oldMainPier':[inside(p,v) for v in oldbases],'newMainPier':[inside(p,v) for v in bases]})
assert oldfixed[0]['oldMainPier'][1] and oldfixed[1]['oldMainPier'][0]
result={'scope':'Pure static continuous segment-vs-footprint math, no gameplay or animation execution; manually identified visible shoe lower edge, not hidden 3D sole certification','frames':frames,'movementBasis':'AnimateMove retains mirror on vertical projected delta0; horizontal mirror follows delta; segment parity selects walk-a/b. Both possible phases checked.','canonicalLegalCells':legal,'directedAdjacentSegments':len(segments),'testedSoleSegments':tested,'baseFromCurrentFormalCap':bases,'violations':violations,'frozenOldFailureControls':oldfixed,'passed':not violations}
(out/'granary-courtyard-walk-corridor-result.json').write_text(json.dumps(result,indent=2)+'\n');print('WALK_CORRIDOR',len(segments),tested,'violations',len(violations),'fixed',oldfixed)
assert not violations,violations[:3]
