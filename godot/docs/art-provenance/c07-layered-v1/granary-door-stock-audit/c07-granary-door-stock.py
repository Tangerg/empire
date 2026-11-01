from pathlib import Path
import json
r=Path('/workspace/scratch/84c9fe948755')
# Each container owns its own local front profile, even where the visible boxes overlap.
woodLeft=[[200,770],[375,860],[375,940],[200,850]]
woodRight=[[565,838],[800,757],[800,821],[565,905]]
woodTop=woodRight
grain=[[520,687],[800,548],[1040,580],[750,735]]
items=[]
def container(name,top,base,fill,known):
 # top ordered left, back, right, front; bottom contacts left, front, right.
 l,b,rr,f=top;bl,bf,br=base
 members=[{'id':'left-face','polygon':[l,f,bf,bl],'uv':woodLeft,'tint':'b7a084'}, {'id':'right-face','polygon':[f,rr,br,bf],'uv':woodRight,'tint':'d1b799'}, {'id':'top-grain-new-stock' if fill else 'top-wood-lid-new-stock','polygon':top,'uv':grain if fill else woodTop,'tint':'c3ad86' if fill else 'd0b28d'}]
 # Rear opaque body top fits the independent original occupied mass; no open-hole claim.
 xmin=min(p[0] for p in top+base);xmax=max(p[0] for p in top+base)
 support=[[xmin-1,bl[1]],[*bl],[*bf],[*br],[xmax+1,br[1]]]
 # Remove duplicate x endpoints, keeping the actual contact, not a second advancing owner.
 support=sorted({p[0]:p[1] for p in support}.items())
 items.append({'scope':known,'textureResourcePath':'res://assets/native/c07-layered-v1/granary-grain-bin-v1-candidate.png','namePrefix':name,'members':members,'supportLine':support,'pointer':'pass-through','newSiteOwner':False})
container('GranaryStockRearColumn',[[522,188],[531,184],[542,188],[530,194]],[[522,207],[530,214],[542,208]],False,'New reasonable rear closed crate; original rear foot hidden by low front containers, no certified original-foot claim')
container('GranaryStockLeftColumn',[[501,201],[511,196],[523,201],[513,206]],[[501,213],[513,219],[523,214]],True,'Original low left front contact513219 retained; other corners/new grain contents are bounded stock design, not plant identity recovery')
container('GranaryStockRightColumn',[[532,201],[544,196],[553,201],[541,206]],[[532,214],[541,218],[553,214]],False,'Original low right front contact541218 retained;535215 remains its own wood side, never main-house support')
x={'scope':'UNBOUND three independent door-front containers; no newly reproduced rear-role bug claimed. Preserve actual C07 front actor and full entry strip.','containers':items,'materialPositives':[[513,219],[541,218],[535,215],[539,211]],'materialNegatives':[[513,220],[541,220],[536,232],[552,232],[568,232],[584,232]],'entryStripSourceBounds':[528,224,592,240],'selection':[35,14],'approach':[36,14]}
(r/'c07-static-preview/granary-door-stock-geometries.json').write_text(json.dumps(x,indent=2)+'\n')
