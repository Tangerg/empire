from pathlib import Path
import hashlib,json
from PIL import Image
root=Path(__file__).resolve().parents[1];repo=root/'empire-c01/godot';out=Path(__file__).resolve().parent
source=root/'c07-static-preview/northeast-gate-v1-sections.json';g=json.loads(source.read_text());asset=repo/g['textureResourcePath'][6:];im=Image.open(asset).convert('RGBA')
selected=json.loads((repo/'data/scenes/c01-07-selected-assembly-ground.json').read_text())
assert not any(o.get('selectionCell')==[83,11] and o.get('ownsMapStructureBody') for o in selected['objects']), 'NE unexpectedly already selected'
assert g['selectionCell']==[83,11] and g['textureResourcePath'].endswith('northeast-gate-v2-stone-candidate.png')
s=g['scale'];t=g['translation'];witnesses=[]
for label,p in [('left-hole-unresolved',[1416,59]),('right-hole-beam-unresolved',[1431,66]),('front-post-positive',[1427,67]),('lower-platform-frame-positive',[1435,78])]:
 uv=[(p[i]-t[i])/s for i in range(2)];pixel=[round(v) for v in uv]
 witnesses.append({'id':label,'source':p,'uv':uv,'nearestPixel':pixel,'rgba':list(im.getpixel(tuple(pixel)))})
road=root/'c07-static-preview/northeast-gate-original-road-regions.json';r=json.loads(road.read_text());assert r['uniqueIntegerSourcePointCount']==1103
paths=[source,asset,road,repo/'data/scenes/c01-07.json',repo/'data/scenes/c01-07-selected-assembly-ground.json']
report={'scope':'Read-only input ownership audit. NE remains unselected; existing v2 stone is a failed candidate, not production geometry. No engine commands, rendering, state load/write, or gameplay.', 'inputs':[{'path':str(p.relative_to(root)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths], 'registration':{'scale':s,'translation':t},'assetSize':list(im.size),'sections':[v['id'] for v in g['sections']], 'roadFrozenCount':1103,'witnesses':witnesses,'negativeScope':'Raw nearest RGBA only; not formal Draw/Pick or endpoint acceptance. Left hole and right lower-beam hole are separate failures.', 'designCandidate':{'status':'RETRACTED AS EXECUTABLE CUT: lower back corner lies in beam/floor contact shadow; old opaque shared boundary cannot be alpha-separated.','kind':'continuous beam cross-section, not point erasure or pixel restoration','assetPolygon':[[1130,470],[1185,480],[1185,495],[1130,485]],'sourcePolygon':[[x*s+t[0],y*s+t[1]] for x,y in [[1130,470],[1185,480],[1185,495],[1130,485]]],'unverified':'Back lower endpoint may overlap dark floor rather than wood; must resolve owner before constructing. Do not apply as accepted geometry.'}}
(out/'current-input-owner-audit.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'unselected':True,'frozenRoadCount':1103,'witnesses':witnesses},indent=2))
