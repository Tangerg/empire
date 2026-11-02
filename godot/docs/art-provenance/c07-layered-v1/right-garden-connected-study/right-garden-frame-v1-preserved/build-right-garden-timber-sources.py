"""Offline new coherent garden trellis. Runtime surface output is the only selected fact.
Original lower visible wood witnesses inform posts; hidden bases/roof plane are explicitly new design.
"""
from pathlib import Path
import json,math
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');texture='res://assets/native/c07-layered-v1/training-fence-v2-rejected.png';sources={}
def source(name,line):
 d={'textureResourcePath':texture,'supportLine':line,'members':[],'scope':'New coherent open trellis; hidden feet explicit design. Original lower wood samples retained, no original roof exactness claimed.'};sources[name]=d;return d
front=source('front',[[547,451.5],[585,432.5]]);back=source('back',[[539,447.5],[577,428.5]])
def add(d,name,poly,tint,axis):
 a,b=axis;length=math.dist(a,b);along=[(b[i]-a[i])/length for i in range(2)];across=[-along[1],along[0]];uv=[]
 for q in poly:
  v=[q[i]-a[i]for i in range(2)];uv.append([max(95.5,min(130.5,113.5+sum(v[i]*across[i]for i in range(2))*4)),max(180.5,min(598.5,190+sum(v[i]*along[i]for i in range(2))*10))])
 d['members'].append({'id':name,'polygon':poly,'uv':uv,'tint':tint})
def post(d,name,x,y):
 top=y-28
 add(d,name+'-front',[[x-1,top],[x+1,top+.5],[x+1,y],[x-1,y-1]],'c3c6cc',[[x,top],[x,y]])
 add(d,name+'-side',[[x+1,top+.5],[x+1.8,top],[x+1.8,y-1],[x+1,y]],'8f969e',[[x+1.5,top],[x+1.5,y]])
 add(d,name+'-cap',[[x-1,top],[x,top-.5],[x+1.8,top],[x+1,top+.5]],'d9e2ed',[[x-1,top],[x+1.8,top]])
def beam(d,name,a,b):
 add(d,name+'-front',[a,b,[b[0],b[1]+1.5],[a[0],a[1]+1.5]],'b7bdc5',[a,b])
 add(d,name+'-top',[[a[0],a[1]-.6],[b[0],b[1]-.6],b,a],'d1d9e3',[a,b])
for i,(x,y)in enumerate([(550,450),(563,443.5),(582,434)]):post(front,'front-post-'+str(i),x,y)
for i,(x,y)in enumerate([(542,446),(574,430)]):post(back,'back-post-'+str(i),x,y)
beam(front,'front-top-rail',[548.5,422.75],[584,405]);beam(back,'back-top-rail',[540.5,418.75],[576,401])
# Two actual diagonally oriented top frame members join the post caps, not a solid roof plane.
for name,a,b in [('left-cross',[540.5,418.25],[552,424]),('right-cross',[572.5,402.25],[584,408])]:
 line=[[a[0]-1,a[1]+27.5],[b[0]+1,b[1]+28.5]];d=source(name,line);beam(d,name,a,b)
# Narrow intermediate slats rest on both side rails; no opaque rectangle fills the holes.
for i,x in enumerate([550,558,566]):
 y=418-(x-542)*.5;a=[x,y-.7];b=[x+8,y+3.3];d=source('slat-'+str(i),[[x-1,y+27.5],[x+9,y+32.5]]);beam(d,'open-slat-'+str(i),a,b)
for name,d in sources.items():(p/f'right-garden-timber-{name}-source.json').write_text(json.dumps(d,indent=2)+'\n')
(p/'right-garden-timber-source-index.json').write_text(json.dumps({'scope':'Explicit NEW compact coherent trellis; original upper rafter silhouette is not pixel-reconstructed.','sourceNames':list(sources),'baseDomain':[[550,450],[582,434],[574,430],[542,446]],'postHeight':28,'fixedOriginalWoodWitnesses':[[550,441],[563,437],[581,420]],'absent':'Foliage and rooted bed not yet added; no claim complete garden.'},indent=2)+'\n')
print(len(sources),'structural support owners',sum(len(d['members'])for d in sources.values()),'members')
