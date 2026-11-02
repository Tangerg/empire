"""Bounded source-informed low platform. Hidden rear/centre connections are new design.
No planters, benches, foreground corner post or changes to logical terrain.
Runtime ownership belongs only to exported surface, not this offline provenance.
"""
from pathlib import Path
import json
out=Path('/workspace/scratch/84c9fe948755/c07-static-preview')
texture='res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png'
# Reuse actual warm stone; broad solid interiors, not anti-aliased silhouette edges.
left=[[300,1245],[455,1320],[455,1335],[300,1260]]
right=[[550,1310],[780,1195],[780,1212],[550,1327]]
top=[[410,650],[490,650],[490,850],[410,850]]
members=[]
def face(name,p,uv,tint='ffffff'):
 members.append({'id':name,'polygon':p,'uv':uv,'tint':tint})
def down(p,h):return [p[0],p[1]+h]
upper=[[716,475],[767,501],[820,475]]
face('upper-platform',[[766,448],upper[2],upper[1],upper[0]],top,'ffffff')
for n,edge in enumerate([upper,[[715,481],[767,508],[822,481]],[[714,487],[767,514],[824,487]]]):
 for side,uv in [(0,left),(1,right)]:
  a,b=edge[side:side+2]; face(f'step-{n}-riser-{side}',[a,b,down(b,4),down(a,4)],uv)
 if n:
  previous=[down(p,4) for p in prev]
  for side in range(2):
   face(f'step-{n}-tread-{side}',[previous[side],previous[side+1],edge[side+1],edge[side]],top,'ffffff')
 prev=edge
source={'textureResourcePath':texture,'supportLine':[[714,491],[767,518],[824,491]],'members':members,'scope':'Source-informed low outer platform/three steps only. Hidden rear and centre connections are new design. Independent planters/benches/front corner post absent; no gameplay or terrain changes.'}
(out/'central-platform-source.json').write_text(json.dumps(source,indent=2)+'\n');print('CENTRAL_PLATFORM_SOURCE',len(members))

# Horizontal carrying faces sort at their own projected plane row; each vertical step has its own actual feet.
components=[]
plane={**source,'supportMode':'horizontal-plane','members':[m for m in members if 'riser' not in m['id']]}
components.append(('horizontal',plane))
for n,edge in enumerate([upper,[[715,481],[767,508],[822,481]],[[714,487],[767,514],[824,487]]]):
 components.append((f'riser-{n}',{**source,'supportLine':[down(p,4) for p in edge],'members':[m for m in members if m['id'].startswith(f'step-{n}-riser')]}))
for name,data in components:(out/f'central-platform-{name}-source.json').write_text(json.dumps(data,indent=2)+'\n')
