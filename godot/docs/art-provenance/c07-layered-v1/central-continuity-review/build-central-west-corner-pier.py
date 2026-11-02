from pathlib import Path
import json
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');foot=[[680,478],[685,482],[690,479],[685,475]];top=[[x,y-7] for x,y in foot]
# Source partial low corner pier is separate from the white planter. Hidden back is new design.
uTop=[[410,650],[490,650],[490,850],[410,850]];left=[[300,1245],[455,1320],[455,1335],[300,1260]];right=[[550,1310],[780,1195],[780,1212],[550,1327]]
m=[{'id':'corner-cap','polygon':top,'uv':uTop,'tint':'ffffff'},{'id':'corner-left','polygon':[top[0],top[1],foot[1],foot[0]],'uv':left,'tint':'ffffff'},{'id':'corner-right','polygon':[top[1],top[2],foot[2],foot[1]],'uv':right,'tint':'ffffff'}]
s={'textureResourcePath':'res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png','supportLine':foot[:3],'members':m,'scope':'Separate low west corner pier, source partial visible lower edge at685482; hidden back and simple supported geometry new design. No borrowed white-planter foot.'};(p/'central-west-corner-pier-source.json').write_text(json.dumps(s,indent=2)+'\n')
