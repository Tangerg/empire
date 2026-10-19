import argparse,importlib.util,json,hashlib,collections,re,subprocess
from pathlib import Path
from PIL import Image
parser=argparse.ArgumentParser(description='Read-only reconstruction of the 80 Gray Flag narrative outputs')
parser.add_argument('root',type=Path)
parser.add_argument('output',type=Path)
args=parser.parse_args()
root=args.root.resolve()
args.output.mkdir(parents=True,exist_ok=True)
pack=root/'packages/story-candidate-01/assets/final-fantasy-v1'
spec=importlib.util.spec_from_file_location('legacy',pack/'tools/build_final_fantasy_v1.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
sha=lambda b:hashlib.sha256(b).hexdigest()
assets=[a for a in json.loads((pack/'manifest-final-fantasy-v1.json').read_text())['assets'] if a['category']=='narrative-static']
boards=[];candidates=collections.defaultdict(list); rendered={}; slot_sources={}
for name,slots in m.NARRATIVE_SOURCES.items():
 family='characters' if name.startswith('character') else 'props' if name.startswith('prop') else 'scenes'
 source=pack/'masters/narrative'/family/name
 read=pack/'intermediate/alpha/narrative'/family/name if family=='props' else source
 with Image.open(source) as raw: source_mode=raw.mode;source_size=list(raw.size);raw_alpha=raw.getchannel('A').getextrema() if raw.mode=='RGBA' else None
 with Image.open(read) as raw: im=raw.convert('RGBA' if family=='props' else 'RGB')
 if family=='props':
  im.putdata([(r,g,b,0) if r>225 and b>175 and g<105 and a>0 else (r,g,b,a) for r,g,b,a in im.get_flattened_data()])
 boards.append({'path':str(source.relative_to(root)),'sha256':sha(source.read_bytes()),'size':source_size,'mode':source_mode,'alphaExtrema':raw_alpha,'slots':slots,'readPath':str(read.relative_to(root)),'readSha256':sha(read.read_bytes())})
 for ci,slot in enumerate(slots):
  if slot is None: continue
  col,row=ci%2,ci//2
  box=[round(col*im.width/2)+5,round(row*im.height/2)+5,round((col+1)*im.width/2)-5,round((row+1)*im.height/2)-5]
  cell=m.grid_cell(im,2,2,col,row,5)
  if slot in set(range(0,6))|set(range(28,44)): kind='character-card'; size=(192,224);output=m.fit_opaque(cell,size).convert('RGBA')
  elif slot in set(range(14,22))|{44,54,73,74,75,76,77,78,79}:
   kind='key-prop';size=(128,128);output=m.fit_transparent(cell,size,4,False) if cell.mode=='RGBA' else m.fit_opaque(cell,size).convert('RGBA')
  else:kind='scene';size=(256,144);output=m.fit_opaque(cell,size).convert('RGBA')
  rendered[slot]=output.copy();slot_sources[slot]={'board':str(source.relative_to(root)),'boardCell':ci,'cropBox':box}
  candidates[sha(output.tobytes())].append({'slot':slot,'board':str(source.relative_to(root)),'boardCell':ci,'cropBox':box,'kind':kind,'size':size})
rows=[]
for a in assets:
 p=pack/a['png']
 with Image.open(p) as im:
  h=sha(im.tobytes());alpha=im.getchannel('A');hist=alpha.histogram();edges=list(alpha.crop((0,0,im.width,1)).get_flattened_data())+list(alpha.crop((0,im.height-1,im.width,im.height)).get_flattened_data())
  rows.append({**a,'fileSha256':sha(p.read_bytes()),'pixelSha256':h,'mode':im.mode,'actualSize':list(im.size),'alphaExtrema':alpha.getextrema(),'transparentFraction':hist[0]/(im.width*im.height),'edgeMaxAlpha':max(edges),'exactLegacyMatches':candidates.get(h,[])})
text=(root/'packages/story-candidate-01/src/story.ts').read_text();scenes=dict(re.findall(r"(\w+): '(C01-[^']+)'",text.split('const SCENES = {')[1].split('} as const;')[0]));counts=collections.Counter(re.findall(r'SCENES\.(\w+)',text));speakers=collections.Counter(re.findall(r"speaker: '(\w+)'",text))
topic_text=(root/'docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md').read_text().split('## 叙事静态图，共 80 项')[1].split('## 战斗单位')[0]
ordered_ids=re.findall(r'`(C01-[^`]+)`',topic_text);assert len(ordered_ids)==80
for item in rows:
 item['catalogSlot']=ordered_ids.index(item['topicId'])
 item['documentedSource']=slot_sources[item['catalogSlot']]
 if item['exactLegacyMatches']: assert len(item['exactLegacyMatches'])==1 and item['exactLegacyMatches'][0]['slot']==item['catalogSlot'], item['topicId']
 else:
  from PIL import ImageChops,ImageStat
  with Image.open(pack/item['png']) as current:
   diff=ImageChops.difference(current,rendered[item['catalogSlot']]); item['legacyReproductionDifference']={'meanAbsoluteRGBA':ImageStat.Stat(diff).mean,'bounds':diff.getbbox(alpha_only=False)}
report={'checkpoint':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'readOnly':True,'missingLegacyTopicSource':not m.TOPIC_SOURCE.exists(),'countsByKind':dict(collections.Counter(a['kind'] for a in assets)),'boards':boards,'assets':rows,'speakerBeatCounts':dict(speakers),'storySceneReferenceCounts':{scenes[k]:v for k,v in counts.items()},'noExactMatch':[x['topicId'] for x in rows if len(x['exactLegacyMatches'])!=1]}
consumer_paths=['packages/story-candidate-01/src/presentation/candidate-01-story.ts','packages/story-candidate-01/src/presentation/candidate-01-bindings.ts','packages/story-candidate-01/src/story.ts']
consumer_text='\n'.join((root/p).read_text() for p in consumer_paths)
direct=[a['topicId'] for a in rows if a['topicId'] in consumer_text]
for item in rows: item['hasDirectConsumer']=item['topicId'] in direct
report['directConsumerTopicIds']=direct
report['overrideNarrativeCount']=sum(a['topicId'] in json.loads((root/'tools/art/source-overrides.json').read_text())['assets'] for a in rows)
report['hdNarrativeCount']=sum(a['topicId'] in {x['topicId'] for x in json.loads((pack/'manifest-tactical-runtime-hd.json').read_text())['assets']} for a in rows)
contracts=consumer_paths+['packages/game-ui/src/ui/campaign-game.ts','packages/game-ui/src/styles/campaign.css','packages/story-candidate-01/src/presentation/index.ts','packages/story-candidate-01/src/presentation/candidate-01-assets.ts','packages/story-candidate-01/assets/final-fantasy-v1/tools/build_final_fantasy_v1.py','docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md']
report['contractFileHashes']={p:sha((root/p).read_bytes()) for p in contracts}
report['exactRgbaReproductions']=sum(bool(a['exactLegacyMatches']) for a in rows)
report['exactRgbOnlyReproductions']=sum(not a['exactLegacyMatches'] and a['legacyReproductionDifference']['meanAbsoluteRGBA'][:3]==[0,0,0] for a in rows)
report['sourcesStableAcrossAudit']=all(sha((root/b['path']).read_bytes())==b['sha256'] and sha((root/b['readPath']).read_bytes())==b['readSha256'] for b in boards) and all(sha((pack/a['png']).read_bytes())==a['fileSha256'] for a in rows)
assert report['sourcesStableAcrossAudit']
report['scope']='Read-only byte/crop/consumer audit. Visual identity review is separate; no source generation or production changes.'
(args.output/'inventory.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('counts',report['countsByKind'],'unmatched',report['noExactMatch']);print('speakers',speakers);print('scenes',report['storySceneReferenceCounts'])
for x in sorted(rows,key=lambda x:x['exactLegacyMatches'][0]['slot'] if x['exactLegacyMatches'] else 999):
 match=x['exactLegacyMatches']; print(match[0]['slot'] if match else '?',x['topicId'],x['label'],x['kind'],x['alphaExtrema'], match[0]['board'].split('/')[-1] if match else 'RGB-exact/alpha-diff')
