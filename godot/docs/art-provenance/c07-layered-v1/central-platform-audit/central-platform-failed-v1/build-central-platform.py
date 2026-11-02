"""Bounded source-informed low platform. Hidden rear/centre connections are new design.
No planters, benches, foreground corner post or changes to logical terrain.
Runtime ownership belongs only to exported surface, not this offline provenance.
"""
from pathlib import Path
import json
out=Path('/workspace/scratch/84c9fe948755/c07-static-preview')
texture='res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png'
# Reuse actual warm stone; broad solid interiors, not anti-aliased silhouette edges.
left=[[410,620],[490,660],[490,900],[410,860]]
right=[[530,640],[620,600],[620,860],[530,900]]
top=[[300,1140],[400,1190],[400,1205],[300,1155]]
members=[]
def face(name,p,uv,tint='ffffff'):
 members.append({'id':name,'polygon':p,'uv':uv,'tint':tint})
def down(p,h):return [p[0],p[1]+h]
upper=[[716,475],[767,501],[820,475]]
face('upper-platform',[[766,448],upper[2],upper[1],upper[0]],[[530,600],[620,600],[620,900],[530,900]],'ede2ce')
for n,edge in enumerate([upper,[[715,481],[767,508],[822,481]],[[714,487],[767,514],[824,487]]]):
 for side,uv in [(0,left),(1,right)]:
  a,b=edge[side:side+2]; face(f'step-{n}-riser-{side}',[a,b,down(b,4),down(a,4)],uv)
 if n:
  previous=[down(p,4) for p in prev]
  for side in range(2):
   face(f'step-{n}-tread-{side}',[previous[side],previous[side+1],edge[side+1],edge[side]],top,'fff3d9')
 prev=edge
source={'textureResourcePath':texture,'supportLine':[[714,491],[767,518],[824,491]],'members':members,'scope':'Source-informed low outer platform/three steps only. Hidden rear and centre connections are new design. Independent planters/benches/front corner post absent; no gameplay or terrain changes.'}
(out/'central-platform-source.json').write_text(json.dumps(source,indent=2)+'\n');print('CENTRAL_PLATFORM_SOURCE',len(members))
