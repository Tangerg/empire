from pathlib import Path
from PIL import Image
import numpy as np,json
r=Path('/workspace/scratch/84c9fe948755'); a=np.asarray(Image.open(r/'empire-c01/godot/assets/native/c07-layered-v1/southwest-gate-v1-stone-candidate.png'))[:,:,3]
s=.18803418803418792;t=[82.10256410256423,724.923076923077]
def low(x):return int(np.where(a[:,x]>=128)[0][-1])
def bridge(x):return 963+(x-510)*(1080-963)/(803-510)
left=[[0,low(110)-110*(low(210)-low(110))/100]]+[[x,low(x)] for x in range(110,551)]
right=[[x,low(x)]for x in range(803,1264)]+[[1326,low(1263)+(1326-1263)*(low(1263)-low(1163))/100]]
recess=[[x,low(x)]for x in range(550,605)]
sections=[{'id':'left-blue-tower-and-front-leg','polygonPixels':[[0,0],[550,0],[550,1186],[0,1186]],'supportLinePixels':left},
{'id':'front-arch-above-recess','polygonPixels':[[550,0],[604,0],[604,650],[550,740]],'supportLinePixels':[[550,bridge(550)],[604,bridge(604)]]},
{'id':'recessed-left-return','polygonPixels':[[550,740],[604,650],[604,1186],[550,1186]],'supportLinePixels':recess},
{'id':'front-arch-over-open-gap','polygonPixels':[[604,0],[803,0],[803,1186],[604,1186]],'supportLinePixels':[[604,bridge(604)],[803,1080]]},
{'id':'right-leg-and-red-tower','polygonPixels':[[803,0],[1326,0],[1326,1186],[803,1186]],'supportLinePixels':right}]
x={'scope':'UNBOUND southwest stone v1; unchanged RGBA, actual own foot profile plus explicit virtual arch support. Approx3px registration differences not accepted as old exact feet. Wooden leaf absent.','siteId':'southwest-gate','namePrefix':'SWGate','textureResourcePath':'res://assets/native/c07-layered-v1/southwest-gate-v1-stone-candidate.png','scale':s,'translation':t,'selectionCell':[15,51],'pointerSourcePolygon':[[271,872],[289,879],[289,918],[271,911]],'sections':sections}
(r/'c07-static-preview/southwest-gate-v1-sections.json').write_text(json.dumps(x,indent=2)+'\n')
