from pathlib import Path
import ast, hashlib, json, math
import numpy as np
from PIL import Image

out = Path(__file__).resolve().parent
root = out.parents[1]
repo = root / 'empire-c01'
regpath = root / 'c07-static-preview/west-inner-rear-house-v1-registration.json'
bluepath = root / 'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json'
frozenpath = root / 'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json'
levelpath = repo / 'godot/data/grayflag-campaign-levels.json'
assetpath = repo / 'godot/assets/native/c07-layered-v1/west-inner-rear-house-v1-candidate.png'
reg = json.loads(regpath.read_text())
blue = json.loads(bluepath.read_text())['blueNewClosedBase']['polygon']
frozen = json.loads(frozenpath.read_text())
level = next(v for v in json.loads(levelpath.read_text()) if v['id'] == 'c01-07')
im = Image.open(assetpath).convert('RGBA')
assert hashlib.sha256(assetpath.read_bytes()).hexdigest() == reg['assetSha256']

# Import only pure math definitions; never execute another report's top-level writers.
mathpath = root / 'c07-static-preview/central-rear-west-bed-review/verify-rear-west-actual-four-leg-footing.py'
scanpath = root / 'c07-readonly-review/right-garden-source-study/stone-v2-foot-study/verify-thin-stone-wall.py'
for path, names in [(mathpath, ['inside', 'cross', 'area', 'intersection', 'cc', 'ptseg']), (scanpath, ['boundary_scan'])]:
    tree = ast.parse(path.read_text())
    exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name in names], type_ignores=[]), str(path), 'exec'))

def world(p):
    return (np.asarray(p, float) * reg['scale'] + reg['worldOrigin']).tolist()

feet = [world(p) for p in reg['visibleStoneFoot']]
L, R, F = np.array(feet[0]), np.array(feet[-1]), np.array(world(reg['anchorPixels']))
H = L + R - F
poly = feet + [H.tolist()]
lo, hi = np.min(poly, axis=0), np.max(poly, axis=0)
assert len(feet) == 759 and [p[0] for p in reg['visibleStoneFoot']] == list(range(330, 1089))
assert max(p[1] for p in reg['visibleStoneFoot']) == reg['anchorPixels'][1]

cells = []
for y in range(math.floor(lo[1] / 16), math.floor(hi[1] / 16) + 1):
    for x in range(math.floor(lo[0] / 16), math.floor(hi[0] / 16) + 1):
        clipped = intersection(poly, [[16*x, 16*y], [16*x+16, 16*y], [16*x+16, 16*y+16], [16*x, 16*y+16]])
        a = area(clipped)
        if a > 1e-8:
            cells.append({'cell': [x, y], 'terrain': level['terrain'][y][x], 'positiveArea': a, 'intersectionPolygon': clipped})

# Bounded canonical neighborhood includes a two-cell margin beyond the whole base.
# Check all directed adjacent legal edges, both real walk phases, actual horizontal
# facing and both possible retained facings for vertical movement. No engine action.
xrange = [math.floor(lo[0] / 16) - 2, math.floor(hi[0] / 16) + 2]
yrange = [math.floor(lo[1] / 16) - 2, math.floor(hi[1] / 16) + 2]
legal = [(x,y) for y in range(yrange[0], yrange[1]+1) for x in range(xrange[0], xrange[1]+1) if level['terrain'][y][x] not in '#_']
edges = [(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1]) == 1]
aa, bb, meta = [], [], []
for a,b in edges:
    for west in ([False,True] if a[0] == b[0] else [b[0] < a[0]]):
        for frame in frozen['frames']:
            for p in frame['sole']:
                offset = [(p[0]-frame['anchor'][0])*frozen['scale']*(-1 if west else 1), (p[1]-frame['anchor'][1])*frozen['scale']]
                aa.append([16*a[k]+8+offset[k] for k in range(2)])
                bb.append([16*b[k]+8+offset[k] for k in range(2)])
                meta.append({'fromCell': a, 'toCell': b, 'west': west, 'frame': frame['index'], 'soleFramePixel': p})
A, B = np.array(aa), np.array(bb)
walk = boundary_scan(A, B, poly, meta)
possible = np.flatnonzero((np.all(A >= lo, axis=1)&np.all(A <= hi, axis=1)) | (np.all(B >= lo, axis=1)&np.all(B <= hi, axis=1)))
inside_ids = [int(i) for i in possible if inside(A[i],poly) or inside(B[i],poly)]
BA = np.array(blue); BB = np.roll(BA,-1,axis=0)
relation = boundary_scan(BA, BB, poly, [{'blueBoundaryEdge':i} for i in range(len(BA))])
rear_in_blue = [i for i,p in enumerate(poly) if inside(p,blue)]
blue_in_rear = [i for i,p in enumerate(blue) if inside(p,poly)]
foot_samples = []
for p, source in zip(reg['visibleStoneFoot'], feet):
    cell = [math.floor(v/16) for v in source]
    foot_samples.append({'asset':p, 'source':source, 'rgba':list(im.getpixel(tuple(p))), 'cell':cell, 'terrain':level['terrain'][cell[1]][cell[0]]})

auditpath = repo / 'godot/docs/art-provenance/luojia-geometry.json'
auditgroup = next(v for v in json.loads(auditpath.read_text())['blockedPolygons'] if v['id'] == 'west-inner-back-house-foundation')
cell_areas = {tuple(v['cell']):v['positiveArea'] for v in cells}
audit_cells = {tuple(v) for v in auditgroup['blockedCells']}
responsibility_rows = [{'cell':p, 'newDesignedBaseArea':cell_areas.get(tuple(p),0), 'meaning':'Positive new ground-domain area only; no original whole-cell foundation claim.'} for p in auditgroup['blockedCells']]
soleassetpath = repo / 'godot' / frozen['assetPath'].removeprefix('res://')
soleim = Image.open(soleassetpath).convert('RGBA')
sole_alpha = []
for frame in frozen['frames']:
    values = [soleim.getpixel((p[0]+frame['frameRegion'][0], p[1]+frame['frameRegion'][1]))[3] for p in frame['sole']]
    sole_alpha.append({'frame':frame['index'],'points':len(values),'alphaRange':[min(values),max(values)],'unchangedFrozenRoi':frame['roi']})
    assert min(values) >= 200
inputs = {name:{'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()} for name,path in [('registration',regpath),('candidate',assetpath),('blueWholeBase',bluepath),('frozenSoles',frozenpath),('canonicalLevel',levelpath),('mathHelper',mathpath),('scanHelper',scanpath),('originalAudit',auditpath),('soleAsset',soleassetpath)]}
report = {
    'ready':True,
    'scope':'Bounded read-only source/candidate pixel inspection and exact continuous segment math. No GUI, engine action, gameplay/save access or production modifications.',
    'inputs':inputs,
    'registration':{k:v for k,v in reg.items() if k != 'visibleStoneFoot'},
    'candidatePixelInspection':{
        'size':list(im.size), 'visibleStoneFootCount':len(feet),
        'footAlphaRange':[min(v['rgba'][3] for v in foot_samples),max(v['rgba'][3] for v in foot_samples)],
        'nearOutlineRgba':list(im.getpixel(tuple(reg['anchorPixels']))),
        'meaning':'Manually inspected low masonry band, not roof/canvas bottom. The fixed near point is a dark opaque stone-outline contact, not pale stone interior. No door step protrudes beyond the two principal wall axes.',
        'originalContext':'Original rear annex visible front/left wall and orange roof are partly masked by blue house, trees and courtyard stonework. No complete original hidden foundation is certified from the source crop.'
    },
    'newWholeGroundBase':{
        'left':L.tolist(),'frontReferenceAsset':reg['anchorPixels'],'frontReference':F.tolist(),'right':R.tolist(),
        'hiddenRear':H.tolist(),'derivation':'NEW hidden corner L + R - near, fixed near anchor654,1068. Visible 759-point lower contour is retained without filtering or adjustment.',
        'polygon':poly,'area':area(poly),'sourceBounds':[lo.tolist(),hi.tolist()],
        'rearDesignStatus':'New plausible closure along the two visible wall axes, not evidence of original hidden masonry/underground volume.'
    },
    'canonicalPositiveAreaCells':cells,
    'nonBlockedPositiveAreaCells':[c for c in cells if c['terrain'] != '#'],
    'visibleFootSamples':foot_samples,
    'coarseOriginalResponsibilityNotSolidFloor':{
        'id':auditgroup['id'],'oldPolygon':auditgroup['polygonPixels'],'oldBlockedCellCount':len(audit_cells),
        'perOldCellNewGroundArea':responsibility_rows,
        'oldCellsWithNoPositiveNewGroundArea':[r['cell'] for r in responsibility_rows if not r['newDesignedBaseArea']],
        'newGroundBlockedCellsOutsideOldResponsibility':[c['cell'] for c in cells if tuple(c['cell']) not in audit_cells],
        'meaning':'The original 19-cell audit is an ownership envelope, not a certified physical mask. Eleven old cells have positive new base area, eight do not; two additional canonical blocked cells have new base area. No missing cell is automatically assigned to another object, filled, or certified by roof alpha.'
    },
    'blueWholeBaseRelation':{'boundary':relation,'rearVerticesInsideBlue':rear_in_blue,'blueVerticesInsideRear':blue_in_rear,'disjoint':relation['intersectingSegments']==0 and not rear_in_blue and not blue_in_rear},
    'localWalk':{
        'legalBoundsInclusive':{'x':xrange,'y':yrange},'legalCells':legal,'directedEdges':edges,
        'scale':frozen['scale'],'frames':frozen['frames'],'unchangedSoleImageValidation':sole_alpha,'facing':'Horizontal follows movement X; vertical preserves either possible prior east/west facing. Both frozen walk-a/b support-sole contours, continuous interpolation per legal edge.',
        'boundary':walk,'endpointInsideCount':len(inside_ids),'endpointInsideExamples':[meta[i] for i in inside_ids[:5]],
        'noBoundaryOrInteriorIntersection':walk['intersectingSegments']==0 and not inside_ids
    },
    'limits':[
        'No original 19-cell responsibility polygon treated as a solid foundation or filled to satisfy coverage.',
        'Ground-domain disjointness does not certify roof/eave layering, hidden 3D architecture or native visual quality.',
        'Frozen manually inspected Torren support-boot contours only, not all boots or all character classes; conservative phase coverage, not a performed movement.',
        'All 759 supplied fixed foot samples retained; no registration adjustment or replacement sample chosen.'
    ]
}
(out/'registration-frozen-input.json').write_text(json.dumps(reg,indent=2)+'\n')
(out/'rear-house-v1-whole-base-result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'ready':True,'axes':{k:report['newWholeGroundBase'][k] for k in ['left','frontReference','right','hiddenRear','area','sourceBounds']},'cells':[{k:v for k,v in c.items() if k!='intersectionPolygon'} for c in cells],'nonBlocked':report['nonBlockedPositiveAreaCells'],'blueRelation':report['blueWholeBaseRelation'],'walk':walk,'insideCount':len(inside_ids),'legalCellsCount':len(legal),'directedEdgesCount':len(edges)},ensure_ascii=False,indent=2))
