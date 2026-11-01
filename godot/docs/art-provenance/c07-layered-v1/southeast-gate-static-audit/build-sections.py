from pathlib import Path
from PIL import Image
import numpy as np,json
r=Path('/workspace/scratch/84c9fe948755');p=r/'empire-c01/godot/assets/native/c07-layered-v1/southeast-gate-v2-stone-candidate.png';a=np.asarray(Image.open(p))[:,:,3]
s=.193939393939;t=[1193.2424242424,722.9333333333]
def low(x):return int(np.where(a[:,x]>=128)[0][-1])
left=[[0,low(84)-84*(low(184)-low(84))/100]]+[[x,low(x)] for x in range(84,536)]+[[771,1078+(771-535)*(937-1078)/(845-535)]]
right=[[x,low(x)] for x in range(845,1265)]+[[1326,low(1264)+62*(low(1264)-low(1164))/100]]
sections=[{'id':'left-tower-and-left-arch','polygonPixels':[[0,0],[771,0],[771,1186],[0,1186]],'supportLinePixels':left},
{'id':'arch-front-above-return','polygonPixels':[[771,0],[845,0],[845,740],[771,675]],'supportLinePixels':[[771,1078+(771-535)*(937-1078)/(845-535)],[845,937]]},
{'id':'recessed-right-return','polygonPixels':[[771,675],[845,740],[845,937],[771,891]],'supportLinePixels':[[771,890-4*48/70],[845,938]]},
{'id':'right-return-lower-edge-fringe','polygonPixels':[[771,891],[845,937],[845,1186],[771,1186]],'supportLinePixels':[[771,890-4*48/70],[845,938]]},
{'id':'right-leg-and-tower','polygonPixels':[[845,0],[1326,0],[1326,1186],[845,1186]],'supportLinePixels':right}]
x={'scope':'UNBOUND unchanged RGBA with single pixel ownership partition; left/gap physical-vs-virtual feet and recessed return separate; door leaf absent; material interface review pending','textureResourcePath':'res://assets/native/c07-layered-v1/southeast-gate-v2-stone-candidate.png','scale':s,'translation':t,'selectionCell':[84,57],'pointerSourcePolygon':[[1242,869],[1259,878],[1259,910],[1242,902]],'sections':sections,'originalBackPoints':[[1296,899],[1297,899]],'originalFrontPoints':[[1297,927],[1298,929]],'originalOpeningPoints':[[1304,920],[1308,925]],'recoveredGroundPoints':[[1354,910],[1352,912],[1348,908]]}
(r/'c07-static-preview/southeast-gate-v2-sections.json').write_text(json.dumps(x,indent=2)+'\n')
