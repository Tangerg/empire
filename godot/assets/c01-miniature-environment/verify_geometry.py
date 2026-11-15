from pathlib import Path
import struct,json,itertools
import numpy as np
R=Path(__file__).resolve().parent
def matrix(n):
 if 'matrix'in n:return np.array(n['matrix']).reshape((4,4),order='F')
 x,y,z,w=n.get('rotation',[0,0,0,1]);m=np.eye(4);m[:3,:3]=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]];m[:3,:3]=m[:3,:3]@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0]);return m
results=[]
for asset in json.loads((R/'manifest.json').read_text())['assets']:
 p=R/asset['path'].split('/')[-1];raw=p.read_bytes();length,typ=struct.unpack_from('<II',raw,12);j=json.loads(raw[20:20+length]);assert raw[:4]==b'glTF';assert struct.unpack_from('<I',raw,8)[0]==len(raw)
 points=[];triangles=0;names=[];surface_count=sum(len(m['primitives']) for m in j['meshes'])
 if 'surfaces' in asset:assert surface_count==asset['surfaces']
 if 'materials' in asset:assert len(j.get('materials',[]))==asset['materials']
 def walk(i,parent):
  global triangles
  n=j['nodes'][i];t=parent@matrix(n);names.append(n.get('name',''))
  if 'mesh'in n:
   for prim in j['meshes'][n['mesh']]['primitives']:
    a=j['accessors'][prim['attributes']['POSITION']]
    for c in itertools.product(*zip(a['min'],a['max'])):points.append((t@np.array([*c,1]))[:3])
    triangles+=j['accessors'][prim['indices']]['count']//3
  for child in n.get('children',[]):walk(child,t)
 for i in j['scenes'][j.get('scene',0)]['nodes']:walk(i,np.eye(4))
 points=np.array(points);lo=points.min(0);hi=points.max(0);w,d=asset['footprint_cells'];assert max(abs(lo[0]),abs(hi[0]))<=w/2+.001,(p,lo,hi);assert max(abs(lo[2]),abs(hi[2]))<=d/2+.001,(p,lo,hi);assert asset['roof_node'] in names and asset['wall_node'] in names;assert lo[1]>=-.001
 results.append({'file':p.name,'bytes':len(raw),'triangles':triangles,'aabb_min':lo.tolist(),'aabb_max':hi.tolist(),'names':names,'fits_wall_footprint':True,'surfaces':surface_count,'materials':len(j.get('materials',[]))})
print(json.dumps(results,indent=2));(R/'geometry-verification.json').write_text(json.dumps(results,indent=2))
