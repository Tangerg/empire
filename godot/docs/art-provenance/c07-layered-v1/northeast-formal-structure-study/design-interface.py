"""One bounded new frame/floor interface; not a claim of original-pixel restoration."""
from pathlib import Path
import json,copy,math
p=Path(__file__).resolve().parent
load=lambda n:json.loads((p/n).read_text());write=lambda n,v:(p/n).write_text(json.dumps(v,indent=2)+'\n')
g=load('northeast-frozen-v2-sections.json')
# This complete visible replacement domain includes the old inseparable beam/floor shadow.
# Its top is the retained roof's underside; the lower edge meets the retained outer plinth.
cut=[[995,392],[1118,442],[1252,392],[1252,522],[1118,566],[995,522]]
basis=copy.deepcopy(g);basis['excludePolygons']=[cut];write('northeast-interface-base-sections.json',basis)
wood=[[1119,451],[1126,451],[1126,528],[1119,528]]
top=[[1151,545],[1179,545],[1179,549],[1151,549]]
dark=[[1046,489],[1059,489],[1059,491],[1046,491]]
floor=[[1146,502],[1162,502],[1162,508],[1146,508]]
rows=[]
def face(name,poly,uv,tint='#ffffff'):
 assert len(poly)==len(uv);rows.append({'id':name,'polygon':poly,'uv':uv,'tint':tint})
# Four continuous structural columns; the rear column is mostly hidden by the front.
# All remain carried by the original right stone tower's single physical support curve.
face('rear-post',[[1108,392+(1108-995)*50/123],[1125,442-(1125-1118)*50/134],[1125,480],[1108,480]],wood,'#b4a08b')
outer=[[1017,515],[1118,475],[1233,515],[1118,558]]
inner=[[1034,515],[1118,482],[1216,515],[1118,550]]
face('floor-horizontal',inner,floor,'#d9d5cd')
face('back-left-frame',[outer[0],outer[1],inner[1],inner[0]],top,'#bc9a72')
face('back-right-frame',[outer[1],outer[2],inner[2],inner[1]],top,'#bd9d78')
face('front-left-frame',[outer[0],inner[0],inner[3],outer[3]],top,'#d2b087')
face('front-right-frame',[inner[3],inner[2],outer[2],outer[3]],top,'#e0bd8b')
face('front-left-apron',[cut[5],cut[4],outer[3],outer[0]],top,'#a4845e')
face('front-right-apron',[outer[3],outer[2],cut[3],cut[4]],top,'#c5a16e')
def ceiling(x):
 a,b=(cut[0],cut[1]) if x<=1118 else (cut[1],cut[2]);return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0])
for name,left,mid,right,foot in [('left',1003,1025,1033,516),('right',1220,1238,1247,516),('front',1105,1118,1133,557)]:
 face(name+'-post-light',[[left,ceiling(left)],[mid,ceiling(mid)],[mid,foot],[left,foot-1]],wood,'#e0c49f')
 face(name+'-post-side',[[mid,ceiling(mid)],[right,ceiling(right)],[right,foot-2],[mid,foot]],wood,'#ab8b68')
source={'scope':'UNSELECTED new continuous four-column/frame/floor interface. Old roof/stone are retained outside one declared replacement domain; no claim of old floor preservation or hidden original join recovery.', 'texture':g['textureResourcePath'],'supportLine':g['sections'][2]['supportLinePixels'],'members':rows,'columnCuts':list(range(1003,1248,8))+[1247]}
# Exact domain partition, avoiding duplicate last boundary.
lo=math.floor(min(x for row in rows for x,y in row['polygon']));hi=math.ceil(max(x for row in rows for x,y in row['polygon']))
source['columnCuts']=list(range(lo,hi,8))+[hi]
write('northeast-interface-geometry.json',source)
write('northeast-interface-design-contract.json',{'designNotRestoration':True,'replacementDomainTexturePixels':cut,'retained':'All baseline pixels/geometry outside declared interface domain, original arch/legs/roof except any later independently proven seam issue; original physical tower support curve;1103 road points.','fixedHoles':[[1416,59],[1431,66]],'fixedMaterialPositives':[[1427,67],[1435,78]],'notYetVerified':['GPU composition','physical connections','fixed holes','outside-domain invariance','actual actor/road','leaf absent']})
print('new interface',len(rows),'faces',len(source['columnCuts'])-1,'source depth partitions')
