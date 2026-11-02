"""Offline local NEW open trellis, awaiting finite footing review.
One input corner domain owns the post feet and rail connections. Existing shared
wood material and formal surface exporter are reused. No production mutation.
"""
from pathlib import Path
import json, math
OUT=Path(__file__).resolve().parent
TEXTURE='res://assets/native/c07-layered-v1/training-fence-v2-rejected.png'
# Original hidden feet are not recoverable; this whole footprint is NEW design.
FEET={'left':[362.,473.],'front':[393.,488.5],'right':[424.,473.],'back':[393.,457.5]}
HEIGHT=29.;POST_WIDTH=2.8
sources=[]
def source(name,curve,foot=None):
    value={'id':name,'textureResourcePath':TEXTURE,'supportLine':curve,'members':[],
           'scope':'NEW left courtyard open trellis; fixed physical footing review required.'}
    if foot is not None:value['physicalFootPolygon']=foot
    sources.append(value);return value
def member(owner,name,polygon,tint,axis):
    a,b=axis;length=math.dist(a,b);along=[(b[k]-a[k])/length for k in (0,1)];across=[-along[1],along[0]]
    uv=[]
    for point in polygon:
        delta=[point[k]-a[k] for k in (0,1)]
        uv.append([max(95.5,min(130.5,113.5+sum(delta[k]*across[k] for k in (0,1))*4)),
                   max(180.5,min(598.5,190+sum(delta[k]*along[k] for k in (0,1))*10))])
    owner['members'].append({'id':name,'polygon':polygon,'uv':uv,'tint':tint})
def post_points(foot):
    x,y=foot
    return [[x-1,y-1],[x+1,y],[x+1.8,y-1],[x-.2,y-2]]
def support_at_head(foot):
    x=foot[0]+.4;polygon=post_points(foot);ys=[]
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        if min(a[0],b[0])<=x<=max(a[0],b[0]) and a[0]!=b[0]:
            ys.append(a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0]))
    return [x,max(ys)]
def post(name,foot):
    x,y=foot;left,front,right,back=post_points(foot)
    rise=lambda q:[q[0],q[1]-HEIGHT]
    owner=source('post-'+name,[left,front,right],[left,front,right,back])
    member(owner,'left-face',[rise(left),rise(front),front,left],'aab0b9',[rise(foot),foot])
    member(owner,'right-face',[rise(front),rise(right),right,front],'c3c6cc',[rise(foot),foot])
    member(owner,'cap',[rise(left),rise(back),rise(right),rise(front)],'d9e2ed',[rise(left),rise(right)])
for name,point in FEET.items():post(name,point)
def rail(name,footA,footB):
    if footA[0]>footB[0]:footA,footB=footB,footA
    a=[footA[0]+.4,footA[1]-1-HEIGHT];b=[footB[0]+.4,footB[1]-1-HEIGHT]
    owner=source(name,[support_at_head(footA),support_at_head(footB)])
    member(owner,'front',[a,b,[b[0],b[1]+1.5],[a[0],a[1]+1.5]],'b7bdc5',[a,b])
    member(owner,'top',[[a[0],a[1]-.6],[b[0],b[1]-.6],b,a],'d1d9e3',[a,b])
for name,a,b in [('left-front','left','front'),('front-right','front','right'),('right-back','right','back'),('back-left','back','left')]:rail(name,FEET[a],FEET[b])
def lerp(a,b,t):return [(1-t)*a[k]+t*b[k] for k in (0,1)]
for index,t in enumerate([1/3,2/3]):rail('open-slat-'+str(index),lerp(FEET['left'],FEET['front'],t),lerp(FEET['back'],FEET['right'],t))
for value in sources:(OUT/f"left-courtyard-frame-{value['id']}-source.json").write_text(json.dumps(value,indent=2)+'\n')
(OUT/'left-courtyard-frame-new-layout.json').write_text(json.dumps({'scope':'UNSELECTED NEW four-post/ten-owner open frame; original hidden feet are not asserted. No foliage or soil yet.','feet':FEET,'height':HEIGHT,'postWidth':POST_WIDTH,'components':sources},indent=2)+'\n')
print(len(sources),'local physical support owners')
