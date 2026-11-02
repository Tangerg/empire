from pathlib import Path
import ast,json,math,hashlib
import numpy as np
from PIL import Image
out=Path(__file__).resolve().parent;root=out.parents[1];repo=root/'empire-c01'
levelpath=repo/'godot/data/grayflag-campaign-levels.json';level=next(v for v in json.loads(levelpath.read_text()) if v['id']=='c01-07')
bluepath=root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json';blue=json.loads(bluepath.read_text())['blueNewClosedBase']['polygon']
rearpath=root/'c07-readonly-review/west-inner-rear-house-review/rear-house-v1-whole-base-result.json';rear=json.loads(rearpath.read_text())['newWholeGroundBase']['polygon']
wallpath=root/'c07-static-preview/left-garden-stone-new-layout.json';walls=json.loads(wallpath.read_text())['components']
frozenpath=root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json';frozen=json.loads(frozenpath.read_text())
rootpath=root/'c07-readonly-review/right-garden-source-study/rooted-vine-foot-review/two-rooted-vine-foot-review.json';rootmodel=json.loads(rootpath.read_text())
mathpath=root/'c07-static-preview/central-rear-west-bed-review/verify-rear-west-actual-four-leg-footing.py';scanpath=root/'c07-readonly-review/right-garden-source-study/stone-v2-foot-study/verify-thin-stone-wall.py'
for path,names in [(mathpath,['inside','cross','area','intersection','cc','ptseg']),(scanpath,['boundary_scan'])]:
 tree=ast.parse(path.read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),str(path),'exec'))
base=[[362,473],[393,488.5],[424,473],[393,457.5]]
soil=[[366,473],[393,486.5],[420,473],[393,459.5]]
relative=[[-1,-1],[1,0],[1.8,-1],[-.2,-2]]
posts=[{'id':name,'groundCenter':p,'height':29,'visibleTopDesign':[p[0],p[1]-29],'physicalFoot':(np.array(p)+relative).tolist()} for name,p in zip(['left','near','right','rear-new-hidden'],base)]
rootparts=[]
rootlocal=[v['pixel'] for v in rootmodel['actualVisibleLowerFootAssetPixels']]+[v['pixel'] for v in rootmodel['newHiddenRearAssetClosure']]
for name,contact,scale in [('left-vine',[369,473],.02),('near-vine',[390,484],.02)]:
 poly=((np.array(rootlocal)-[565,1486])*scale+contact).tolist()
 rootparts.append({'id':name,'anchorPixels':[565,1486],'groundFrontPixels':[570,1486],'contact':contact,'scale':scale,'actualGroundFront':[(570-565)*scale+contact[0],contact[1]],'physicalFoot':poly,'allRootVerticesInsideSoil':all(inside(p,soil) for p in poly)})
legal=[(x,y) for y in range(26,34) for x in range(20,29) if level['terrain'][y][x] not in '#_'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];aa=[];bb=[];meta=[]
for a,b in edges:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for frame in frozen['frames']:
   for p in frame['sole']:
    off=[(p[0]-frame['anchor'][0])*frozen['scale']*(-1 if west else 1),(p[1]-frame['anchor'][1])*frozen['scale']]
    aa.append([16*a[k]+8+off[k] for k in range(2)]);bb.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':frame['index'],'soleFramePixel':p})
A=np.array(aa);B=np.array(bb)
def cells(poly):
 lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);rr=[]
 for y in range(math.floor(lo[1]/16),math.floor(hi[1]/16)+1):
  for x in range(math.floor(lo[0]/16),math.floor(hi[0]/16)+1):
   ar=area(intersection(poly,[[x*16,y*16],[x*16+16,y*16],[x*16+16,y*16+16],[x*16,y*16+16]]))
   if ar>1e-8:rr.append({'cell':[x,y],'terrain':level['terrain'][y][x],'positiveArea':ar})
 return rr
def relation(poly,other):
 C=np.array(other);D=np.roll(C,-1,axis=0);scan=boundary_scan(C,D,poly,[{'otherBoundaryEdge':i} for i in range(len(C))]);pin=[i for i,p in enumerate(poly) if inside(p,other)];oin=[i for i,p in enumerate(other) if inside(p,poly)]
 return {'boundary':scan,'verticesInsideOther':pin,'otherVerticesInside':oin,'disjoint':not pin and not oin and scan['intersectingSegments']==0}
def walk(poly):
 scan=boundary_scan(A,B,poly,meta);lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);ids=np.flatnonzero((np.all(A>=lo,axis=1)&np.all(A<=hi,axis=1))|(np.all(B>=lo,axis=1)&np.all(B<=hi,axis=1)));ins=[int(i) for i in ids if inside(A[i],poly) or inside(B[i],poly)]
 return {'boundary':scan,'endpointInsideCount':len(ins),'endpointInsideExamples':[meta[i] for i in ins[:3]],'clear':not ins and scan['intersectingSegments']==0}
def evaluate(poly):
 c=cells(poly); wr={v['id']:relation(poly,v['physicalFootPolygon']) for v in walls}
 return {'canonicalCells':c,'nonBlockedPositiveArea':[v for v in c if v['terrain']!='#'],'blue':relation(poly,blue),'rearHouse':relation(poly,rear),'stoneWallComponents':wr,'walk':walk(poly)}
basecheck=evaluate(base);postchecks={p['id']:evaluate(p['physicalFoot']) for p in posts};rootchecks={p['id']:evaluate(p['physicalFoot']) for p in rootparts}
# Owner's independently proposed narrower alternative: one bounded extra check,
# not a fit search or silent replacement of the source witnesses.
owner_base=[[360,472],[390,487],[417,473.5],[387,458.5]]
owner_posts=[{'id':name,'groundCenter':p,'height':28,'topDesign':[p[0],p[1]-28],'physicalFoot':(np.array(p)+relative).tolist()} for name,p in zip(['left','near','right','rear-new-hidden'],owner_base)]
owner_soil=[[366,472],[390,484],[411,473.5],[387,461.5]]
owner_roots=[]
for name,contact in [('left-vine',[371,473]),('near-vine',[389,482])]:
 rp=((np.array(rootlocal)-[565,1486])*.02+contact).tolist()
 owner_roots.append({'id':name,'contact':contact,'scale':.02,'anchorPixels':[565,1486],'groundFrontPixels':[570,1486],'actualGroundFront':[contact[0]+.1,contact[1]],'physicalFoot':rp,'allVerticesInsideSoil':all(inside(p,owner_soil) for p in rp)})
owner_checks={'groundEnvelope':evaluate(owner_base),'posts':{v['id']:evaluate(v['physicalFoot']) for v in owner_posts},'roots':{v['id']:evaluate(v['physicalFoot']) for v in owner_roots}}
owner_internal={'rootToPosts':{r['id']:{p['id']:relation(r['physicalFoot'],p['physicalFoot']) for p in owner_posts} for r in owner_roots},'rootsMutually':relation(owner_roots[0]['physicalFoot'],owner_roots[1]['physicalFoot'])}
owner_proposal={'ready':True,'status':'All NEW design, narrower owner proposal. No original hidden-foot claim. Eligible for subsequent native/visual study only.','groundEnvelope':owner_base,'posts':owner_posts,'soilPlane':owner_soil,'roots':owner_roots,'checks':owner_checks,'internalFiniteRootAndPostRelations':owner_internal,'sourceContract':'Same fixed wood positives/wood-owner negatives as main report. No immutable old material point is changed to make this proposal pass.'}
(out/'owner-narrow-open-frame-proposal-check.json').write_text(json.dumps(owner_proposal,ensure_ascii=False,indent=2)+'\n')
sourcepath=repo/'godot/assets/native/scenes/luojia.png';source=Image.open(sourcepath).convert('RGB')
point_specs=[('near-post-visible-wood',[393,465],'wood positive; lower end occluded by stone cap, not foot'),('near-post-low-visible-wood',[393,472],'wood positive; not certified ground contact'),('left-post-visible-rod',[368,458],'dark timber segment; possible pale endpoint below is unclassified, not certified foot'),('right-post-visible-rod',[427,455],'timber segment beside house; lower part hidden by vegetation/stone'),('near-left-beam',[382,453],'timber beam positive'),('near-right-beam',[410,451],'timber beam positive'),('left-open-bay',[377,460],'not timber: dark open bay/background vegetation; negative for wood owner only'),('left-low-open-bay',[383,463],'not timber: dark open bay below frame and left of near post; negative for wood owner only')]
points=[{'id':i,'source':p,'rgb':list(source.getpixel(tuple(p))),'meaning':m} for i,p,m in point_specs]
report={'ready':True,'scope':'One bounded connected open timber/vine courtyard group. All ground proposals explicitly NEW; no engine, movement, GUI, new interaction or production writes.','inputs':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [levelpath,bluepath,rearpath,wallpath,frozenpath,rootpath,sourcepath]},'sourceEvidence':{'crop':[350,420,434,494],'pixelWitnesses':points,'approximatelyVisibleUpperBeamTopology':{'nearLeft':[[365,445],[393,460]],'nearRight':[[393,460],[427,443]],'backSegments':'Intermittent wood beside blue/rear house and leaves around y430..442; no continuous rear frame or foot certified.'},'feetStatus':'Three visible rod/beam regions are clear; near wood disappears at y474..475 behind stone cap. Left/right lower ends are mixed or hidden. No complete original post footing, vine ground-root or original closed timber foundation is certified.','exclude':['selected blue and rear house masonry','near/front stone enclosure','independent pots outside return wall','far trees and far rear perimeter wall'],'holesAreOwnerSpecific':'Wood-negative points may contain plants, stone or house background. Do not turn them into guaranteed whole-group transparent holes.'},'newDesign':{'openFrameGroundEnvelope':base,'meaning':'Envelope for four discrete posts and a zero-height planted soil bed, NOT a solid building foundation or new collision rule. Both axes use slope +/-0.5; edges31x15.5, projected overallwidth62 anddepth31.','posts':posts,'soilPlane':soil,'roots':rootparts,'rootReuseMeaning':'Two registrations reuse the existing right-garden vine actual 74-point woody lower contour plus its explicit NEW small rear closure. Registration anchor5651486 remains separate from real wood groundFront5701486. This is NEW placement, not restoration of an invisible original root.','heightMeaning':'29px uniform new post height keeps near head around393459.5 and side heads362444/424444. Original feet remain unproved; height is design, not a recovered measurement.'},'wholeEnvelopeCheck':basecheck,'finitePostChecks':postchecks,'actualReusedRootChecks':rootchecks,'localWalkScope':{'legalBoundsInclusive':{'x':[20,28],'y':[26,33]},'legalCells':legal,'directedEdges':edges,'testedContinuousSoleSegments':len(A),'frozenSource':str(frozenpath),'contract':'Same frozen real Torren walk-a/b soles. Horizontal follows move direction; vertical retains either prior facing. This is conservative static interpolation, not gameplay.'},'ownerNarrowAlternativeReport':'owner-narrow-open-frame-proposal-check.json','limits':['Ground clearance alone does not certify eave overlap, wood/leaf depth rendering or visual fit. Parent native/visual review still required.','Original source cannot prove any exact root anchor or all four complete post feet here. New hidden geometry is clearly new design.','Do not extend this proposal toward the old west long wall, neighboring street or outside pots.','No blanket front496/503 assigned to all wood/leaves; each post/root/beam must retain its own actual physical or structural support.']}
report['selectionForNextStudy']={'adoptedByOwner': 'review proposal362473→393488.5→424473→393457.5,height29', 'ownerNarrowAlternative': 'Not adopted; retained only as bounded comparison, not a replacement.'}
report['sourceEvidence']['withdrawnPreliminaryNegative']={'source': [381, 466], 'rgb': [79, 58, 33], 'status': 'Withdrawn before contract freeze: brown mixed material is insufficient for wood-negative certification. The initial math-run.json includes this preliminary label and is not the final source contract. Final second wood-negative is383463, RGB13,17,13, visibly within open bay.'}
(out/'open-frame-source-and-new-ground-proposal.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
summary=lambda v:{'nonBlocked':v['nonBlockedPositiveArea'],'blue':v['blue']['boundary']['minimumDistance'],'blueDisjoint':v['blue']['disjoint'],'rear':v['rearHouse']['boundary']['minimumDistance'],'rearDisjoint':v['rearHouse']['disjoint'],'wallMinimum':min(q['boundary']['minimumDistance'] for q in v['stoneWallComponents'].values()),'wallDisjoint':all(q['disjoint'] for q in v['stoneWallComponents'].values()),'walkMinimum':v['walk']['boundary']['minimumDistance'],'walkClear':v['walk']['clear']}
print(json.dumps({'whole':summary(basecheck),'posts':{k:summary(v) for k,v in postchecks.items()},'roots':{k:summary(v) for k,v in rootchecks.items()},'rootInsideSoil':[v['allRootVerticesInsideSoil'] for v in rootparts],'walkCount':len(A),'legal':len(legal),'edges':len(edges),'sourcePoints':points},indent=2))

print(json.dumps({'ownerAlternative':{'whole':summary(owner_checks['groundEnvelope']),'posts':{k:summary(v) for k,v in owner_checks['posts'].items()},'roots':{k:summary(v) for k,v in owner_checks['roots'].items()},'rootInsideSoil':[v['allVerticesInsideSoil'] for v in owner_roots]}},indent=2))
