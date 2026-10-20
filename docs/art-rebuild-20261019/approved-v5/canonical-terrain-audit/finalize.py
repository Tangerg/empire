from pathlib import Path
from PIL import Image
import json,csv,hashlib
O=Path(__file__).parent
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
d=json.load(open(O/'canonical-terrain-mapping.json'))
refs=O/'first-family-references';refs.mkdir(exist_ok=True)
first=[]
for r in d['topics']:
 if not r['topicId'].startswith('C01-TERRAIN-BORDER-'):continue
 image=Image.open(R/r['source']['board']);crop=image.crop(r['source']['cropXYXY'])
 p=refs/(r['topicId'].lower()+'-source-slot.png');crop.save(p)
 first.append({'topicId':r['topicId'],'label':r['label'],'observedIdentity':r['observedSourceIdentity'],'referencePath':str(p),'referenceSHA256':hashlib.sha256(p.read_bytes()).hexdigest(),'sourceBoard':r['source']['board'],'sourceBoardSHA256':r['source']['boardSHA256'],'sourceSlotZeroBased':[r['source']['slotColumn'],r['source']['slotRow']],'sourceCropXYXY':r['source']['cropXYXY'],'referenceRole':'Exact original source-slot crop, unedited pixels; appearance/motif reference, not finished artwork','tileMode':r['manifest']['tileMode'],'frames':r['manifest'].get('variants',r['manifest'].get('connectionMasks')),'runtimeDimensions1x':r['runtime']['1']['dimensions'],'runtimeDimensions2x':r['runtime']['2']['dimensions'],'logicalTileSize':[32,32],'physicalTileSize':[64,64],'alpha':'fully opaque' if r['manifest']['tileMode']=='variants' else 'reuse exact historical per-mask binary alpha at each density','anchor':None,'canonicalCount':1,'generationStatus':'not-generated'})
style=Path('/workspace/shared/empire-art-rebuild/calibration-v5-concept/concept-v2.png')
contract={
'schemaVersion':1,'status':'audit-only, no artwork generated or repository edited','family':'C01-TERRAIN-BORDER-1..4','topics':first,
'styleReference':{'path':str(style),'sha256':hashlib.sha256(style.read_bytes()).hexdigest(),'role':'approved chunky/matte visual language only; original terrain crop owns identity'},
'proposedArtDirection':['Top-down flat ground; broad simplified matte forms, restrained ochre/soil/charcoal palette, minimum high-frequency microtexture','Keep golden cut-straw clumps, rainy mud with cool puddles, blackened agricultural furrows and quiet embers, and cart ruts/foot impressions distinguishable','No new buildings, bridges, props, navigation routes, elevation, collision, or hazard shapes','For connected topics, material detail must not pretend that a fixed diagonal rut is an authored N/E/S/W turn; preserve source motifs without adding route topology','Keep any complete small motif inside a tile-safe interior; peripheral marks must cross seams coherently rather than end in cropped fragments'],
'exportContract':{'density':2,'masks':'0..15 in integer order, N=1 E=2 S=4 W=8; 0 is isolated central pad, 15 is four-way connection','1xMask':{'coreRoundedRectangleInclusive':[10,10,21,21],'radius':4,'N':[12,0,19,16],'E':[16,12,31,19],'S':[12,16,19,31],'W':[0,12,16,19]},'2xMask':{'coreRoundedRectangleInclusive':[20,20,43,43],'radius':8,'N':[24,0,39,32],'E':[32,24,63,39],'S':[24,32,39,63],'W':[0,24,32,39]},'variantOrder':['a','b','c','d'],'1xVariantStrip':[128,32],'2xVariantStrip':[256,64],'1xConnectedStrip':[512,32],'2xConnectedStrip':[1024,64],'noImplicitAnchor':'No anchor or footprint currently declared; flat cell origin 0,0. Never introduce planted-foot sprite positioning.','preserveLogicalMetadata':True,'doNotReplaceWithEnvironmentAtlas':True},
'acceptance':['Both 1x and 2x sheet dimensions and mode metadata unchanged','For each connected tile, alpha matches the exact baseline mask at that density, including all closed edges and open 8px/16px spans','Opaque variants have alpha=255 everywhere; connected strips have real alpha=0 outside mask, never painted checkerboards','Texture seams: compare every a/b/c/d E-W and S-N pairing; compare all open connected-edge pairings; shared border signature required in deliverable, not presumed from the old source','Review 2x2 identical and 4x4 mixed opaque tilings; connected isolated/end/straight/corner/T/cross fixtures; native 32px cell scale plus 2x inspection','Source-grounded motif review against exact source slots, plus approved-v5 actor/material cohesion fixture','Formal catalog registration and HD resolver tests; keep gameplay status catalog-only unless a separate existing consumer is explicitly bound','Count4canonical identities only after raw/export/formal-catalog stages pass; no environment increment and no live-game assertion'],
'currentBaselineEdges':'All alpha edges match the existing contract; no topic is RGB edge-perfect at either density. The old canonical builder does not lock tile color edges.',
'boundaries':'A source-grounded redraw may simplify image motifs and make the material periodic. It must not change NESW mask geometry, gameplay terrain, map layout, or substitute current environment sources and call them canonical completions.'}
notes_path=O/'first-family-reviewed-notes.json'
if notes_path.exists():
 reviewed=json.load(open(notes_path))
 for key in ['authorshipAndDerivation','defectDisposition','acceptance']:
  contract[key]=reviewed[key]
 for topic in contract['topics']:
  topic.update(reviewed['topics'][topic['topicId']])
json.dump(contract,open(O/'first-family-border-contract.json','w'),indent=2,ensure_ascii=False)
with open(O/'canonical-terrain-mapping.csv','w') as f:
 w=csv.writer(f);w.writerow(['topicId','label','sourceBoard','sourceSHA256','globalIndex0','slotColumn0','slotRow0','cropXYXY','tileMode','runtime1x','runtime2x','anchor','mapping','currentGroundConsumer','observedSourceIdentity','fragmentRisk'])
 for r in d['topics']:
  s=r['source'];w.writerow([r['topicId'],r['label'],s['board'],s['boardSHA256'],s['sourceIndex'],s['slotColumn'],s['slotRow'],json.dumps(s['cropXYXY']),r['manifest']['tileMode'],json.dumps(r['runtime']['1']['dimensions']),json.dumps(r['runtime']['2']['dimensions']),'none-declared',r['mapping'],'none-found/catalog-only',r['observedSourceIdentity'],r['sourceIntegrity']['note']])
# Hash audit inputs relevant to scope/progress and environment provenance; no full-board copies.
paths=['docs/art-rebuild-20261019/canonical-topic-manifest.json','docs/art-rebuild-20261019/approved-v5/progress.json','docs/art-rebuild-20261019/approved-v5/scope-ledger.json','packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json','packages/story-candidate-01/assets/final-fantasy-v1/manifest-tactical-runtime-hd.json','packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/tools/build_environment_builder_v1.py','packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/surface-common-4x4.png','packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/surface-regional-4x4.png','packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/route-materials-4x4.png','packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/water-materials-4x4.png']
json.dump([{'path':p,'sha256':hashlib.sha256((R/p).read_bytes()).hexdigest()} for p in paths],open(O/'input-checkpoint-hashes.json','w'),indent=2)
print('first family and CSV ready')
