from pathlib import Path
from PIL import Image
import numpy as np,json
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview'
a=np.array(Image.open(r/'empire-c01/godot/assets/native/c07-layered-v1/west-inner-blue-house-v1-candidate.png'))[:,:,3];H,W=a.shape
feet=[]
for x in range(150,1036):
 ys=np.where(a[:,x]>=244)[0]
 if len(ys) and ys[-1]>950:feet.append([x,int(ys[-1])])
by=dict(feet);lo,hi=feet[0][0],feet[-1][0]
def foot(x):
 if x<lo:return by[lo]+(x-lo)*(by[200]-by[lo])/(200-lo)
 if x>hi:return by[hi]+(x-hi)*(by[hi]-by[1000])/(hi-1000)
 return by[round(x)]
strips=[]
for left in range(0,W,8):
 right=min(left+8,W);pts=[q for q in feet if left<=q[0]<right]
 x,y=max(pts,key=lambda q:q[1]) if pts else ((left+right-1)/2,foot((left+right-1)/2))
 strips.append({'rightPixels':right,'groundFrontPixels':[float(x),float(y)]})
asset={'resourcePath':'res://assets/native/c07-layered-v1/west-inner-blue-house-v1-candidate.png','pixelSize':[W,H],'anchorPixels':[467,1318],'groundFrontPixels':[467,1318],'contactShadowRadiiPixels':[35,6],'depthStrips':strips}
scene=json.load(open(p/'west-inner-house-study.json'));g=json.load(open(p/'west-inner-house-study-ground.json'));c=json.load(open(p/'west-inner-house-study-objects.json'));c['objects']['west-inner-blue-house-v1']=asset;g['objects'].append({'id':'west-inner-blue-house-main','assetId':'west-inner-blue-house-v1','contact':[422/16,467/16],'scale':.1,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'});g['objectCatalogPath']='res://west-inner-pair-study-objects.json';scene['assembledGroundPath']='res://west-inner-pair-study-ground.json'
for suffix,v in [('',scene),('-ground',g),('-objects',c)]: (p/f'west-inner-pair-study{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
(p/'west-inner-blue-v2-registration.json').write_text(json.dumps({'scope':'UNSELECTED new-design registration. Original visible doorstep intersects actual legal walk shoe segments. No gameplay. Hidden floor not certified.','scale':.1,'anchorPixels':[467,1318],'worldAnchor':[422,467],'worldOrigin':[375.3,335.2],'candidateVisibleStoneFoot':feet,'stoneFootMeaning':'Opaque measured last pixels within visually identified stone-base x150..1035; each slice owns deepest sample in its interval; roof edges extrapolated.','registrationChange':'Whole sprite moved north3px. Same uniform scale/source/feet; reclaim original legal p28,29 boundary, not shoe-sample alteration. Initial 0.015px shoe clearance and 102 foot pixels in p retained as v1 evidence.','pending':['Full hidden-floor domain','Rule/walk constraints','Orange-neighbor physical separation','Garden independent ownership']},indent=2)+'\n')
print(len(feet),len(strips),lo,hi)
