from pathlib import Path
import json, math, hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFont
out=Path(__file__).resolve().parent; root=out.parents[1]; repo=root/'empire-c01'
basepath=out/'rear-house-v1-whole-base-result.json'; base=json.loads(basepath.read_text()); reg=json.loads((out/'registration-frozen-input.json').read_text())
auditpath=repo/'godot/docs/art-provenance/luojia-geometry.json'; audit=json.loads(auditpath.read_text()); group=next(v for v in audit['blockedPolygons'] if v['id']=='west-inner-back-house-foundation')
sourcepath=repo/'godot/assets/native/scenes/luojia.png'; source=Image.open(sourcepath).convert('RGB'); assetpath=repo/'godot/assets/native/c07-layered-v1/west-inner-rear-house-v1-candidate.png'; im=Image.open(assetpath).convert('RGBA'); a=np.asarray(im)[:,:,3]
levelpath=repo/'godot/data/grayflag-campaign-levels.json'; level=next(v for v in json.loads(levelpath.read_text()) if v['id']=='c01-07')
areas={tuple(v['cell']):v['positiveArea'] for v in base['canonicalPositiveAreaCells']}
s=reg['scale']; ox,oy=reg['worldOrigin']; w,h=im.size
# Exact continuous area of box texels under the fixed uniform transform, clipped
# against each 16x16 cell. This is an unfiltered material projection, not a
# statement about GPU interpolation, scene depth visibility or ground support.
def alpha_area(x,y):
    left,top,right,bottom=16*x,16*y,16*x+16,16*y+16
    xl=max(0,math.floor((left-ox)/s)); xr=min(w,math.ceil((right-ox)/s)); yt=max(0,math.floor((top-oy)/s)); yb=min(h,math.ceil((bottom-oy)/s))
    xs=np.arange(xl,xr); ys=np.arange(yt,yb)
    wx=np.maximum(0,np.minimum(right,ox+(xs+1)*s)-np.maximum(left,ox+xs*s))
    wy=np.maximum(0,np.minimum(bottom,oy+(ys+1)*s)-np.maximum(top,oy+ys*s))
    weights=wy[:,None]*wx[None,:]; aa=a[yt:yb,xl:xr]
    return {str(t):float(np.sum(weights[aa>=t])) for t in [1,9,128,200,245]}
rows=[]
for cell in group['blockedCells']:
    x,y=cell; ca=alpha_area(x,y); ground=areas.get(tuple(cell),0)
    rows.append({'cell':cell,'terrain':level['terrain'][y][x],'newBasePositiveArea':ground,'candidateMaterialAreaByMinimumAlpha':ca,'category':'new-ground-domain-positive-area' if ground else ('projected-material-only-no-ground-domain' if ca['9']>0 else 'no-candidate-substantive-material'),'substantiveAlphaDefinition':'A>=9 excludes generated A1..8 scatter but does not redefine texture or engine opacity; all alpha thresholds retained separately.','originalCenterPixel':{'source':[16*x+8,16*y+8],'rgb':list(source.getpixel((16*x+8,16*y+8)))},'otherCoarseAuditResponsibilityIds':[q['id'] for q in audit['blockedPolygons'] if q['id']!=group['id'] and cell in q['blockedCells']]})
# Fixed source grid: reference pixels only, no source asset mutation.
box=(328,352,440,448); z=8; crop=source.crop(box).resize(((box[2]-box[0])*z,(box[3]-box[1])*z),Image.Resampling.NEAREST); draw=ImageDraw.Draw(crop)
for x in range(21,28):
    xx=(x*16-box[0])*z; draw.line((xx,0,xx,crop.height),fill=(30,200,230),width=1)
for y in range(22,29):
    yy=(y*16-box[1])*z; draw.line((0,yy,crop.width,yy),fill=(30,200,230),width=1)
for v in rows:
    x,y=v['cell']; p=((x*16-box[0])*z+3,(y*16-box[1])*z+3); draw.rectangle((p[0],p[1],p[0]+55,p[1]+13),fill=(0,0,0));draw.text(p,f'{x},{y}',fill=(255,255,255))
crop.save(out/'original-19-cell-responsibility-grid.png')
original_observations = {
 (21,23):'Foreground yellow-green foliage; a diagonal neighboring wall/eave edge lies near the upper-left. No original rear-house ground foot certified in this cell.',
 (22,23):'Rear-house upper gable/plaster and sloping eave, partly occluded by foliage. Not a visible ground-foot band.',
 (23,23):'Rear-house upper gable with narrow window and roof/eave edge. Not a visible ground-foot band.',
 (24,23):'Orange roof material. A projected roof is not the physical foundation of this grid cell.',
 (25,23):'Orange roof and adjacent blue-roof projection at the right edge. Neither is ground-foot evidence here.',
 (21,24):'Foreground foliage and woody/trunk-colored material; tree root/ground contact is not certified within this cell.',
 (22,24):'Foreground green foliage masking the rear-house wall. Ground ownership behind the foliage remains unproved.',
 (21,25):'Foreground tree foliage/trunk region; no certified rear-house stone foot or visible tree-root foundation in this cell.'
}
bluepath=root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json'
blue=json.loads(bluepath.read_text())['blueNewClosedBase']['polygon']; blue_min_y=min(p[1] for p in blue)
for row in rows:
    p=row['originalCenterPixel']['source']; q=[math.floor((p[k]-reg['worldOrigin'][k])/s) for k in range(2)]
    row['candidateCenterPixel']={'asset':q,'rgba':list(im.getpixel(tuple(q)))}
    if row['newBasePositiveArea']==0:
        row['originalMaterialObservation']=original_observations[tuple(row['cell'])]
        row['selectedBlueGroundDomain']={'intersectionArea':0,'separationProof':'Cell bottom <=416; full selected blue NEW ground domain minimumY=421.6. Blue roof projection is not physical takeover.'}
        row['neighborOwnershipEvidence']={'coarseAuditOnly':row['otherCoarseAuditResponsibilityIds'],'status':'No transfer of ground responsibility certified. Overlapping coarse old wall audit is only a lead; hidden wall/tree bases are not measured here.'}
        row['remainingResponsibility']='Unresolved physical responsibility, not automatically bare house foundation. No arbitrary infill or neighboring-owner assignment.'
counts={name:sum(row['category']==name for row in rows) for name in ['new-ground-domain-positive-area','projected-material-only-no-ground-domain','no-candidate-substantive-material']}
report={'ready':True,'scope':'Fixed 19-cell legacy responsibility projected separately onto NEW ground domain and immutable candidate alpha, plus original source evidence. No engine/GUI/actions or production modifications.','inputHashes':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [basepath,assetpath,sourcepath,auditpath,levelpath]},'uniformRegistration':{k:reg[k] for k in ['scale','anchorPixels','worldAnchor','worldOrigin']},'rows':rows,'classificationCounts':counts,'selectedBlueBaseMinimumY':blue_min_y,'remainingEightCells':[r['cell'] for r in rows if r['newBasePositiveArea']==0],'closureConclusion':'The local house geometry may be assessed independently, but the old 19-cell physical responsibility is not closed: eleven cells have some NEW base area, seven only elevated projection, one no substantive candidate material. No remaining cell is silently assigned to the blue house or a courtyard wall.','alphaMethod':'Exact positive texel-box overlap area under source=origin+asset*scale, measured independently per16x16 cell. A1/9/128/200/245 all reported. No interpolation, clipping by other scene objects, or GPU rendered visibility claimed.','groundMethod':'Unchanged 759 visible stone-foot polyline plus explicit NEW hidden rear corner, from preceding whole-base report; never substitute alpha or old audit polygon.','limits':['Per-cell positive ground area does not mean the full cell is filled.','Projected roof/body pixels do not certify foundation, collision or all original physical responsibility.','Coarse overlapping audit ownership is a clue only, not certified neighboring ground support.']}
(out/'rear-house-v1-19-cell-responsibility-projection.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps([{'cell':v['cell'],'baseArea':v['newBasePositiveArea'],'alpha9Area':v['candidateMaterialAreaByMinimumAlpha']['9'],'alpha200Area':v['candidateMaterialAreaByMinimumAlpha']['200'],'category':v['category'],'otherAudit':v['otherCoarseAuditResponsibilityIds']} for v in rows],indent=2))
