from pathlib import Path
import json,math,numpy as np
from PIL import Image
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';s=.14505957;t=np.array([54.7883672,-4.29011913]);g=json.loads((p/'northwest-gate-v1-initial-sections.json').read_text());a=np.asarray(Image.open(r/'empire-c01/godot/assets/native/c07-layered-v1/northwest-gate-v1-stone-candidate.png'))[:,:,3]
g['excludePolygons']=[[[420,820],[525,820],[525,1186],[420,1186]],[[760,840],[908,840],[908,1030],[760,1030]]];g['scope']='UNBOUND native architecture surface reconstruction candidate; original RGBA regions reused as UV, rules unchanged'
(p/'northwest-native-base.json').write_text(json.dumps(g,indent=2)+'\n')
def src(v):return (np.array(v)*s+t).tolist()
def make(name,quads,support):
 return {'textureResourcePath':g['textureResourcePath'],'namePrefix':name,'supportLine':[src(v) for v in support],'members':[{'id':str(i),'polygon':[src(v) for v in pos],'uv':uv,'tint':'ffffff'}for i,(pos,uv)in enumerate(quads)]}
left=[]
for y0,y1,dx0,dx1,dy0,dy1 in [(820,880,0,-10,0,0),(880,1186,-10,-10,0,6)]:
 uv=[[420,y0],[525,y0],[525,y1],[420,y1]];pos=[[420,y0+dy0],[525+dx0,y0+dy0],[525+dx1,y1+dy1],[420,y1+dy1]];left.append((pos,uv))
def low(x):return int(np.where(a[:,x]>=128)[0][-1])
# Lower quad affine mapping; derive actual contact from unchanged alpha edge.
support=[]
for x in range(420,526):
 y=low(x);support.append([420+(x-420)*95/105, y+(y-880)*6/306])
L=make('NWLeftJambSurface',left,support)
uv=[[760,840],[908,840],[908,1030],[760,1030]];pos=[[760,840],[908,840],[908,1058],[760,1058]]
support=[[x,low(x)+(low(x)-840)*28/190]for x in range(760,909)]
R=make('NWRightJambSurface',[(pos,uv)],support)
for c in [L,R]:
 pts=[v for m in c['members'] for v in m['polygon']];end=math.ceil(max(v[0] for v in pts))+1;a0,b0=c['supportLine'][-2:];dy=(b0[1]-a0[1])/(b0[0]-a0[0]);c['supportLine'].append([end,b0[1]+(end-b0[0])*dy]);c['supportScope']='Physical transformed lower alpha profile; final segment extrapolates only upper overhang/subpixel strip margin, not new material.'
(p/'northwest-native-surfaces.json').write_text(json.dumps({'components':[L,R]},indent=2)+'\n')
