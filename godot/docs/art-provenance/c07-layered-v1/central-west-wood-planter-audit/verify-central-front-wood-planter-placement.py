"""Conservative whole stone body separation; foliage may overlap other foliage."""
from pathlib import Path
import json,os
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview')
d=json.load(open(p/os.environ.get('C07_WOOD_DESIGN','central-front-wood-planter-design.json')))
def box(d):
 return [[d['worldAnchor'][k]+(v[k]-d['anchor'][k])*d['scale'] for k in [0,1]] for v in d['stoneWholeBox']]
def disjoint(a,b):return any(a[1][k]<b[0][k] or b[1][k]<a[0][k] for k in [0,1])
a=box(d);white=box(json.load(open(p/'central-white-planter-design.json')));platform=json.load(open(p/'central-platform-source.json'))
rows=[]
for m in platform['members']:
 b=[[min(q[k] for q in m['polygon']) for k in [0,1]],[max(q[k] for q in m['polygon']) for k in [0,1]]]
 rows.append({'id':m['id'],'bounds':b,'disjoint':disjoint(a,b)})
r={'scope':'Entire visible stone-body conservative rectangles; not only center/foot samples. Separate physical ground bases; overlapping foliage is permitted.','woodStoneBox':a,'whiteStoneBox':white,'whiteDisjoint':disjoint(a,white),'platform':rows,'passed':disjoint(a,white) and all(v['disjoint'] for v in rows)}
(p/os.environ.get('C07_WOOD_PLACEMENT_OUTPUT','central-front-wood-planter-placement.json')).write_text(json.dumps(r,indent=2)+'\n');print('FRONT_WOOD_STONE_SEPARATION',r['passed']);assert r['passed'],'Front wood planter complete stone body must separate from white planter and platform'
