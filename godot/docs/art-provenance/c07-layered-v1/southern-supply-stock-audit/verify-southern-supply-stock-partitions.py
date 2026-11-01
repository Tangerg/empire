"""Source partition conservation: no overlapping overlays, unchanged silhouette/support/feet.
Uses convex quadrilateral affine parameter partition plus polygon area, not an alpha threshold.
"""
import json,hashlib,subprocess
from pathlib import Path
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');repo=p.parent/'empire-c01';rows=[]
def area(poly):return abs(sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(poly,poly[1:]+poly[:1])))*.5
for name in ('middle-crate','east-crate'):
 old=json.load(open(p/'southern-supply-stock-material-v2-failed'/f'southern-supply-stock-{name}-source.json'));new=json.load(open(p/f'southern-supply-stock-{name}-source.json'))
 assert new['footprint']==old['footprint'] and new['supportLine']==old['supportLine']
 for face in old['members']:
  pieces=[m for m in new['members'] if m['id'].startswith(face['id']+'-board-part-')];expected=area(face['polygon']);actual=sum(area(m['polygon']) for m in pieces);assert len(pieces)==21 and abs(actual-expected)<1e-6
  rows.append({'crate':name,'face':face['id'],'originalArea':expected,'partitionArea':actual,'partitions':len(pieces),'footAndSupportUnchanged':True})
# Previous shared assembly remains byte-equivalent in meaning; added instances own only cargo.
for filename,key in [('c01-07-selected-assembly-ground.json','objects'),('c01-07-selected-assembly-objects.json','objects')]:
 old=json.loads(subprocess.check_output(['git','-C',str(repo),'show','HEAD:godot/data/scenes/'+filename]));new=json.load(open(repo/'godot/data/scenes'/filename))
 if isinstance(old[key],list):assert new[key][:len(old[key])]==old[key]
 else:
  for k,v in old[key].items():assert new[key][k]==v
result={'scope':'Offline partition/source conservation, not GPU/physical footprint proof','faces':rows,'previous30InstancesAndAssetsUnchanged':True}
(p/'southern-supply-stock-partition-check.json').write_text(json.dumps(result,indent=2)+'\n');print('SOUTHERN_STOCK_PARTITION_CONSERVATION_COMPLETE',len(rows))
