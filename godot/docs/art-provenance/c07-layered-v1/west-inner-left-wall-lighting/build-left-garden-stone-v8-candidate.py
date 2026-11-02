"""One bounded NEW rule-priority L enclosure; original visible feet are not copied.
Finite physical footprints derive from reviewed proposal. All surfaces export through
existing TexturedSurfaceExport; only merged support data will own runtime geometry.
"""
from pathlib import Path
import json,math
p=Path(__file__).parent
texture='res://assets/native/c07-layered-v1/west-inner-right-garden-stone-v2-candidate.png'
patches={'warm': [[285, 464], [376, 421], [376, 523], [285, 566]], 'cap': [[166, 201], [270, 153], [379, 204], [273, 248]]}
def quad(p,u0,v0,u1,v1):
 def at(u,v):return [(1-v)*((1-u)*p[0][k]+u*p[1][k])+v*((1-u)*p[3][k]+u*p[2][k])for k in (0,1)]
 return [at(u0,v0),at(u1,v0),at(u1,v1),at(u0,v1)]
def face(m,name,polygon,rows,cols,tint,cap=False):
 # Same closed physical face, with finite bevel bands and staggered broad masonry.
 # No repeated white pin-point seams, no new raster or transparency changes.
 width=abs(polygon[1][0]-polygon[0][0]);height=abs(polygon[3][1]-polygon[0][1])
 rowCount=1 if cap else rows
 for row in range(rowCount):
  v0=row/rowCount;v1=(row+1)/rowCount
  cuts=[0.];pos=-(4 if row%2 else 0);sizes=[10,8,11,9]
  if cols>1:
   index=0
   while pos<width:
    pos+=sizes[(index+row)%len(sizes)];index+=1
    if 0<pos<width:cuts.append(pos/width)
  cuts.append(1.)
  for i,(u0,u1) in enumerate(zip(cuts,cuts[1:])):
   du=min(.25/max(width,1),(u1-u0)/7);dv=min(.22/max(height,1),(v1-v0)/7)
   base=tuple(int(tint[k:k+2],16) for k in (0,2,4))
   variation=[0,-7,4,-3][(i+row*2)%4]
   body=''.join(f'{max(0,min(255,v+variation)):02x}' for v in base)
   # Mortar is one opaque supporting face; rounded stone prisms sit upon it.
   # Shared physical support/depth remains exactly the original finite wall.
   m.append({'id':f'{name}-{row}-{i}-bed','polygon':quad(polygon,u0,v0,u1,v1),'uv':patches['warm'],'tint':'88745e'})
   lo=u0+du;hi=u1-du;top=v0+dv;bottom=v1-dv
   cu=min(.45/max(width,1),(hi-lo)/5);cv=min(.45/max(height,1),(bottom-top)/5)
   outline=[(lo+cu,top),(hi-cu,top),(hi,top+cv),(hi,bottom-cv),(hi-cu,bottom),(lo+cu,bottom),(lo,bottom-cv),(lo,top+cv)]
   def mapPoint(poly,u,v):
    return [(1-v)*((1-u)*poly[0][k]+u*poly[1][k])+v*((1-u)*poly[3][k]+u*poly[2][k]) for k in (0,1)]
   texture=patches['cap' if cap else 'warm']
   points=[mapPoint(polygon,u,v) for u,v in outline]
   uv=[mapPoint(texture,(u-lo)/(hi-lo),(v-top)/(bottom-top)) for u,v in outline]
   if cap:
    m.append({'id':f'{name}-{row}-{i}-stone','polygon':points,'uv':uv,'tint':body})
    continue
   # One finite bevel ring gives the stone a top-lit relief rather than a flat tile.
   iu=min(.30/max(width,1),(hi-lo)/6);iv=min(.26/max(height,1),(bottom-top)/6)
   core=[(lo+cu+iu,top+iv),(hi-cu-iu,top+iv),(hi-iu,top+cv+iv),(hi-iu,bottom-cv-iv),(hi-cu-iu,bottom-iv),(lo+cu+iu,bottom-iv),(lo+iu,bottom-cv-iv),(lo+iu,top+cv+iv)]
   tones=['f8edda','e9dbc3','d5c3a8','c5b094','baa386','d3bda0','e8d7ba','f4e5cd']
   tones=[''.join(f'{min(255,round(int(t[k:k+2],16)*base[j]/255)):02x}' for j,k in enumerate((0,2,4))) for t in tones]
   for side in range(8):
    nxt=(side+1)%8;coords=[outline[side],outline[nxt],core[nxt],core[side]]
    m.append({'id':f'{name}-{row}-{i}-bevel-{side}','polygon':[mapPoint(polygon,u,v) for u,v in coords],'uv':[mapPoint(texture,(u-lo)/(hi-lo),(v-top)/(bottom-top)) for u,v in coords],'tint':tones[side]})
   m.append({'id':f'{name}-{row}-{i}-stone','polygon':[mapPoint(polygon,u,v) for u,v in core],'uv':[mapPoint(texture,(u-lo)/(hi-lo),(v-top)/(bottom-top)) for u,v in core],'tint':body})

components=[]
def wall(name,A,B,inside,height,tint):
 C=[B[k]+inside[k]for k in (0,1)];D=[A[k]+inside[k]for k in (0,1)];up=lambda q:[q[0],q[1]-height];m=[];cols=max(1,round(abs(B[0]-A[0])/8))
 face(m,'front',[up(A),up(B),B,A],3,cols,tint)
 face(m,'cap',[up(A),up(B),up(C),up(D)],1,cols,'fff5e6',True)
 # Only exposed western endpoint needs a vertical end; other ends join the finite piers.
 if name=='west-long':face(m,'west-end',[up(D),up(A),A,D],3,1,'e8dcca')
 # Physical lower envelope of the whole finite base, not sorted rear corners.
 polygon=[A,B,C,D];curve=[]
 for x in sorted(set(q[0]for q in polygon)):
  ys=[]
  for u,v in zip(polygon,polygon[1:]+polygon[:1]):
   if min(u[0],v[0])<=x<=max(u[0],v[0]):
    if u[0]==v[0]:ys.extend([u[1],v[1]])
    else:ys.append(u[1]+(x-u[0])*(v[1]-u[1])/(v[0]-u[0]))
  curve.append([x,max(ys)])
 components.append({'id':name,'textureResourcePath':texture,'supportLine':curve,'members':m,'physicalFootPolygon':[A,B,C,D]})
def pier(name,P,height):
 left,front,right,back=P;up=lambda q:[q[0],q[1]-height];m=[]
 face(m,'west',[up(left),up(front),front,left],4,1,'cfbda4');face(m,'east',[up(front),up(right),right,front],4,1,'fff0d6');face(m,'cap',[up(left),up(back),up(right),up(front)],1,1,'fff7e8',True)
 components.append({'id':name,'textureResourcePath':texture,'supportLine':[left,front,right],'members':m,'physicalFootPolygon':P})
wall('west-long',[300,454],[352,480],[4,-2],19,'cbbba5')
pier('middle',[[352,480],[356,482],[360,480],[356,478]],28)
wall('near-long',[356,482],[392,500],[4,-2],19,'cbbba5')
pier('near',[[392,500],[398,503],[404,500],[398,497]],28)
wall('east-return',[404,500],[430,487],[-4,-2],19,'fff0d6')
for c in components:(p/f"left-garden-stone-{c['id']}-source.json").write_text(json.dumps(c,indent=2)+'\n')
(p/'left-garden-stone-new-layout.json').write_text(json.dumps({'scope':'NEW straight L plan, continuous physical joins, rule priority; old near foot516 and old whole mask front496 are neither current feet nor ownership. Far back courtyard/tree/trellis absent.','components':components,'material':'Unedited existing approved right-garden RGBA, only certified A>=253 warm/cap UV patches. No generated raster edit.','oldBackControl':'Torren23,31 foot504 is now in front of near pillar foot503; keep original point record but do not require it to be hidden under this changed geometry.'},indent=2)+'\n')
