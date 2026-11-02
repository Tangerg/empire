from pathlib import Path
import json,copy
p=Path(__file__).resolve().parent
load=lambda n:json.loads((p/n).read_text());write=lambda n,v:(p/n).write_text(json.dumps(v,indent=2)+'\n')
baseline=load('northeast-baseline-surface.json');candidate=load('northeast-interface-surface.json')
for name,column in [('left-post',1035),('right-beam',1139)]:
 result=copy.deepcopy(candidate);row=copy.deepcopy(next(v for v in baseline['members'] if v['supportId']==f'right-lookout-and-leg-{column}'))
 row['id']='negative-original-'+name;row['supportId']='retained-'+row['supportId'];assert any(v['id']==row['supportId'] for v in result['supports'])
 result['members'].append(row);write(f'northeast-negative-{name}-surface.json',result)
 for kind in ['objects','ground','scene']:
  d=load(f'northeast-interface-{kind}.json')
  if kind=='objects':d['objects']['northeast-static-study']['surfaceGeometryPath']=f'res://northeast-negative-{name}-surface.json'
  if kind=='ground':d['objectCatalogPath']=f'res://northeast-negative-{name}-objects.json'
  if kind=='scene':d['assembledGroundPath']=f'res://northeast-negative-{name}-ground.json'
  write(f'northeast-negative-{name}-{kind}.json',d)
 print(name,'restores old original column',column,'without changing support count')
