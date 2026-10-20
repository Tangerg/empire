from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import json,hashlib,numpy as np
O=Path(__file__).parent
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
P=R/'packages/story-candidate-01/assets/final-fantasy-v1'
d=json.load(open(O/'mapping-raw.json'))
visual={
'BORDER-1':('Golden straw stubble clumps and cut wheat on dry ochre earth','Intact material source; no mislabeled object or missing slot'),
'BORDER-2':('Rain-softened brown mud, diagonal ruts, cool gray puddles, scattered pebbles','The only consumed sample a is clipped to generic cardinal arms; diagonal ruts do not turn with the mask'),
'BORDER-3':('Charred furrows, black wheat stubble, tiny ember points','Intact material source; ember texture must remain subdued at native size'),
'BORDER-4':('Paired wheel ruts, bootprints, stones, dry muddy soil','A diagonal rut field is clipped to a generic NESW network; not authored turning tracks'),
'RIVER-1':('Diagonal grassy bank, large stone shoreline, blue water','Window a plus generic path mask can cut shoreline into unrelated fragments; source is a shoreline scene, not a 16-way shore topology'),
'RIVER-2':('Shallow blue water over visible rounded stones and pale flowing ripples','Intact water material source; inherited generic skinny connector masks are not the modern water-area occupancy contract'),
'RIVER-3':('Stone and dirt bridgehead between wooden side beams/posts','Only upper-left window survives in masks; corner posts/beams are fragmented; this is not a reusable standing bridge sprite'),
'RIVER-4':('Broken paving edge and jagged wooden bridge remains over water','Generic masking cuts a compositional broken-edge source into fragments; source explicitly contains broken structure geometry'),
'CAPITAL-1':('Dark uneven stone masonry paving, damp blue seams','Intact material source; old mask topology retained despite walkway label'),
'CAPITAL-2':('Small ordered cobbles with a central drainage strip and square grates','Sample a is offset left of the centered drain; masks can preserve only tiny drain fragments'),
'CAPITAL-3':('Pale cracked limestone paving with intersecting blue gutters and drains','Four overlapping square samples clip portions of the gutter junction; hospital meaning comes from identity/context, not recognizable hospital icon'),
'CAPITAL-4':('Pale octagonal palace paving with a burgundy geometric border','Four square samples fragment the palace border; no complete palace building'),
'SILVERWOOD-1':('Olive/brown leaf litter, moss, twigs, pebbles, small plants','Intact ground material; this same original source slot also feeds the separate HD twin-hills ground derivative'),
'SILVERWOOD-2':('Thick branching bark roots over dark forest soil','The upper-left sample and cardinal mask cut a unique root network; do not count root branches as independent art'),
'SILVERWOOD-3':('Straw mat or thatched edge, pebble border, packed earth and leafy planting','Source is a linear boundary, not a nursery building; independent crops lose continuity'),
'SILVERWOOD-4':('Dense layered green leaves and ferns','Intact close-up foliage material; do not reinterpret it as standing tree scenery'),
'FORGE-1':('Dark forge paving crossed by iron trench grates and square service cover','Four samples truncate the grate junction; source art is flat paving, not standalone mechanism'),
'FORGE-2':('Bright amber molten channel between metal-lined dark stone banks','Upper-left material sample and generic masking fragment central lava channel; no evidence that each resulting arm is an authored lava bend'),
'FORGE-3':('Dark hexagonal stone tiles with bronze rune seams and small junction emblems','Intact paving material; avoid inventing legible text or new rune symbols'),
'FORGE-4':('Dark paving with brass rails or archive machinery strips and scattered parchment','Four source windows split mechanical runs and paper fragments; archive label is context-supported, not a separate building'),
'WASTELAND-1':('Wind-eroded ochre earth, shallow cracks, small gray stones','Intact ground material'),
'WASTELAND-2':('Tiered rock shelves and ledges with visible vertical faces','Not a flat rock texture: masking the upper-left shelf can sever elevation cues; do not infer elevation/gameplay changes'),
'WASTELAND-3':('Bare camp soil, woven rugs, tent stakes, sticks and small stones','Four crops fragment a camp composition; not four independent camps or props'),
'WASTELAND-4':('Dark wind-scoured soil with diagonal pale erosion streaks and dry grass','Storm identity is visual weathering only; no lightning or new hazard geometry in source'),
'GRAVEYARD-1':('Bare grass-flecked earth enclosed by a ring of loose round stones','Label says ordinary gravestone ground, but source slot contains no obvious standing gravestones; preserve observed ring rather than invent graves'),
'GRAVEYARD-2':('Ordered rows of upright name/grave markers inside a stone border with small warm lamps','Four crop windows clip markers and ring; top/bottom of original composition omitted'),
'GRAVEYARD-3':('Dark cracked soil with faint desaturated purple staining and dead tufts','Intact contaminated ground; avoid substituting bright magical goo'),
'GRAVEYARD-4':('Concentric stone rings with rectangular name markers and small amber lamps','Four square samples sever the purification circle; no full circle remains as a canonical tile'),
'OATHLIGHT-1':('Gray polygonal stone centered on a gold hub with six small circular nodes','Four overlapping samples cut the hub, yielding emblem fragments rather than four authored node designs'),
'OATHLIGHT-2':('Gray paving, gold controlled hub, straight band and triangular frame','Only upper-left crop enters generic connector masks; central control seal is clipped'),
'OATHLIGHT-3':('Gray paving, broken radial bands, dim disconnected hub','Only upper-left crop enters generic connector masks; no new broken-state topology should be invented'),
'OATHLIGHT-4':('Loose network of round stone/gold node discs linked by colored fine cords','Four crops clip the network; source supports a less rigid open network, not a distinct independent sigil per variant')}
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
edge_summary=[]
for rec in d['topics']:
 short=rec['topicId'][12:]
 obs,risk=visual[short]
 rec['observedSourceIdentity']=obs;rec['sourceIntegrity']={'mappingVerified':True,'wholeSourceSlotIntact':True,'note':risk,'notClaimed':'No exact original natural-language prompt or missing manifest-complete topic order was recovered; mapping is independently proven by pixel equality.'}
 rec['consumerStatus']={'catalogLoaded':True,'selectedTexture':'runtime-hd','pixelDensity':2,'gameplayGroundConsumerFound':False,'staticTopicReferenceFound':False,'runtimeRole':'catalog-only canonical terrain; current ground uses environment/world-material owners','liveGameObserved':False}
 rec['anchorContract']={'declaredAnchor':None,'declaredFootprint':None,'cellOrigin':[0,0],'logicalCell':[32,32],'note':'Flat top-down tile grid; no standing sprite foot or bottom-center anchor may be inferred.'}
 rec['variantsAreDerived']=True
 rec['independentIdentityCount']=1
 rec['remakeStatus']='not-remade-approved-v5'
 rec['edges']={}
 for den in (1,2):
  im=Image.open(R/rec['runtime'][str(den)]['path']).convert('RGBA');a=np.array(im);s=32*den;n=im.width//s;tiles=[a[:,k*s:(k+1)*s] for k in range(n)]
  checks=[]
  for direction in ('horizontal','vertical'):
   for i in range(n):
    for j in range(n):
     if rec['manifest']['tileMode']=='nesw-16':
      if direction=='horizontal' and not(i&2 and j&8):continue
      if direction=='vertical' and not(i&4 and j&1):continue
     ea,eb=(tiles[i][:,-1],tiles[j][:,0]) if direction=='horizontal' else (tiles[i][-1,:],tiles[j][0,:])
     visible=(ea[:,3]>0)&(eb[:,3]>0)
     diff=np.abs(ea[visible,:3].astype(int)-eb[visible,:3].astype(int))
     checks.append({'axis':direction,'from':i,'to':j,'alphaEqual':bool(np.array_equal(ea[:,3],eb[:,3])),'rgbEqual':bool(np.array_equal(ea[visible,:3],eb[visible,:3])),'meanAbsoluteRGBDifference':round(float(diff.mean()),3) if len(diff) else 0,'maxRGBDifference':int(diff.max()) if len(diff) else 0})
  stats={'checks':len(checks),'allAlphaEdgesMatch':all(x['alphaEqual'] for x in checks),'allColorEdgesMatch':all(x['rgbEqual'] for x in checks),'exactRGBPairs':sum(x['rgbEqual'] for x in checks),'meanAbsoluteRGBDifference':round(float(np.mean([x['meanAbsoluteRGBDifference'] for x in checks])),3),'maxRGBDifference':max(x['maxRGBDifference'] for x in checks)}
  rec['edges'][str(den)]=stats
  edge_summary.append({'topicId':rec['topicId'],'density':den,**stats})
  if den==2:
   # Contact proof: each source family strip is copied without alteration then nearest upscaled for native pixel inspection.
   pass
# Separate consumer-source dependencies are explicitly not canonical topic bindings.
d['sharedSourceDerivatives']=[{'id':'C01-ENV-TWIN-HILLS-GROUND','sourceTopicSlot':'C01-TERRAIN-SILVERWOOD-1','sourceBoard':d['boards'][1]['path'],'cropXYXY':[24,567,338,1062],'derivedPath':'packages/story-candidate-01/assets/final-fantasy-v1/runtime-hd/environment/env-twin-hills-ground.png','independentIdentityCount':0,'sourceEvidence':'build_tactical_runtime_hd.py build_environment: grid_cell(source,4,2,0,1,24), tint/mirror mosaic','currentConsumerReferenceFound':False}]
# actual written derived path checked below
allpng=list((P/'runtime-hd/environment').glob('*.png'))
for p in allpng:d['sharedSourceDerivatives'][0]['availableFiles']=[str(x.relative_to(R)) for x in allpng]
d['audit']={'scope':'C01 canonical terrain32 only','readOnlyRepository':True,'repository':'/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019','checkpoint':'b179027da36ed4642a76c3b823e50fe3b40b4cfe','uniquePixelMappings':32,'verifiedDensityOutputs':64,'sourceBoards':4,'sourceCells':32,'canonicalIdentities':32,'variantsTopics':18,'neswTopics':14,'tilesPerDensity':296,'authoredIndependentTopicsApprovedV5':0,'currentGameplayGroundBindings':0,'environmentRegisteredCellsSeparate':186,'allSourceFilesDecodable':True,'sourceOrderRecoveredBy':'unique exact regenerated RGBA pixels at both densities, not manifest sort','originalBuilderExecutableAsWhole':False,'originalBuilderBlocker':'assets/manifest-complete.json absent; do not rerun original main. Replay only terrain math with explicit recovered mapping.'}
d['filesToRead']=['packages/story-candidate-01/src/presentation/candidate-01-assets.ts','packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts','packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts','packages/story-candidate-01/src/presentation/candidate-01-runtime.ts','packages/story-candidate-01/src/presentation/candidate-01-runtime.test.ts','packages/story-candidate-01/assets/final-fantasy-v1/tools/build_final_fantasy_v1.py','packages/story-candidate-01/assets/final-fantasy-v1/tools/build_tactical_runtime_hd.py']
d['sourceFileHashes']=[{'path':x,'sha256':hashlib.sha256((R/x).read_bytes()).hexdigest()} for x in d['filesToRead']]
json.dump(d,open(O/'canonical-terrain-mapping.json','w'),indent=2,ensure_ascii=False)
json.dump(edge_summary,open(O/'tile-edge-audit.json','w'),indent=2)
for family in ['BORDER','RIVER','CAPITAL','SILVERWOOD','FORGE','WASTELAND','GRAVEYARD','OATHLIGHT']:
 rows=[r for r in d['topics'] if r['topicId'].startswith('C01-TERRAIN-'+family+'-')]
 sheet=Image.new('RGB',(1152,4*160),'#eeeae2');draw=ImageDraw.Draw(sheet)
 for k,r in enumerate(rows):
  y=k*160;draw.text((8,y+5),r['topicId']+' | 1x logical pixels enlarged 2x | '+r['manifest']['tileMode'],fill='black',font=font)
  im=Image.open(R/r['runtime']['1']['path']).convert('RGBA').resize((r['manifest']['width']*2,64),Image.Resampling.NEAREST)
  checker=Image.new('RGB',im.size,'#d8d5cc');cd=ImageDraw.Draw(checker)
  for cy in range(0,64,8):
   for cx in range(0,im.width,8):
    if (cx//8+cy//8)%2:cd.rectangle((cx,cy,cx+7,cy+7),fill='#a4a7a0')
  checker.paste(im,mask=im.getchannel('A'));sheet.paste(checker,(8,y+30))
  for i in range(im.width//64):draw.text((8+i*64,y+101),str(i),fill='black',font=font)
  if r['manifest']['tileMode']=='variants':
   draw.text((8,y+125),'a / b / c / d are overlapping source windows; no full motif guarantee',fill='black',font=font)
  else:draw.text((8,y+125),'0..15 = N1 E2 S4 W8; same RGB sample a, clipped by generated alpha',fill='black',font=font)
 sheet.save(O/(family.lower()+'-runtime-proof.png'))
print('wrote detailed mapping, edges and 8 runtime crop proofs')
print('color-edge-perfect',sum(x['allColorEdgesMatch'] for x in edge_summary),'/',len(edge_summary))
