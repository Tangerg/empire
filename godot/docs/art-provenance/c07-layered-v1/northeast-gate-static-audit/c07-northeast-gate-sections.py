from pathlib import Path
from PIL import Image
import numpy as np,json
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';asset=r/'empire-c01/godot/assets/native/c07-layered-v1/northeast-gate-v1-stone-candidate.png';a=np.asarray(Image.open(asset))[:,:,3];h,w=a.shape
s=.14537444933920696;t=[1265.398678414097,-.8876651982378121]
def low(x):return int(np.where(a[:,x]>=128)[0][-1])
# Bounds are actual observed lower stone columns, not roof-only alpha columns.
left=[[0,low(140)-140*(low(240)-low(140))/100]]+[[x,low(x)]for x in range(140,609)]
right=[[x,low(x)]for x in range(878,1251)]+[[w,low(1250)+(w-1250)*(low(1250)-low(1150))/100]]
g={'scope':'UNBOUND northeast raw stone v1 preview; source road/lookout failures retained; hidden tower foundations unaccepted.','siteId':'northeast-gate','namePrefix':'NEGate','textureResourcePath':'res://assets/native/c07-layered-v1/northeast-gate-v1-stone-candidate.png','scale':s,'translation':t,'selectionCell':[83,11],'pointerSourcePolygon':[[1410,112],[1424,116],[1424,146],[1410,141]],'sections':[{'id':'left-blue-tower-and-arch-leg','polygonPixels':[[0,0],[609,0],[609,h],[0,h]],'supportLinePixels':left+[[609,low(608)]]},{'id':'arch-over-open-mouth','polygonPixels':[[609,0],[878,0],[878,h],[609,h]],'supportLinePixels':[[609,low(560)],[878,low(940)]]},{'id':'right-lookout-and-leg','polygonPixels':[[878,0],[w,0],[w,h],[878,h]],'supportLinePixels':right}]}
(p/'northeast-gate-v1-sections.json').write_text(json.dumps(g,indent=2)+'\n')
