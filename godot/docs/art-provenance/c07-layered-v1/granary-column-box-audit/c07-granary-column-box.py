from pathlib import Path
import json
r=Path('/workspace/scratch/84c9fe948755')
# Original object is a four-sided wooden enclosure around a post, not a round barrel.
# Function and hidden post end remain unknown; geometry does not invent a plant or open hole.
left=[[200,770],[375,860],[375,940],[200,850]]
right=[[565,838],[800,757],[800,821],[565,905]]
top=[[400,201],[408,198],[420,202],[410,205]]
x={'scope':'UNBOUND wooden post-surrounding box, historical barrel misidentification corrected; no function or original rear-role bug claim','textureResourcePath':'res://assets/native/c07-layered-v1/granary-grain-bin-v1-candidate.png','namePrefix':'GranaryColumnBox','supportLine':[[399,211.4],[400,212],[410,218],[420,214],[421,213.6]],'members':[{'id':'left-wood-face','polygon':[[400,201],[410,205],[410,218],[400,212]],'uv':left,'tint':'bca58a'},{'id':'right-wood-face','polygon':[[410,205],[420,202],[420,214],[410,218]],'uv':right,'tint':'bba487'},{'id':'dark-solid-top-not-transparent-hole','polygon':top,'uv':right,'tint':'625b51'}],'pointer':'pass-through','newSiteOwner':False}
(r/'c07-static-preview/granary-column-box-geometry.json').write_text(json.dumps(x,indent=2)+'\n')
