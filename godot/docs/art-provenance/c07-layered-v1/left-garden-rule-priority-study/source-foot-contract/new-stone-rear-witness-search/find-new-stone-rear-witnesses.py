from pathlib import Path
from PIL import Image
import json,hashlib,math
import numpy as np
out=Path(__file__).resolve().parent;root=out.parents[2];G=root/'empire-c01/godot';sp=root/'c07-static-preview'
layout=json.loads((sp/'left-garden-stone-new-layout.json').read_text());merged=json.loads((sp/'left-garden-stone-merged-surface.json').read_text());origin=np.array([300,420]);C=json.loads((G/'data/selected-assets.json').read_text())['assets'];B=json.loads((G/'data/art-bindings.json').read_text());L=next(x for x in json.loads((G/'data/grayflag-campaign-levels.json').read_text()) if x['id']=='c01-07')
texture=G/layout['components'][0]['textureResourcePath'].replace('res://','');tex=np.array(Image.open(texture).convert('RGBA'))
support={v['id']:np.array(v['frontPixels'])+origin for v in merged['supports']}
# Gather exact deployed members/UVs and local support columns. Fixed half-pixel source lattice; no Pick-based selection.
points=[]
for m in merged['members']:
 P=np.array(m['polygonPixels'])+origin;UV=np.array(m['uvPixels']);lo=np.min(P,axis=0);hi=np.max(P,axis=0)
 for y in np.arange(math.floor(lo[1])+.25,math.ceil(hi[1]),.5):
  for x in np.arange(math.floor(lo[0])+.25,math.ceil(hi[0]),.5):
   q=np.array([x,y]);found=None
   for j in range(1,len(P)-1):
    ids=[0,j,j+1];T=P[ids];M=np.column_stack((T[1]-T[0],T[2]-T[0]));det=np.linalg.det(M)
    if abs(det)<1e-10:continue
    z=np.linalg.solve(M,q-T[0]);w=np.array([1-z[0]-z[1],z[0],z[1]])
    if min(w)>1e-6:found=w@UV[ids];break
   if found is None:continue
   tx,ty=np.floor(found).astype(int)
   if 0<=tx<tex.shape[1] and 0<=ty<tex.shape[0] and tex[ty,tx,3]>=244:
    points.append({'source':q.tolist(),'support':support[m['supportId']].tolist(),'member':m['id'],'uv':found.tolist(),'texturePixel':[int(tx),int(ty)],'materialAlpha':int(tex[ty,tx,3])})
Q=np.array([v['source'] for v in points]);D=np.array([v['support'][1] for v in points]);minY=float(Q[:,1].min());maxD=float(D.max());maxRadius=max(max(z['frameAnchors'][0][0],z['frameRegions'][0][2]-z['frameAnchors'][0][0])*z['presentationScale']/z.get('pixelDensity',1) for z in C if 'frameRegions'in z and 'frameAnchors'in z and 'presentationScale'in z)
# These bounds cover all actual-C07 frame0 canvas bounds touching this wall; only p neighbours, no occupants/state mutation.
assets={}
for u in L['units']:
 ab=B['CANDIDATE_01_ACTOR_ART'].get(u['key']);b=ab['unitArt']if ab else B['CANDIDATE_01_UNIT_ART'][u['unit']];z=next(x for x in C if (x.get('id')==b['appearanceId']if 'appearanceId'in b else x.get('topicId')==b['topicId']and 'id'not in x));aid=z.get('id',z.get('assetId'));assets.setdefault(aid,{'record':z,'units':[]})['units'].append(u['key'])
maxRadius=max(max(v['record']['frameAnchors'][0][0],v['record']['frameRegions'][0][2]-v['record']['frameAnchors'][0][0])*v['record']['presentationScale']/v['record'].get('pixelDensity',1) for v in assets.values())
cells=[]
for y in range(max(0,math.ceil((minY-8)/16)),min(L['height'],math.ceil((maxD-8)/16))):
 for x in range(max(0,math.floor((Q[:,0].min()-maxRadius-8)/16)),min(L['width'],math.ceil((Q[:,0].max()+maxRadius-8)/16)+1)):
  if L['terrain'][y][x]=='p':cells.append([x,y])
results=[];records=[];boxCandidates=[]
for aid,v in assets.items():
 z=v['record'];p=G/z['resourcePath'].replace('res://','');im=np.array(Image.open(p).convert('RGBA'));fr=z['frameRegions'][0];anchor=np.array(z['frameAnchors'][0]);sc=z['presentationScale']/z.get('pixelDensity',1)
 records.append({'assetId':aid,'unitKeys':v['units'],'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'frame':0,'region':fr,'anchor':anchor.tolist(),'effectiveScale':sc})
 for cell in cells:
  foot=np.array(cell)*16+8
  for facing in [1,-1]:
   px=np.floor((Q-foot)/sc*np.array([facing,1])+anchor).astype(int)
   valid=(Q[:,1]<=foot[1])&(D>foot[1])&(px[:,0]>=0)&(px[:,0]<fr[2])&(px[:,1]>=0)&(px[:,1]<fr[3]);idx=np.flatnonzero(valid)
   if len(idx):boxCandidates.append({'assetId':aid,'cell':cell,'facing':'east'if facing==1 else'west','bboxMaterialSamples':len(idx)})
   hit=[int(i) for i in idx if im[fr[1]+px[i,1],fr[0]+px[i,0],3]>=200]
   if hit:
    hs=[]
    for i in hit:
     x,y=px[i];local=im[max(fr[1],fr[1]+y-1):min(fr[1]+fr[3],fr[1]+y+2),max(fr[0],fr[0]+x-1):min(fr[0]+fr[2],fr[0]+x+2),3]
     hs.append({**points[i],'actorFramePixel':px[i].tolist(),'actorAlpha':int(im[fr[1]+y,fr[0]+x,3]),'actor3x3MinimumAlpha':int(local.min()),'depthFrontGap':float(D[i]-foot[1])})
    hs.sort(key=lambda h:(h['actor3x3MinimumAlpha'],h['depthFrontGap']),reverse=True)
    results.append({'assetId':aid,'unitKeys':v['units'],'cell':cell,'terrain':'p','facing':'east'if facing==1 else'west','foot':foot.tolist(),'hitCount':len(hs),'strongest':hs[:12]})
report={'ready':True,'scope':'Bounded static search: only five actual new stone components, exact deployed merged polygons/UV/supports; all12 actual C07 initial appearances frame0, original scale, both facings and only legal p neighbours within material/depth bounds. No engine/GUI/action/state/Pick.','inputs':[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in [sp/'left-garden-stone-new-layout.json',sp/'left-garden-stone-merged-surface.json',G/'data/grayflag-campaign-levels.json',texture]],'lattice':{'step':.5,'offset':.25,'materialSamples':len(points),'criteria':'Interior of exact rendered member, material alpha>=244, sourceY<=actorFootY, actual local supportY>actorFootY, actor alpha>=200; fixed lattice not success-picked.'},'cells':cells,'appearances':records,'conservativeBoxCandidates':boxCandidates,'results':results,'limits':['No whole-world or other-role search; appearances are from actual initial level units, not invented mount/ballista assets.','Absence of robust fixed-lattice samples is not proof that no subpixel alpha intersection exists.','Positive samples still need parent native owner/depth/Pick confirmation; no static Pick claim.']}
(out/'new-stone-rear-witness-search.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'ready':True,'samples':len(points),'appearances':len(records),'cells':cells,'boxCandidates':boxCandidates,'results':results},indent=2))
