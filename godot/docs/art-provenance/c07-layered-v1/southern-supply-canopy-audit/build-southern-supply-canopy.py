"""Bounded original southern canopy reconstruction using existing cloth/wood material.
Runtime explicit surfaces own polygons/UV/support. This offline design is frozen provenance.
"""
import json,math,pathlib
out=pathlib.Path('/workspace/scratch/84c9fe948755/c07-static-preview')
texture='res://assets/native/c04-layered-v1/supply-canopy.png'
parts=[]
def member(id,poly,uv,tint='ffffff'):return {'id':id,'polygon':poly,'uv':uv,'tint':tint}
ring=[[1080,694],[1103,687],[1125,667],[1174,694],[1224,715],[1199,733],[1174,743],[1127,727]]
uv=[[540,340],[625,310],[710,280],[835,320],[960,360],[875,390],[790,420],[665,380]]
roof={'id':'cloth','namePrefix':'SouthernSupplyCloth','textureResourcePath':texture,'scope':'Original roof bounds and three visible feet; continuous mildly sagging cloth reconstruction, no main/rack/cargo. High rear corner has separately owned authored rear post, not floating wall attachment.','supportLine':[[1077,750.021505376],[1081,752],[1174,798],[1224,778],[1227,776.8]],'members':[member('continuous-cloth-sector-'+str(i),[ring[i],ring[(i+1)%8],[1152,714]],[uv[i],uv[(i+1)%8],[750,350]],'fff3d8')for i in range(8)]}
def beam(id,a,b,width):
 dx=b[0]-a[0];dy=b[1]-a[1];length=math.hypot(dx,dy);nx=-dy/length*width/2;ny=dx/length*width/2
 return member(id,[[a[0]+nx,a[1]+ny],[b[0]+nx,b[1]+ny],[b[0]-nx,b[1]-ny],[a[0]-nx,a[1]-ny]],[[402,420],[402,520],[414,520],[414,420]],'bba079')
roof['members']=[beam('west-front-continuous-wood-edge-rail',[1081,695],[1174,744],2.3),beam('front-right-continuous-wood-edge-rail',[1174,744],[1224,716],2.3)]+roof['members']
parts.append(roof)
for name,x,top,foot,original in [('west',1081,694,752,True),('front',1174,743,798,True),('right',1224,715,778,True),('rear',1125,667,732,False)]:
 half=3 if name in ['west','front'] else 2;collar=4 if name in ['west','front'] else 3;members=[member(name+'-continuous-clean-wood-shaft',[[x-half,top-2],[x+half,top-2],[x+half,foot-3],[x-half,foot-3]],[[402,420],[414,420],[414,520],[402,520]],'d6be96')]
 # Original collars are blue-gray; same opaque pale cloth patch supplies fine grain,
 # deliberately tinted as authored small collar material, no repeated metal-band wood texture.
 members += [member(name+'-blue-gray-foot-left',[[x-collar,foot-5],[x,foot-4.5],[x,foot],[x-collar,foot-1.2]],[[730,340],[750,340],[750,360],[730,360]],'516c78'),member(name+'-blue-gray-foot-right',[[x,foot-4.5],[x+collar,foot-5.8],[x+collar,foot-1.3],[x,foot]],[[750,340],[770,340],[770,360],[750,360]],'3d5362')]
 if name=='front':
  members += [member('front-west-knee',[[x-1,top+12],[x-17,top-9],[x-19,top-8],[x-1,top+16]],[[402,420],[414,420],[414,520],[402,520]],'c2aa84'),member('front-east-knee',[[x+1,top+12],[x+14,top-7],[x+16,top-8],[x+1,top+16]],[[402,420],[414,420],[414,520],[402,520]],'c2aa84')]
 elif name in ['west','right']:
  sign=1 if name=='west' else -1
  members.append(member(name+'-knee',[[x,top+13],[x+sign*13,top+2],[x+sign*15,top+2],[x,top+17]],[[402,420],[414,420],[414,520],[402,520]],'c2aa84'))
 extent=[p[0]for m in members for p in m['polygon']]
 parts.append({'id':name+'-post','namePrefix':'SouthernSupply'+name.title()+'Post','textureResourcePath':texture,'scope':'Observed original visible foot, approximate ±2 source pixels.'if original else'Authored hidden rear foot, structural completion; not original pixel recovery.','supportLine':[[math.floor(min(extent))-1,foot],[math.ceil(max(extent))+1,foot]],'members':members})
source={'scope':'Southern canvas and four independently supported posts, static candidate. No movement/AI/save. Three feet visible in original; rear foot authored hidden completion. Existing material reused, no raster image edit.','components':parts,'visibleOriginalFeet':[[1081,752],[1174,798],[1224,778]],'authoredHiddenRearFoot':[1125,732]}
(out/'southern-supply-canopy-geometries.json').write_text(json.dumps(source,indent=2)+'\n')
for part in parts:(out/f"southern-supply-canopy-{part['id']}-source.json").write_text(json.dumps(part,indent=2)+'\n')
