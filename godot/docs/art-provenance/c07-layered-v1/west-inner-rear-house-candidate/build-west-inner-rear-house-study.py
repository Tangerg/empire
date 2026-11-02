from pathlib import Path
import json,math,hashlib
from PIL import Image
p=Path(__file__).parent;root=p.parent/'empire-c01/godot';asset=root/'assets/native/c07-layered-v1/west-inner-rear-house-v1-candidate.png';im=Image.open(asset)
# Explicit inspected stone-base domain, not a whole-image alpha foot detector.
foot=[]
for x in range(330,1089):
 ys=[y for y in range(800,im.height)if im.getpixel((x,y))[3]>=245]
 if not ys:raise ValueError(f'Missing inspected stone base column{x}')
 foot.append([x,max(ys)])
left=foot[0];right=foot[-1];near=max(foot,key=lambda q:q[1]);anchor=near;scale=.085;world=[384,430]
def extrapolate(x):
 a,b=(left,near)if x<near[0]else(near,right)
 return a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0])
strips=[]
for end in range(8,im.width+8,8):
 end=min(end,im.width);start=end-8 if end%8==0 else end-end%8
 pts=[q for q in foot if start<=q[0]<end]
 q=max(pts,key=lambda v:v[1])if pts else[(start+end-1)/2,extrapolate((start+end-1)/2)]
 strips.append({'rightPixels':end,'groundFrontPixels':q})
origin=[world[k]-scale*anchor[k]for k in(0,1)]
registration={'scope':'UNSELECTED independent rear annex. Visible base traced only inside manually inspected stone band. Hidden back closure is NEW design, not recovered original pixels.','assetSha256':hashlib.sha256(asset.read_bytes()).hexdigest(),'scale':scale,'anchorPixels':anchor,'worldAnchor':world,'worldOrigin':origin,'visibleStoneFoot':foot,'inspectedStoneDomain':{'x':[330,1088],'yMin':800},'alphaExterior':'Outside-A>8 bbox259,117..1144,1071; tiny A1..8 generated edge traces retained, no threshold rewriting','pending':['complete hidden base within canonical blocked terrain','mutual separation from selected blue house','actual-role front/back and nearby walking soles','whole-block scale and artwork review']}
(p/'west-inner-rear-house-v1-registration.json').write_text(json.dumps(registration,indent=2)+'\n')
scene=json.load(open(root/'data/scenes/c01-07-selected-assembly.json'));g=json.load(open(root/'data/scenes/c01-07-selected-assembly-ground.json'));c=json.load(open(root/'data/scenes/c01-07-selected-assembly-objects.json'));c['objects']['west-inner-rear-house-v1']={'resourcePath':'res://assets/native/c07-layered-v1/'+asset.name,'pixelSize':list(im.size),'anchorPixels':anchor,'groundFrontPixels':anchor,'depthStrips':strips,'contactShadowRadiiPixels':[24,5]};g['objects'].append({'id':'west-inner-rear-house-main','assetId':'west-inner-rear-house-v1','contact':[v/16 for v in world],'scale':scale,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'});scene['assembledGroundPath']='res://west-inner-rear-house-study-ground.json';g['objectCatalogPath']='res://west-inner-rear-house-study-objects.json'
for suffix,value in [('',scene),('-ground',g),('-objects',c)]:(p/f'west-inner-rear-house-study{suffix}.json').write_text(json.dumps(value,indent=2)+'\n')
print({'baseLeft':left,'baseNear':near,'baseRight':right,'strips':len(strips),'origin':origin})
