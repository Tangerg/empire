from pathlib import Path
from PIL import Image
import numpy as np,json
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';b=r/'empire-c01/godot/data/scenes';a=np.array(Image.open(r/'empire-c01/godot/assets/native/c07-layered-v1/west-inner-right-garden-stone-v2-candidate.png'))[:,:,3];H,W=a.shape
feet=[]
for x in range(145,1418):
 ys=np.where(a[:,x]>=244)[0]
 if len(ys):feet.append([x,int(ys[-1])])
by=dict(feet);lo,hi=feet[0][0],feet[-1][0]
def foot(x):
 if x<lo:return by[lo]+(x-lo)*(by[200]-by[lo])/(200-lo)
 if x>hi:return by[hi]+(x-hi)*(by[hi]-by[1350])/(hi-1350)
 return by[round(x)]
strips=[]
for left in range(0,W,8):
 right=min(left+8,W);pts=[q for q in feet if left<=q[0]<right]
 x,y=max(pts,key=lambda q:q[1]) if pts else ((left+right-1)/2,foot((left+right-1)/2));strips.append({'rightPixels':right,'groundFrontPixels':[float(x),float(y)]})
asset={'resourcePath':'res://assets/native/c07-layered-v1/west-inner-right-garden-stone-v2-candidate.png','pixelSize':[W,H],'anchorPixels':[270,927],'groundFrontPixels':[270,927],'contactShadowRadiiPixels':[45,8],'depthStrips':strips}
scene=json.load(open(b/'c01-07-selected-assembly.json'));g=json.load(open(b/'c01-07-selected-assembly-ground.json'));c=json.load(open(b/'c01-07-selected-assembly-objects.json'));c['objects']['west-inner-right-garden-stone-v2']=asset;g['objects'].append({'id':'west-inner-right-garden-stone','assetId':'west-inner-right-garden-stone-v2','contact':[541/16,462/16],'scale':.038,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'});g['objectCatalogPath']='res://west-inner-garden-stone-study-objects.json';scene['assembledGroundPath']='res://west-inner-garden-stone-study-ground.json'
for suffix,v in [('',scene),('-ground',g),('-objects',c)]: (p/f'west-inner-garden-stone-study{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
(p/'west-inner-right-garden-stone-v2-registration.json').write_text(json.dumps({'scope':'Unselected independent foreground stone boundary only; timber/foliage absent. Hidden back thickness NEW design not yet certified.','scale':.038,'anchorPixels':[270,927],'worldAnchor':[541,462],'worldOrigin':[530.74,426.774],'visibleStoneFoot':feet,'depth':'Actual per-slice deepest foot. No shared old house groundFront.','pending':['full thin base domain and legal walk','exact front material controls','timber/foliage responsibility and support']},indent=2)+'\n')
print(len(feet),len(strips),lo,hi)
