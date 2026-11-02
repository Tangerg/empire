from pathlib import Path
import json,math
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');source=json.load(open(p/'central-rear-east-bed-native-source.json'));O=source['origin'];merged={'schema':1,'supports':[],'members':[]}
for part in source['parts']:
 n=part['id'];d=json.load(open(p/f'rear-bed-{n}-surface.json'));lo=[math.floor(min(v[k] for v in part['polygon'])) for k in [0,1]]
 for s in d['supports']:
  s['id']=n+'-'+s['id'];s['frontPixels']=[s['frontPixels'][k]+lo[k]-O[k] for k in [0,1]]
  if n=='rooted-foliage':
   s['frontPixels']=[793-O[0],455-O[1]];s['id']='foliage-physical-root'
   if any(z['id']==s['id'] for z in merged['supports']):continue
  merged['supports'].append(s)
 for m in d['members']:
  m['id']=n+'-'+m['id'];m['supportId']='foliage-physical-root' if n=='rooted-foliage' else n+'-'+m['supportId'];m['polygonPixels']=[[v[k]+lo[k]-O[k] for k in [0,1]] for v in m['polygonPixels']];merged['members'].append(m)
(p/'central-rear-east-bed-native-surface.json').write_text(json.dumps(merged,indent=2)+'\n');print(len(merged['supports']),len(merged['members']))
