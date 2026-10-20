from pathlib import Path
from PIL import Image
import json,hashlib,subprocess
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');S=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-scenes')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(d,n,obj): (d/n).write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
common_docs=['docs/story-candidates/candidate-01/deep-customized/09-static-assets.md','docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md']
config={
'arch-capital':{
 'docs':['docs/story-candidates/candidate-01/deep-customized/07-throne-without-king.md','docs/story-candidates/candidate-01/deep-novel-v2/07-throne-without-king-novel.md'],
 'preserved':['Elevated substantial fortified-city setting with dark blue-gray roofed timber-and-stone neighborhoods','Central stout round palace/control core and a second broad domed civic structure','Layered defensive hierarchy, arched gates, wall towers and broad connected streets','Tall narrow watchtowers on individual rocky perimeter hills','Muted slate, ochre and olive materials, modest localized warm windows'],
 'interpreted':['Nested defensive hierarchy condensed to an outer urban wall and smaller palace enclosure, not a literal canonical street map','Foreground water gate, left older wall route and rear tower route express the authored three approaches','Public water basins, grain storage and working districts express actual civic services without claiming a governance outcome'],
 'notClaimed':['Exact canonical cadastral/city map','Named character appearances, forced-kneeling episode or chosen ending','Every service district independently recognizable at canonical256x144','Formal runtime binding or browser/live-game coverage'],
 'caveats':['Centered390x778 cover retains main palace, rear dome, avenue, basins and foreground gate/bridge but clips the broad wall ring, lateral service districts and most outer hilltop towers. Right gate tower and bridge edge are partly clipped.','At256x144 the city plan, palace, wall and river gate read; tiny citizens, facade details and individual services do not.','All towers are inside the native1672x941 and exact1664x936 crop. Two distant rear finials have only a few pixels of top clearance, not the requested45px. The slightly wider960x534 desktop-cover crop can graze the highest peripheral finial; main palace and all primary city structures remain intact.'],
 'review':'''# Astaria capital architecture source review

## Exact source and canonical identity
C01-ARCH-CAPITAL, 阿斯塔里亚王都, is original narrative index65, scene-board-09.png bottom-right. Exact source inset[632,632,1249,1249] is617×617 from the RGB1254×1254 board. Full board and source cell were visually inspected. Board SHA256 matches the recovered audit. The original is a whole layered slate-roofed walled city with a round palace/control core, a rear dome, arched gates, many working districts and narrow watchtowers on surrounding rocky hills. It is not a standalone keep, palace prop or transparent map structure.

## Story interpretation
The chapter-seven brief calls for three outer approaches, inner-city civic functions and the palace/control center. The novel specifically distinguishes the older west wall, south river gate and northern tower route, and describes hospitals, water, grain and defenses sharing Crown infrastructure. The remake keeps an orderly, inhabited city with broad routes, water gate/basins and grain-storage cues. It makes no claim that liberation, kneeling, a chosen ending or a named person's appearance has occurred. The original defensive hierarchy is simplified into a strong urban ring plus a smaller palace enclosure; this is a setting interpretation, not an exact canonical street map. Care/barracks functions are background building suggestions, not individually confirmed at small size.

## Generation and retained attempts
Built-in image_gen, initial source-faithful redraw plus one targeted repair. Initial output had two rear watchtower roof tips clipped by the top canvas boundary. It is retained intact in rejected-v1/ with original prompt/request/reference hashes and rejection reason. The selected edit completes those tower tips and otherwise retains the city composition. Broad matte color planes, substantial roof and wall silhouettes, simplified stonework and clean contour language follow the approved concept-v2 family. No programmatic painting or texture cleanup occurred.

## Native/export contract
Selected raw.png, native.png and master.png are byte-identical RGB1672×941, effectively opaque. Center crop[4,2,1668,938] yields1664×936, source-native density6.5 for canonical256×144. No upscaling. Full raw/master remain unchanged. QA-only crops and downsamples are separate. All primary architecture and rear tower tips are inside the native and exact16:9 crop, but the repaired rear tips have less top breathing room than the prompt requested.

## Visual QA and actual scope
Full native, exact source-native crop,256×144 canonical,960×534 desktop cover,390×778 centered portrait,280×245 staging inset and366×220 cover were inspected. At256×144 the palace, enclosing wall, avenues and river gate remain recognizable, while citizens and service details are small. Centered portrait retains the complete main palace, rear dome, central avenue and basins, and most of the foreground gate/bridge. It clips side districts, almost all perimeter watchtowers, the broad ring-wall plan, part of the right gate tower and bridge edge. The slightly wider desktop crop can graze one highest peripheral finial because its margin is only a few native pixels; the main city core is unaffected.

The historical audit and fresh read-only TS/TSX/JS/CSS scan found no direct current consumer for this exact topic. Cover examples are hypothetical compatibility with existing narrative CSS geometry. They do not prove live-game rendering and do not simulate display filters, gradients, overlays or text. No consumer or selector was invented.

## Handoff
Ready for parent review, backup and explicit opaque narrative registration. This worker made no repository, Git, runtime, selector or unrelated-source edits. Provenance, prompts, requests, reference/source hashes and rejected native artwork are included.
'''
},
'arch-veinport':{
 'docs':['docs/story-candidates/candidate-01/deep-customized/04-grey-banner-queen.md','docs/story-candidates/candidate-01/deep-customized/05-two-peaces.md'],
 'preserved':['Working blue-gray river harbor with angular timber piers, two lifting cranes, small brown river boats and cargo','Three connected spatial precincts: turreted civic/service hall compound, central fenced open market and watch/transport yard','Slate gabled hall with stout capped corner turrets, grain shed and stored supplies','Multicolor broad canvas awnings around a public well, timber fences, wide connecting earth roads','Modest watchtower, wagon/horses and guards; slate rock banks and olive/russet autumn trees'],
 'interpreted':['Modest public ledger table and separate adults express civic administration without naming a regime or decision','Recomposed the square source into landscape; moved civic hall toward upper center for better narrow-cover compatibility','Targeted framing edit removed the clipped lowest decorative boat rather than repositioning it; four complete boats remain and harbor function is unchanged'],
 'notClaimed':['Three separate temporal governance-stage images or a stage/state selector','Chosen uprising/crisis/council outcome or simultaneous historical events','Named heroes, coronation, forced kneeling or new faction heraldry','Formal runtime binding or browser/live-game coverage'],
 'caveats':['390x778 centered portrait retains the civic porch/ledger, most main hall, well and several market stalls, but clips outer right hall turret, the entire watch yard, lifting cranes, boats and most riverfront; only a lower-left dock/water glimpse remains. It is not a complete harbor overview in portrait.','At256x144 harbor/cranes, hall, market and watch yard remain separated; tiny ledger marks, faces and individual goods are not legible.','Native and exact crop preserve the hall roof tips, though top margin is smaller than requested55px. The extended lower-right dock and side palisades meet the frame as environmental continuation.','Canonical label requests uprising/governance crisis/council stages, but source, manifest and current code contain only one static image and no state selector. This bundle supplies one coherent location plate only.'],
 'review':'''# Veinport architecture source review

## Exact source and canonical identity
C01-ARCH-VEINPORT, 维恩港三阶段治理区, is original narrative index64, scene-board-09.png bottom-left. Exact source inset[5,632,622,1249] is617×617 from the RGB1254×1254 board. The complete original board and extracted cell were visually inspected. Board SHA256 matches the audit. The source shows a river harbor on the left and three connected spatial compounds: turreted timber/stone civic-service hall with stored supplies above, fenced open market in the middle, and watch/transport yard below. These are spatial precincts in one scene, not three separately drawn historical states.

## Story and stage limitation
The authored static-assets brief requests uprising, governance crisis and council phases. Chapter-four/five material describes unions, old officials, medical services, supplies and shared civic administration. The audited source and manifest provide one static setting, and no stage/state selector or direct current topic consumer was found. This remake therefore retains one neutral working-harbor/civic ensemble. A public ledger porch and separate anonymous adults suggest administration without declaring an outcome. It does not invent a triptych, three state variants, chronology, faction allegiance or named hero episode.

## Generation and retained attempts
Built-in image_gen, initial source-faithful redraw plus one targeted framing edit. Attempt1 clipped the civic hall roof finial and lowest boat, and placed the hall too far right. Its original native output, prompt, request, reference hashes and rejection reason remain in rejected-v1/. The selected edit moves the hall toward the center and completes its roof silhouette. It removes the clipped lowest decorative boat rather than relocating it; four complete small river boats remain, with both cranes and harbor infrastructure preserved. Main hall remains slightly right of exact center and has less top margin than the requested55px. Style is broad matte cel-painted planes, heavy beams/palisades, substantial stone/tile forms and grouped autumn foliage. No Python/programmatic art painting occurred.

## Native/export contract
Selected raw.png, native.png and master.png are byte-identical RGB1672×941 and effectively opaque. Exact crop[4,2,1668,938] yields1664×936 source-native at density6.5 for logical256×144. No upscaling, stretching or repainted art. Master preserves all authored native pixels; QA outputs are separate crop/resize/composite artifacts. Main hall tips and all four boat hulls remain inside native and exact crop. Peripheral dock/fence ends continue naturally to scene edges.

## Visual QA and crop limits
Inspected full native, exact source-native crop, canonical256×144,960×534 desktop cover,390×778 centered portrait,280×245 staging inset and366×220 cover. Full landscape retains clear harbor, civic compound, market and transport-yard identities. At256×144 the four major zones remain readable; faces, goods and ledger marks are too small to inspect individually. The centered portrait keeps the hall porch with public ledger, most main hall, public well and several market stalls. It clips the outer right hall turret, all cranes and boats, nearly all riverfront and the entire watch yard. A dock/water glimpse remains at lower left, but this portrait is not a complete harbor overview. The square-ish inset clips outer quays and part of the watch yard; landscape/desktop best preserve the intended ensemble.

All cover crops are offline hypothetical compatibility using existing narrative CSS dimensions. No live browser/game rendering, display-filter accuracy or new consumer selection is claimed. The one-static-image/three-stage limitation must remain explicit through integration.

## Handoff
Ready for parent review, backup and explicit opaque narrative registration as one location plate. No repository, Git, runtime, selector, state or unrelated topic changes were made by this worker. Original board/cell, lore context, exact prompts/requests, hashes, raw/master and rejected attempt are retained.
'''
}}
for slug,c in config.items():
 d=S/slug;a=json.loads((d/'audited-source.json').read_text());im=Image.open(d/'master.png');topic=a['topicId'];src=R/a['source']['path']
 (d/'source-review.md').write_text(c['review']);
 docs=common_docs+c['docs'];(d/'canonical-design-context.txt').write_text('\n\n'.join('SOURCE: '+p+'\n\n'+(R/p).read_text() for p in docs))
 paths=[a['source']['path'],'docs/art-rebuild-20261019/approved-v5/remaining-narrative-audit/manifest.json','packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json','packages/game-ui/src/styles/campaign.css']+docs
 write(d,'source-input-hashes.json',{p:sha(R/p) for p in paths})
 search=subprocess.run(['rg','-n','-i',topic+'|'+slug,'apps','packages','--glob','*.ts','--glob','*.tsx','--glob','*.js','--glob','*.css'],cwd=R,text=True,capture_output=True)
 write(d,'consumer-recheck.json',{'topicId':topic,'historicalAuditActualUse':a['actualUse'],'currentReadOnlyScan':'rg -n -i '+topic+'|'+slug+' apps packages --glob *.ts --glob *.tsx --glob *.js --glob *.css','exitCode':search.returncode,'matches':search.stdout.splitlines(),'stderr':search.stderr,'directRuntimeConsumerFound':bool(search.stdout),'selectorChanges':False,'newConsumerSuggested':False,'cssCoverQA':'hypothetical compatibility only; no browser/live evidence','stageVariantGap':slug=='arch-veinport'})
 write(d,'semantic-reconstruction.json',{'topicId':topic,'label':a['label'],'category':'narrative-static','kind':'scene','originalNarrativeIndexZeroBased':a['originalNarrativeIndexZeroBased'],'sourcePath':a['source']['path'],'sourceBoardSha256':sha(src),'sourceBoardSha256MatchesAudit':sha(src)==a['source']['sha256'],'cellLabel':a['source']['cellLabel'],'legacyInsetCropBox':a['source']['legacyInsetCropBox'],'sourceCellSize':[617,617],'historicalAuditActualUse':False,'semanticReview':{'preserved':c['preserved'],'interpreted':c['interpreted'],'notClaimed':c['notClaimed'],'caveats':c['caveats']}})
 assert im.size==(1672,941);assert len({sha(d/f) for f in ['raw.png','native.png','master.png']})==1;assert im.convert('RGBA').getchannel('A').getextrema()==(255,255);assert Image.open(d/'qa-source-native-1664x936.png').size==(1664,936);assert Image.open(d/'qa-logical-256x144.png').size==(256,144);assert sha(src)==a['source']['sha256']
 write(d,'final-verification.json',{'topicId':topic,'nativePreserved':True,'nativeSize':list(im.size),'effectiveAlphaExtrema':[255,255],'sourceNativeExportCrop':[4,2,1668,938],'sourceNativeExportSize':[1664,936],'logicalSize':[256,144],'density':6.5,'upscaled':False,'visualQAComplete':True,'nativeAndLogicalAndCenteredNarrowInspected':True,'rejectedAttemptsPreserved':['rejected-v1'],'currentConsumerFound':bool(search.stdout),'runtimeOrSelectorEdits':False,'liveGameVerified':False,'masterSha256':sha(d/'master.png'),'visualCaveats':c['caveats']})
 (d/'hash.txt').write_text(sha(d/'master.png')+'  master.png\n')
 write(d,'bundle-manifest.json',{'topicId':topic,'selectedMaster':'master.png','files':[{'path':str(p.relative_to(d)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(d.rglob('*')) if p.is_file() and p.name!='bundle-manifest.json']})
 print(slug,sha(d/'master.png'),'files',len(list(d.rglob('*'))),'direct consumers',bool(search.stdout),'scan stderr',search.stderr)
