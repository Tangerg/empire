#!/usr/bin/env python3
"""Read-only narrative source, pixel and consumer audit; writes only beside this script."""
from pathlib import Path
from PIL import Image, ImageOps
from collections import Counter
import ast, hashlib, json, re

REPO=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
OUT=Path(__file__).resolve().parent
PACK=REPO/'packages/story-candidate-01/assets/final-fantasy-v1'
def load(p):return json.loads(p.read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rel(p):return str(p.relative_to(REPO))
def alpha_stats(im):
 if im.mode!='RGBA':return {'mode':im.mode,'hasAlpha':False,'effectiveAlphaExtrema':[255,255]}
 a=im.getchannel('A');h=a.histogram();w,y=im.size
 edges=list(a.crop((0,0,w,1)).getdata())+list(a.crop((0,y-1,w,y)).getdata())+list(a.crop((0,0,1,y)).getdata())+list(a.crop((w-1,0,w,y)).getdata())
 return {'mode':im.mode,'hasAlpha':True,'extrema':list(a.getextrema()),'zeroPixels':h[0],'partialPixels':sum(h[1:255]),'opaquePixels':h[255],'edgeMaxAlpha':max(edges),'alphaBoundingBox':list(a.getbbox()) if a.getbbox() else None}
progress_path=REPO/'docs/art-rebuild-20261019/approved-v5/progress.json'
ledger_path=REPO/'docs/art-rebuild-20261019/approved-v5/scope-ledger.json'
progress=load(progress_path); ledger=load(ledger_path)
assets=[a for a in load(PACK/'manifest-final-fantasy-v1.json')['assets'] if a['category']=='narrative-static']
byid={a['topicId']:a for a in assets}; led={a['topicId']:a for a in ledger['canonicalTopics']}
hd={a['topicId']:a for a in load(PACK/'manifest-tactical-runtime-hd.json')['assets']}
overrides=load(REPO/'tools/art/source-overrides.json')['assets']; consumers=load(OUT/'consumers.json')
catalog=REPO/'docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md'
section=catalog.read_text().split('## 叙事静态图，共 80 项')[1].split('\n## ')[0]
order=re.findall(r'^- `(C01-[^`]+)`：(.+)$',section,re.M)
assert len(order)==80 and {i for i,l in order}==set(byid)
builder=PACK/'tools/build_final_fantasy_v1.py';tree=ast.parse(builder.read_text())
source_map=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='NARRATIVE_SOURCES' for t in n.targets))
# Only these image-in/image-out helpers run. Never import or call the legacy builder, chroma helper, write_asset or main.
ns={'Image':Image,'ImageOps':ImageOps,'RESAMPLE':Image.Resampling.LANCZOS}
helpers=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'grid_cell','clear_white_edge_gutter','alpha_bbox','fit_transparent','fit_opaque'}]
exec(compile(ast.Module(body=helpers,type_ignores=[]),str(builder),'exec'),ns)
slots={}; unused=[];sources=[]
for board,indices in source_map.items():
 family='characters' if board.startswith('character') else 'props' if board.startswith('prop') else 'scenes'
 p=PACK/'masters/narrative'/family/board
 assert p.exists(),p
 with Image.open(p) as im:
  info={'path':rel(p),'sha256':sha(p),'size':list(im.size),'alpha':alpha_stats(im),'family':family,'slotIndices':indices}
 sources.append(info)
 for cell,index in enumerate(indices):
  if index is None:unused.append({'board':rel(p),'cellIndexZeroBased':cell});continue
  assert index not in slots
  x,y=cell%2,cell//2;w,h=info['size']; box=[round(x*w/2)+5,round(y*h/2)+5,round((x+1)*w/2)-5,round((y+1)*h/2)-5]
  slots[index]={**info,'cellIndexZeroBased':cell,'columnZeroBased':x,'rowZeroBased':y,'cellLabel':['top-left','top-right','bottom-left','bottom-right'][cell],'legacyInsetCropBox':box,'legacyInsetPx':5}
assert set(slots)==set(range(80))

def subgroup(topic,kind):
 if kind=='character-card':return 'character-card-stage'
 if topic.startswith('C01-ARCH-'):return 'architecture-scene'
 if topic.startswith('C01-STYLE-'):return 'style-scene'
 if topic.startswith('C01-SCENE-'):return 'everyday-scene'
 if kind=='key-prop':return 'key-prop' if topic.startswith('C01-PROP-') else 'chapter-topic-as-key-prop'
 return 'chapter-scene'

all_sources=[]; records=[]; verification=[]
for index,(topic,label) in enumerate(order):
 a=byid[topic];s=slots[index];p=REPO/s['path'];formal=led[topic]['formalBinding']=='bound' or topic in progress['topicStages']
 outpath=PACK/a['png']
 with Image.open(outpath) as im: runtime={'path':rel(outpath),'sha256':sha(outpath),'size':list(im.size),'alpha':alpha_stats(im)}; actual=im.copy()
 known_formula=None;pixel_match=None;intermediate=None
 if s['family']=='props':
  cached=PACK/'intermediate/alpha/narrative/props'/p.name
  intermediate={'path':rel(cached),'exists':cached.exists()}
  if cached.exists():
   intermediate['sha256']=sha(cached)
   with Image.open(cached) as im:im=im.convert('RGBA')
   im.putdata([(r,g,b,0) if r>225 and b>175 and g<105 and al>0 else (r,g,b,al) for r,g,b,al in im.getdata()])
   cell=ns['grid_cell'](im,2,2,s['columnZeroBased'],s['rowZeroBased'],5)
   reconstructed=ns['fit_transparent'](cell,(128,128),4,False)
   known_formula='cached original chroma → saturation cleanup → 2x2 cell inset5 / edge-white cleanup → alpha>=28 bbox fit,4px pad,center,128x128 → final magenta alpha cleanup'
 else:
  with Image.open(p) as im:im=im.convert('RGB')
  cell=ns['grid_cell'](im,2,2,s['columnZeroBased'],s['rowZeroBased'],5)
  reconstructed=ns['fit_opaque'](cell,(a['width'],a['height'])).convert('RGBA')
  known_formula='original RGB → explicit 2x2 cell inset5 → ImageOps.fit centered cover → RGBA opaque'
 if known_formula:
  reconstructed.putdata([(r,g,b,0) if r>230 and b>190 and g<60 and al>0 else (r,g,b,al) for r,g,b,al in reconstructed.getdata()])
  pixel_match=reconstructed.mode==actual.mode and reconstructed.size==actual.size and reconstructed.tobytes()==actual.tobytes()
 verify={'topicId':topic,'formalApprovedReplacement':formal,'originalReconstructionMatchesCurrentPixels':pixel_match,'formula':known_formula,'intermediate':intermediate}
 verification.append(verify)
 # Actual consumers, keeping static generic API availability distinct.
 scene_stories=[v for v in consumers['stories'] if v['scene']==topic]
 speakernames=[k for k,v in consumers['storyPortraits'].items() if v==topic]
 dialogue=[{'storyId':v['id'],'speaker':speaker,'beatIndicesZeroBased':[i for i,k in enumerate(v['speakers']) if k==speaker]} for v in consumers['stories'] for speaker in speakernames if speaker in v['speakers']]
 types=[k for k,v in consumers['characterTypeBindings'].items() if v==topic]
 actor_keys=['campaign-torren'] if topic=='C01-CHAR-TORREN-01' else []
 hud_actors=[v for v in consumers['initialActors'] if v['unit'] in types or v.get('key') in actor_keys]
 hud_spawns=[v for v in consumers['reinforcements'] if v['unit'] in types or v.get('key') in actor_keys]
 roster=[v for v in consumers['roster'] if v['id'] in speakernames]
 chapters=[int(k) for k,v in consumers['chapterArt'].items() if v==topic]
 staging=[v for v in consumers['levels'] if any(x['id']==v['briefingId'] for x in scene_stories)]
 use={'storyScenes':[{'id':v['id'],'chapter':v['chapter'],'title':v['title'],'location':v['location']} for v in scene_stories],
      'stagingHeroLevels':[v['id'] for v in staging], 'storyDialogue':dialogue,'directRosterIds':[v['id'] for v in roster],
      'hudUnitTypes':types,'hudActorKeys':actor_keys,'hudInitialActors':hud_actors,'hudReinforcements':hud_spawns,'codexUnitTypes':types,
      'chapterDefaultBackgrounds':chapters,'chapterChoiceScreens':[v['id'] for v in consumers['choices'] if v['chapter'] in chapters],
      'chapterStagingAndResultLevels':[v['id'] for v in consumers['levels'] if v['chapter'] in chapters],
      'menuConsumers':['apps/game/src/main.ts:title','apps/experience-lab/src/main.ts:experience-art'] if topic=='C01-CH01-S05' else [],
      'boardPictureConsumers':[]}
 actualuse=any(use[k] for k in use)
 risks=[]
 if a['kind']=='character-card':
  master_req='independent native genuine-RGBA reusable subject, complete authored canvas and transparent open gaps; preserve immutable source; may compose over separately-authored opaque card background'
  export='existing character-card-composite-v1 supports192x224 opaque RGBA card only; no HD entry; whole source scaled uniformly with explicit renderWidth/origin, no foreground bbox fit/crop'
  if topic not in consumers['storyPortraits'].values() and not actor_keys:risks.append('No shipped card consumer or stage/age progression selector; source creation is catalog work only.')
 elif a['kind']=='scene':
  master_req='deliberately opaque full narrative composition; architecture here is a setting plate, not reusable board architecture or map structure'
  export='canonical256x144 opaque RGBA PNG, no anchor/frames/HD entry; a new explicit registered opaque narrative export path is required; do not run legacy bulk builder'
 else:
  master_req='independent native genuine-RGBA reusable prop with transparent exterior/open gaps; CH03-S11 original is an opaque scene exception requiring identity/kind decision'
  export='canonical128x128 PNG, no anchor/frames/HD entry; new explicit whole-canvas transparent-prop export path required; legacy bbox/chroma cleanup is evidence only'
 if topic.startswith('C01-CHAR-IVRA-'):risks.append('Ivra age/maturity continuity is held; no chapter/rank/age selector; preserve accepted battle quartet; do not invent/select later-age identity.')
 if topic=='C01-CHAR-TORREN-01':risks.append('At audit snapshot, campaign roster lacks portrait-table entry and passes no actor context to fallback, therefore generic swordsman; parent is investigating narrow binding propagation.')
 if topic=='C01-CH01-S00':risks.append('Canonical label is Roderick tying wrist guard, but original source is a loose bracer on prop-board04 cell3; current key-prop128x128 is faithful to source rather than full scene brief. No shipped consumer.')
 if topic=='C01-CH03-S11':risks.append('Declared key-prop128x128 is actually scene-board07 cell0 full cemetery scene and fully opaque. No isolated name-tag master here; distinct PROP-NAME-TAGS exists. Do not silently remap/reclassify.')
 if topic=='C01-CH05-S26':risks.append('Late-campaign council/gray-night identity is reused by shipped early black-camp, mercenary, old-banner and finale scenes; remake does not resolve semantic selection mismatch.')
 if topic=='C01-ARCH-REDSTONE':risks.append('Also used for cemetery story beats11 and entire chapter2 fallback; changing source must not silently change these selectors.')
 if topic=='C01-ARCH-THREE-BRIDGES':risks.append('Square original center-cover to16:9 cuts top and bottom bridges; native16:9 composition/export review must keep all3 bridge identities legible.')
 if topic=='C01-ARCH-VEINPORT':risks.append('Label mentions3 governance stages, but one static image and no stage/state selector exist.')
 if topic=='C01-ARCH-TWIN-HILLS':risks.append('Concept requires year0/year4 variants, but one static image and no time/state selector exist.')
 rec={'topicId':topic,'label':label,'originalNarrativeIndexZeroBased':index,'subgroup':subgroup(topic,a['kind']),'kind':a['kind'],'formalBoundAtSnapshot':formal,'preserveApproved':formal,'remaining':not formal,'ledger':led[topic],
      'source':s,'originalExportVerification':verify,'canonicalManifest':a,'runtime':runtime,'hdRecord':hd.get(topic),'actualUse':actualuse,'consumers':use,
      'masterAlphaContract':master_req,'exportContract':export,'risks':risks}
 if topic in overrides:
  o=overrides[topic];rec['protectedOverride']=o
  cp=REPO/o['character']['source'];bp=REPO/o['background']['source']
  with Image.open(cp) as im:rec['protectedCharacterMaster']={'path':rel(cp),'sha256':sha(cp),'size':list(im.size),'alpha':alpha_stats(im),'registeredHashMatches':sha(cp)==o['character']['sha256']}
  with Image.open(bp) as im:rec['protectedBackground']={'path':rel(bp),'sha256':sha(bp),'size':list(im.size),'alpha':alpha_stats(im),'registeredHashMatches':sha(bp)==o['background']['sha256']}
 records.append(rec)

inputs=[progress_path,ledger_path,catalog,builder,PACK/'manifest-final-fantasy-v1.json',PACK/'manifest-tactical-runtime-hd.json',PACK/'manifest-hero-appearances.json',REPO/'tools/art/source-overrides.json',REPO/'tools/art/export_overrides.py']
inputs += [REPO/p for p in ['packages/story-candidate-01/src/story.ts','packages/story-candidate-01/src/campaign.ts','packages/story-candidate-01/src/levels.ts','packages/story-candidate-01/src/presentation/candidate-01-story.ts','packages/story-candidate-01/src/presentation/candidate-01-assets.ts','packages/story-candidate-01/src/presentation/candidate-01-bindings.ts','packages/story-candidate-01/src/presentation/candidate-01-actor-art.ts','packages/story-candidate-01/src/presentation/candidate-01-runtime.ts','packages/story-candidate-01/src/presentation/index.ts','packages/game-ui/src/ui/campaign-game.ts','packages/game-ui/src/ui/hud.ts','packages/game-ui/src/art/portraits.ts','packages/game-ui/src/styles/campaign.css','packages/game-ui/src/styles/app.css','apps/game/src/main.ts','apps/experience-lab/src/main.ts','apps/experience-lab/src/experience.css']]
summary={'canonicalScope':len(records),'formalBound':sum(x['formalBoundAtSnapshot'] for x in records),'remaining':sum(x['remaining'] for x in records),'totalByKind':dict(Counter(x['kind'] for x in records)),'remainingBySubgroup':dict(Counter(x['subgroup'] for x in records if x['remaining'])),'actualUseTopics':sum(x['actualUse'] for x in records),'actualUseRemaining':sum(x['actualUse'] and x['remaining'] for x in records),'catalogOnlyRemaining':sum(not x['actualUse'] and x['remaining'] for x in records),'sourceBoardCount':len(sources),'mappedTopicCount':len(slots),'missingSourceBoards':[],'unusedBoardCells':unused,'originalPixelMatches':sum(x['originalReconstructionMatchesCurrentPixels'] is True for x in verification),'approvedReplacementsExpectedMismatches':sum(x['formalApprovedReplacement'] and x['originalReconstructionMatchesCurrentPixels'] is False for x in verification),'unexpectedPixelMismatches':[x['topicId'] for x in verification if not x['formalApprovedReplacement'] and x['originalReconstructionMatchesCurrentPixels'] is not True],'narrativeHDCount':sum(x['hdRecord'] is not None for x in records)}
manifest={'schemaVersion':1,'audit':'remaining-c01-narrative-contracts','snapshotUtc':'2026-10-20T00:21:00Z','repo':str(REPO),'authority':'repository docs/art-rebuild-20261019/approved-v5/progress.json and scope-ledger.json; shared progress/classified-progress/canonical snapshots are not current','repoFormalTotalAtSnapshot':progress['formalRuntimeBoundTopics'],'summary':summary,'sourceOrderAuthority':{'path':rel(catalog),'explanation':'Generated catalog preserves manifest-complete topic iteration order. NARRATIVE_SOURCES explicit numeric slot assignments use this order, not alphabetically sorted final manifest. All80 mapped identities checked against canonical manifest; unchanged runtime pixels independently cross-check map.','legacyManifestCompletePaths':[{'path':str(p.relative_to(REPO)),'exists':p.exists()} for p in [REPO/'packages/story-candidate-01/assets/manifest-complete.json',REPO/'docs/story-candidates/candidate-01/assets/manifest-complete.json']]},'sources':sources,'topics':records,'inputHashes':{rel(p):sha(p) for p in inputs}}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(OUT/'pixel-verification.json').write_text(json.dumps({'summary':summary,'topics':verification},ensure_ascii=False,indent=2)+'\n')
print(json.dumps(summary,ensure_ascii=False,indent=2))
print('\nUSED REMAINING:')
for r in records:
 if r['actualUse'] and r['remaining']:print(r['topicId'],'story',len(r['consumers']['storyScenes']),'staging',len(r['consumers']['stagingHeroLevels']),'chapter',r['consumers']['chapterDefaultBackgrounds'])
