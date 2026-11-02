"""One bounded NEW rule-priority L enclosure; original visible feet are not copied.
Finite physical footprints derive from reviewed proposal. All surfaces export through
existing TexturedSurfaceExport; only merged support data will own runtime geometry.
"""
from pathlib import Path
import json,math
p=Path(__file__).parent
texture='res://assets/native/c07-layered-v1/west-inner-right-garden-stone-v2-candidate.png'
patches={'warm':[[300,650],[350,650],[350,720],[300,720]],'cap':[[245,190],[285,190],[285,215],[245,215]]}
def quad(p,u0,v0,u1,v1):
 def at(u,v):return [(1-v)*((1-u)*p[0][k]+u*p[1][k])+v*((1-u)*p[3][k]+u*p[2][k])for k in (0,1)]
 return [at(u0,v0),at(u1,v0),at(u1,v1),at(u0,v1)]
def face(m,name,polygon,rows,cols,tint,cap=False):
 # Closed non-overlapping masonry/mortar partition. Thin dark joint is opaque stone,
 # never a transparent hole; every cap segment also has a visible joint.
 for row in range(rows):
  off=.5 if row%2 else 0;cuts=sorted(set([0.,1.]+[(i+off)/cols for i in range(cols+1)if 0<(i+off)/cols<1]))
  for i,(u0,u1)in enumerate(zip(cuts,cuts[1:])):
   v0=row/rows;v1=(row+1)/rows;du=min(.009,(u1-u0)/8);dv=.012
   for suffix,r,color in [('block',(u0+du,v0+dv,u1-du,v1-dv),tint),('left',(u0,v0,u0+du,v1),'8c7962'),('right',(u1-du,v0,u1,v1),'8c7962'),('upper',(u0+du,v0,u1-du,v0+dv),'eee0c8'),('lower',(u0+du,v1-dv,u1-du,v1),'9f8b73')]:m.append({'id':f'{name}-{row}-{i}-{suffix}','polygon':quad(polygon,*r),'uv':patches['cap'if cap else'warm'],'tint':color})
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
 face(m,'west',[up(left),up(front),front,left],4,1,'d1c4b6');face(m,'east',[up(front),up(right),right,front],4,1,'fff0d6');face(m,'cap',[up(left),up(back),up(right),up(front)],1,1,'fff7e8',True)
 components.append({'id':name,'textureResourcePath':texture,'supportLine':[left,front,right],'members':m,'physicalFootPolygon':P})
wall('west-long',[300,454],[352,480],[4,-2],19,'c9bcae')
pier('middle',[[352,480],[356,482],[360,480],[356,478]],28)
wall('near-long',[356,482],[392,500],[4,-2],19,'c9bcae')
pier('near',[[392,500],[398,503],[404,500],[398,497]],28)
wall('east-return',[404,500],[430,487],[-4,-2],19,'fff0d6')
for c in components:(p/f"left-garden-stone-{c['id']}-source.json").write_text(json.dumps(c,indent=2)+'\n')
(p/'left-garden-stone-new-layout.json').write_text(json.dumps({'scope':'NEW straight L plan, continuous physical joins, rule priority; old near foot516 and old whole mask front496 are neither current feet nor ownership. Far back courtyard/tree/trellis absent.','components':components,'material':'Unedited existing approved right-garden RGBA, only certified A>=253 warm/cap UV patches. No generated raster edit.','oldBackControl':'Torren23,31 foot504 is now in front of near pillar foot503; keep original point record but do not require it to be hidden under this changed geometry.'},indent=2)+'\n')
