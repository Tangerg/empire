from pathlib import Path
from PIL import Image
import numpy as np,json,math
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';b=r/'empire-c01/godot/data/scenes'
path=r/'empire-c01/godot/assets/native/c07-layered-v1/west-inner-house-v2-candidate.png';im=np.array(Image.open(path));a=im[:,:,3];H,W=a.shape
# Visible generated stone foot, not three invented original outer feet.
feet=[]
for x in range(300,1094):
 ys=np.where(a[:,x]>=244)[0]
 if len(ys) and ys[-1]>800:feet.append([x,int(ys[-1])])
lo,hi=feet[0][0],feet[-1][0];by={x:y for x,y in feet}
def foot(x):
 if x<lo:return by[lo]+(x-lo)*(by[340]-by[lo])/(340-lo)
 if x>hi:return by[hi]+(x-hi)*(by[hi]-by[1050])/(hi-1050)
 return by[round(x)]
strips=[]
for left in range(0,W,8):
 right=min(left+8,W);pts=[q for q in feet if left<=q[0]<right]
 x,y=max(pts,key=lambda q:q[1]) if pts else ((left+right-1)/2,foot((left+right-1)/2))
 strips.append({'rightPixels':right,'groundFrontPixels':[float(x),float(y)]})
asset={'resourcePath':'res://assets/native/c07-layered-v1/west-inner-house-v2-candidate.png','pixelSize':[W,H],'anchorPixels':[700,1043],'groundFrontPixels':[700,1043],'contactShadowRadiiPixels':[35,6],'pointerPolygonPixels':[[608,790],[638,750],[681,731],[725,744],[765,783],[774,1019],[613,1060]],'depthStrips':strips}
scene=json.load(open(b/'c01-07-selected-assembly.json'));g=json.load(open(b/'c01-07-selected-assembly-ground.json'));c=json.load(open(b/'c01-07-selected-assembly-objects.json'));c['objects']['west-inner-house-v2']=asset;g['objects'].append({'id':'west-inner-house-main','assetId':'west-inner-house-v2','contact':[509/16,454/16],'scale':.11,'selectionCell':[33,30],'ownsMapStructureBody':True,'pointer':'target'});g['objectCatalogPath']='res://west-inner-house-study-objects.json';scene['assembledGroundPath']='res://west-inner-house-study-ground.json'
for suffix,v in [('',scene),('-ground',g),('-objects',c)]: (p/f'west-inner-house-study{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
(p/'west-inner-house-v2-registration.json').write_text(json.dumps({'scope':'Unselected bounded single-orange-house study. Generated hidden outer base is NEW design. Original only certifies doorway threshold band; no recovered three original corners. Roof/chimney and door silhouette checked independently.','scale':.11,'anchorPixels':[700,1043],'worldAnchor':[509,454],'worldOrigin':[432,339.27],'candidateVisibleStoneFoot':feet,'stepWorldSamples':[[x*.11+432,y*.11+339.27]for x,y in [(620,1067),(700,1043),(780,1025)]],'visibleFootOwnership':'Each8pxstrip uses the deepest actual stone-foot sample within that strip, not a shallower ideal line; beyond stone span use edge continuation only for roof overhang support.','pointerMeaning':'Actual new arched door material; site33,30 and approach34,30 unchanged; no targeting generic house bbox.'},indent=2)+'\n')
print('stone samples',len(feet),'strips',len(strips))
