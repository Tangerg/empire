"""Conservative complete stone-body separation, not a claim all foliage must avoid platform."""
from pathlib import Path
import json,os
from PIL import Image
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';d=json.load(open(p/os.environ.get('C07_WHITE_DESIGN','central-white-planter-design.json')));s=json.load(open(p/'central-platform-source.json'));scale=d['scale'];anchor=d['anchor'];world=d['worldAnchor'];lo=[world[k]+(d['stoneWholeBox'][0][k]-anchor[k])*scale for k in [0,1]];hi=[world[k]+(d['stoneWholeBox'][1][k]-anchor[k])*scale for k in [0,1]];bounds=[]
for m in s['members']:
 a=[min(q[k] for q in m['polygon']) for k in [0,1]];b=[max(q[k] for q in m['polygon']) for k in [0,1]];overlap=not(hi[0]<a[0] or b[0]<lo[0] or hi[1]<a[1] or b[1]<lo[1]);bounds.append({'member':m['id'],'bounds':[a,b],'overlapWithConservativeStoneBox':overlap})
# Frozen actual old stone face witness, independent of the rectangle-only guard.
uv=[515,730];rgba=Image.open(r/d['assetPath']).getpixel(tuple(uv));mapped=[world[k]+(uv[k]-anchor[k])*scale for k in [0,1]]
def inside(pt,poly):
 x,y=pt;hit=False
 for i,(a,b) in enumerate(zip(poly,poly[1:]+poly[:1])):
  if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:hit=not hit
 return hit
owners=[m['id'] for m in s['members'] if inside(mapped,m['polygon'])]
result={'scope':'Conservative entire visible stone box: successful disjoint bounds imply no material overlap. Old placement additionally retains an actual gray stone texel intruding onto actual platform polygon. Foliage allowed to overhang.','design':d,'stoneWorldBox':[lo,hi],'platformMembers':bounds,'fixedStoneWitness':{'texture':uv,'rgba':rgba,'mappedSource':mapped,'platformPolygonOwners':owners},'allDisjoint':all(not b['overlapWithConservativeStoneBox'] for b in bounds)};(p/os.environ.get('C07_WHITE_PLACEMENT_OUTPUT','central-white-planter-platform-clearance.json')).write_text(json.dumps(result,indent=2)+'\n');print('WHITE_PLANTER_STONE_PLATFORM_CLEARANCE',result['allDisjoint'],owners);assert result['allDisjoint'],'Independent ground-standing white planter stone body must stay separate from platform'
