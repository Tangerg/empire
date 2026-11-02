"""Offline, exact-offset join of reviewed plane/riser exports; runtime advances only merged geometry."""
from pathlib import Path
import json,math
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');result={'schema':1,'supports':[],'members':[]}
for name in ['horizontal','riser-0','riser-1','riser-2']:
 s=json.load(open(p/f'central-platform-{name}-source.json'));d=json.load(open(p/f'central-platform-{name}-surface.json'));points=[v for m in s['members'] for v in m['polygon']];origin=[math.floor(min(v[k] for v in points)) for k in [0,1]];delta=[origin[0]-714,origin[1]-448]
 for v in d['supports']:v['id']=name+'-'+v['id'];v['frontPixels']=[v['frontPixels'][k]+delta[k] for k in [0,1]];result['supports'].append(v)
 for v in d['members']:v['id']=name+'-'+v['id'];v['supportId']=name+'-'+v['supportId'];v['polygonPixels']=[[q[k]+delta[k] for k in [0,1]] for q in v['polygonPixels']];result['members'].append(v)
(p/'central-platform-surface.json').write_text(json.dumps(result,indent=2)+'\n');print('CENTRAL_PLATFORM_MERGE',len(result['supports']),len(result['members']))
