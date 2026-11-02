"""Independent NE leaf, using observed silhouette/foot; hidden joinery is new design."""
from pathlib import Path
import json
root = Path(__file__).resolve().parent
polygon = [[1355,104],[1360,97],[1364,94],[1369,94],[1373,96],[1373,131],[1355,139]]
# Reuse the already certified gate timber region, not a new texture or runtime shader.
uv = [[736+(x-1355)*61/18,827+(y-94)*72/45] for x,y in polygon]
source = {
 'scope':'Unselected independent left leaf. Observed silhouette approximate +/-2 source pixels; hidden hinge/joinery is new design. No opening or rule changes.',
 'textureResourcePath':'res://assets/native/c07-layered-v1/southeast-gate-v1-candidate.png',
 'namePrefix':'NortheastWoodLeaf', 'supportLine':[[1355,139],[1373,131]],
 'members':[{'id':'arched-left-leaf','polygon':polygon,'uv':uv,'tint':'ffffff','coverage':'opaque'}]}
for name,y in [('upper-rail',111),('lower-rail',126)]:
 source['members'].append({'id':name,'polygon':[[1356,y],[1372,y-7.1],[1372,y-5.8],[1356,y+1.3]],'uv':[[740,840],[740,890],[749,890],[749,840]],'tint':'c5b291','coverage':'opaque'})
(root/'northeast-leaf-source.json').write_text(json.dumps(source,indent=2)+'\n')
