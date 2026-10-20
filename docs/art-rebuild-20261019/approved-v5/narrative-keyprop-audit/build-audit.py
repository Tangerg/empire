#!/usr/bin/env python3
"""Read-only repo audit. Only original PNG crops and reports are saved beside this file."""
import ast, hashlib, json, re, sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image, ImageOps
sys.dont_write_bytecode = True
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
O=Path(__file__).resolve().parent
P=R/'packages/story-candidate-01/assets/final-fantasy-v1'
load=lambda p:json.loads(p.read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rel=lambda p:str(p.relative_to(R))
old=load(O.parent/'remaining-narrative-audit/manifest.json')
assets=load(P/'manifest-final-fantasy-v1.json')['assets']
byid={a['topicId']:a for a in assets}
ledger=load(R/'docs/art-rebuild-20261019/approved-v5/scope-ledger.json')
led={a['topicId']:a for a in ledger['canonicalTopics']}
hd={a['topicId']:a for a in load(P/'manifest-tactical-runtime-hd.json')['assets']}
overrides=load(R/'tools/art/source-overrides.json')['assets']
consumers=load(O/'consumers.json')
selected=[a for a in assets if a['category']=='narrative-static' and (a['topicId'].startswith('C01-PROP-') or a.get('kind')=='key-prop')]
assert len(selected)==17
catalog=R/'docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md'
section=catalog.read_text().split('## 叙事静态图，共 80 项')[1].split('\n## ')[0]
order=re.findall(r'^- `(C01-[^`]+)`：(.+)$',section,re.M)
builder=P/'tools/build_final_fantasy_v1.py'
tree=ast.parse(builder.read_text())
source_map=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='NARRATIVE_SOURCES' for t in n.targets))
# Execute only image-in/image-out helpers to reproduce old pixels in memory. Never run the builder.
ns={'Image':Image,'ImageOps':ImageOps,'RESAMPLE':Image.Resampling.LANCZOS}
helpers=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'grid_cell','clear_white_edge_gutter','alpha_bbox','fit_transparent','fit_opaque'}]
exec(compile(ast.Module(body=helpers,type_ignores=[]),str(builder),'exec'),ns)
slots={}
for board,indices in source_map.items():
 family='characters' if board.startswith('character') else 'props' if board.startswith('prop') else 'scenes'
 for cell,index in enumerate(indices):
  if index is not None:slots[order[index][0]]=(P/'masters/narrative'/family/board,cell,index)

def alpha(im):
 if im.mode!='RGBA':return {'mode':im.mode,'hasAlpha':False,'effectiveAlphaExtrema':[255,255]}
 a=im.getchannel('A');h=a.histogram();w,y=im.size
 edge=[a.getpixel((x,z)) for z in (0,y-1) for x in range(w)]+[a.getpixel((x,z)) for x in (0,w-1) for z in range(1,y-1)]
 return {'mode':im.mode,'hasAlpha':True,'extrema':list(a.getextrema()),'zeroPixels':h[0],'partialPixels':sum(h[1:255]),'opaquePixels':h[255],'nearOpaquePixels':sum(h[240:]),'edgeMaxAlpha':max(edge),'alphaBoundingBox':list(a.getbbox()) if a.getbbox() else None}

notes={
'FIRST-GREY-FLAG':('Ragged smoky gray cloth with stitch repairs on a wood T-pole, tied rope and spear finial. The pole visibly ends at the bottom of its source panel.','Preserve the smoke-darkened cloth and one remaining bright blue thread required by chapter1; no new heraldry. Original panel does not make the blue thread legible.','HOLD source-cutoff: full pole end is unavailable. Do not copy the flat cut into an allegedly complete transparent master.'),
'FREE-OATH-STONE':('Two split upright stone slabs on a broken stone base, connected by a dark iron ring; incised nonverbal marks.','Canonical singular STONE is not the planned plural FREE-OATH-STONES. Ending prose calls for seven differently shaped stones, independent holders, and no recombination into a crown.','Identity scope hold for a narrative-accurate final-ending asset. Preserve this specific singular canonical object until intentionally redesigned.'),
'CROWN-FRAGMENT':('One incomplete curved bronze/gold crown band with raised angular bevels, a diamond boss, broken high ends and dark inner face.','It is a fragment, never a complete wearable crown. The creative brief forbids obviously evil spikes; dignity and practical construction matter.','Clear original silhouette; complete subject, no text or symbol transcription needed.'),
'DRAGON-ALLIANCE-CLASP':('Gold interlocking clasp/rings bearing one torn dark-gray cloth piece and one red scale/leaf-shaped pendant.','Keep this intact alliance token distinct from planned-only BROKEN-DRAGON-CLASP. No permanent bridle, saddle or ownership mark on Ivra is implied.','Clear source silhouette; confirm cloth versus wing-material interpretation before adding new lore.'),
'LEFT-GLOVE':('Worn blue-gray cylindrical wrist guard, tan stitched repairs and rope tie; its near opening shows a dark hollow interior.','LEFT-GLOVE is the stable ID, but its canonical label and current source are a wrist guard, not a fingered glove. Chapter1 ties this to Leia and Roderick; preserve wear, blue-gray material, seam and loose cord.','Recommended; do not substitute CH01-S00\'s distinct metal-rimmed bracer or Roderick\'s literal left glove.'),
'EXILE-HARDTACK':('Three rough tan hard biscuits, cracked/nicked edges, bundled in a folded gray cloth.','Scarce plain food, not a luxurious pastry, crest-shaped loaf or decorated later gray-wing biscuit. No writing or emblem.','Recommended; complete subject and simple countable identity.'),
'CONTROL-FRAGMENT':('Torn pale parchment fragment with geometric pseudo-script, bound across the middle by a bronze ornamental band.','No readable text or existing heraldry per prop-sheet02 requirement. The oath-language brief distinguishes will/name/witness and missing refusal; no approved literal glyph system exists.','Source identity clear but symbol semantics require a brief before generation; do not hallucinate readable spell text.'),
'THREE-PEOPLES-CLASP':('Circular alliance fastening composed of braided olive cord, bronze angular links, dark steel link and tied rust-red cloth.','Three peoples must remain visibly distinct within one releasable covenant. No existing heraldry or readable text per sheet02. Preserve the open center.','Clear complete original; avoid turning it into a crown, single empire insignia or welded unbreakable ring.'),
'ADA-MEDICINE':('Two corked bottles: teal glass with rope/fabric tie; brown/clear glass with reddish strip and broad pale bandage wrap.','Canonical double-writing medicine bottle and prose require two retained national scripts/labels on a cracked bottle. Original two-bottle art does not visibly communicate two scripts or crack.','HOLD semantic mismatch: do not call two bottles dual-language merely because their colors differ. Exact fictional text is not specified; two visibly distinct abstract writing systems may be proposed for review.'),
'IVRA-BROKEN-BRIDLE':('Separated torn brown leather bridle strap with rectangular buckle, detached torn red-brown strap, and two broken halves of a metal bit/ring.','The broken/open condition is essential: freedom to leave, not tame-dragon ownership. Preserve all loose parts and open gaps. No dragon, saddle or body attached.','Recommended; complete multi-part object and clear story semantics.'),
'EXPIRING-MANDATE':('Bronze round clasp with a dark central radial disk, two slotted strap ends and a separately drawn release pin.','Detachable and externally removable by Tasha. Novel adds falling silver sand/time limit, while original is dial-like with no sandglass. Do not invent legible clock numerals or dates.','HOLD temporal mechanism interpretation if matching novel; original release pin and detachable construction remain mandatory.'),
'NAME-TAGS':('Four wooden humanoid memorial effigies behind four hanging wood tags with dark tally-like marks.','Living waist-name tags and dead individual names should correspond; original tallies are not actual names. Distinct from CH03-S11 cemetery scene. Do not invent canon names or replace with anonymous skulls.','Semantic brief required for labels and living/dead pairing; preserve multiple individual markers, not one generic medallion.'),
'CASUALTY-REGISTER':('Open worn ledger with repeated rows/columns of marks, attached identity tags and a separate dark pencil.','Ordinary soldiers\' identities and public accountability matter. No specific name list is approved. Preserve row structure, individual records and writing tool; do not generate false readable canon names.','Complete source, but dense marks fail at128px. Historical output has only27 fully opaque pixels, so it is not a transparency quality reference.'),
'GRANARY-KEY':('One dark iron key attached to three colored large rings, with a smaller metal connector ring.','Prose specifies three separately held keys for merchant/militia/refugee representatives and two representatives present to open. Original shows one key with three colored rings, not three independently controlled keys.','HOLD semantic mismatch. Do not claim three-party governance from three decorative rings without deciding whether to preserve source or redesign.'),
'EVACUATION-MAP':('Unfurled folded parchment map with blue/green/red dashed arrow paths, circles/triangles/squares and a blue river.','Multiple routes/communities must be distinguishable; canonical label says multilingual, but source uses shapes/colors and no literal languages. No geographic labels or real text are approved.','Symbol semantics and route topology require review; preserve arrows, river crossings and differentiated routes, not random decorative glyphs. Historical output has only4 fully opaque pixels.'),
'CH01-S00':('A loose blue-gray metal-rimmed stitched bracer with a separate tied rope, on magenta. No person or hands.','Authored scene brief is Roderick fastening Leia\'s guard through hand interaction. Existing canonical128px key-prop/source instead depicts the loose object. Distinct LEFT-GLOVE already exists.','HOLD scene-versus-prop identity. Preserve current key-prop metadata and bytes; do not silently use the bracer as the planned scene or remap to LEFT-GLOVE.'),
'CH03-S11':('Complete opaque isometric cemetery: people carrying lights/markers, central statue, graves, masonry paths, fenced gate and autumn trees.','Living waist-name tags and dead gravestone names are a scene interaction. Distinct transparent PROP-NAME-TAGS owns isolated markers.','HOLD full-scene exception. Preserve opaque128×128 and key-prop kind until explicit classification/identity decision; never chroma-key this scene or paint transparent holes into it.')
}
recommended=['C01-PROP-LEFT-GLOVE','C01-PROP-EXILE-HARDTACK','C01-PROP-IVRA-BROKEN-BRIDLE','C01-PROP-CROWN-FRAGMENT']
input_paths=[R/'AGENTS.md',R/'PROJECT_RULES.md',catalog,builder,R/'tools/art/source-overrides.json',R/'tools/art/export_overrides.py',R/'tools/art/test_export_overrides.py',R/'tools/art/test_narrative_card_overrides.py',R/'tools/art/test_narrative_scene_overrides.py',P/'manifest-final-fantasy-v1.json',P/'manifest-tactical-runtime-hd.json',R/'docs/art-rebuild-20261019/approved-v5/scope-ledger.json',R/'docs/art-rebuild-20261019/approved-v5/progress.json']
input_paths += [R/x for x in ['packages/story-candidate-01/src/story.ts','packages/story-candidate-01/src/campaign.ts','packages/story-candidate-01/src/levels.ts','packages/story-candidate-01/src/presentation/candidate-01-story.ts','packages/story-candidate-01/src/presentation/candidate-01-assets.ts','packages/game-ui/src/ui/campaign-game.ts','packages/game-ui/src/styles/campaign.css']]
before={rel(p):sha(p) for p in input_paths}
rows=[]
for a in selected:
 tid=a['topicId'];p,c,index=slots[tid];x,y=c%2,c//2
 with Image.open(p) as source:
  assert source.size==(1254,1254) and source.mode=='RGB'
  full=[x*627,y*627,(x+1)*627,(y+1)*627];box=[full[0]+5,full[1]+5,full[2]-5,full[3]-5]
  crops=[]
  dest=O/'original-crops'/tid.lower();dest.mkdir(parents=True,exist_ok=True)
  for label,b in [('full-cell',full),('legacy-inset',box)]:
   cropped=source.crop(tuple(b));out=dest/(label+'.png');cropped.save(out)
   with Image.open(out) as reread:assert reread.mode==source.mode and reread.tobytes()==cropped.tobytes()
   crops.append({'path':str(out.relative_to(O)),'sourceCropBox':b,'size':list(cropped.size),'mode':cropped.mode,'sha256':sha(out),'pixelSha256':hashlib.sha256(cropped.tobytes()).hexdigest(),'originalPixelsOnly':True})
  source_info={'path':rel(p),'sha256':sha(p),'size':list(source.size),'mode':source.mode,'genuineAlpha':False,'cellIndexZeroBased':c,'slot':['TL','TR','BL','BR'][c],'originalNarrativeIndexZeroBased':index,'fullCellCropBox':full,'legacyInsetCropBox':box,'crops':crops}
 runtime=P/a['png']
 with Image.open(runtime) as im:runtime_info={'path':rel(runtime),'sha256':sha(runtime),'size':list(im.size),'alpha':alpha(im)};actual=im.copy()
 if p.parent.name=='props':
  cached=P/'intermediate/alpha/narrative/props'/p.name
  with Image.open(cached) as im:im=im.convert('RGBA')
  im.putdata([(r,g,b,0) if r>225 and b>175 and g<105 and al>0 else (r,g,b,al) for r,g,b,al in im.getdata()])
  reconstructed=ns['fit_transparent'](ns['grid_cell'](im,2,2,x,y,5),(128,128),4,False)
  derivation='cached chroma alpha, magenta cleanup, inset5 cell, alpha>=28 bbox fit with4px pad, final magenta cleanup'
 else:
  with Image.open(p) as im:reconstructed=ns['fit_opaque'](ns['grid_cell'](im,2,2,x,y,5),(128,128)).convert('RGBA')
  derivation='RGB inset5 cell, centered square fit, opaque RGBA'
 reconstructed.putdata([(r,g,b,0) if r>230 and b>190 and g<60 and al>0 else (r,g,b,al) for r,g,b,al in reconstructed.getdata()])
 match=reconstructed.mode==actual.mode and reconstructed.size==actual.size and reconstructed.tobytes()==actual.tobytes()
 assert match,tid
 usage={'storyScenes':[s['id'] for s in consumers['stories'] if s['scene']==tid], 'portraitKeys':[k for k,v in consumers['storyPortraits'].items() if v==tid], 'chapterDefaults':[k for k,v in consumers['chapterArt'].items() if v==tid], 'characterTypeBindings':[k for k,v in consumers['characterTypeBindings'].items() if v==tid]}
 assert not any(usage.values())
 key=tid.removeprefix('C01-PROP-') if tid.startswith('C01-PROP-') else tid.removeprefix('C01-')
 identity,semantics,risk=notes[key]
 rows.append({'topicId':tid,'label':a['label'],'pending':led[tid]['formalBinding']=='pending','ledger':led[tid],'canonicalRecord':a,'canonicalAnchor':a.get('anchor'),'hdRecord':hd.get(tid),'registeredOverride':overrides.get(tid),'source':source_info,'runtime':runtime_info,'originalReconstruction':{'matches':match,'method':derivation},'actualConsumers':usage,'shippedConsumerCount':0,'visualReview':{'reviewedOriginalBoard':True,'sourceIdentity':identity,'semanticRequirements':semantics,'riskOrHold':risk,'highResolutionInputAvailable':True,'referenceNativeCellSize':[617,617],'runtimeIsHighResolutionReference':False},'recommendedPriority':recommended.index(tid)+1 if tid in recommended else None,'proposedExportClassification':'held-opaque-fullscene-exception' if tid=='C01-CH03-S11' else 'held-chapter-scene-versus-prop-identity' if tid=='C01-CH01-S00' else 'transparent-prop-v1','proposedSourceAlpha':'independent native genuine RGBA, transparent exterior/open gaps, complete subject; no extracted/chroma-keyed legacy source'})
assert all(t['pending'] and t['hdRecord'] is None and t['registeredOverride'] is None for t in rows)
# Current exporter rejects every key-prop before reading its incompatible source.
sys.path.insert(0,str(R/'tools/art'))
import export_overrides
probes=[]
probe_path=O/'unregistered-probe-catalog.json'
for t in rows:
 probe_path.write_text(json.dumps({'schemaVersion':1,'assets':{t['topicId']:{'kind':'transparent-prop-v1','prop':{'source':t['source']['path'],'sha256':t['source']['sha256']}}}}))
 try:export_overrides.plan(R,probe_path)
 except ValueError as exc:probes.append({'topicId':t['topicId'],'rejected':True,'message':str(exc)})
 else:raise AssertionError('Unexpected key-prop support')
probe_path.unlink()
protected={k:v for k,v in overrides.items() if v.get('kind') in ('character-card-composite-v1','opaque-scene-v1')}
result={'schemaVersion':1,'audit':'C01 narrative key-prop source/export audit','createdUtc':datetime.now(timezone.utc).isoformat(),'repo':str(R),'readOnlyRepository':True,'scope':{'pendingTopics':len(rows),'propPrefix':sum(t['topicId'].startswith('C01-PROP-') for t in rows),'chapterKeyPropExceptions':2,'genuineAlphaOriginalMasters':0,'legacyRuntimeAlpha':16,'opaqueRuntimeExceptions':['C01-CH03-S11'],'topicsWithHd':0,'topicsWithShippedConsumers':0,'currentNarrativeFormalCount':sum(a['category']=='narrative-static' and led[a['topicId']]['formalBinding']=='bound' for a in assets),'currentFormalTotal':ledger['summary']['formalBound']},'recommendedFirstFour':recommended,'topics':rows,'currentExporterRejections':probes,'protectedNarrativeOverrides':protected,'inputHashesBefore':before,'inputHashesAfter':{rel(p):sha(p) for p in input_paths},'historicalHdPaths':[{'filename':s,'found':list(map(rel,R.rglob(s)))} for s in ['campaign-props-sheet-02-hd.png','manifest-hd.json']],'verification':{'sourceBoardMappingsFrom':'AST literal NARRATIVE_SOURCES + canonical topic catalog order','originalPixelReconstructionsMatched':len(rows),'originalCropsExtracted':sum(len(t['source']['crops']) for t in rows),'originalCropDecodedPixelsVerified':True,'repoWritesPerformed':False,'gitCommandsPerformed':False,'generationPerformed':False,'browserOrLiveGameQA':False,'tests':'scene-tests.log, card-tests.log, static-tests.log'}}
(O/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(result['scope'],indent=2));print('Original crop count:',result['verification']['originalCropsExtracted']);print('Changed audited repo inputs:',[k for k in before if before[k]!=result['inputHashesAfter'][k]])
