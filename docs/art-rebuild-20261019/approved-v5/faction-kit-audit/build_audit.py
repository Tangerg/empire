#!/usr/bin/env python3
"""Read-only C01 faction identity/crop audit; writes only beside this script."""
import sys,json,hashlib,importlib.util,math,csv
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
sys.dont_write_bytecode=True
ROOT=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
OUT=Path(__file__).resolve().parent
PACK=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1'
LEGACY=PACK/'tools/build_final_fantasy_v1.py'
spec=importlib.util.spec_from_file_location('legacy',LEGACY)
legacy=importlib.util.module_from_spec(spec);spec.loader.exec_module(legacy)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rel=lambda p:str(p.relative_to(ROOT))
raw_path=PACK/'masters/factions/faction-kits-12.png'
alpha_path=PACK/'intermediate/alpha/factions/faction-kits-12.png'
raw=Image.open(raw_path)
alpha=Image.open(alpha_path).convert('RGBA')
alpha.putdata([(r,g,b,0 if r>225 and b>175 and g<105 and a>0 else a) for r,g,b,a in alpha.get_flattened_data()])
manifest=json.loads((PACK/'manifest-final-fantasy-v1.json').read_text())
assets=[a for a in manifest['assets'] if a['category']=='faction-kit']
hds=json.loads((PACK/'manifest-tactical-runtime-hd.json').read_text())['assets']
current={a['topicId']:Image.open(PACK/a['png']).convert('RGBA') for a in assets}
records={a['topicId']:a for a in assets}
order=['LORNE','VILSA','HOLY-LIGHT','GRAY-BANNER','SILVERWOOD','MOUNTAIN-FORGE','WASTELAND','MILITIA-MERCENARY','DRAGON-COVENANT','OATH-CONTROLLED','NAMED-DEAD','LATE-ALLIANCE']
semantics={
'LORNE':dict(objects=['Pointed riveted old-steel shield with broad upward ivory chevron and dark wine-red lower field','Patched ivory and wine-red cloth strip with ragged lower edge','Plain straight steel sword with small rounded pommel'],emblem='Preserve broad upward ivory chevron, old-steel upper field, dark wine-red lower field; no crown, lion or new heraldry',palette='Old steel, worn ivory, restrained wine-red; repaired practical feudal equipment'),
'VILSA':dict(objects=['Pointed navy shield, brass rim and rivets, ivory upward chevron','Folded navy cloth with ivory lower horizontal stripe and brass-bottom trim','Straight spear with steel diamond/leaf-shaped head'],emblem='Preserve navy/ivory upward-chevron shield and standardized brass fittings; do not replace with an eagle or villain crest',palette='Standardized navy, ivory, restrained brass; orderly practical equipment, not evil-black armor'),
'HOLY-LIGHT':dict(objects=['Tall pointed ivory-and-charcoal paneled shield with central lit candle and holder','Ivory folded cloth with charcoal longitudinal stripe, crossband and small fringe','Round amber/gold wax seal with source-specific stamped glyph and two hanging tabs'],emblem='Keep literal candle-and-holder motif and original seal glyph; do not invent a sun/cross/holy crest or readable text',palette='Warm ivory, charcoal, restrained amber; medicine/registration meaning'),
'GRAY-BANNER':dict(objects=['Round patchwork wood shield with dark-metal rim, steel boss and muted blue-gray repair panel','Folded blue-gray cloth with large visible brown repair stitches and brown hem','Simple narrow steel spear with cloth/leather tie'],emblem='Source has no independent crest; repaired round shield and visibly stitched gray cloth are the identity. Do not add a crown or generic flag logo',palette='Gray-blue patched cloth, ordinary iron, old leather and wood'),
'SILVERWOOD':dict(objects=['Oval vertically planked wooden shield with woven/vine-bound rim and iron boss','Patched moss/sage cloth swatch with lacing and ragged edge','Curved wooden longbow with string'],emblem='No heraldic tree/leaf symbol is drawn; preserve woven wood, cloth repair and longbow as identity',palette='Sage/moss green, bark brown, wood and textile; avoid flawless white aristocratic elf styling'),
'MOUNTAIN-FORGE':dict(objects=['Angular six-sided iron shield with riveted border and large amber hammer-like smithing emblem over a downward shaft and warm forge curl','Dark leather work apron with neck loop and tool pocket','Short thick square-headed metal hammer with small source-specific stamped face detail'],emblem='Preserve source hammer/shaft/forge-curl emblem and hammer-face stamp, rather than adding mountain/cog/anvil heraldry',palette='Iron-black, stone-gray, worn leather, restrained amber; craft and archive institution'),
'WASTELAND':dict(objects=['Round patchwork wooden shield with warm rust/ochre and blue-gray triangular panels, rim and iron boss','Folded terracotta/worn leather cloth swatch with stitches and dark lower hem','Slender practical steel-tipped spear with wrapped shaft'],emblem='Retain patchwork triangular panel arrangement, no skull/totem or invented tribal crest',palette='Weathered earth-red, old leather and practical metal; trade, riding and professional labor rather than barbarian stereotype'),
'MILITIA-MERCENARY':dict(objects=['Round vertically planked shield with muted blue-gray left and ochre-brown right panels and steel boss','Folded dark teal cloth or kit roll secured by a brown belt and small circular brass clasp','Short practical square-headed gray metal hammer with wood handle'],emblem='Retain simple two-tone plank shield and small source clasp; no city crest or mercenary logo is present',palette='Blue-gray, worn teal, wood brown and restrained ochre; ordinary practical militia/mercenary equipment'),
'DRAGON-COVENANT':dict(objects=['Tall angular beveled steel shield/plate with a literal hanging balance-scale emblem and source geometric/rune-like border relief','Gray overlapping dragon-scale material swatch with dark wine-red edge','Separate looped gray strap/cord with rectangular silver buckle'],emblem='CRITICAL: original shield motif is hanging balance scales, not a dragon-head silhouette. Preserve source emblem and scale material. The separate loop/buckle is not authorization to add a saddle or bridle',palette='Old gray metal, overlapping gray scales, muted wine-red edge'),
'OATH-CONTROLLED':dict(objects=['Lorne-like old-steel, ivory-chevron, wine-red shield bound by two crossing amber/gold inscribed bands','Patched ivory/wine-red cloth bound by two horizontal amber/gold inscribed bands','Plain straight steel sword'],emblem='Preserve recognizable Lorne base and crossed binding bands; band marks are source ornament, not new readable wording. Do not substitute skulls, glowing red eyes or generic evil glyphs',palette='Old steel, ivory, wine-red and constrained amber/gold binding bands'),
'NAMED-DEAD':dict(objects=['Tall pointed ivory shield with large lit amber lantern motif','Folded gray cloth tied with cord and two wooden name tags','Small separate lit amber memory lantern'],emblem='Preserve lantern shield and two wooden name tags; no skull or blue-ghost replacement and no fabricated readable names',palette='Gray cloth, weathered ivory, wooden names, warm amber memory lamp'),
'LATE-ALLIANCE':dict(objects=['Round shield of multiple differently colored radial panels, central gray boss enclosed by segmented gold ring/clasps','Several distinct vertical folded cloth swatches in gray-blue, green, rust, ochre and ivory, held by individual interlinked-looking buckles','Separate small linked clasp/chain with multicolor ties'],emblem='Preserve visibly plural colors, individual buckles and linked clasp; do not collapse alliance into one imperial flag/crest/crown',palette='Retain multiple source faction colors together with restrained gold connectors; coalition remains plural'),
}
for folder in ['source-crops','legacy-alpha-crops','native-proofs']:(OUT/folder).mkdir(exist_ok=True)
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',15)
small=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',12)
board=Image.new('RGB',(4*354,3*486),(30,34,41));draw=ImageDraw.Draw(board)
native_sheet=Image.new('RGB',(4*354,3*213),(30,34,41));nd=ImageDraw.Draw(native_sheet)
def edge_stats(im):
 a=im.getchannel('A');w,h=im.size
 return {name:{'maxAlpha':max(values),'pixelsAtLeast28':sum(v>=28 for v in values)} for name,values in {
  'left':[a.getpixel((0,y)) for y in range(h)],'right':[a.getpixel((w-1,y)) for y in range(h)],
  'top':[a.getpixel((x,0)) for x in range(w)],'bottom':[a.getpixel((x,h-1)) for x in range(w)]}.items()}
def composite(im,bg):
 out=Image.new('RGBA',im.size,bg);out.alpha_composite(im);return out.convert('RGB')
rows=[]
for index in range(12):
 row,col=divmod(index,4)
 full_box=[round(col*raw.width/4),round(row*raw.height/3),round((col+1)*raw.width/4),round((row+1)*raw.height/3)]
 crop_box=[full_box[0]+5,full_box[1]+5,full_box[2]-5,full_box[3]-5]
 cell=legacy.grid_cell(alpha,4,3,col,row,5)
 native=legacy.fit_transparent(cell,(128,128),2,False)
 native.putdata([(r,g,b,0 if r>230 and b>190 and g<60 and a>0 else a) for r,g,b,a in native.get_flattened_data()])
 matches=[t for t,im in current.items() if native.tobytes()==im.tobytes()]
 assert matches==['C01-FACTION-'+order[index]],(index,matches)
 topic=matches[0];asset=records[topic];slug=topic.lower().replace('c01-','')
 assert asset['width']==asset['height']==128 and asset['anchor']==[64,64]
 assert not any(h['topicId']==topic for h in hds)
 full=raw.crop(full_box);legacy_raw=raw.crop(crop_box)
 full.save(OUT/'source-crops'/f'{index+1:02d}-{slug}-full-cell.png')
 legacy_raw.save(OUT/'source-crops'/f'{index+1:02d}-{slug}-legacy-inset5.png')
 cell.save(OUT/'legacy-alpha-crops'/f'{index+1:02d}-{slug}.png')
 native.save(OUT/'native-proofs'/f'{index+1:02d}-{slug}-128.png')
 full_alpha=legacy.clear_white_edge_gutter(alpha.crop(full_box))
 fa=full_alpha.getchannel('A'); ca=cell.getchannel('A')
 full_count=sum(v>=28 for v in fa.get_flattened_data());crop_count=sum(v>=28 for v in ca.get_flattened_data())
 bbox=legacy.alpha_bbox(cell)
 core_board=[bbox[0]+crop_box[0],bbox[1]+crop_box[1],bbox[2]+crop_box[0],bbox[3]+crop_box[1]]
 core_w,core_h=bbox[2]-bbox[0],bbox[3]-bbox[1]
 scale=min(124/core_w,124/core_h);fit_size=[max(1,round(core_w*scale)),max(1,round(core_h*scale))]
 entry={'slot':index+1,'row':row+1,'column':col+1,'topicId':topic,'label':asset['label'],
  'canonical':{**asset,'repositoryPath':rel(PACK/asset['png']),'fileSha256':sha(PACK/asset['png']),'pixelSha256':hashlib.sha256(current[topic].tobytes()).hexdigest(),'mode':'RGBA','hdRecord':None},
  'source':{'board':rel(raw_path),'boardSha256':sha(raw_path),'fullCellBox':full_box,'legacyInset':5,'legacySourceCropBox':crop_box,'coordinateConvention':'Pillow half-open [left,top,right,bottom]; row/column/slot 1-based; round uses Python ties-to-even','referenceFullCell':f'source-crops/{index+1:02d}-{slug}-full-cell.png','referenceLegacyInset5':f'source-crops/{index+1:02d}-{slug}-legacy-inset5.png','referenceFullCellSha256':sha(OUT/'source-crops'/f'{index+1:02d}-{slug}-full-cell.png'),'referenceLegacyInset5Sha256':sha(OUT/'source-crops'/f'{index+1:02d}-{slug}-legacy-inset5.png')},
  'legacyReproduction':{'inputAlpha':rel(alpha_path),'inputAlphaSha256':sha(alpha_path),'matchedTopicIds':matches,'exactPixelEquality':True,'alphaCoreThreshold':28,'coreBoxWithinInsetCell':list(bbox),'coreBoxOnBoard':core_board,'bboxFittedSize':fit_size,'fitOrigin':[(128-fit_size[0])//2,(128-fit_size[1])//2],'legacyCellEdgeAlpha':edge_stats(cell),'fullCellEdgeAlpha':edge_stats(full_alpha),'fullCellCorePixels':full_count,'insetCellCorePixels':crop_count,'corePixelsLostByInset5':full_count-crop_count,'referenceLegacyAlpha':f'legacy-alpha-crops/{index+1:02d}-{slug}.png','nativeProof':f'native-proofs/{index+1:02d}-{slug}-128.png'},
  'semanticConstraints':semantics[order[index]],
  'consumers':{'viteCatalogBundled':True,'authoredGameplayOrUiTopicReferences':[],'browserObserved':False,'offlinePreview':'packages/story-candidate-01/assets/final-fantasy-v1/previews/map-assets-1x.png','qualification':'No authored direct topic/path reference found in apps/packages/tools source. Generic candidate01Asset/candidate01StoryArt APIs can resolve arbitrary externally supplied IDs, so this is not proof of impossibility of future or external consumption.'}}
 rows.append(entry)
 x=col*354+20;y=row*486+12
 draw.text((x,y),f'{index+1:02d}  r{row+1}c{col+1}  {order[index]}',font=font,fill=(240,235,220))
 draw.text((x,y+21),f'full cell {full_box}',font=small,fill=(180,190,203))
 board.paste(full,(x,y+45))
 nx=col*354+16;ny=row*213+10
 nd.text((nx,ny),f'{index+1:02d} {order[index]}',font=font,fill=(240,235,220))
 native_sheet.paste(composite(native,(229,224,209,255)),(nx,ny+30))
 native_sheet.paste(composite(native,(42,52,67,255)),(nx+152,ny+30))
 nd.text((nx,ny+166),'128 px light/dark; exact current pixels',font=small,fill=(180,190,203))
board.save(OUT/'source-board-labeled.png');native_sheet.save(OUT/'native-128-review.png')
code_refs=[];code_files=0
for base in ['apps','packages','tools']:
 for path in (ROOT/base).rglob('*'):
  if not path.is_file() or path.suffix not in ('.ts','.tsx','.js','.jsx','.mjs','.html','.css'):continue
  code_files+=1
  for lineno,line in enumerate(path.read_text(errors='replace').splitlines(),1):
   if 'C01-FACTION-' in line or 'runtime/faction-kit/' in line or 'faction-kit' in line:
    code_refs.append({'path':rel(path),'line':lineno,'text':line.strip()})
source_json_files=0;source_json_refs=[]
for base in ['apps','packages']:
 for path in (ROOT/base).rglob('*.json'):
  if '/src/' not in str(path):continue
  source_json_files+=1
  for lineno,line in enumerate(path.read_text().splitlines(),1):
   if 'C01-FACTION-' in line or 'runtime/faction-kit/' in line:
    source_json_refs.append({'path':rel(path),'line':lineno,'text':line.strip()})
result={
 'schemaVersion':1,'audit':'C01 faction-kit identity/crop/consumer audit','repository':str(ROOT),'artifactDirectory':str(OUT),'generatedDateUtc':'2026-10-20',
 'scope':{'count':12,'codeEdits':False,'gitWrites':False,'imageGeneration':False,'sourceMutation':False},
 'sourceBoard':{'path':rel(raw_path),'sha256':sha(raw_path),'size':list(raw.size),'mode':raw.mode,'columns':4,'rows':3,'mappingEvidence':'All 12 row-major cells were reconstructed with original gutter cleanup, alpha-bbox fitting and final pixel cleanup; each uniquely matches the current named runtime PNG byte-for-byte at pixel level. Topic catalogue order independently agrees. Runtime manifest is alphabetically sorted after generation.'},
 'checks':{'uniqueSlotMatches':12,'exactRuntimePixelMatches':12,'contractMatches128Centered':12,'hdEntries':0,'sourceCodeFilesScanned':code_files,'sourceJsonFilesSeparatelyScanned':source_json_files,'sourceJsonTopicOrPathReferences':source_json_refs,'sourceCodeLiteralReferences':code_refs,'legacyTopicInputExists':(PACK.parent/'manifest-complete.json').exists(),'legacyTopicInputWarning':'Legacy full builder expects absent packages/story-candidate-01/assets/manifest-complete.json. It must not be run to produce this family; it also overwrites unrelated exports.'},
 'cropFindings':{'noTopicSwapDetected':True,'slotsWhoseLegacyCoreTouchesCellEdge':[r['slot'] for r in rows if any(e['pixelsAtLeast28'] for e in r['legacyReproduction']['legacyCellEdgeAlpha'].values())],'slotsWithCorePixelsLostByInset5':[r['slot'] for r in rows if r['legacyReproduction']['corePixelsLostByInset5']>0],'interpretation':'Edge hits refer to alpha >= 28 after existing legacy chroma/gutter operations. Use raw full-cell crop to distinguish art from cleanup residues; remake complete outlines rather than copying clipped cuts. Full board is identity evidence, not an approved transparent source.'},
 'globalSemanticConstraints':['Twelve equipment/material-kit cutouts, not portrait/scene sprites or a single logo per faction','Use approved concept-v2 chunky matte visual language; preserve original object set, emblem topology, material identity and relative arrangement','True alpha exterior; no white/magenta/checkerboard backdrop, board separator lines or labels baked in','Keep every shield/weapon/cloth/strap/tag/lantern/clasp fully in canvas with transparent breathing room','Do not invent a new heraldic symbol or readable text where source has no such element','New masters are independent artwork. Do not use legacy chroma removal, alpha-bbox recentering, recoloring or cropped original cells as a substitute for redraw','Topic IDs, canonical labels, category, runtime paths, size 128x128 and center anchor [64,64] remain fixed; no HD contract or gameplay binding is needed for this family','Broad art-assets/02-asset-scope prose loosely names additional social groups. Exact twelve topics come from canonical manifest and 07-topic-catalog; do not substitute capital guard/border militia topics'],
 'minimalExporterRecommendation':{'path':'tools/art/export_overrides.py','change':'Add faction-kit: 128 to existing static contracts and include it in unsupported-category diagnostic. Existing source/hash-only schema, pack-master confinement, RGBA/edge validation, complete-canvas contain fit, exact centered anchor check, per-topic output-path check and check-mode already fit this family. Retain non-battle HD rejection.','sourceCatalog':'tools/art/source-overrides.json','specShape':{'source':'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/factions/<faction-slug>/master.png','sha256':'<verified SHA-256 of immutable new generated PNG>'},'output':'Existing runtime/faction-kit/<faction-slug>.png only, 128x128 RGBA, 2px fit padding; no new crop field, composite kind, HD schema, anchor calculation or frame exporter','tests':['Valid 128x128 centered faction kit produces deterministic matching export and check mode passes','Reject changed dimensions/anchor, framed/variant entry, wrong source hash, source outside pack masters, RGB/opaque exterior, clipped edge and unexpected HD record','Ensure fit preserves complete authored master canvas and real alpha rather than alpha-bbox cropping','Verify canonical IDs/path/count are unchanged and approved static bindings use canonical runtime path with no HD','Visual QA of all 12 master full canvases and native128 light/dark composites, with source-board comparison for each emblem/object group'], 'catalogPromotion':'Only after approved source identity and export verification; update exact canonical qualityTier/progress/ledger using existing process, without claiming in-game usage'},
 'evidence':[{'path':rel(LEGACY),'lines':[99,108],'meaning':'Exact grid_cell crop formula'},{'path':rel(LEGACY),'lines':[135,176],'meaning':'Historical chroma/pixel cleanup and alpha-bbox fitting'},{'path':rel(LEGACY),'lines':[264,280],'meaning':'Row-major cells zipped to topic order, 2px pad and centered anchor'},{'path':rel(LEGACY),'lines':[466,480],'meaning':'4x3 faction board and later alphabetic manifest sort'},{'path':rel(LEGACY),'lines':[497,497],'meaning':'Offline map-assets preview includes faction-kit'},{'path':'packages/story-candidate-01/src/presentation/candidate-01-assets.ts','lines':[175,231],'meaning':'Generic Vite catalog eager imports and topic resolution; faction kits included in ordinary runtime graph'},{'path':'packages/story-candidate-01/assets/final-fantasy-v1/ART-DIRECTION.md','meaning':'Faction material and identity guardrails'},{'path':'docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md','lines':[168,183],'meaning':'Exact topic order and Chinese labels'}],
 'assets':rows}
(OUT/'faction-kit-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
with (OUT/'faction-kit-slot-map.csv').open('w') as f:
 writer=csv.writer(f);writer.writerow(['slot','row','column','topicId','label','runtimePath','canonicalSize','anchor','fullCellBox','legacySourceCropBox','exactRuntimePixelMatch','corePixelsLostByInset5'])
 for r in rows:writer.writerow([r['slot'],r['row'],r['column'],r['topicId'],r['label'],r['canonical']['repositoryPath'],'128x128','[64,64]',json.dumps(r['source']['fullCellBox']),json.dumps(r['source']['legacySourceCropBox']),True,r['legacyReproduction']['corePixelsLostByInset5']])
print(json.dumps({'exactPixelMatches':12,'artifacts':str(OUT),'cropFindings':result['cropFindings'],'codeFilesScanned':code_files,'sourceCodeLiteralReferences':code_refs},indent=2))
