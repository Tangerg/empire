"""Offline authored timber geometry; original visible feet under review.
Runtime owns exported explicit surface, never this generator in parallel.
"""
import json,math
from pathlib import Path
r=Path('/workspace/scratch/84c9fe948755');out=r/'c07-static-preview'
texture='res://assets/native/c07-layered-v1/training-fence-v2-rejected.png'
uvrect=[95,180,132,600]
members=[]
def add(name,poly,tint,axis):
 a,b=axis;length=math.dist(a,b);along=[(b[i]-a[i])/length for i in range(2)];across=[-along[1],along[0]]
 uv=[]
 for p in poly:
  d=[p[i]-a[i] for i in range(2)];uv.append([max(95.5,min(130.5,113.5+sum(d[i]*across[i] for i in range(2))*4)),max(180.5,min(598.5,190+sum(d[i]*along[i] for i in range(2))*10))])
 members.append({'id':name,'polygon':poly,'uv':uv,'tint':tint})
# Visible original wood edges; right hidden completion explicitly limited to half a pixel below y223.
posts=[(291,208.5,26.5),(309,219,26),(325,223.5,28.5)]
for i,(x,y,height) in enumerate(posts):
 top=y-height
 add(f'post-{i}-front',[[x-1.5,top],[x+1.5,top+1],[x+1.5,y],[x-1.5,y-1]],'c3c6cc',[[x,top],[x,y]])
 add(f'post-{i}-side',[[x+1.5,top+1],[x+2.5,top],[x+2.5,y-1],[x+1.5,y]],'8f969e',[[x+2,top],[x+2,y]])
 add(f'post-{i}-cap',[[x-1.5,top],[x-.5,top-1],[x+2.5,top],[x+1.5,top+1]],'d9e2ed',[[x-1.5,top],[x+2.5,top]])
# Original visible beam direction, extrapolated only to the immediate post joint.
for name,a,b in [('left-upper',[292,190],[307.5,198.35]),('right-upper',[310.5,200.83],[323.5,205.17]),('left-lower',[292.5,204.65],[307.5,215.04]),('right-lower',[310.5,215.32],[323.5,221.23])]:
 add(name,[a,b,[b[0],b[1]+2],[a[0],a[1]+2]],'b7bdc5',[a,b])
 add(name+'-top',[[a[0],a[1]-.6],[b[0],b[1]-.6],b,a],'d1d9e3',[a,b])
for i,(a,b) in enumerate([([293,204.5],[306,199.5]),([311.5,215],[323,207])]):
 dx=b[0]-a[0];dy=b[1]-a[1];ln=math.hypot(dx,dy);n=[-dy/ln*.85,dx/ln*.85]
 polygon=[[[293,203.75],[306,198.75],[307,200.25],[294,205.25]],[[311,214.5],[322,206.25],[324,207.75],[312,215.5]]][i]
 add(f'bay-{i}-brace',polygon,'a7b2c0',[a,b])
support=[[288,206.75],[291,208.5],[309,219],[325,223.5],[328,224.34375]]
d={'textureResourcePath':texture,'supportLine':support,'members':members,'scope':'Source-informed timber candidate; visible wood-edge feet approximately +/-1px, right foot half-pixel hidden completion new design; diagonal connection design not pixel-identical reconstruction; no gameplay.'}
(out/'granary-courtyard-timber-source.json').write_text(json.dumps(d,indent=2)+'\n');print('timber source candidate only',len(members),'members')
