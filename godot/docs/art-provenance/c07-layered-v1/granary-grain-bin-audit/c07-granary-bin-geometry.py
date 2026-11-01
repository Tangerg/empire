from pathlib import Path
import json
r=Path('/workspace/scratch/84c9fe948755');items=[]
plain=[[1180,500],[1220,500],[1220,760],[1180,760]]
face=[[565,838],[800,757],[800,821],[565,905]]
def part(name,poly,uv,tint='ffffff'):items.append({'id':name,'polygon':poly,'uv':uv,'tint':tint})
part('new-hidden-left-side',[[410,226],[417,233],[417,251],[410,238]],[[200,770],[375,860],[375,940],[200,850]],'b6a28a')
part('front-wood-body',[[417,233],[441,225],[441,243],[432,245],[417,251]],[[565,838],[800,757],[800,821],[710,855],[565,905]],'c9b79c')
part('grain-level',[[411,226],[430,221],[440,226],[417,234]],[[520,687],[800,548],[1040,580],[750,735]],'dfc6a0')
part('rear-wood-rim-frozen432224',[[429,222],[442,225],[442,228],[429,225]],face,'bda68a')
part('left-rim',[[410,225],[429,218.5],[431,221],[412,228]],face,'e1c6a0')
part('left-front-upright',[[415.5,233],[418.5,233],[418.5,250.5],[417,251.5],[415.5,250.5]],[[1180,500],[1220,500],[1220,750],[1200,760],[1180,750]],'d8bd96')
part('front-middle-upright',[[429,229],[431,228.5],[431,245.3],[429,246.1]],plain,'c6ac89')
part('right-upright',[[439.5,221],[442.5,220],[442.5,242],[441,243.5],[439.5,242.5]],[[1180,500],[1220,500],[1220,750],[1200,760],[1180,750]],'d8bd96')
x={'scope':'UNBOUND original-foot/local-depth grain bin; source wood432224/225 and foot contacts preserved. Hidden left side completion is new design. Whole generated sprite rejected; only verified wood/grain materials reused.','textureResourcePath':'res://assets/native/c07-layered-v1/granary-grain-bin-v1-candidate.png','namePrefix':'GranaryBinColumn','supportLine':[[409,237],[410,238],[417,251],[432,245],[441,243],[444,242]],'members':items,'materialPositives':[[432,224],[432,225],[432,245],[441,243],[417,251]],'materialNegatives':[[432,246],[441,245],[417,253],[440,248],[448,238],[430,217]],'newSiteOwner':False,'pointer':'pass-through'}
(r/'c07-static-preview/granary-bin-geometry.json').write_text(json.dumps(x,indent=2)+'\n')
