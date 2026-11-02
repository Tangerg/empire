from pathlib import Path
import json,struct,math
p=Path(__file__).resolve().parent
load=lambda n:json.loads((p/n).read_text());write=lambda n,v:(p/n).write_text(json.dumps(v,indent=2)+'\n');f=lambda x:struct.unpack('f',struct.pack('f',x))[0]
g=load('northeast-frozen-v2-sections.json');source=load('northeast-interface-geometry.json');s=f(g['scale']);t=list(map(f,g['translation']));localOrigin=load('northeast-baseline-registration.json')['localOrigin']
pts=[v for m in source['members'] for v in m['polygon']];newOrigin=[math.floor(min(v[i] for v in pts)) for i in range(2)]
result={'schema':1,'supports':[],'members':[]}
for label,file,origin in [('retained','northeast-interface-base-surface.json',[0,0]),('interface','northeast-interface-new-surface.json',newOrigin)]:
 surface=load(file)
 def world(v):return [f(f(f(f(v[i])+origin[i])*s+t[i])-localOrigin[i]) for i in range(2)]
 for row in surface['supports']:
  row['id']=label+'-'+row['id'];row['frontPixels']=world(row['frontPixels']);result['supports'].append(row)
 for row in surface['members']:
  row['id']=label+'-'+row['id'];row['supportId']=label+'-'+row['supportId'];row['polygonPixels']=[world(v) for v in row['polygonPixels']];result['members'].append(row)
write('northeast-interface-surface.json',result)
for name in ['objects','ground','scene']:
 d=load(f'northeast-baseline-{name}.json')
 if name=='objects':d['objects']['northeast-static-study']['surfaceGeometryPath']='res://northeast-interface-surface.json'
 if name=='ground':d['objectCatalogPath']='res://northeast-interface-objects.json'
 if name=='scene':d['assembledGroundPath']='res://northeast-interface-ground.json'
 write(f'northeast-interface-{name}.json',d)
print('UNSELECTED_INTERFACE',len(result['supports']),len(result['members']))
