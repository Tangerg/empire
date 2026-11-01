"""Bounded new cargo layout within original blocked responsibility bands.
Closed boxes use existing plank UV; the bale instance reuses its exact existing
asset/support fact. Hidden cargo layout is authored, not old-pixel recovery.
"""
from pathlib import Path
import json,copy
out=Path('/workspace/scratch/84c9fe948755/c07-static-preview')
source=json.load(open(out/'granary-door-stock-geometries.json'))['containers'][0]
boxes=[]
for name,left,near,right,back,height in [
 ('middle-crate',[1132,774],[1155,785],[1174,774],[1151,760],30),
 ('east-crate',[1246,786],[1268,796],[1285,787],[1266,777],18)]:
 top=lambda p:[p[0],p[1]-height]
 polys=[[top(left),top(near),near,left],[top(near),top(right),right,near],[top(left),top(back),top(right),top(near)]]
 members=[]
 for old,poly in zip(source['members'],polys):
  m=copy.deepcopy(old);m['polygon']=poly; m['uv']=([[220,780],[360,850],[360,890],[220,820]] if m['id']=='left-face' else [[600,810],[760,750],[760,790],[600,850]]);members.append(m)
 # Framing stays on each closed face; no holes or changed outer silhouette/feet.
 def quad(poly,u0,v0,u1,v1):
  def point(u,v):return [(1-v)*((1-u)*poly[0][i]+u*poly[1][i])+v*((1-u)*poly[3][i]+u*poly[2][i]) for i in (0,1)]
  return [point(u0,v0),point(u1,v0),point(u1,v1),point(u0,v1)]
 panels=list(members);members=[]
 for face in panels:
  us=[0,.075,.327,.339,.660,.672,.925,1];vs=[0,.085,.915,1]
  for iu in range(len(us)-1):
   for iv in range(len(vs)-1):
    rect=(us[iu],vs[iv],us[iu+1],vs[iv+1]);m=copy.deepcopy(face);m['id']=face['id']+f'-board-part-{iu}-{iv}';m['polygon']=quad(face['polygon'],*rect)
    frame=iv!=1 or iu in (0,6);joint=iv==1 and iu in (2,4)
    m['uv']=quad(face['uv'],*rect)
    if frame:m['tint']='9a7855' if face['id']=='left-face' else 'b89569'
    elif joint:m['tint']='a58867'
    members.append(m)
 obj={'id':name,'namePrefix':'SouthernSupply'+name.title().replace('-',''),'textureResourcePath':source['textureResourcePath'],'scope':'Authored closed crate within original cargo responsibility region. No original hidden-foot accuracy claim.','supportLine':[[left[0]-1,left[1]],left,near,right,[right[0]+1,right[1]]],'members':members,'footprint':[left,near,right,back]};boxes.append(obj)
 (out/f'southern-supply-stock-{name}-source.json').write_text(json.dumps(obj,indent=2)+'\n')
(out/'southern-supply-stock-layout.json').write_text(json.dumps({'scope':'Three bounded cargo instances for previously missing main-body foundation responsibilities. Other racks/left stacks/scenery still absent.','boxes':boxes,'bales':{'id':'southern-supply-right-bales','assetId':'granary-large-grain-v1','contactSource':[1196,780],'scale':.035},'requiredPreviouslyUncoveredCells':[[72,46],[73,47],[74,47],[79,48]],'originalCargoGroups':['supply-middle-crate-stack-base','supply-right-grain-stack-base','supply-east-sack-crate-ground']},indent=2)+'\n')
