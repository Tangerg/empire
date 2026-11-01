from pathlib import Path
import json
r=Path('/workspace/scratch/84c9fe948755')
wood=[[400,380],[417,380],[417,620],[400,620]]
items=[]
def quad(id,p,tint='c6b8a1',uv=None):items.append({'id':id,'polygon':p,'uv':uv or wood,'tint':tint})
quad('west-corner-certified-foot',[[380,155],[383,155],[386,215],[384,217],[382,215]],uv=[[400,380],[417,380],[417,600],[408,620],[400,600]])
quad('intermediate-visible-rod-hidden-foot-design',[[406,168],[409,169],[410,218],[408,220],[406,218]],'aca18e',[[400,380],[417,380],[417,600],[408,620],[400,600]])
quad('front-central-visible-rod-hidden-foot-design',[[437.5,181],[441.5,181],[441.5,222],[439.5,223],[437.5,222]],'d1c0a6',[[400,380],[417,380],[417,600],[408,620],[400,600]])
quad('east-attached-visible-rod-hidden-foot-design',[[501,153],[505,153],[505,206],[503,208],[501,206]],'c4b7a2',[[400,380],[417,380],[417,600],[408,620],[400,600]])
# Small connected braces; no filled dark backdrop beneath the cloth.
quad('front-left-knee',[[437.5,182],[429,177],[437.5,190],[440,186]],'b5a690')
quad('front-right-knee',[[441,183],[449,178],[442,192],[440,188]],'b5a690')
quad('west-knee',[[383,156],[391,160],[384,168],[381,164]],'b5a690')
quad('east-knee',[[502,155],[494,159],[502,168],[504,162]],'b5a690')
# Matching UVs on the shared diagonal keep one continuous fabric projection.
quad('rear-cloth-plane',[[380,154],[444,128],[504,154]],'b6b7c3',[[540,340],[710,280],[960,360]])
quad('front-cloth-plane',[[380,154],[504,154],[440,181]],'fff5e6',[[540,340],[960,360],[790,420]])
x={'scope':'UNBOUND complete granary cloth canopy geometry. Only west384216 is an observed ground join. Intermediate408219/front439.5223/east503207 are explicitly authored hidden-foot completions after observed shafts, not recovered original feet. Main house/cargo/red roof excluded.','textureResourcePath':'res://assets/native/c04-layered-v1/supply-canopy.png','namePrefix':'GranaryCanopyColumn','supportLine':[[379,215.1666666667],[384,216],[408,219],[439.5,223],[503,207],[506,206.244094488]],'members':items,'materialPositives':[[438,166],[444,133],[440,180],[381,174],[384,212],[384,216],[407,184],[407,196],[439,200],[440,204],[439,217],[439,219],[503,185],[503,194]],'materialNegatives':[[384,218],[448,216],[440,245]],'siteIdentity':{'selectionCell':[35,14],'approach':[36,14],'newOwner':False,'pointer':'pass-through'},'textureAudit':{'pureWoodRectangle':[400,380,417,620],'pureWoodMinAlpha':249,'clothUvQuad':[[540,340],[710,280],[960,360],[790,420]],'reason':'Only opaque existing material pixels are reused; original C04 cargo/fog/background are not geometry.'}}
(r/'c07-static-preview/granary-canopy-geometry.json').write_text(json.dumps(x,indent=2)+'\n')
