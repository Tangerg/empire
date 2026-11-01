from pathlib import Path
import json
r=Path('/workspace/scratch/84c9fe948755');groups=[]
woodtex='res://assets/native/c07-layered-v1/granary-main-v1-candidate.png'
wood=[[143,470],[158,470],[158,600],[143,600]]
def member(name,poly,uv=wood,tint='c6b599'):return {'id':name,'polygon':poly,'uv':uv,'tint':tint}
def group(name,members,support,texture=woodtex,scope=''):
 groups.append({'namePrefix':name,'textureResourcePath':texture,'members':members,'supportLine':support,'scope':scope,'pointer':'pass-through','newSiteOwner':False})
# Solid clay face distinct from its timber fascia. Source manual edge uncertainty is~1px.
roofsupport=[[299,168.6],[301,170],[361,213],[431,191],[434,190.05]]
group('GranaryShedRoofColumn',[member('continuous-clay-plane',[[300,124],[382,94],[431,133],[357,161]],[[0,0],[1535,0],[1535,1023],[0,1023]],'cbbba7')],roofsupport,'res://assets/native/c07-layered-v1/orange-clay-tile-material-v1.png','Observed clay-face/eave landmarks, not rough wooden corner positions. Roof support envelope from actual left/near and new hidden right supports.')
group('GranaryShedFasciaColumn',[
 member('left-descending-eave',[[299,124],[357,160],[358,164],[299,128]],tint='9e927f'),
 member('front-long-eave',[[357,160],[431,132],[432,136],[358,164]],tint='a99b83'),
 member('rear-ridge-beam',[[299,122],[382,92],[384,95],[300,126]],tint='c4ac88'),
 member('far-side-edge',[[382,93],[432,132],[432,135],[381,96]],tint='a59881')],roofsupport,scope='Observed structural edge bands, separate from orange roof interior.')
for name,x,y,top,width,cert in [('Left',301,170,126,3,True),('Near',361,213,163,3,True),('Right',430,191,136,3,False),('Rear',382,148,97,3,False)]:
 poly=[[x-width/2,top],[x+width/2,top],[x+width/2,y-.5],[x,y+.5],[x-width/2,y-.5]]
 uv=[[143,470],[158,470],[158,595],[150,600],[143,595]]
 ms=[member('upright',poly,uv)]
 if name=='Near':ms += [member('connected-left-knee',[[359.5,165],[351,160],[352,165],[359.5,176]],tint='b6a184'),member('connected-right-knee',[[362.5,164],[371,159],[369,164],[362.5,175]],tint='b6a184')]
 if name=='Left':ms += [member('left-knee',[[302.5,129],[312,135],[310,138],[302.5,134]],tint='b6a184')]
 lo=min(p[0] for m in ms for p in m['polygon'])-1;hi=max(p[0] for m in ms for p in m['polygon'])+1
 group('GranaryShed'+name+'PostColumn',ms,[[lo,y],[hi,y]],scope=('Observed source foot '+str([x,y]) if cert else 'NEW hidden support completion; original foot not visible/certified')+'; each post owns its own depth, not the roof envelope')
group('GranaryShedBackPanelColumn',[member('new-back-storage-plank-panel',[[305,140],[330,154],[330,194],[305,179]],tint='8d806a')],[[304,178.4],[305,179],[330,194],[331,194.6]],scope='Bounded new panel completing original storage/lattice volume over nonwalkable interior. Hidden base not original-foot proof; no artificial all-under-roof black fill.')
x={'scope':'UNBOUND left shed structural reconstruction. Original roof has no certified legal C07 actor overlap: not a reproduced roof gameplay bug. Original two feet and clay/wood edges retained; hidden posts/back panel explicitly new design. Stock not part of this structural group.','components':groups,'materialPositives':[[320,130],[360,130],[400,130],[301,160],[301,170],[361,200],[361,213]],'materialNegatives':[[301,172],[361,215],[440,180]],'certifiedFeet':[[301,170],[361,213]],'authoredHiddenFeet':[[430,191],[382,148]],'noOldRoofRoleFailureClaim':True}
(r/'c07-static-preview/granary-left-shed-geometries.json').write_text(json.dumps(x,indent=2)+'\n')
